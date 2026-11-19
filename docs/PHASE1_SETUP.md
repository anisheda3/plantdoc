# Phase 1 — Foundation + Demo Mode: Setup & Test Guide

Everything here is already written and compile-verified. This guide is the Unity-side wiring.

## 1. Files created

**Scripts (`Assets/Scripts/`)**

| Folder | Files |
|---|---|
| Core | `PlantHealthState.cs` (+`StateDelta`), `SimState.cs` (+`StateBase`), `SimulationStateManager.cs`, `SessionContext.cs`, `GameEvents.cs`, `ScanQualityReport.cs`, `HealthReport.cs`, `AppFlowController.cs`, `DemoModeController.cs`, `States/SimpleStates.cs`, `States/FunctionalStates.cs` |
| AI | `IPlantDiseaseAI.cs`, `PlantAnalysisResult.cs`, `AIManager.cs`, `MockAIService.cs`, `RemoteAIService.cs`, `LocalAIService.cs`, `AIResponseParser.cs`, `ConfidencePolicy.cs`, `ImageQualityAnalyzer.cs`, `ImagePreprocessor.cs` |
| Plants | `PlantData.cs`, `FeatureData.cs`, `PlantDatabase.cs`, `PlantVisualBinder.cs`, `FeatureAnalysisManager.cs` |
| Diseases | `DiseaseData.cs`, `SimulationProfile.cs`, `DiseaseSimulationManager.cs` |
| Treatment | `TreatmentStep.cs`, `TreatmentPlan.cs`, `PreventionPlan.cs`, `ChemicalTreatmentInfo.cs`, `TreatmentManager.cs`, `PreventionManager.cs` |
| Simulation | `RecoveryManager.cs`, `PlantStateEvaluator.cs` |
| UI | `UIManager.cs`, `StageDescriptor.cs` |
| Utilities | `Log.cs`, `MiniJson.cs` |

Also: `Assets/Editor/DemoDataBuilder.cs`, `Assets/Shaders/PlantDisease.shader`, `Assets/Tests/EditMode/*` (13 tests), `server/` (FastAPI).

## 2. Unity project creation
1. Unity Hub → New Project → **3D (URP)**, Unity **2022.3 LTS**.
2. Copy the `Assets/` folder from this workspace over the new project's `Assets/`.
3. Package Manager → install **AR Foundation**, **ARCore XR Plugin**, **ARKit XR Plugin**, **XR Interaction Toolkit**, **Input System** (these are needed in Phases 2 and 8; Phase 1 runs without them).
4. Window → General → Test Runner → confirm EditMode tests appear.

## 3. Generate the demo data
Menu: **Tools → AI Plant Doctor → Build Demo Data**

Creates under `Assets/Data/`: 4 `FeatureData`, 2 `DiseaseData` (Tomato Early Blight, Late Blight), 2 `SimulationProfile`, 2 `TreatmentPlan` (4 and 3 steps), 2 `PreventionPlan`, `Plant_Tomato`, `PlantDatabase`, `ConfidencePolicy`, `ImageQualitySettings`.

## 4. Scene setup (`Assets/Scenes/Bootstrap.unity`)

Create one GameObject **`_Systems`** and add these components:

| Component | Inspector fields to assign |
|---|---|
| `SimulationStateManager` | — |
| `AIManager` | Inference Mode = **Mock**, Plant Database = `PlantDatabase`, Confidence Policy = `ConfidencePolicy`, Remote Endpoint = `http://localhost:8000/analyze` |
| `FeatureAnalysisManager` | — |
| `DiseaseSimulationManager` | Visual Binder = the plant's `PlantVisualBinder` |
| `TreatmentManager` | Visual Binder, Recovery Manager |
| `RecoveryManager` | Visual Binder; Seconds Per Simulated Day = `0.6` |
| `PreventionManager` | (optional generic fallback plan) |
| `AppFlowController` | Plant Database, Quality Settings = `ImageQualitySettings`, and all 7 managers above |
| `DemoModeController` | Flow = `AppFlowController`, Plant Database |
| `UIManager` | Flow = `AppFlowController` |

