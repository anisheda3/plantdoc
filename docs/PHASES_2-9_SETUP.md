# Phases 2–9 — AR, VR, Interaction & Polish Layers

Builds on `PHASE1_SETUP.md`. Everything below is written, compiled and tested.

---

## Phase 2 — AR Layer

### Key design decision: the camera is behind an interface
`ICameraFrameSource` abstracts "something that can give us a frame". Three implementations:

| Implementation | Use | Compiles without AR packages? |
|---|---|---|
| `WebCamFrameSource` | Editor Play Mode testing | yes |
| `ARCameraFrameSource` | Android/iOS device AR | only with `PLANTDOCTOR_AR_FOUNDATION` |
| `VRScannerTool` | VR virtual scanner | yes |

**Why this matters:** the project compiles and runs today even though AR Foundation isn't installed. `ARCameraFrameSource.cs` is wrapped in `#if PLANTDOCTOR_AR_FOUNDATION`, which the `versionDefines` in `PlantDoctor.Runtime.asmdef` switch on automatically when the package is installed. *(October 2026: AR Foundation 5.2.2 + ARCore are now installed and the AR scene is generated — see `PHONE_AR_GUIDE.md`.)* Without that guard the project would fail to compile on any machine lacking the package.

### Files
`AR/ICameraFrameSource.cs`, `AR/WebCamFrameSource.cs`, `AR/ARCameraFrameSource.cs`, `AR/ARScanController.cs`, `AR/AROverlayRenderer.cs`, `UI/ScanReticleView.cs`

### Scene setup (`ARExperience`)
1. GameObject **`AR Session`** → `ARSession`. GameObject **`XR Origin`** → `XROrigin` + `ARCameraManager` + `ARCameraFrameSource`.
2. On `_Systems`, add `ARScanController`:
   - Flow = `AppFlowController`
   - Frame Source Behaviour = `ARCameraFrameSource` (device) or `WebCamFrameSource` (Editor)
   - Quality Settings = `ImageQualitySettings`
   - Region Of Interest = `(0.2, 0.2, 0.6, 0.6)`
3. Add `AROverlayRenderer` (assign `PlantDatabase`) and `ScanReticleView` (assign the scan controller).
4. Wire the **SCAN** button to `ARScanController.CaptureAndSubmit()`.

`ScanReticleView` exposes `CurrentColor`, `HintText`, `CanScan` each frame — bind to your reticle `Image` and hint `Text`.

### Player settings for device
- **Android:** Minimum API 24+, ARCore XR Plugin enabled, Graphics API = OpenGLES3/Vulkan, delete Vulkan if ARCore complains. Camera permission is requested by AR Foundation.
- **iOS:** Camera Usage Description filled in, ARKit plugin enabled, target iOS 12+.
- For a LAN inference server over plain HTTP, allow cleartext traffic (Android) / set ATS exception (iOS).

---

## Phase 8 — VR Layer

### Key design decision: no hard dependency on XRI
`VRInteractionEvents` is a thin façade with `NotifyGrabbed/Released/Hovered/Activated`. XR Interaction Toolkit components forward into it via UnityEvents in the Inspector. Gameplay code subscribes to the façade, so the project compiles without XRI installed and interactions are unit-testable.

### Files
`VR/VRScannerTool.cs`, `VR/VRInteractionEvents.cs`, `VR/VRTreatmentInteractor.cs`

### Scene setup (`VRGarden`)
1. XR Origin (VR) from XRI samples; add teleport + snap-turn providers.
2. Create a **Scanner** prefab: a mesh + a child `Camera` (small FOV, targets the leaf) + `VRScannerTool`.
   Add `XRGrabInteractable` so the user can pick it up.
3. On the plant's leaf objects add `XRGrabInteractable` + `VRInteractionEvents`, and wire:
   - `Select Entered` → `VRInteractionEvents.NotifyGrabbed`
   - `Activated` → `VRInteractionEvents.NotifyActivated`
