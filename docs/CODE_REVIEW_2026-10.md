# Code review — October 2026

Scope: every file under `Assets/Scripts`, `Assets/Editor`, `Assets/Shaders`, `Assets/Tests`,
`server/` and `tools/` (≈60 C# files, 1 shader, the FastAPI service).

## Verdict: good — B+ before, kept and built on

The architecture is genuinely solid, so it was **kept, not rewritten**:

- Clean layering with one contract between AI and simulation (`PlantAnalysisResult`).
- An explicit state machine, an event bus, and managers that never reference each other.
- Data-driven content (ScriptableObjects; model labels resolved through aliases).
- Honesty rules enforced in code: mock results are watermarked, uncertain results are
  "possible", treatment is never a full cure (residual floors), and the server refuses to
  invent predictions.
- Defensive throughout: no exceptions on bad JSON, missing data or camera failures.
- 31 meaningful tests, a server test suite, and careful comments.

What held it back was a set of real defects, mostly in paths that only run on a phone
(which had never been built), plus a UI that was too small to read on a device.

## Defects found and fixed

| # | Where | Problem | Impact | Fix |
|---|---|---|---|---|
| 1 | `PlantDoctor.Runtime.asmdef` | `PLANTDOCTOR_AR_FOUNDATION` turns on when AR Foundation is installed, but the assembly did not reference the AR assemblies | Installing AR Foundation **broke compilation** — phone AR could never build | Added `Unity.XR.ARFoundation`, `ARSubsystems`, `CoreUtils` references |
| 2 | `ARCameraFrameSource` | Read `ARSession.state` before any availability check (always `None`); converted the CPU image in sensor orientation | "Unsupported" was never detected; frames were sideways relative to the reticle | Availability resolved by `ARAvailabilityProbe`; capture now blits the GPU background through ARCameraBackground's own material (WYSIWYG, any orientation) |
| 3 | `WebCamFrameSource` | Required permission synchronously; used camera 0 (front on many phones); ignored rotation/mirroring | On Android it failed with "permission denied" on first launch, showed the selfie camera, sideways | Async permission request, rear camera, `ViewportMapping` for rotation/mirror/aspect-fill (preview and capture share it) |
| 4 | `ImageQualityAnalyzer` | "Coverage" counted only *green* pixels over the *whole frame*; sharpness measured on a 64-px thumbnail | Diseased (yellow/brown) leaves were rejected as "leaf too small"; background clutter skewed results; blur was undetectable | HSV foliage test (green→yellow→brown), metrics computed inside the framed region, sharpness near native resolution |
| 5 | Diagnosis flow | A **healthy** result had no disease, so `DiagnosisState` raised `MISSING_SIMULATION_DATA` | Healthy plants landed on the error screen | Healthy path: diagnosis says healthy, then prevention → report |
| 6 | `TreatmentManager.PerformCurrentStep` | No guard against repeats | Tapping "Perform" repeatedly stacked the same treatment and drove severity to the floor | Each step applies once (`CurrentStepPerformed`) |
| 7 | `DiseaseSimulationManager` | Leaving the progression stage early left the state half-progressed | Treatment started from a partial state; step 1 looked like a near-cure | The state snaps to the end of the progression when the stage is skipped |
| 8 | `AppFlowController.ApplyAnalysis` | Session state was not reset between scans | Treatment progress could leak from one plant into the next | Fresh analysis = fresh session |
| 9 | `RemoteAIService` | Discarded the server's JSON error body | "AI server error (503)" instead of "no trained model installed" | Server codes and messages are surfaced |
| 10 | `PlantDisease.shader` | Single-sided, no shadow-caster pass, flat lighting | The backs of leaves were invisible; the twin cast no shadow | Two-sided, shadow caster + depth passes, translucency, wind, healing glow |
| 11 | Disease masks | Lesion mask peaked at ~0.4, imported as sRGB | Lesions **never appeared** on the twin | Masks use the full 0..1 reveal range and are imported as linear data |
| 12 | `MainScreenView` UI | Body text at 17 pt on a 1080-px canvas; ASCII bar charts; IMGUI fallback | Unreadable on a phone | New UI (see `PHONE_AR_GUIDE.md`) |
| 13 | `AIManager` | Backend fixed at build time | The phone could not reach a LAN server whose address changes | In-app Settings: Demo / Server, address, API key, connection test (persisted) |

Not defects, but notes: the earlier review's "mojibake" concern does not apply — the files are
valid UTF-8 (only PowerShell 5's default decoding displayed them garbled). The
`tools/headless-verify` stub harness is retired in favour of the real Unity runner
(`tools/run-tests.ps1`).

## Verification

| Check | Result |
|---|---|
| Unity 2022.3.62f3 batch compile (AR Foundation 5.2.2 + ARCore 5.2.2 installed) | 0 errors, 0 warnings in project code |
| EditMode tests (31 original + 18 new) | **49 / 49 pass** |
| PlayMode end-to-end: real scene, synthetic camera → quality gate → AI → … → report, plus the healthy path | **pass** |
| Screenshots of every stage (PlayMode, 1080×2340) | `docs/screenshots/` |
| Android build (IL2CPP, ARM64, GLES3, all shaders compiled for the device) | **succeeded** — `Builds/Android/PlantDoctor.apk`, 25 MB |

What this verification still cannot cover: real-device AR tracking, ARCore light estimation,
and how real phone-camera frames score on the quality gate. Those need a phone.