Then a **`PlantRoot`** GameObject:
1. Add a placeholder mesh (Capsule + a few Quads is fine for Phase 1 — `TODO[ASSET]`).
2. Create a material from `PlantDoctor/PlantDisease`, assign it.
3. Add `PlantVisualBinder`; leave Target Renderers empty to auto-collect children.

## 5. Running the demo loop
Call from a UI button or a small test driver:

```csharp
await demoModeController.RunScenario("tomato", "early_blight", severity: 0.68f, confidence: 0.94f);
```
then drive navigation with `AppFlowController`:
`ContinueFromPlantIdentified()` → `ContinueFromFeatures()` → `ContinueFromDiagnosis()` → `ContinueFromSimulation()` → `BeginTreatmentSteps()` → (`PerformTreatmentStep()` then `NextTreatmentStep()`, ×4) → auto `RecoveryTimeline` → `NextPreventionMeasure()` ×6 → `FinalReport`.

`UIManager.CurrentPanelText` holds the rendered panel for each stage; bind it to a `TextMeshProUGUI` or read it in the console.

Expected console trace:
```
[AI] Image submitted (1x1) via DEMO / MOCK AI
[AI] [MOCK] Plant: Tomato
[AI] [MOCK] Disease: Early Blight
[AI] [MOCK] Confidence: 0.94
[CORE] FSM Welcome -> PlantIdentified
[SIM] Feature analysis built (4 parameters)
[TREATMENT] Plan 'Tomato — Early Blight' started (4 steps)
[TREATMENT] Step 1 completed
[SIM] Time-lapse finished at day 2, severity 0.56
```

## 6. Testing
- **EditMode tests:** Test Runner → Run All. 13 tests: AI parsing (valid, malformed, empty, missing plant_id, server error, healthy, confidence banding), simulation (clamping, state seeding, report maths, divide-by-zero), FSM (enter/exit, unregistered state).
- **Manual error paths:** set Inference Mode = `Remote` with the server down → expect `NETWORK_UNAVAILABLE` and the Error stage, no crash. Set Mock plant id to `"banana"` → expect `UNSUPPORTED_PLANT`.
- **Backend:** `cd server && uvicorn main:app --port 8000`; `curl localhost:8000/health`.

## 7. Verification already performed in this workspace
- All 39 C# scripts compile clean (0 errors, 0 warnings) against a UnityEngine stub via .NET 8 Roslyn.
- All 13 EditMode tests executed and passed.
- FastAPI server booted; `/health`, `/labels`, and all three `/analyze` error paths returned the documented codes.

## 8. Honesty guarantees built into Phase 1
- `MockAIService` output always carries `origin = Mock`; `UIManager` stamps `DEMO / MOCK AI — simulated result, not a real model prediction` on every panel and on the final report.
- `LocalAIService` and the FastAPI server return `MODEL_UNAVAILABLE` rather than inventing a prediction when no model exists.
- Every feature row prints its source (`VISUAL AI ESTIMATE` vs `SENSOR MEASUREMENT`).
- Confidence < 0.60 renders "POSSIBLE DISEASE" plus a rescan/expert prompt, never a diagnosis.
- Chemical guidance lives in `ChemicalTreatmentInfo` with dosage, safety, PHI and regulatory fields, and generated entries are marked `TODO[DATA]` rather than recommending a specific product.

## Next: Phase 2 (AR)
`ARSessionBootstrap`, `ARScanController` (camera → `AppFlowController.SubmitScan`), `ARPlantAnchor`, `AROverlayRenderer` for bounding boxes/masks. The pipeline entry point `SubmitScan(Texture2D, Rect, float)` already exists and is quality-gated, so Phase 2 only adds the input layer.