4. Add `VRTreatmentInteractor` to `_Systems` (Flow + the interaction events object).

`VRTreatmentInteractor.TryPerform` **validates the gesture against the current step's `interactionMode`** — spraying when the step asks you to prune is ignored with a log line, so users must perform the correct treatment.

### Reuse guarantee
`VRScannerTool` implements `ICameraFrameSource`, so VR scanning runs through the **identical** quality gate → preprocessing → `IPlantDiseaseAI` path as AR. No duplicated pipeline.

---

## Phase 9 — Polish

`Utilities/FeedbackDirector.cs` subscribes to `GameEvents` and plays audio cues for scan success/rejection, diagnosis, treatment steps, recovery and errors. All clips optional — missing clips are skipped, so it never throws. Attach to `_Systems` with an `AudioSource`; assign clips when you have them (`TODO[ASSET]`).

Haptics: `VRTreatmentInteractor.SendHaptics()` is the single hook — wire it to `XRBaseController.SendHapticImpulse` in the scene.

---

## Realism correction made during this phase

The first end-to-end run produced **severity 0% / recovery 100%** — which would have violated the brief's rule that treatment must never be presented as a guaranteed cure. Fixed by adding residual floors:

```csharp
// TreatmentPlan
public float residualSeverityFloor = 0.15f;  // disease pressure never fully vanishes
public float residualDamageFloor  = 0.10f;   // necrotic tissue does not regenerate
```

Applied in `TreatmentManager.PerformCurrentStep()` and `RecoveryManager` (which asymptotes toward the floor). Demo data: Early Blight 0.15/0.10, Late Blight 0.20/0.15 (more lasting damage).

Result is now **68% → 15% severity, 78% recovery**, closely matching the brief's own worked example. Four `RealismTests` lock this in.

---

## Verification performed

| Check | Result |
|---|---|
| Full compile (all 51 scripts, Unity stub, Roslyn) | 0 errors, 0 warnings |
| EditMode test suite | **31/31 passed** |
| End-to-end pipeline harness | **26/26 checks passed** |

The E2E harness drives the real production classes — `AIResponseParser`, `PlantDatabase`, `PlantStateEvaluator`, `TreatmentPlan`, `SessionContext` — through scan → analysis → mapping → features → diagnosis → 4 treatment steps → time-lapse → prevention → report, plus five error paths. Actual output:

```
[STEP 3] FEATURE ANALYSIS
  Leaf Color     72 %   95 %   Abnormal   VISUAL AI ESTIMATE
  Leaf Spots     88 %    5 %   Abnormal   VISUAL AI ESTIMATE
  Yellowing      61 %   10 %   Abnormal   VISUAL AI ESTIMATE
  Wilting        34 %    5 %   Abnormal   VISUAL AI ESTIMATE

[STEP 5-6] TREATMENT + TIME-LAPSE
  Step 1/4 -> day 2,  severity 52 %
  Step 2/4 -> day 4,  severity 38 %
  Step 3/4 -> day 7,  severity 20 %
  Step 4/4 -> day 10, severity 15 %

[FINAL] Recovery: 78 %   Treatment: 4/4   Prevention: Excellent
```

Note the raw model label `Tomato___Early_blight` resolved to the authored `DiseaseData` through `aiLabelAliases` — confirming a retrained model needs **no code change**, only a data edit.

---

## Remaining work (needs the Unity Editor / real assets)

These cannot be produced headlessly and are marked `TODO[ASSET]` in code:
- Plant/leaf 3D models, lesion mask textures, treatment tool models, greenhouse environment
- Audio clips and haptic profiles
- Timeline assets (the code path uses coroutine-driven state interpolation, which works today; Timeline is an optional authoring upgrade)
- Prefab/scene wiring per the tables above
- On-device AR/VR testing (requires physical hardware)
