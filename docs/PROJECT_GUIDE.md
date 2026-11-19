# AI Plant Doctor: every decision and the tech behind it, in plain language

This is the one-stop explanation of the project. It says **what** was chosen, **why**, and **who decided** (you, or Claude on its own). It is written for someone who does not need to know the jargon; hard words are explained in the glossary at the end.

Dates are October 2026. The detailed, numbered log of the most recent work is in `docs/DECISIONS.md`; this page covers the whole project.

---

## 1. What the app is

AI Plant Doctor is an **Android app** that looks at a leaf through the phone camera, works out what plant it is and whether it is sick, and then **teaches** you what to do about it: it shows a 3D copy of your plant ("the twin") with the disease on it, walks you through treatment steps, shows how the plant recovers, and ends with a prevention checklist and a health report.

Fourteen plants are supported, every crop in the PlantVillage dataset: **tomato, potato, bell pepper, apple, blueberry, cherry, corn, grape, orange, peach, raspberry, soybean, squash, strawberry**, with 27 diseases.

Important honesty point: **there is no trained AI model yet.** Until there is one, the app runs in **Demo mode**: it pretends to analyse and always labels the result **"DEMO AI"**. That is on purpose (see section 4).

---

## 2. The tech stack (what is used, and why, simply)

### The app itself

| Tool | What it is | Why it was chosen |
|---|---|---|
| **Unity 2022.3.62f3 (LTS)** | A game engine: draws 3D, runs on phones, handles the camera | It can draw the 3D plant, run on Android and use the phone's augmented-reality (AR) features, all from one code base. LTS = long-term support, so it stays stable. |
| **C#** | The programming language Unity uses | Required by Unity. |
| **URP (Universal Render Pipeline) 14** | Unity's lightweight way of drawing graphics | Fast enough for phones and still gives shadows and lighting. |
| **AR Foundation 5.2.2 + ARCore 5.2.2** | Unity's AR layer, plus Google's AR for Android | Lets the 3D plant stand on a real table or floor seen through the camera. |
| **Input System 1.7, TextMeshPro 3.0.6** | Touch/keyboard input and crisp text | Standard Unity parts for phone touch and sharp text on small screens. |
| **Custom shaders (HLSL)** | Small programs that decide how each pixel is coloured | The disease look (spots, yellowing, wilting) is drawn by a shader, so one plant can show any disease without making new 3D models. Also a soft "contact shadow" and a shadow-only surface so the plant looks sat on the real ground. |
| **Procedural meshes (code-made 3D shapes)** | The plant's stems, leaves, roots, fruit, soil are built by code at run time | Four species from one generator instead of four hand-made models; adding a plant means adding a data file, not modelling. |
| **ScriptableObjects (data files)** | Unity's way of storing data as assets | Plants, diseases, treatment plans, prevention plans and species looks are data, not code. |
| **A state machine** | A rule-book of "which screen/step comes next" | The app is a guided flow (scan, identify, check, diagnose, zoom, spread, treat, recover, prevent, report). A state machine keeps the order clean and lets "back" work. |
| **Poppins font; Lucide icons** | The look of the text and icons | Clean modern look; both are free-to-use open licences (the icon and photo credits are in `docs/CREDITS.md`). |
| **Unity Test Framework (EditMode + PlayMode)** | Automatic tests | EditMode checks logic quickly; PlayMode runs the real app flow end to end and takes screenshots. |
| **Headless Unity ("batchmode")** | Running Unity from the command line with no window | Lets Claude build the scene, run tests, take screenshots and build the APK by script (`tools/compile.sh`, `tools/run-tests.ps1`). |

### The AI side (designed, partly built, not trained)

