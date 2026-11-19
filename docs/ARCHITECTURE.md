# AI Plant Doctor — AR/VR Plant Disease Detection & Treatment Simulator
## Architecture Document (pre-implementation deliverable)

Unity 2022.3 LTS · URP · C# · AR Foundation 5.x · XR Interaction Toolkit 2.5+ · OpenXR · Input System · ScriptableObject data layer · FastAPI inference backend.

---

## 1. Complete System Architecture

```
                              ┌──────────────┐
                              │     USER     │
                              └──────┬───────┘
                     ┌───────────────┴───────────────┐
                 AR MODE                          VR MODE
        (AR Foundation, device cam)     (XRI, virtual garden/lab)
                     │                               │
             ARScanController                 VRInspectionController
                     └───────────────┬───────────────┘
                                     ▼
                            IScanSource → ScanRequest(Texture2D, meta)
                                     ▼
                       ImageQualityAnalyzer (blur/exposure/coverage/motion)
                                     │  reject → ScanQualityFeedback UI
                                     ▼ accept
                       ImagePreprocessor (crop, resize 224², normalize, encode)
                                     ▼
                        AIManager  ──uses──▶  IPlantDiseaseAI
                                              ├── RemoteAIService (HTTP/FastAPI)
                                              ├── LocalAIService  (Sentis/ONNX)
                                              └── MockAIService   (DEMO ONLY)
                                     ▼
                          AIResponseParser → PlantAnalysisResult
                                     ▼
                          ConfidencePolicy (high / moderate / uncertain)
                                     ▼
          PlantDatabase + DiseaseDatabase (ScriptableObject registries, keyed by AI ids)
                                     ▼
                        SimulationStateManager (FSM, event bus)
        ┌──────────────┬───────────────┬───────────────┬──────────────┐
 FeatureAnalysis   DiseaseSim      Treatment       Recovery      Prevention
   Manager          Manager         Manager        Manager        Manager
        └──────────────┴──────┬────────┴───────────────┴──────────────┘
                              ▼
                        PlantHealthState  (single source of truth)
                              ▼
              PlantVisualBinder → shader params / blendshapes / VFX / UI
                              ▼
                        FinalReportManager
```

Key rule: **AI layer and simulation layer are decoupled**. The only contract between them is `PlantAnalysisResult` (plain serializable DTO). Simulation never calls the network; AI never touches a `GameObject`.

---

## 2. Unity Folder Structure

```
Assets/
├── Scripts/
│   ├── Core/        SimulationStateManager, StateBase, GameEvents, ServiceLocator,
│   │                AppConfig, SessionContext, PlantHealthState
│   ├── AI/          IPlantDiseaseAI, PlantAnalysisResult, AIManager, RemoteAIService,
│   │                LocalAIService, MockAIService, AIResponseParser, ConfidencePolicy,
│   │                ImageQualityAnalyzer, ImagePreprocessor
│   ├── AR/          ARSessionBootstrap, ARScanController, ARPlantAnchor, AROverlayRenderer
│   ├── VR/          VRRigBootstrap, VRInspectionController, VRTreatmentInteractor
│   ├── Plants/      PlantData, PlantDatabase, PlantInstance, PlantVisualBinder
│   ├── Diseases/    DiseaseData, DiseaseDatabase, SimulationProfile, DiseaseSimulationManager
│   ├── Treatment/   TreatmentPlan, TreatmentStep, TreatmentManager, TreatmentEffect
│   ├── Simulation/  RecoveryManager, RecoveryTimelineDriver, PlantStateEvaluator
│   ├── UI/          UIManager, StagePanel, FeatureComparisonView, DiagnosisView,
│   │                TreatmentView, RecoveryView, PreventionView, ReportView, DemoModeView
│   └── Utilities/   Logger (channelled), MathUtils, AsyncUtils, JsonSafe
├── Data/  Plants/ Diseases/ Treatments/ Features/   (ScriptableObject assets)
├── Prefabs/  Plants/ AR/ UI/ Treatment/
├── Materials/ Shaders/ Textures/ Animations/ Timelines/ Models/
├── Scenes/  Bootstrap, MainMenu, ARExperience, VRGarden
└── Tests/  EditMode/ PlayMode/
```

Scene decision: **single-scene-per-mode + modular managers** (Bootstrap → ARExperience or VRGarden). The 7 "stages" are FSM states with UI panels, not separate scenes — avoids cross-scene state marshalling and reload cost on mobile.

