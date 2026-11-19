# Review of the Claude change set — what was applied, what was not

Document reviewed: `claudeBTP.txt` (5,077 lines, 64 code blocks).

**Verdict: the analysis is excellent, roughly 70% of the code is directly usable, and
about 30% would not have compiled.** Every claim was checked against the real source
before anything was applied. Details below.

---

## Quality assessment

### Where it is genuinely strong

The diagnostic work is the best part of this document. It found **five real bugs that
I missed**, and I confirmed every one by reading the actual file:

| Bug | Verified where | Real impact |
|---|---|---|
| `ImagePreprocessor.Prepare()` texture never destroyed | `AppFlowController.SubmitScan` line 105 | Leaks a `Texture2D` **per scan**. Guaranteed OOM on a long mobile session. |
| Anonymous lambdas subscribed, never unsubscribed | `UIManager.OnEnable` lines 30–33 | 3 of 5 handlers are lambdas. Cannot be removed. Object pinned on a static delegate forever. |
| `_buffer` leaked on teardown | `ARCameraFrameSource.OnDestroy` | `StopSource()` only detaches the frame callback. The converted CPU texture leaked. |
| `_buffer` leaked on teardown | `VRScannerTool.OnDestroy` | Released the `RenderTexture` but not the readback texture. |
| No `OnDisable` on coroutine owners | `DiseaseSimulationManager`, `RecoveryManager` | Unity kills the coroutine but leaves `_routine` non-null, so `IsPlaying` lies forever and the sim can never restart. |

The last one is subtle and genuinely good catch. The others are the classic Unity
mobile leak set.

It was also **honest about uncertainty**. 24 separate `// ASSUMPTION:` and `// ADAPT:`
markers, and it repeatedly refused to write code when it did not have the literal
source in front of it. That is the right instinct and it is why the later batches are
trustworthy.

### Where it is weak

**1. It is a transcript, not a patch set.** Early batches were written blind and then
superseded by literal-exact rewrites later in the same document. Applying it top to
bottom would apply the wrong version of six files. Concretely:

| File | Speculative version | Superseded by |
|---|---|---|
| `PlantDisease.shader` | line 107 (248 lines) | line 1202 (131 lines) |
| `PlantVisualBinder.cs` | line 385 | line 1342 |
| `FeatureRowView.cs` | lines 978, 1037 | line 1705 |
| `SafeAreaFitter.cs` | line 689 | line 2445 |
| `ConfirmationDialogView.cs` | line 1081 | line 2487 |
| `MainScreenView.cs` | line 1851 (587 lines) | line 4274 (202 lines) |

**2. It invented a parallel theme system.** `UIThemeSO.cs` is a second theme
ScriptableObject with `[CreateAssetMenu(menuName = "Plant Doctor/UI Theme")]` —
note the space. Our existing `UITheme.cs` uses `"PlantDoctor/UI Theme"`. Two theme
assets, two menu entries one space apart, different field names (`H1Size` vs
`fontHeading`). This directly violates the project's own "restyle from one asset" goal.

**3. Several components depend on events that do not exist.** `HealthGaugeView` needs
`GameEvents.OnHealthReportUpdated`, `AnimatedScanReticle` needs
`GameEvents.OnScanQualityUpdated`. Neither exists. Both were flagged `// ADAPT` by
the author, correctly — but they are not drop-in.

**4. One factual error.** The document asserts `PlantVisualBinder.cs` lives at
`Assets/Scripts/Simulation/`. It is actually at `Assets/Scripts/Plants/`. Later
batches corrected this themselves.

**5. Some severity ratings are inflated for our stage.** "No auth on `/analyze`" is
listed as a security hole. True in production; the server currently has no model and
returns `MODEL_UNAVAILABLE` to every request. Worth fixing, not urgent.

---

## What was applied — 12 changes, all verified

Each was checked against the real file, applied surgically, then compiled and tested.

### Memory leaks — 4 fixes

**1. `Core/AppFlowController.cs`** — wrapped the AI call in `try/finally` so the
prepared texture is always destroyed. Guarded with `prepared != rawImage` so a
pass-through never destroys the caller's texture.