| Tool | What it is | Why |
|---|---|---|
| **FastAPI server (`server/`)** | A small Python web service the app can call | Keeps the heavy model off the phone. It answers `/health`, `/labels`, `/analyze`. If no model is installed it returns "model unavailable", it **never invents a prediction**. |
| **ONNX / onnxruntime** | A standard file format for trained models | So a model trained in PyTorch can be run by the server (and later possibly on the phone). |
| **Training pipeline (`training/`)** | PyTorch, `timm` EfficientNet-B0, albumentations, scikit-learn, etc. | A plan and code to train one model with several outputs (plant, condition, visible signs, severity, photo quality) on about 17 public datasets. **It was never run**; you said the AI model work waits "after some time". |
| **"AI Model Project Memo"** (Claude Docs) | A written design for that model | So the model can be built later without re-deciding everything. |

### Python tools used around the app

| Tool | Used for |
|---|---|
| **NumPy, OpenCV, Pillow** | Cutting leaf photos out of their backgrounds, building textures, drawing and scoring masks. |
| **Hugging Face "datasets-server"** | Fetching PlantVillage leaf photos (flaky, so scripts retry). |
| **Zenodo** | Downloading the PlantSeg dataset. |
| **Windows tools** | Git Bash and PowerShell on a Windows 11 laptop; the project is **not** a git repository (backups are zip files and archived APKs). |

### Data used (all free to use)

| Data | Licence | Used for | Shipped in the APK? |
|---|---|---|---|
| **PlantVillage** healthy and diseased leaf photos (via a Hugging Face mirror) | CC0 (no restrictions) | Realistic leaf textures for the plants; healthy and diseased test photos | Leaf cut-outs only |
| **PlantSeg** (Wei et al., real field photos with human-drawn disease masks) | CC BY 4.0 on the version used | Testing how well the spot tracer works | **No** |
| Photos of diseases/treatments in the app | CC0 / CC BY / CC BY-SA, credited in `docs/CREDITS.md` | Illustrations | Yes |

### Methods used for measuring (explained in section 7)

CIELAB colour, Otsu thresholding, GrabCut, IoU / Dice / AUC scores, a held-out test set.

---

## 3. How the app works (short tour)

1. **Scan**: the phone camera shows a guide frame. The app checks the photo is good enough (sharp, lit, not shaky). A bad photo is **rejected before any analysis** and the user is asked to retry.
2. **Analyse**: the photo goes to the "AI layer". Today that is the Demo AI. Later it is the server or an on-phone model. The AI layer and the rest of the app only talk through one plain data object, so swapping the AI changes nothing else.
3. **Identify, health check, diagnosis**: the result is matched to the app's plant and disease data. If the AI is **unsure**, the app says so and does not treat it as a diagnosis.
4. **Zoom in**: a magnified, turnable leaf with labels that point at the real spots.
5. **How it spread, treatment, recovery, prevention, report**: the 3D plant shows the disease and then recovers as treatment steps are done. Treatment is **never** shown as a guaranteed full cure.
6. **Back / home** buttons work at every step.

The 3D plant exists twice: one placed in AR on a real surface, and one in a separate viewer used behind the screen when AR is not available (for example in the editor or on a phone without ARCore).

---

## 4. Rules the project follows (the "never do" list)

These came up again and again and shaped many decisions:

* **Never pass off a demo as real.** Demo results are labelled "DEMO AI"; estimates from the AI are labelled "visual AI estimate", never as a sensor reading.
* **Never make up a prediction.** The server refuses instead of guessing.
* **Never promise a cure.** Treatment always leaves some remaining severity.
* **Never treat an unsure result as a diagnosis.**
* **Never claim something works on a phone if it was only tested in the editor.** Every report says which is which.
* **Keep older builds.** Before each new APK, the previous one goes into `Builds/Archive/` (your request).
* **Use licensed material only.** Free-to-use photos and fonts, credited. Paid "premium templates" cannot be downloaded or copied, and Claude said so.

---

## 5. Every decision, in the order it happened

"By" says who decided. **You** = you asked or chose. **Claude** = Claude chose on its own, and you can overrule it.

### Phase 0: Review and design (early October)

