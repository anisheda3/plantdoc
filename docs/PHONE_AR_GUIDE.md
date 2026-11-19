# Plant Doctor on your phone — build, install, scan

## What you get

One app that adapts to the phone:

| Phone | Mode | What happens |
|---|---|---|
| ARCore-supported Android (most phones from 2018 on) | **AR** | Live camera. Scan a leaf, then place a 3D twin of your plant on the table beside it. The twin is lit by the room (ARCore light estimation), casts a real shadow on the surface, and shows the disease, treatment and recovery. Drag to rotate, pinch to resize, tap another surface to move it. |
| Android without ARCore | **Camera** | Same scan with the plain camera; the twin is shown in a 3D studio view. |
| Unity Editor | **Camera** | Uses your webcam (or **Try demo**). |

ARCore is marked *optional*, so the APK installs on every Android 7+ (API 24) ARM64 phone.

## 1. Build the APK

Close Unity first if it is open, then either:

**Editor:** `Tools > AI Plant Doctor > Build Android APK`

**Command line** (from the project folder):
```powershell
& "C:\Program Files\Unity\Hub\Editor\2022.3.62f3\Editor\Unity.exe" -batchmode -projectPath . `
  -buildTarget Android -executeMethod PlantDoctor.EditorTools.BuildScript.CommandLineBuild -logFile build.log