---

## 3. Class / Component Architecture

```
SimulationStateManager (MonoBehaviour, one per mode scene)
  ├─ Dictionary<SimState, StateBase>
  ├─ StateBase { Enter(ctx) Tick(dt) Exit() }
  └─ SessionContext { PlantAnalysisResult, PlantData, DiseaseData,
                      PlantHealthState initial/current, treatmentProgress }

GameEvents (static, C# events — no UnityEvent spaghetti)
  OnScanRequested, OnScanQualityFailed, OnScanCompleted,
  OnAIAnalysisStarted, OnAIAnalysisCompleted, OnAIAnalysisFailed,
  OnPlantIdentified, OnDiseaseDetected, OnFeatureSelected,
  OnTreatmentStarted, OnTreatmentStepCompleted, OnTreatmentCompleted,
  OnRecoveryTick, OnRecoveryCompleted, OnPreventionCompleted, OnReportReady

AIManager  →  IPlantDiseaseAI (injected per AppConfig.InferenceMode)
PlantDatabase / DiseaseDatabase : ScriptableObject, Dictionary<string,T> built on enable
PlantVisualBinder : reads PlantHealthState each frame → MaterialPropertyBlock
                    (_LesionDensity,_Yellowing,_Wilt,_Damage,_Growth) + blendshapes + VFX
UIManager : owns panel stack, subscribes only to GameEvents (never to managers directly)
```

Single responsibility enforced: no manager references another manager's internals; they communicate via `SessionContext` + `GameEvents`.

---

## 4. ScriptableObject Schemas

| Asset | Key fields |
|---|---|
| `FeatureData` | featureId, displayName, MeasurementSource {VisualAIEstimate, SensorMeasurement, UserInput}, optimalValue, optimalTolerance, higherIsBetter, meaning, whyItMatters, overlayColor |
| `PlantData` | plantId, plantName, healthyModel, leafPrefab, FeatureData[], DiseaseData[], thumbnail |
| `DiseaseData` | diseaseId, diseaseName, pathogenType, description, causes, symptoms[], untreatedOutcome, severityThresholds(mild/moderate/severe), supportingFeatureIds[], TreatmentPlan, PreventionPlan, SimulationProfile, aiLabelAliases[] |
| `SimulationProfile` | progressionCurves per state channel, lesion texture/mask, shader param ranges, VFX prefabs, progression duration, recovery curve |
| `TreatmentStep` | stepId, title, instruction, rationale, InteractionMode{Tap,Grab,Spray,Info}, targetRegion, StateDelta effects, simulatedDaysAdvanced, optional `ChemicalTreatmentInfo` |
| `ChemicalTreatmentInfo` | activeIngredient, dosage, applicationMethod, safetyNotes, preHarvestInterval, regulatoryNote (all data, never hard-coded) |
| `TreatmentPlan` | planId, planName, TreatmentStep[], expectedSeverityReduction, disclaimer |
| `PreventionPlan` | planId, PreventionMeasure[] {category, title, detail}, quizItems[] |

`StateDelta` = per-channel float deltas applied to `PlantHealthState` (diseaseSeverity, lesionDensity, yellowing, wilting, leafDamage, healthyGrowth).

---

## 5. AI ↔ Unity API Contract

`POST /analyze` — `multipart/form-data`: `image` (jpeg), optional `plant_hint`, `client_version`.

```json
{
  "schema_version": "1.0",
  "model_version": "plantdoc-v0.3.1",
  "plant_id": "tomato", "plant_name": "Tomato",
  "disease_id": "early_blight", "disease_name": "Early Blight",
  "confidence": 0.94, "severity": 0.68,
  "is_healthy": false,
  "features": {
    "leaf_color": { "value": 0.72, "optimal": 0.95, "status": "abnormal", "source": "visual_ai" }
  },
  "regions": [ { "type": "lesion", "bbox": [0.31,0.22,0.18,0.15], "score": 0.87 } ],
  "recommendation": { "treatment_id": "tomato_early_blight_treatment" },
  "warnings": []
}
```
Errors: `{"error_code":"IMAGE_UNUSABLE|UNSUPPORTED_PLANT|MODEL_ERROR","message":"..."}` with HTTP 4xx/5xx. Also `GET /health`, `GET /labels`.
Unity mirrors this with `[Serializable]` DTOs + a tolerant parser (missing fields → safe defaults, never exceptions).

