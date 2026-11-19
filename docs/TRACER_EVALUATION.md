# How well does the leaf tracer work? (measured on real photos)

The tracer (`Assets/Scripts/AI/LeafFeatureTracer.cs`) finds the leaf in a scan and traces the spots on it from the photo's colours. Until 19 Oct 2026 it had only been tested on a synthetic leaf. This page is what was measured on real photos, how, and what it does and does not mean. Every decision behind it is in `docs/DECISIONS.md`.

## Data (all kept outside the app; `tools/assets/_cache`, never in the APK)

| Set | Source | Use |
|---|---|---|
| PlantSeg, 4 crops (apple, tomato, potato, bell pepper) | [Wei et al., PlantSeg, Zenodo v6](https://zenodo.org/records/16665770), CC BY 4.0 per record (the newest Zenodo version says CC BY-NC 4.0; evaluation use only, not redistributed). Real in-the-wild photos with human-drawn disease masks. Official split. | 360 photos sampled from Training/Validation to tune; **393 Test photos held out** and scored once for the final numbers |
| PlantVillage healthy, 96 leaves | CC0, lab close-ups | false alarms |
| PlantVillage diseased, 270 leaves (9 classes) | CC0, lab close-ups, class labels only (no masks) | does the tracer flag more of a diseased leaf than of a healthy one? Even-numbered photos tuned, odd-numbered held out |

Every photo is centre-cropped to a square and resized to 224 px, like a scan. The real C# tracer is run headless in Unity (`Assets/Editor/TracerEval.cs`) and scored by `tools/eval/score_tracer.py`, `score_healthy.py` and `score_detect.py`.

## Results

**1. Pixel accuracy against the human masks (PlantSeg, held-out 393 photos)**

| | IoU | Dice | precision | recall |
|---|---|---|---|---|
| Tracer before this work (fixed hue rules) | 0.077 | 0.143 | 0.33 | 0.09 |
| **Tracer now** | **0.172** | **0.294** | 0.30 | 0.29 |
| Classical baseline: GrabCut leaf + Otsu on Lab a* | 0.198 | 0.330 | 0.57 | 0.23 |
| Marking every pixel as diseased (a trivial reference) | 0.176 | 0.299 | 0.18 | 1.00 |

By crop (IoU): bell pepper 0.25, potato 0.22, tomato 0.18, apple 0.12. For the diseases the app shows as spots, 0.19; for others (viruses, mildew, leaf mould, fruit rot) 0.13.

*What this means:* it is about twice as good as it was, but at pixel level it is no better than a trivial reference and a little below a simple GrabCut baseline. **Do not rely on its outlines being exact on busy, in-the-wild photos.** Reasons seen in the photos: the human masks outline whole diseased regions (mottling, dead tan patches, rotten fruit), many PlantSeg photos are fruit, tubers or several leaves rather than the single leaf close-up the app asks for, and a colour threshold cannot tell a shadow from a lesion.

**2. False alarms on healthy leaves (96 real healthy photos)**: on average 2.0% of a healthy leaf is flagged (median 0.5%); 34% of photos have more than 1% flagged, none more than 5%. The first version of the redesign flagged 46% of every healthy leaf (it split any even leaf in two); a minimum-gap rule fixed that.

**3. Diseased vs healthy on lab-style close-ups (PlantVillage, held-out half)**: the affected share separates them with AUC 0.85 (chance is 0.5). At a 5% threshold: 45% of diseased leaves are caught, 96% of healthy ones are left alone.

**4. The "about N% of the leaf" figure in the results sheet.** On in-the-wild photos it does not track the human masks (mean error 32 percentage points, correlation -0.11), so treat it as a rough colour estimate; on clean close-ups it is only weakly informative (it misses more than half of lightly diseased leaves). The caption says "Rough estimate from colour" for that reason, and the number is rounded to the nearest 5%.

Speed: about 90 ms per scan in the Editor.

## What was not done / cannot be said
* No real phone photos were available, so nothing here is measured on the user's phone camera.
* No learned segmenter was tried (no model, and PyTorch cannot run on this laptop).
* The human masks were drawn by the dataset's annotators, not by us; they are not perfectly consistent (some mottled leaves are marked, similar ones are not).
* The tuning choices (threshold mode, closing radius, minimum gap, minimum deviation) were picked on the tuning photos only; the held-out numbers above were computed once afterwards.

## Reproduce
```
py -3.11 -I tools/eval/prep_plantseg.py <PlantSeg folder> tools/assets/_cache/tracer_eval
py -3.11 -I tools/eval/prep_healthy.py tools/assets/_cache/plantvillage tools/assets/_cache/tracer_eval/healthy
py -3.11 tools/eval/fetch_pv_diseased.py tools/assets/_cache/tracer_eval/pv_diseased 30
tools/compile.sh -executeMethod PlantDoctor.EditorTools.TracerEval.Run -evalDir <folder>   # runs the real tracer
py -3.11 -I tools/eval/score_tracer.py <folder> --baselines --gallery 12
py -3.11 -I tools/eval/score_healthy.py <healthy folder>
py -3.11 -I tools/eval/score_detect.py <healthy folder> <diseased folder> --parity odd
```


## The crops and diseases added with the full PlantVillage set (1 Nov 2026)

The tracer was **not re-tuned** for the new crops: all of these photos are new to it, so every number below is held out.
566 diseased photos (19 classes, 27-30 each) and 192 healthy photos (8 crops, 24 each), PlantVillage lab close-ups, 224 px.

| | Result |
|---|---|
| Diseased vs healthy (AUC of the affected share) | **0.71** (the first four crops: 0.85) |
| At a 5% threshold | 42% of diseased leaves caught, 88% of healthy leaves left alone |
| Healthy leaves | median 1.3% flagged; 0-25% of photos above 5% depending on the crop (cherry 25%, grape 21%, blueberry 17%, corn 13%, raspberry and strawberry 8%, peach and soybean 0%) |

By disease (share of photos with more than 5% of the leaf flagged):

* **Found well**: strawberry leaf scorch 97%, grape esca 87%, grape black rot 83%, corn northern leaf blight 80%, tomato yellow leaf curl 60%, grape leaf blight 57%.
* **Partly**: tomato septoria 50%, tomato mosaic 50%, apple black rot 47%, corn gray leaf spot 47%, peach bacterial spot 34%, corn common rust 27%, orange greening 23%, tomato bacterial spot 20%.
* **Missed**: tomato leaf mold 13%, cherry powdery mildew 7%, squash powdery mildew 3%, tomato target spot 3%, tomato spider mites 0%.

Why: the tracer looks for tissue that is *redder* than the leaf's own green (CIELAB a*). Brown, purple and red-brown lesions stand out; **white** powdery mildew, the faint stippling of spider mites, the pale patches of leaf mould and the yellow mottle of citrus greening barely change a*, so they are missed. Detecting them would need a second cue (whiteness / loss of saturation, yellow shift in b*) and a new tuning round on separate photos; that was not done here.

What it means in the app: for the missed diseases the traced outline and "about N% of the leaf" will usually say little or nothing, and the plant then shows the disease's authored look instead (planting only uses a trace when something was found).