**2. `UI/UIManager.cs`** — replaced 3 anonymous lambdas with named handlers
(`HandleAnalysisProgress`, `HandleScanQualityFailed`, `HandleAnalysisFailed`) and
added the matching `-=` lines. Subscriptions are now symmetric.

**3. `AR/ARCameraFrameSource.cs`** — `OnDestroy` now destroys `_buffer`.

**4. `VR/VRScannerTool.cs`** — `OnDestroy` now destroys `_buffer`.

### Coroutine lifecycle — 2 files

**5. `Diseases/DiseaseSimulationManager.cs`** and **6. `Simulation/RecoveryManager.cs`** —
added `void OnDisable() => Stop();` to both, plus an `isActiveAndEnabled` guard before
every `StartCoroutine` (1 call site in the first, 2 in the second), since
`StartCoroutine` throws on an inactive object.

### Mobile lifecycle — 2 additions

**7. `AR/ARScanController.cs`** — added `OnApplicationPause`. Releases the camera and
stops the quality loop on pause, reacquires on resume. Android holds the camera
exclusively; without this, resume often fails with "camera already in use".

**8. `AI/AIManager.cs`** — added `OnApplicationPause` that cancels in-flight inference.
Reuses the existing `CancelActiveRequest()`.

### Performance — 1 fix

**9. `AI/ImageQualityAnalyzer.cs`** — `Downsample` now reuses a `_sampleBuffer` field
instead of allocating a fresh `Color[]` per call. This runs several times a second on
the live preview loop; the old version produced a steady GC sawtooth.

### Build hygiene — 2 changes

**10. `UI/DemoRunner.cs`** — wrapped in `#if UNITY_EDITOR || DEVELOPMENT_BUILD` so the
IMGUI debug panel cannot ship in a release player. Verified the only other references
are in `Assets/Editor/` (already Editor-only) and one comment in `MainScreenView.cs`.

**11. `UI/SafeAreaFitter.cs`** — new file, 37 lines. Keeps a `RectTransform` inside
`Screen.safeArea` for notched devices. Self-contained, no dependencies. **Not yet
attached to anything** — see "what you must do in Unity" below.

### Client/server contract — 1 change

**12. `AI/RemoteAIService.cs` + `AI/AIManager.cs`** — added an optional `apiKey`
constructor parameter sent as `X-API-Key`, plus explicit handling for HTTP 401
(`UNAUTHORIZED`) and 429 (`RATE_LIMITED`). Default endpoint moved to `/v1/analyze`.
A new `remoteApiKey` inspector field was added to `AIManager`.

### Backend — 6 files

`server/main.py` rewritten with: optional API-key auth, an in-process sliding-window
rate limiter, `/v1` versioned routes, CORS configuration, structured logging with
request IDs, and deprecated unprefixed aliases so the old client keeps working.
Plus new `Dockerfile`, `.dockerignore`, `.env.example`, and `tests/test_main.py`.

`schema.py` and `inference.py` were not touched.

---

## What was NOT applied, and why

### Rejected — would not compile

| Item | Reason |
|---|---|
| `HealthGaugeView.cs` | Needs `GameEvents.OnHealthReportUpdated` — does not exist. Also needs `HealthReport.HealthPercent01` and `.Severity`, neither of which our `HealthReport` has. |
| `AnimatedScanReticle.cs` | Needs `GameEvents.OnScanQualityUpdated` — does not exist. |
| `DiseaseSeverity.cs` enum | Nothing in our codebase produces or consumes it. Our model is a continuous `float severity`, not a 4-band enum. Adding it creates two competing severity representations. |
| `UIThemeSO.cs` | Duplicate theme system, `CreateAssetMenu` path differs from ours by one space. Would produce two near-identical menu entries and two theme assets. |

### Rejected — depends on files we do not have