---

## 6. AR User Flow
Welcome → permission check → live camera + reticle & framing guide → continuous quality meter (blur/lux/coverage) → SCAN → preprocess → "Analyzing… ███" (async, non-blocking) → plant identified card → AR overlays on anchored leaf (lesion/yellowing masks, tappable) → feature comparison table → diagnosis (with confidence banding) → disease progression simulation on virtual twin anchored beside real plant → treatment steps (tap-to-act + guidance paths) → time-lapse after each step → prevention cards → final report → save/share.

## 7. VR User Flow
Spawn in virtual lab/greenhouse → locomotion (teleport + snap turn) → approach diseased plant → ray-highlight → grab leaf, bring to inspection station → "scan" via virtual scanner tool (renders leaf to RenderTexture → same `IPlantDiseaseAI`) → floating holographic analysis panel → same feature/diagnosis stages → tool rack (pruning shears, sprayer, watering can) → physical grab-and-perform treatment with haptics → time-lapse plays on the plant in-world → prevention board → report.

## 8. State Machine
`WELCOME → SCAN → AI_ANALYSIS → PLANT_IDENTIFIED → FEATURE_ANALYSIS → DIAGNOSIS → DISEASE_SIMULATION → TREATMENT_INTRO → TREATMENT_STEP (loop) → RECOVERY_TIMELINE → PREVENTION → FINAL_REPORT`
Side states: `SCAN_RETRY`, `LOW_CONFIDENCE`, `ERROR`. Every state implements Enter/Tick/Exit; transitions only via `StateManager.Transition(SimState)` triggered by events, never directly by buttons.

## 9. Data Flow (camera → simulation)
`camera frame → CPU image → quality gate → crop/resize/normalize → JPEG bytes → IPlantDiseaseAI.AnalyzePlant() (await) → JSON → PlantAnalysisResult → ConfidencePolicy → PlantDatabase.Resolve(plant_id) + DiseaseDatabase.Resolve(disease_id) → SessionContext → PlantHealthState seeded from severity+features → PlantVisualBinder drives shaders → UI renders from state`. Missing DB entry → graceful `SIMULATION DATA NOT FOUND` screen, no crash.

## 10. Recommended MVP (hackathon)
Tomato only, Early Blight + Late Blight + Healthy; Mock + Remote AI; AR scan (single-image capture, no persistent tracking beyond a plane anchor); feature table + 2D overlays; 4-step treatment; shader-driven time-lapse; prevention cards; report; Demo Mode so it never fails on stage. VR = stretch.

## 11. Development Order
Phase 1 Foundation+Demo → 2 AR → 3 AI (mock→REST→FastAPI) → 4 Diagnosis UI → 5 Treatment → 6 Time-lapse → 7 Prevention/Report → 8 VR → 9 Polish.

## 12. Assets Required
Tomato plant + leaf models (LOD0 ≤8k tris), lesion/yellowing mask textures, URP disease Shader Graph, healthy/diseased albedo set, treatment tool models (shears, sprayer, can), UI icon set, greenhouse VR environment, SFX + haptic clips. Placeholders (procedural quads/primitives + procedural masks) ship in Phase 1 and are marked `// TODO[ASSET]`.

## 13. AI Components Required
(1) leaf/plant detector or ROI segmenter, (2) plant species classifier, (3) disease classifier (PlantVillage-class dataset baseline), (4) severity regressor / lesion-area estimator, (5) feature extractor mapping masks→normalized feature scores, (6) calibration + confidence thresholding, (7) export to ONNX.

## 14. How the Real Model Connects
Train in PyTorch → export ONNX → serve from FastAPI (`RemoteAIService`) **or** import as `.onnx` into Unity Sentis (`LocalAIService`). Both implement `IPlantDiseaseAI`; swapping is a one-line `AppConfig.InferenceMode` change. Labels must map to `DiseaseData.aiLabelAliases`, so a new model label is registered as data, not code.

**Honesty constraints baked in:** mock results are watermarked `DEMO / MOCK AI`; camera-derived numbers are labelled `VISUAL AI ESTIMATE` and never presented as sensor readings; low-confidence results are shown as "possible", not diagnoses; chemical guidance is educational and carries safety/regulatory fields.