```

Output: `Builds/Android/PlantDoctor.apk`

The build step re-applies all settings (`Tools > AI Plant Doctor > Configure Project for Android AR`):
IL2CPP + ARM64, OpenGL ES 3 (ARCore's camera shader needs it), API 24+, portrait,
ARCore loader on, the URP AR background feature, and plain HTTP allowed for a LAN server.

## 2. Install

Enable *Developer options → USB debugging* on the phone, connect it, then:
```powershell
& "C:\Program Files\Unity\Hub\Editor\2022.3.62f3\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe" install -r Builds\Android\PlantDoctor.apk
```
Or copy the APK to the phone and open it (allow "install unknown apps").

On first launch, allow camera access. If the phone supports ARCore but *Google Play
Services for AR* is missing, the app offers to install it.

## 3. Scan a plant

The app opens in the **classic interface**: a top bar with the step (1 of 8), a plant behind it, and
one bottom sheet that changes with each step. Nothing moves on its own; the plant and the leaf only
turn when you drag them.

1. **Plant Doctor** (welcome): *Scan a plant*, or *Try a demo* to run the app without a leaf.
2. **Scan a leaf**: fill the frame with one leaf. The Light / Focus / Leaf chips turn green when the
   frame is good, and the frame you line up is exactly the region that is checked and analysed.
   Tap the shutter; a blurry, dark or leafless frame is rejected with the reason and never sent to the AI.
3. **Plant identified**, then **Health check** (what the AI estimated against a healthy plant; tap a row
   to learn what it means), then **Diagnosis**.
4. **Zoom in**: the magnified leaf, with labels that point at the real spots. Drag to turn it, pinch to
   zoom, double-tap to reset. It is a generated stand-in leaf, not a photo of yours (your scan is shown
   beside it for comparison).
5. **How it spread**: the plant replays how the infection developed.
6. **Treatment**: each step with a *Do this step* button; after each one the plant fast-forwards a few
   days so you can see the effect.
7. **Recovery** and **Prevention** tips.
8. **Health report**, then *Scan another plant*.

In AR the plant stands on a surface the phone has really detected, from the identification to the
report: a ring shows where (green = solid plane, amber = estimate), it places itself when the ring is
steady, a tap puts it exactly where your finger points, and *Place it in front of me* is an explicit
fallback. Without ARCore the plant is drawn behind the sheet instead.

**The other interface.** The project also contains a card-based *Dashboard* interface (one big scan
button, a single Diseased to Recovered animation page, scan history, PDF report). Choose it with
`Tools > AI Plant Doctor > Build App Scene (Dashboard UI)` or, from the command line,
`-executeMethod PlantDoctor.EditorTools.BuildScript.CommandLineSetup -uiStyle dashboard`
(`tools
un-tests.ps1 -Style dashboard` tests that one). `Build App Scene` builds the classic one.

## 4. Which AI answers?

The project has **no trained model yet**, so by default the app uses **Demo AI**: your scan
really goes through the camera, the quality checks and the whole pipeline, but the
*diagnosis* comes from the scenario chosen in **Settings (gear) → Demo plant / Demo condition**.
Every such result is labelled **DEMO AI** — that is deliberate, not a bug.

| Demo plant | Twin | Conditions |
|---|---|---|
| Tomato | compound photographic leaves, round fruit, roots on the ground | Early blight · Late blight · Healthy |
| Potato | low bushy clump with several stems, no fruit | Early blight · Late blight · Healthy |
| Bell pepper | simple photographic leaves, bell fruit | Bacterial spot · Powdery mildew · Healthy |
| Apple | small tree with trunk, canopy and apples | Apple scab · Cedar apple rust · Healthy |

## Tracing and planting the spots

After a scan the app traces what is wrong on **your** leaf, straight from the photo's pixels (no model
needed, `Assets/Scripts/AI/LeafFeatureTracer.cs`): it finds the leaf, then the dark or brown lesions and
the yellowed patches on it.

- **Tracing**: the scan photo in the results sheet gets outlines (leaf in mint, lesions red, yellow patches
  amber) and a caption such as "Traced on your scan · 7 lesions, 1 yellow area". These traced spots also
  replace the demo's fixed boxes in the AI result, so the AR markers use them.
- **Planting**: the same spots become the disease masks of the 3D plant. The magnified leaf at *Zoom in*
  shows them where they are on your leaf (its labels point at them), and the plant's leaflets show them as
  a repeated, mirrored pattern. Spots grow from their centres as severity rises and shrink as the
  treatment works.

Limits: it is colour analysis, so a leaf on a green background, strong shadows or a very dark leaf can fool
it. When no leaf or no spots are found (and in Demo Mode without a photo) the app falls back to the
authored disease look. Healthy results are not traced.

There are no per-species 3D models: one generator builds a **plant** or **tree** from the
identified species' profile (`Assets/Data/Species/`), and the disease's own look (masks and
colours, e.g. orange rust spots, white mildew) is projected onto it. Leaves are cut-outs of real
photographs from the PlantVillage dataset (CC0), so their outline and veins are real; each plant
stands on a small heap of soil with a branching root system lying on the surface it is placed on
(no pot). Roots never go below the surface, so the plant looks planted on any table, floor or lawn.
To look at the plants without the app, run `Tools > AI Plant Doctor > Render Plant Gallery`; the
pictures land in `docs/screenshots/plants/`.
With Server AI, the plant the model identifies picks the twin the same way.

For real diagnoses, run the inference server with a trained model (see `server/README.md`),
then in the app open **Settings (gear) → Server AI**:

- Address: `http://<your PC's LAN IP>:8000/v1/analyze` (find the IP with `ipconfig`)
- API key: only if you set `PLANTDOCTOR_API_KEY` on the server
- **Test connection** reports whether the server is reachable and whether a model is loaded.

The phone and PC must be on the same Wi-Fi, and Windows Firewall must allow port 8000.
Until a model is installed the server answers `MODEL_UNAVAILABLE`, and the app says so.

## Troubleshooting

| Symptom | Cause / fix |
|---|---|
| Black screen behind the UI in AR | The URP AR background feature is missing — run *Configure Project for Android AR* and rebuild. |
| "Camera permission was denied" | Android Settings → Apps → Plant Doctor → Permissions → Camera. |
| Always camera mode on an ARCore phone | Install/update *Google Play Services for AR* from the Play Store. |
| Scan always "too blurry" | Tap the screen to focus, step back a little, or add light. The threshold is `minSharpness` in `Assets/Data/Config/ImageQualitySettings.asset`. |
| Server test fails | Same Wi-Fi? Firewall open on 8000? Server started with `--host 0.0.0.0`? |

## Re-generating the scene

Everything in the scene is generated (no hand-wired prefabs):
`Tools > AI Plant Doctor > Build App Scene` rebuilds `Assets/Scenes/PlantDoctor.unity`,
the plant twin, materials, UI sprites, fonts and the full UI.

Tests: `powershell -ExecutionPolicy Bypass -File tools\run-tests.ps1` (add `-Screens` to
refresh `docs/screenshots`).