| Decision | By | Why |
|---|---|---|
| Review the existing code before building more | You | To find problems early. The review (see `docs/CODE_REVIEW_2026-10.md`, `docs/CODE_REVIEW_APPLIED.md`) found and fixed real leaks: a texture created on every scan and never freed, event handlers that could never be removed, camera buffers not released, coroutines left in a broken state. |
| Keep the AI layer and the rest of the app separate; they share only one plain data object | Claude (in `docs/ARCHITECTURE.md`) | You can change the AI (demo, server, on-phone) without touching the plant, treatment or screens. |
| Take a backup zip before changing things (`plant_backup_2026-10-09_before-claude.zip`) | Claude | So the original state is never lost. Note: that zip holds your **older plain "STEP n/7" screen**, not the dark glass design (an earlier message of Claude's wrongly said otherwise). |

### Phase 1: Redesign and the 3D plant (9 to 12 October)

| Decision | By | Why |
|---|---|---|
| A full visual redesign, focused on how the AR plant looks | You | The plant look was your main concern. |
| First design: dark glass, Poppins font, mint accent, a top bar with step segments and one bottom sheet per step (7 steps) | Claude | A clean, modern look that leaves room for the 3D plant. |
| Show disease with a shader + masks on **one** plant instead of separate diseased models | Claude | Any disease can be projected on any species; recovery is just fading the masks. |
| One "health state" object is the single source of truth for how sick the plant looks; one component (`PlantVisualBinder`) applies it | Claude | Nothing else touches how the plant is drawn, so nothing disagrees. |
| Apply health to many parts without copying materials (`MaterialPropertyBlock`) | Claude | Cheaper on phones. |

### Phase 2: Real phone-camera scanning (about 12 October)

| Decision | By | Why |
|---|---|---|
| Scan with the phone camera, with a quality check before analysis | You asked, Claude built | Bad photos give bad answers; they are rejected before reaching the AI. |
| If AR is not available (editor, unsupported phone), fall back to plain camera mode | Claude | The app must still work everywhere. |
| Demo mode with a settings screen to pick the demo plant and condition, always labelled "DEMO AI" | Claude | You can show and test the whole flow with no model, honestly. |
| Optional connection to the FastAPI server (address typed in Settings, "Test connection") | Claude | For when a real model exists. |

### Phase 3: Four species from one generator (12 October)

| Decision | By | Why |
|---|---|---|
| Support tomato, potato, bell pepper and apple | You | These were the crops chosen. |
| One generator builds any species from a small data file; no per-species 3D models | Claude | Adding a plant is data entry, not modelling. Two body plans: soft-stemmed plants (tomato, potato, pepper) and a tree (apple). |
| Each disease has its own look (orange rust, white mildew, ringed spots) through its own masks and colours | Claude | So different diseases look different on the same plant. |
| Potato, pepper and apple diseases and treatment content written to mirror the AI memo; product names left as "to be checked locally" | Claude | Avoid giving specific product advice that was not verified for your region. |

### Phase 4: The AI model plan (12 October)

| Decision | By | Why |
|---|---|---|
| Write an AI model memo and a training pipeline (one EfficientNet-B0 with five outputs) and a server | You asked, Claude built | So a model can be trained later. |
| **Do not train now** ("after some time") | You | No model for now. The pipeline was compiled but never run end to end. |
| Learned (neural-network) methods are off the table for now | Claude (forced) | Windows **Smart App Control** blocks PyTorch's files on this laptop (turn it off, or use Linux inside WSL2; your call). |

### Phase 5: Interface changes (14 to 19 October)

You tried several looks. They are all still understood:

| Step | What happened | By |
|---|---|---|
| 12 Oct | First design (dark glass) | Claude |
| 14 Oct | "Classic muted" restyle (charcoal/ivory) plus a new **Zoom in** step. You found it gloomy, the plant hovering, and the leaf animation bad. | You asked, Claude built |
| 15 Oct | A 12-screen "template" dashboard with a one-page "Cure journey". You asked for "premium templates"; Claude said it cannot download paid templates and built its own. | You asked, Claude built |
| 19 Oct | You asked to **go back to the first UI, but keep the leaf**. You chose "the very first UI from before I started changing things" and "nothing else, only the leaf" (plus the AR placement fix and a plant that does not hover). | You |
| 19 Oct | The first UI was **rebuilt by replaying Claude's own earlier work** from the transcript. | Claude |
| 19 Oct | **Both interfaces stay in the project.** The first (called "Classic") is the default; the template dashboard is still available (`-uiStyle dashboard`). | Claude |

Why keep both: nothing was thrown away, and you can switch back.

### Phase 6: Plant stays put in AR (19 October)

| Decision | By | Why |
|---|---|---|
| Place the plant by testing, in order: a detected flat surface, an estimated surface, then loose feature points; smooth the reticle; attach the plant to the surface's anchor | Claude | A plant that drifts or floats looks fake. |
| Only real buttons block taps and drags on the plant | Claude | Taps on empty UI should not be swallowed. |
| A soft contact shadow plus a shadow-only surface, since AR has no ground to receive shadows | Claude | Makes the plant look sat on the real surface. |

### Phase 7: Three corrections (19 October)

| Decision | By | Why |
|---|---|---|
| **Zoom in shows only the leaf**; the plant is hidden at that step (viewer and AR) | You | The plant and leaf were showing and scrolling together. |
| **Back and home buttons throughout**: the arrow goes back one step (cancelling the analysis if one is running), the house starts over with a confirmation if results would be lost; the Android back key works like the arrow | You | You wanted to cancel or go back at any point. |
| Back from the identified screen goes to a fresh scan | Claude | The old result is discarded, so scanning again must start clean. |
| **Realistic plants, no pots, a root system** | You | You found the first plants silly and the pots "idiotic". |

### Phase 8: How the realistic plants were made (19 October)

| Decision | By | Why |
|---|---|---|
| Leaves are **cut-outs of real PlantVillage photos**, folded and bent a little, three variants per species | Claude | Real outline and veins beat drawn leaves. CC0, so no restrictions. |
| Per-species leaf photos packed into one picture ("atlas") | Claude | A whole plant is a handful of draw calls, which phones need. |
| Stem, bark, soil and root textures made from noise (no photos) | Claude | Free of licences, and tileable. |
| No pot: a heap of soil at the base and roots on the ground | You asked, Claude built | |
| Roots never go below the ground plane | Claude | AR has no ground that could hide them; underground roots would float in the air. |
| Roots rebuilt as **fibrous systems**: a few thick main roots, three levels of side roots, root hairs, soil crumbs, four tints | You asked (the first roots "looked unrealistic"), Claude built | The first roots were smooth pale sticks. |
| The lowest leaf grows higher on tomato and pepper | Claude | So the roots are not hidden by leaves. |

### Phase 9: Feature tracing and planting (19 October)

| Decision | By | Why |
|---|---|---|
| "Tracing" = outline the sick spots on the scan photo; "planting" = the same spots become the disease on the 3D plant | You chose this from options | The words did not exist in the code, so Claude asked. |
| Trace with plain colour analysis, no AI model | Claude | No model exists; it works offline and fast. |
| The traced spots replace the demo's fixed boxes in the AI result, but only when the AI result came from the demo or had no regions | Claude | A real server's regions are not overwritten. |
| Healthy results, demo mode without a photo, or "nothing found" keep the authored disease look | Claude | Never invent spots. |
| The magnified leaf gets the spots exactly where they are on your leaf; the plant's small leaflets get a repeated, mirrored version | Claude | A small leaflet's window onto the mask would often miss a single spot. |
| Spots grow from their centres as severity rises | Claude | It matches how lesions grow. |

### Phase 10: Going deeper, not bigger (19 to 20 October)

You asked what a top professional would do. Claude's answer: **measure**, do not just draw. You said you could not give phone photos or a trained model, so Claude used what was available. The full numbered log is `docs/DECISIONS.md`; in short:

| Decision | By | Why |
|---|---|---|
| Use public datasets with real photos and human-drawn masks for testing; web search and downloads allowed, up to 65 GB | You | |
| Use **PlantSeg** (CC BY 4.0 version) for pixel scoring, **PlantVillage** for healthy and diseased close-ups | Claude | Real photos with human masks; free to use. |
| Keep all test data outside the app (`tools/assets/_cache`) | Claude | The APK stays small; licences stay a research matter. |
| Test the **real C# tracer** by running Unity from the command line, not a Python copy | Claude | A copy could drift from what the app runs. |
| Tune on one set of photos; score once on a held-out set | Claude | Otherwise the numbers flatter the method. |
| **No learned segmenter** | Claude | PyTorch cannot run here and there is no model. |
| Replace the first tracer's fixed colour rules with a colour split per photo (CIELAB a*, Otsu), plus a "minimum gap" rule and a fallback for small spots | Claude | Measured on real photos the first tracer was poor (IoU 0.105 on tuning photos), and an even healthy leaf was wrongly split (46% flagged). |
| Roots **partly buried** under soil ridges | You (chosen from options) | Closest to how roots really are. |
| Leaves at **fully real** proportions (tomato leaf about 20% of the plant's height, was 52%), with more leaves | You (chosen from options) | Matches published plant descriptions. |
| Fruit shrunk with the leaves (tomato 0.05 to 0.03, apple 0.035 to 0.017) | Claude | With real-size leaves the apples were twice as wide as the leaves were long. Easy to revert; you did not approve this separately. |
| Add a **"leaf area affected"** figure to the results caption, rounded to 5% and worded "Rough estimate from colour" | You asked, Claude built | **Claude's own measurements show it is unreliable on real photos** (see section 7). It is left in for you to keep or drop. |
| Save each old APK in `Builds/Archive/` before building the next | You | So you can always go back. |
| Write every decision in `docs/DECISIONS.md` | You | To track progress. |

### Phase 11: All PlantVillage plants and diseases (1 November)

| Decision | By | Why |
|---|---|---|
| Add every PlantVillage crop and disease: 14 crops, 26 diseases (plus pepper powdery mildew already there) | You | PlantVillage is the dataset the future AI model will most likely be trained on, so the app should know every class it has. |
| Blueberry, raspberry and soybean have no diseases | Claude | PlantVillage only has "healthy" photos of them. |
| New body plans: strawberry and squash grow leaves from a crown (rosette), corn is a grass with strap leaves, brace roots and a tassel, grape is a trained vine with arms, shoots, tendrils and bunches, blueberry is a many-stemmed shrub | Claude | Each plant should look like itself, not like a tomato or an apple. |
| Sizes from published plant descriptions (Flora of China, Flora of North America, Flora of Pakistan), tested automatically; where a number was missing, the assumption is written down | You asked, Claude built | "With all the precise measurements and ratios". See `docs/PLANT_MORPHOLOGY_SOURCES.md`. |
| Root details from sources: blueberry roots have no root hairs, soybean roots have nodules, corn has brace roots | Claude | Real differences between these root systems. |
| Orange and squash leaves made from PlantVillage's diseased photos (the cleanest ones); corn and squash leaf shapes drawn around real leaf surface | Claude | PlantVillage has no healthy orange or squash photos and no whole corn leaves. |
| Viruses, citrus greening and esca are shown as incurable: the plan removes plants or wood and controls the insect that spreads them | Claude | The app must never promise a cure that does not exist. |
| The zoomed leaf shows each plant's own leaf | Claude | A corn leaf for corn, a grape leaf for grape. |
| The demo picker scrolls through the 14 plants and lists up to ten conditions | Claude | Four buttons could not hold them. |

---

## 6. What the "AI layer" can be (and is today)

| Option | State | Notes |
|---|---|---|
| **Demo (Mock) AI** | Used today | Fixed fake results, always labelled "DEMO AI". |
| **Remote AI (FastAPI server)** | Built, no model installed | Returns "model unavailable" until a model is put in `server/models/`. |
| **Local on-phone AI** | Designed, not done | Needs a trained model first. |

---

## 7. How well does it work? (measured, in simple terms)

Full detail: `docs/TRACER_EVALUATION.md`.

* **The spot tracer on real field photos** (393 photos it had never seen, scored against human-drawn masks): about **twice as good as before**, but at the pixel level **no better than simply marking every pixel as diseased**. So outlines can look right and still be wrong on busy photos (fruit, several leaves, shadows).
* **On healthy leaves** it now flags about **2%** on average (96 real healthy leaves), and never more than 5%. Before the fix it flagged 46%.
* **On clean lab-style close-ups** (plain background, like the best scan) it tells diseased from healthy leaves reasonably well (about 85% on the AUC score), but catches fewer than half of lightly diseased leaves.
* **The "about N% of the leaf" number is not trustworthy** on real photos (typically off by about 30 percentage points). Keep it only as a rough hint, or remove it.
* **Plants and roots**: published numbers exist for leaf sizes and plant heights, and tests now check against them. **No published measurements of adult root spread were found** for these four crops, so the root *shape* (branching, hairs, fibrous) is believable but not measured.

### What has **not** been checked
* Nothing has been tested on **a real phone's camera** in the field, only in the Unity editor with a synthetic leaf and public photos.
* **AR placement**, the **Android back key**, and how the **roots look in AR** are untested on a device.
* No expert annotators and no blind "does it look real?" test with other people.
* The AI model does not exist, so plant and disease *identification* is the demo.

---

## 8. Where things are

| Place | What is there |
|---|---|
| `Assets/Scripts/` | The app: `Core` (flow, state machine), `AI`, `AR`, `Plants` (twin builder, roots, leaves), `UI` (`Classic` and the dashboard), `Treatment`, `Diseases`, `Simulation` |
| `Assets/Editor/` | Scripts that build the scene, the plants' data, the UI, the plant gallery and the tracer test harness |
| `Assets/Tests/` | EditMode and PlayMode tests |
| `Assets/Art/Plants/` | Leaf photo cut-outs per crop and the shared stem, bark, soil and root textures |
| `server/` | The FastAPI inference server |
| `training/` | The model training pipeline (never run) |
| `tools/` | Build, test and image-processing scripts; `tools/eval/` scores the tracer |
| `docs/` | This guide, `DECISIONS.md`, `TRACER_EVALUATION.md`, `PLANT_MORPHOLOGY_SOURCES.md`, `PHONE_AR_GUIDE.md`, `ARCHITECTURE.md`, `CREDITS.md`, screenshots |
| `Builds/Android/PlantDoctor.apk` | The newest build |
| `Builds/Archive/` | Older builds: realistic plants and back button, feature tracing, realistic roots |

### Handy commands (Windows)
* Compile check: `bash tools/compile.sh`
* Rebuild the app scene and data: `bash tools/compile.sh -buildTarget Android -executeMethod PlantDoctor.EditorTools.BuildScript.CommandLineSetup`
* Run all tests and re-take screenshots: `powershell -ExecutionPolicy Bypass -File tools\run-tests.ps1 -Screens` (close the Unity editor first)
* Build the APK: `bash tools/compile.sh -buildTarget Android -executeMethod PlantDoctor.EditorTools.BuildScript.CommandLineBuild`
* Render all four plants to pictures: Unity menu **Tools > AI Plant Doctor > Render Plant Gallery** (output in `docs/screenshots/plants/`)

---

## 9. Lessons learned (things that bit during the work)

* The interface mixes colours in **linear** colour space, so see-through white must be very faint or it turns grey.
* Unity only saves a script into a scene if the file is named after the script (one script per file).
* A slider's handle stretches to the full height of its area.
* The state machine announces a change **before** the new state runs, so screens must refresh when told the content changed.
* In automated PlayMode tests, avoid waiting for the end of a frame; and a tiny camera distance makes mobile text vanish.
* Hugging Face downloads are unreliable; scripts retry and skip what they already have.
* A test that only uses a synthetic example can look perfect and still fail on real photos (it did).

---

## 10. Glossary

* **AR (augmented reality)**: putting 3D things on top of the real camera view.
* **APK**: the Android app file you install.
* **ARCore**: Google's AR for Android.
* **Atlas**: many small pictures packed into one big picture.
* **AUC**: a score from 0.5 (no better than guessing) to 1.0 (perfect) for telling two groups apart.
* **Batchmode / headless**: running Unity from the command line with no window.
* **CC0 / CC BY**: licences. CC0 means no restrictions; CC BY means you must credit the author.
* **CIELAB**: a way of describing colour close to how people see it. `a*` runs from green (negative) to red (positive).
* **Demo mode**: the app pretends to analyse a leaf, and says so.
* **Dice / IoU**: scores from 0 to 1 for how well a drawn region matches a correct one. 1 is a perfect match.
* **FastAPI**: a Python tool for making a small web service.
* **FSM (state machine)**: a rule-book of which step may follow which.
* **GrabCut**: a classic method for cutting an object out of a picture.
* **Held-out set**: test photos kept aside and used only once at the end, so the score is honest.
* **Mask**: a black-and-white picture marking where something is (here, where the disease is).
* **Mesh**: a 3D shape made of triangles.
* **ONNX**: a file format for trained AI models.
* **Otsu**: an automatic way to choose a cut-off between two groups of values.
* **Procedural**: made by code instead of drawn by hand.
* **PlantSeg / PlantVillage**: public datasets of plant-disease photos.
* **Shader**: a small program that decides how each pixel is coloured.
* **Smart App Control**: a Windows security feature that blocks unsigned programs; here it blocks PyTorch.
* **Twin**: the app's 3D copy of your plant.
* **URP**: Unity's light-weight rendering system.

## Phase 12: PlantSeg plants and diseases, still and species-true zoom leaf (10 Nov 2026)

* **33 plants, 118 diseases**: PlantVillage (14 crops) + PlantSeg (Wei et al. 2024; 18 more plants: banana, basil, bean, broccoli,
  cabbage, carrot, cauliflower, celery, coffee, cucumber, eggplant, garlic, ginger, lettuce, maple, plum, rice, tobacco, wheat; 91 more
  diseases); PlantDoc labels resolve too. Every plant has diseases.
* Disease content is data: `tools/data/*.py` -> `tools/data/make_plantseg_json.py` -> `Assets/Editor/Data/PlantSeg/*.json` ->
  `PlantSegDataBuilder` during project setup. Edit the Python content files, re-run the script, re-run setup.
* Diseases know where they show (`affectedPart`); fruit, head and root diseases are drawn on that organ, on the twin and at the zoom step.
* Zoom leaf: `HeroLeafBuilder` (whole compound, trifoliate, grass blade, simple; `BuildOrgan` for fruit); fades in once the sheet has
  settled and never moves on its own.
* New body plans: Banana, Shrub (coffee), Climber (cucumber), multi-tiller grasses with heads; `CropMeshes` (12 organs), `FlowerMeshes`.
* Leaf textures: `tools/assets/make_species_leaves.py` (PlantVillage crops), `make_commons_leaves.py`, `make_new_leaves.py`, then
  `make_plant_textures.py`. Gallery: `-executeMethod PlantDoctor.EditorTools.PlantGallery.Capture [-species Banana,Wheat]`.
* Not done: the AI model is still untrained (Demo AI); the training taxonomy covers the original four crops only
  (`training/dataset_label_map.json` maps all dataset labels for a future run); disease content not reviewed by a plant pathologist.