The `MainScreenView` decomposition (`MainScreenBodyTextBuilder`, `FeatureListPresenter`,
`FeatureDetailPopupPresenter`, `MainScreenActionBinder`) is **well designed** and the
events it uses all exist. But the new `MainScreenView` also wires `FeatureRowPool`,
`ConfirmationDialogView`, and `arScanController` — none of which exist in our project.

This is a genuinely good refactor and worth doing. It is a five-file coordinated change
that needs the pool and dialog built first, and it should be verified in Unity, not
just the headless harness. **Recommended as the next work item, not bundled into a
bug-fix pass.**

### Deferred — needs judgement, not just code

| Item | Note |
|---|---|
| `PlantDisease.shader` replacement | Adds wind sway, emissive recovery pulse, rim highlight. Keeps all existing properties, so it is compatible. But the harness **cannot compile shaders** — this must be eyeballed in Unity. Applying it blind risks the magenta-plant problem we already fixed once. |
| `PlantVisualBinder.cs` replacement | Paired with the shader above. Same reasoning. |
| `CameraPreviewView` / `CameraLayerVisibility` / `UIBuilder` camera wiring | This is the AR work, which is your next phase anyway. It also extends `ICameraFrameSource` with a preview-texture member, changing an interface with 3 implementers. Belongs in the AR phase. |
| Android back button handler | Small and safe, but genuinely untestable without a device. Do it during the AR phase. |
| `Tween.cs`, `ConfirmationDialogView`, `FeatureRowPool` | Fine code, but they are prerequisites for the UI refactor above. Land them together. |

---

## Verification performed

```
Unity headless harness   Build succeeded    31 / 31 tests pass
Backend pytest            7 / 7 tests pass
Python syntax             main.py, schema.py, inference.py, test_main.py  OK
```

The harness needed four new stub types to compile the new code: `Screen`,
`ScreenOrientation`, `DisallowMultipleComponentAttribute`, `UnityWebRequest.SetRequestHeader`,
plus `Rect.position`/`.size` and `Rect` equality operators. These are stub gaps, not
code defects.

**One `requirements.txt` fix:** the file was missing a trailing newline, which can
break `pip install -r` on some tooling. Added.

### What this verification does NOT cover

Same limits as always, and they matter here:

- **No shader compilation.** This is why the shader replacement was deferred.
- **No rendering, no prefab wiring, no canvas layout.**
- **No assembly definition checking** — the harness compiles everything into one
  project. This is what let the `TMP_Text` error through last time. None of today's
  changes add a new package dependency, so the asmdefs are unchanged and safe.
- **No device testing.** `OnApplicationPause` behaviour is correct by inspection but
  has never run on real Android.

---

## What you must do in Unity

1. Copy the changed files over and let Unity recompile. Console should be clean.
2. `Window → General → Test Runner → EditMode → Run All` → expect **31 passed**.
3. Press Play and run the demo loop once. Expect severity 68% → 15%, recovery 78%.

**`SafeAreaFitter` is not attached to anything yet.** It is a component, not a
singleton. To use it, add it to the `HUD`, `Body`, and `Actions` panels that
`UIBuilder` creates — or add `go.AddComponent<SafeAreaFitter>()` inside `UIBuilder`.
I left `UIBuilder` untouched deliberately; it is a 440-line generator and the change
should be made and eyeballed in the editor.

**`AIManager` inspector note.** Unity does not retroactively rewrite serialized string
fields when a script's default changes. Any existing scene still holds the old
`http://localhost:8000/analyze`. Either update it by hand in the Inspector or re-run
`Build Demo Scene`. The old unprefixed route still works, so nothing breaks either way.

---

## Recommended order from here

1. **Verify this batch in Unity** — 31 tests green, demo loop runs.
2. **AR on phone** — your stated next phase. Pull in `CameraPreviewView`,
   `CameraLayerVisibility`, the `UIBuilder` camera wiring, and the back-button
   handler as part of it. They are all AR work.
3. **The `MainScreenView` refactor** — with `FeatureRowPool`, `ConfirmationDialogView`
   and `Tween` landed together as one reviewed change.
4. **The shader upgrade** — visually verified in the editor, not applied blind.
