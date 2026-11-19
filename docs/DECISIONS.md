# Decision log

Every decision on the "deeper, not bigger" work (tracing, planting, plants, roots) is logged here, newest at the bottom, with why it was made and what it rests on. Entries say who decided: **user** or **Claude**.

## Starting point (19 Oct 2026)

| # | Decision | By | Why |
|---|---|---|---|
| 1 | Realistic plants use CC0 PlantVillage leaf photos, no pots, procedural roots on the ground | user asked, Claude built | User found the first plants "silly" and the pots "idiotic". |
| 2 | Feature tracing = outline spots on the scan photo; feature planting = the same spots become the 3D plant's disease masks | user (chose option) | The words were not in the code; the user picked this meaning from options. |
| 3 | Roots redone as fibrous systems (3 orders of side roots, hairs, soil crumbs, 4 tints) | user asked, Claude built | User found the first roots unrealistic. |
| 4 | Older builds are kept in `Builds/Archive/` before each new build | user | "save the existing apk before you build the new one". |

Honest status at this point: nothing above was checked against real data. The tracer was tuned and tested on a synthetic leaf drawn by Claude; the roots and leaves were judged by eye on Claude's own renders.

## The plan the user approved (19 Oct 2026)

The user cannot supply real phone photos or a trained AI model now. Claude works on what is in its hands:

* **A** find verified public datasets of real diseased-leaf photos with human-drawn lesion masks.
* **B** measure the tracer on them (IoU/Dice, per-lesion precision/recall, tuned on one half, reported on the other).
* **C** compute "leaf area affected" from the trace, compare it with the human masks, and show it in the app, honestly labelled. The AI severity number stays as it is.
* **D** set the plant and root numbers from published measurements and add automated range checks. Claude asks the user before choosing between exposed roots and roots partly buried in soil.
* **E** report what passed, what failed and what is still unverified.

| # | Decision | By | Why |
|---|---|---|---|
| 5 | Web search and downloads from verified sources are allowed; 65 GB limit and the Claude usage limit | user | Needed for A. |
| 6 | Datasets go into `tools/assets/_cache` only and never into the APK | Claude | Keeps the build small; licences stay a research matter. |
| 7 | Ask the user after the root research whether roots stay exposed or are partly buried | user | The user wants to decide this. |
| 8 | Add the "leaf area affected" figure, labelled as measured from the photo; archive the current build first | user | The old build is `Builds/Archive/PlantDoctor_2026-10-19_realistic-roots.apk`. |
| 9 | No learned segmenter (U-Net etc.) | Claude | Windows Smart App Control blocks torch on this laptop and there is no model; classical OpenCV baselines only. |
| 10 | Run the real C# tracer in headless Unity to evaluate it, not a Python copy | Claude | A copy could drift from what the app runs. |

## Research results and the user's answers (19 Oct 2026)

| # | Decision | By | Why / evidence |
|---|---|---|---|
| 11 | Use PlantSeg (Zenodo, version v6, 1.7 GB, MD5 `23385d87...5b9`, CC BY 4.0) as the real-photo lesion ground truth, evaluation only, cached in `tools/assets/_cache/plantseg_raw`, never in the APK | Claude | Real field photos with human polygon masks. The newest Zenodo version lists CC BY-NC 4.0 and the paper text says CC BY-NC-ND 4.0, so the licence differs by version; v6 states CC BY 4.0. Credit the authors (Wei et al., UQ/CSIRO) if any result is published. Not redistributed. |
| 12 | Root and plant numbers are only partly measurable from published sources | Claude | See `docs/PLANT_MORPHOLOGY_SOURCES.md`: flora leaf sizes and heights exist; adult root spread/density for the four crops and open root-scan data were not found. |
| 13 | Roots: **partly buried** | user | Roots run under soil ridges near the crown and emerge toward their ends; fine roots and hairs show only where they leave the soil. |
| 14 | Leaves: **go fully real** (published leaf-to-plant proportions), with more leaves to compensate | user | Real tomato leaf is about 20% of plant height; ours was 52%. Potato's ratio is unverified in the sources and is assumed to be like tomato; apple's real ratio makes leaves tiny, so the leaf count rises sharply. Fruit sizes are not changed. |
| 15 | Fruit shrunk with the leaves: tomato radius 0.05 -> 0.03, apple 0.035 -> 0.017 (pepper unchanged) | Claude (follow-on of 14) | With real-size leaves the apples were twice as wide as the leaves are long, which no real tree has (an apple is about as wide as its leaf is long). Not separately approved; easy to revert in `SpeciesDataBuilder`. |
| 16 | Roots are partly buried: soil ridges (`RootMeshes.AddRidge`) cover the first 50-68% of each primary root; fine roots and hairs show where roots leave the soil | Claude (from 13) | Rendered in the plant gallery and checked by eye only. |

## First measurement of the tracer on real photos (19 Oct 2026)

PlantSeg photos of the four crops, centre-cropped to 224 px like a scan (`tools/eval/prep_plantseg.py`), official split: 360 sampled Training/Validation photos for tuning, 393 Test photos held out. Scored with `tools/eval/score_tracer.py` on the real C# tracer run headless (`Assets/Editor/TracerEval.cs`).

| # | Finding | Evidence |
|---|---|---|
| 17 | **The tracer as shipped is poor on real photos**: IoU 0.105 / Dice 0.189 (precision 0.35, recall 0.13) on the tuning set. A plain Lab-a* Otsu baseline gets IoU 0.213 and "mark every pixel" gets 0.147. | `tools/eval` run on `tracer_eval/tune`. The synthetic leaf I drew earlier hid this. |
| 18 | Why: the human masks outline whole diseased regions (chlorotic mottling, tan dead patches, rotten fruit, virus symptoms), while the tracer only looks for dark/brown spots and strongly yellow patches; many PlantSeg photos are also fruit, tubers or several leaves, not the single leaf close-up the app asks for | Gallery of 12 random tuning photos |
| 19 | Next: replace fixed colour thresholds with a per-photo method (colour distance from the leaf's own healthy green in CIELAB, Otsu-split), tuned on the tuning set only; the test set is only scored for the final report | Method choice by Claude |

## Tuning the tracer (tuning set only; 19-20 Oct 2026)

| # | Decision | By | Evidence (tuning set, 360 photos) |
|---|---|---|---|
| 20 | Replace the fixed hue rules by an Otsu split of CIELAB a* (greenish vs reddish) inside the leaf | Claude | IoU 0.105 -> 0.177. The colour-distance-from-healthy-green variant only reached 0.138. |
| 21 | Join the leaf with a closing of radius 6, no opening of spots | Claude | IoU 0.177 -> 0.187 |
| 22 | **Add a minimum gap between the two Otsu groups (12 a* units); below it the leaf is healthy** | Claude | Without it an even healthy leaf is split anyway: on 96 real healthy PlantVillage leaves 46% of the leaf was flagged and every photo had false spots. With gap 12: flagged share 1.2%, 6% of photos over 1% flagged; IoU on diseased photos falls to 0.171. Gap 8 -> 19% of healthy photos flagged, gap 16 -> 2% (IoU 0.151). Balanced choice by Claude. |
| 23 | Learned segmentation not tried | Claude | torch cannot run here (Smart App Control) and there is no model. |
| 24 | **Hybrid threshold**: Otsu split of a* with a 12-unit minimum gap; when there is no clear split, look for spots that are at least 11 a* units (or 2 robust deviations) redder than the leaf's median | Claude | The Otsu-only tracer found nothing on 2/3 of diseased PlantVillage close-ups (AUC 0.64, sensitivity 33%). Hybrid on the tuning halves: PlantSeg IoU 0.176 (Otsu-only 0.171), PlantVillage diseased-vs-healthy AUC 0.898, sensitivity 51% and specificity 100% at a 5% affected share; healthy leaves flagged 2.0% on average (33 of 96 above 1%, none above 5%). Median-only variants reached AUC 0.86-0.88 but IoU 0.12 and many more false spots. |

## Final held-out results and the "leaf area" figure (20 Oct 2026)

Details in `docs/TRACER_EVALUATION.md`.

| # | Decision | By | Evidence |
|---|---|---|---|
| 25 | Held-out PlantSeg test (393 photos): IoU 0.172 / Dice 0.294, up from 0.077 / 0.143, but **no better than marking every pixel (0.176 / 0.299)** and below a GrabCut + Otsu baseline (0.198 / 0.330) | Claude reporting | Scored once after tuning. Pixel-exact outlines cannot be promised on in-the-wild photos. |
| 26 | The "about N% of the leaf" figure is in the results sheet caption, rounded to 5%, worded "Rough estimate from colour", because the user asked for it | user asked; Claude built | **The evidence does not support it as a measurement**: on wild photos the error against the human masks is 32 percentage points and the correlation -0.11; on lab close-ups it separates diseased from healthy with AUC 0.85 but misses over half of lightly diseased leaves. Left in for the user to keep or drop. |
| 27 | Healthy leaves: 2.0% flagged on average (96 real leaves), none above 5%, versus 46% flagged before the minimum-gap rule | Claude reporting | `score_healthy.py` |
| 28 | Old APK of the previous build (realistic roots) archived before this build | user | `Builds/Archive/PlantDoctor_2026-10-19_realistic-roots.apk` |
| 29 | Plant leaf proportions test and half-buried-roots test added; potato leaf lowered to 0.14 so it fits the same upper bound as tomato (0.25 of height) | Claude | `SpeciesTwinTests` |

## All PlantVillage plants and diseases (1 Nov 2026)

The user asked to add the remaining PlantVillage plants and their diseases "with all the precise measurements and ratios", and to get the final APK.

| # | Decision | By | Why / evidence |
|---|---|---|---|
| 30 | Add all 14 PlantVillage crops and all 26 diseased classes: ten new crops (blueberry, cherry, corn, grape, orange, peach, raspberry, soybean, squash, strawberry), seven more tomato diseases and apple black rot | user | PlantVillage has 38 classes (plus a background class). Pepper powdery mildew stays although PlantVillage lacks it. |
| 31 | Blueberry, raspberry and soybean get no diseases | Claude | PlantVillage only has a healthy class for them; inventing diseases would go beyond "from the dataset". They can still be identified as healthy. |
| 32 | Orange and squash leaf textures come from PlantVillage's *diseased* photos, picked for the fewest visible symptoms | Claude | PlantVillage has no healthy orange or squash class. Wikimedia Commons had no clean single-leaf photos (searched). One orange photo was clean enough; squash photos are all close-ups with mildew, so the squash leaf is a cleaned patch of real surface with a drawn five-lobed outline from the Flora of China description. |
| 33 | Corn's leaf texture is a strip of a real leaf close-up turned so its veins run straight, with a drawn strap outline | Claude | PlantVillage corn photos show only part of a leaf. |
| 34 | Peach leaves are cut out with a Lab-colour + GrabCut method | Claude | The glossy, wavy peach leaves defeat the plain colour mask (holes and fragments). |
| 35 | Archived the previous APK before building | user rule | `Builds/Archive/PlantDoctor_2026-10-20_tracer-evaluated.apk` |
| 36 | New body plans: rosette (strawberry, squash), grass (corn), trained vine (grape), multi-stem shrub (blueberry, via the tree generator); new leaf forms (trifoliate, strap) and fruit (berry, cherry, peach, citrus, grape bunch, strawberry, raspberry, zucchini, corn ear, pod) | Claude | Each crop's real body plan, instead of forcing them into herb or tree. |
| 37 | Root traits from the sources: blueberry has no root hairs, soybean has nodules, corn has brace roots | Claude | See PLANT_MORPHOLOGY_SOURCES.md; nodule size is not sourced (2-6 mm drawn). |
| 38 | Sizes follow published leaf-to-height and petiole-to-blade ratios, with tests; where a plant height is not published (orchard cherry, trained grape, squash), an assumed height is stated in the doc and the test | Claude | Same approach as the first four crops ("go fully real"). |
| 39 | New disease looks: masks for small spots, vein-bounded streaks (gray leaf spot), cigar lesions (northern leaf blight), rust pustules/mite stippling, mildew colonies, esca tiger stripes, mosaic mottle | Claude | The two old masks (ringed spots, blotches) could not show these diseases. |
| 40 | Viruses, citrus greening and esca have no cure: their plans remove plants or wood and control the carrier, with high residual floors (0.40-0.50) | Claude | The app must never model a cure that does not exist. |
| 41 | One CC0 PlantVillage photo per new disease is its reference photo, credited | Claude | Picked automatically for sharpness and visible symptoms (`tools/assets/pv_reference_photos.py`). |
| 42 | Demo picker: a sideways-scrolling strip for 14 plants and a two-column grid for up to ten conditions, hidden when Server AI is chosen | Claude | Four fixed buttons could not hold 14 plants; hiding the demo block keeps the sheet within a 16:9 screen. |
| 43 | The zoomed leaf now shows the species' own leaf photo (from its atlas), not one generic leaf | Claude | A corn strap or grape leaf at "Zoom in" instead of a tomato-like leaf. |
| 44 | The dashboard interface's demo picker (ProfileScreen) still lists only the first four plants | Claude | The classic interface is the default; the dashboard is kept but not extended. |
| 45 | The tracer was not re-tuned for the new crops; measured on 566 new diseased and 192 healthy PlantVillage photos: AUC 0.71, strong on brown/purple lesions (leaf scorch 97%, esca 87%), blind to white mildew, mite stippling and greening mottle | Claude reporting | `docs/TRACER_EVALUATION.md`. A whiteness/yellowness cue would be the next step, with its own tuning set. |

## Lush fruit trees and a redone squash (1 Nov 2026)

| # | Decision | By | Why / evidence |
|---|---|---|---|
| 46 | **Exception to the measured proportions for looks**: apple, cherry, orange and peach canopies draw their leaves 1.7-1.8x their real size (`SpeciesProfile.canopyLeafScale`) and carry 65-80 leaves per twig instead of 34-48 | user ("make an exception just for the lushy look") | At real proportions the crowns looked thin and twiggy. `leafSize` keeps the real proportion (the ratio tests still check it); the scale is applied only when drawing, is limited to these four trees and to at most 2x by a test. Canopy leaf cards were made lighter (3x5 grid) to keep the vertex count down. |
| 47 | **Squash redone from a better reference**: bush zucchini, 0.7 m tall, 16 large dark-green leaves on hollow stalks 1.25x the blade, raised up and out; ridged stout stem; four big yellow-orange flowers on upright stalks; zucchini fruit at the base | user asked; Claude researched | The first squash used the Flora of China petiole (6-9 cm), which made a flat pile of leaves. Prigge & Gibson's description of *Cucurbita pepo* (UCLA Santa Monica Mountains flora) gives petioles 200-370 mm, blades roundish 110-340 x 120-390 mm, palmately 5-7-lobed, ascending to erect, flowers 120-180 mm across on 150-330 mm stalks; KPU's plant database gives blades 20-30 x 15-25 cm, petioles 15-25 cm, plants 0.7-1.3 m; a bush-zucchini trial (FAO AGRIS) measured 75 cm plants. |
| 48 | The squash leaf texture keeps the cleaned real surface but gets a new outline from that description: roundish, a little wider than long, five broad lobes with shallow sinuses, weak basal lobes, a narrow heart-shaped notch, toothed margin; tinted to the "dark green" the flora gives | Claude | No clean, free single-leaf squash photo was found (Wikimedia Commons searched again; candidates show silver-mottled leaves among others). |
| 49 | Archived the all-PlantVillage build before rebuilding | user rule | `Builds/Archive/PlantDoctor_2026-11-01_all-plantvillage.apk` |

## More, visible fruit and no bare stem tips (2 Nov 2026)

| # | Decision | By | Why / evidence |
|---|---|---|---|
| 50 | More fruit: blueberry 20 clusters of 7, cherry 24 clusters of 3, orange 22, peach 18, raspberry 18 clusters of 3, corn 2 ears (one or two per stalk), apple 12 | user ("most of them are not visible") | Real trees carry far more fruit than the 5-9 shown before. |
| 51 | **Exception for visibility**: fruit drawn larger (`fruitDisplayScale`): on the four fruit trees by the same factor as their leaves (1.7-1.8x, so fruit and leaf keep their real proportion to each other), blueberry and raspberry 1.4x; a test limits it to these crops and to 2x | user asked for visible fruit; Claude chose the factors | At real proportion cherries were 7 mm across on a 1.3 m twin, invisible. `fruitRadius` keeps the real value. |
| 52 | Tree fruit sits at the outside of the crown, spread over all twigs; cherries are a brighter red; the corn ear shows yellow kernels at the tip of its green husk | Claude | Inside the fuller crown the fruit was hidden. |
| 53 | All of a plant's fruit of one colour is one mesh | Claude | Dozens of berries would otherwise be dozens of draw calls on a phone. |
| 54 | Every stem, side shoot and vine shoot ends in a tuft of 2-3 young leaves pointing up; heart-shaped leaves (squash, grape) join their stalk inside the notch (`petioleInset`) | user ("stalks protruding like sticks/pipes") | Bare stem tips and stalk ends poking through the blades looked like pipes. |
| 55 | Archived the previous build before rebuilding | user rule | `Builds/Archive/PlantDoctor_2026-11-02_lush-trees-squash.apk` |

## Squash habit corrected (2 Nov 2026)

| # | Decision | By | Why / evidence |
|---|---|---|---|
| 56 | The squash grows on an upright stem 22 cm tall (`crownHeight`), its leaves spiralling up it on stalks angled out about 45 degrees (`petioleRise` 0.62), so the bush is wider than tall | user ("why is the squash plant so down"); Claude fixed | Before, every leaf came from one point at the soil and the outer ones leaned out and drooped into a low dome. A bush zucchini is about 0.75 m tall and 1.2 m across (FAO trial), with ascending to erect leaves (Prigge & Gibson). |
| 57 | Zucchini grow from leaf joints along the stem (axillary), pointing up and out, the lowest resting on the soil; a slightly lighter green | user (fruit hidden under the leaves); Claude fixed | Squash flowers are "solitary, axillary" (Prigge & Gibson), so the fruit forms at the leaf bases on the stem, where photos show it; before, it lay on the ground under the leaves. |
| 58 | Archived the previous build | user rule | `Builds/Archive/PlantDoctor_2026-11-02_more-fruit-tip-leaves.apk` |

## Final habit check of all 14 plants (2 Nov 2026)

| # | Decision | By | Why / evidence |
|---|---|---|---|
| 59 | Every plant is now checked from the side and from above as well as the usual views, and `PlantGallery` writes `docs/screenshots/plants/audit.txt`: profile vs built height, spread, width/height, lowest point and which part, where the fruit hangs. Measured on the mesh vertices, not renderer bounds | user ("run a final check on all the plants for such errors"); Claude | The renderer bounds of a rotated part overstate it: they reported a raspberry cane 6 cm underground that was really above the soil. No part of any plant goes below the soil (lowest point is the roots at 1-6 mm). |
| 60 | Tomato fruit come in trusses of three on one stalk; 3 side shoots and 28 leaves instead of 2 and 24 | Claude | Tomato flowers and fruit form in clusters (trusses) on stalks from the stem between the leaves, not as single fruit. |
| 61 | Blueberry stems 0.72 m instead of 0.42 m | Claude | The bush was built 0.75 m tall against its 1.1 m profile, so the camera framed empty space above it. Now 1.05 m. |
| 62 | Corn's stalk stops at 86% of the height and the tassel sits on top | Claude | The tassel was added above the full height, making the plant 1.83 m against its 1.6 m profile (cut off at the top of the viewer). The ears stay at about 40% of the height. |
| 63 | Raspberry canes rise nearly upright (9 degrees instead of 14) and arch over less at the top (26 cm instead of 32); 66 leaves instead of 54 | Claude | The canes fanned out into a wide V. Now about 1.1 m across on a 1.24 m plant. Red raspberry canes are erect and arch at the tips. |
| 64 | Squash flowers open among the leaf stalks at about the height of the blades instead of above them; the stem is 28 cm (was 22) and the young leaves at its top stand up ("ascending to erect", Prigge & Gibson) | Claude | The flowers stood above the whole plant on long stalks; squash flowers come from the leaf joints and open among the leaves. Lowering them showed the leaves alone only reached 0.45 m; with the young leaves standing up, the plant is 0.58 m. |
| 65 | Strawberry: largest leaf at the top of the flora's ranges (petiole 10 cm, leaflets 7 cm), 13 leaves, stalks standing up from the crown; profile height 15 cm (was 30) | Claude | The clump was 11 cm tall against a 30 cm profile and lay flat. The flora's 10-40 cm includes the flower stalks; the leafy clump is lower. |
| 66 | Potato: five stems leaning out 13 degrees, larger upper leaves (12 cm instead of 9), slightly thinner stems | Claude | The upper stems looked like bare sticks with small leaves; now a clump about as wide as tall. |
| 67 | Checked and left as they were: pepper (fruit hangs under the leaves), apple, cherry, orange, peach (fruit at the crown surface, trunk and scaffold branches), grape (vertical shoots from two cordons, bunches hanging under the cordon), soybean (pods in clusters at the stem nodes) | Claude | They match how the real plants grow. |
| 68 | Archived the previous build before rebuilding | user rule | `Builds/Archive/PlantDoctor_2026-11-02_squash-habit.apk` |

## PlantSeg plants and diseases, realistic plants, still zoom leaf (10 Nov 2026)

| # | Decision | By | Why / evidence |
|---|---|---|---|
| 69 | The second dataset is **PlantSeg** (Wei et al. 2024: 34 hosts, 115 disease classes), all of it; PlantDoc's labels are mapped too | user chose, from options Claude laid out | PlantDoc's 17 disease classes are all PlantVillage diseases already (no new plant or disease), so only PlantSeg gives blueberry, raspberry and soybean diseases. Every PlantSeg class is now a disease or an alias (test `EveryPlantSegLabelResolvesToADiseaseOfItsPlant`). |
| 70 | 18 new plants (banana, basil, bean, broccoli, cabbage, carrot, cauliflower, celery, coffee, cucumber, eggplant, garlic, ginger, lettuce, maple, plum, rice, tobacco, wheat); PlantSeg's zucchini goes to squash and citrus to orange (same species) | Claude | 33 plants in total. Sizes from floras and PROTA/PROSEA (docs/PLANT_MORPHOLOGY_SOURCES.md); values not found are marked "assumed". Maple is sycamore maple, tar spot's main host, drawn as a young tree. |
| 71 | 91 new diseases, written as data (Assets/Editor/Data/PlantSeg/*.json, generated by tools/data/make_plantseg_json.py from three Python content files) instead of C# | Claude | 91 hand-written C# entries would be ~4,000 lines; data is easier to review and correct. Every entry: pathogen, affected part, symptoms, 3 treatment steps, prevention, look, lifecycle, conditions, sources note. Content is general plant-pathology knowledge, **not reviewed by a plant pathologist**; chemical steps name no product or dose. |
| 72 | Diseases now record **where they show** (`DiseaseData.affectedPart`: leaf, fruit, head, root, whole plant) and the pathogen's name; downy mildews and Phytophthora are typed **Oomycete** ("Water mould"), blossom end rot **Abiotic** ("Disorder") | Claude | 16 PlantSeg diseases are fruit rots, 4 are on heads (corn smut, wheat head scab and loose smut, cauliflower soft rot), 1 on the root (carrot cavity spot), 4 kill the whole plant (wilts, bunchy top). Oomycetes are not fungi. |
| 73 | Fruit, head and root diseases show **on that organ**: on the twin the fruit switches to the disease shader and the leaves stay healthy; the zoom step shows the fruit (a single banana finger for banana), titled "The affected fruit / head / root" | Claude | A leaf with fruit-rot spots would be wrong. New masks: fruit rot with spore rings, blossom/cigar end rot. |
| 74 | Ten new symptom masks: angular vein-bounded spots and patches, margin scorch, marginal yellowing, V-shaped lesions (cabbage black rot), tar spots, stripe-rust rows, leaf-curl blisters, fruit rot, end rot | Claude | The PlantVillage masks could not show these patterns. |
| 75 | **Zoom leaf stays still**: it waits, invisible, until the bottom sheet has finished sliding in, then fades in where it belongs; it is moved only by the user | user ("the leaf moves by itself") | Measured frame by frame (PlayMode test): the leaf slid and grew for ~0.8 s after the step opened, following the sheet. The test now asserts the leaf is still. |
| 76 | **Zoom leaf is the species' own leaf as it grows**: whole compound leaf (tomato, potato with interjected leaflets; raspberry), three leaflets on their stalk (strawberry palmately, soybean/bean pinnately), a grass blade lying across the frame (corn, wheat, rice, garlic), one simple leaf otherwise; disease spots run across all leaflets and labels point only at leaf tissue | user ("fix ... the leaf itself for each plant") | Before, tomato and strawberry showed a single leaflet and corn an edge-on sliver. |
| 77 | New leaf photos (Wikimedia Commons, credited): peach, a citrus (clementine) leaf **widened 1.6x** to sweet orange's proportion, real courgette leaves for squash; for the new plants cut-outs for eggplant, ginger, coffee, carrot, maple; built from a real leaf surface inside an outline drawn from the flora for banana (with wind tears), broccoli/cabbage/cauliflower, cucumber (courgette surface), celery, lettuce, tobacco; reused from a close relative for bean (soybean), plum (cherry), basil (pepper), garlic/rice/wheat (corn blades, narrowed and tinted) | Claude | Commons has no clean single-leaf photo for many crops; two candidates (cucumber, celery) turned out to show diseased leaves and were rejected. |
| 78 | Potato's leaf size now sourced: blade 8-22 cm + petiole 2-6 cm (Solanaceae Source); twin leaf 22 cm (was 15), test range 0.08-0.47 (was the borrowed tomato bound); tomato leaf 24 cm, 32 leaves | Claude | The old potato leaves looked like bare sticks. |
| 79 | **Flowers** where the plant flowers while fruiting, from each flora: tomato (yellow reflexed stars with an anther cone, trusses), potato (white wheels, cymes), pepper (white, nodding), strawberry (five white petals), squash (orange trumpets), eggplant (purple), cucumber, bean, basil (spikes), tobacco (pink trumpets) | Claude | The old squash flower was a flat yellow cone; the others had none. |
| 80 | **PlantSeg images are not shipped**; disease reference photos for the new diseases come from Wikimedia Commons | Claude | Decision recorded with the tracer evaluation: the newest PlantSeg release is CC BY-NC and its images were collected from many websites, so it is an evaluation set only. |
| 81 | The settings demo picker lists 33 plants in three rows that scroll sideways | Claude | One strip of 33 would be too long. |
| 82 | Cabbage head has a generated texture (wrapped leaves, pale veins, overlapping edges); plum gets the same lush-crown and fruit-visibility exception as the other fruit trees (1.8x) | Claude | A plain sphere looked like a plastic ball; plum's real leaves and fruit are invisible at tree scale. |
| 83 | Reference photos for the PlantSeg diseases: Commons searched by pathogen + host, then by common name; every candidate **looked at** before use; 70 of 91 diseases have a photo, 21 have none rather than a wrong one (micrographs, diagrams, old books, other plants, a protein model were rejected) | Claude | tools/assets/disease_photos_commons.py; picks in tools/assets/_cache/dphotos/picks.json; credited in docs/CREDITS.md. |
| 84 | training/dataset_label_map.json maps every PlantVillage, PlantSeg and PlantDoc class name to the app's plant and disease ids (188 labels) | Claude | The training taxonomy (training/plantdoc_ai/taxonomy.py) is still the original four-crop memo plan and was not extended; a model trained on any of the three datasets can be mapped through this file. |
| 85 | Archived the previous build before rebuilding | user rule | `Builds/Archive/PlantDoctor_2026-11-02_plant-habit-check.apk` |

## Zoom-leaf markers back (11 Nov 2026)

| # | Decision | By | Why / evidence |
|---|---|---|---|
| 86 | The zoom step's labels always find points on the leaf: the spot finder tries strong lesions deep in a blade first, then relaxes (nearer the margin, weaker lesions, any leaf tissue); halo and area labels relax the same way; at least one label always shows | user ("the marker pointing on the leaf is now gone") | With the new whole-leaf, grass-blade and fruit specimens the strict finder often found nothing (wheat, rice, banana, the angular-spot diseases). New test `EveryDiseaseGetsMarkersOnTheZoomedLeaf` checks all 118 diseases get at least two markers, all on tissue. |
| 87 | Fixed the new masks that used Unity's `Mathf.SmoothStep(a, b, t)` as if it were GLSL smoothstep (angular spots, blisters, stripe rust; flower rim shading) | Claude | The angular-spot mask peaked at 0.06, so 10 diseases (downy mildews, angular leaf spot, bacterial spot and blight, rusts, blue mold) barely showed and had no markers. Older masks were left as they were (tuned and approved visually). |
| 88 | Archived the previous build before rebuilding | user rule | `Builds/Archive/PlantDoctor_2026-11-10_plantseg.apk` |

## Mango, sugarcane, chilli (11 Nov 2026)

| # | Decision | By | Why / evidence |
|---|---|---|---|
| 89 | Added **mango, sugarcane and chilli** with their diseases: mango's 7 MangoLeafBD classes (anthracnose, bacterial canker, cutting weevil, die back, gall midge, powdery mildew, sooty mould), sugarcane's 4 (mosaic, red rot, rust, yellow leaf; Daphal & Koli, Mendeley), chilli's 5 (leaf curl, anthracnose fruit rot, Cercospora leaf spot, powdery mildew, whitefly; Roboflow "chili v2" and the Bangladesh chilli set) | user ("there is no mango, sugarcane and chilli ... do it") | Neither PlantVillage nor PlantSeg has these crops. Only the datasets' class names are used (as aliases); no images from them are shipped, so their licences (not shown on the Mendeley pages) do not matter here. Chilli's "yellowish" class (unspecified yellowing) gets no disease of its own. 36 plants, 134 diseases. |
| 90 | Chilli is its own plant (hot pepper, *Capsicum annuum*: slender hanging fruit, narrower leaves), using the bell-pepper leaf photos narrowed 0.72x; sugarcane uses the corn blade strips narrowed; mango gets a Commons leaf photo, the fruit trees' 1.6x lush-crown and fruit-visibility exception, and a new mango fruit shape | Claude | Chilli and bell pepper are one species with the same leaf; Commons has a clean mango leaf on white (CC BY-SA 3.0). |
| 91 | Fixed the fruit-shape helper that took a fractional power of a negative number (NaN): mango, eggplant, banana finger and ginger rhizome had half their surface missing (the eggplant looked like a cup) | Claude | Found while checking the new mango. |
| 92 | Reference photos for 11 of the 16 new diseases (Commons, reviewed); mango die back, cutting weevil and sugarcane red rot, rust, yellow leaf have none rather than wrong ones | Claude | Same rule as decision 83. |
| 93 | Archived the previous build before rebuilding | user rule | `Builds/Archive/PlantDoctor_2026-11-10_markers-back.apk` |
| 94 | **Content database** `Database/plant_doctor_content.db/.sql`: the app's 36 plants and 134 diseases (symptoms, lifecycle, conditions, treatment steps, prevention, dataset labels, sources) in SQLite, modelled on the user's treatment_content.sql without its 3D scenes, tools, quizzes, sliders and rules; that file's method limitations, follow-ups, severity profiles and reviewed sources merged in for its 24 diseases | user ("create a database similar to this ... without the irrelevant addons") | Built by Assets/Editor/ContentExport.cs + tools/data/make_content_db.py; the user's file kept as Database/source/treatment_content.sql. Connected in decision 95. |

## Content database connected (11 Nov 2026)

| # | Decision | By | Why / evidence |
|---|---|---|---|
| 95 | **The content database is the source of truth and is connected to both sides.** Server: reads the SQLite file read-only, resolves the model's class name to plant and disease, returns the disease's content with every `/analyze` (schema 1.2) and serves `/v1/plants`, `/v1/content/{plant}/{disease}`, `/v1/resolve`. App: reads a JSON snapshot of the database (`Resources/Content/plant_doctor_content.json`); setup applies its text to the disease assets; at run time the screens show method limitations, follow-up checks, the recovery note and sources from it, and unknown class names resolve through its label table | user ("yup connect it") | Unity cannot read SQLite on Android without a native SQLite plugin, and this PC's Smart App Control blocks unsigned native DLLs, so the app ships the snapshot instead. The server's routes were not run here (FastAPI is not installed); its content layer is tested with the standard library (`server/tests/test_content.py`). |
| 96 | Archived the previous build before rebuilding | user rule | `Builds/Archive/PlantDoctor_2026-11-11_mango-sugarcane-chilli.apk` |

## CropScan XR backend, cotton, and the missing CropScan classes (19 Nov 2026)

| # | Decision | By | Why / evidence |
|---|---|---|---|
| 97 | **Why the APK could not use the CropScan backend**: `cropscan-api-ovmb.onrender.com` is a different API from the app's own server. The app posted to `/v1/analyze` (404 there) with field `image` (CropScan wants `file`), expected the app server's response (CropScan returns `ScanOut`: status, disease_id, confidence, top3), checked `/v1/health` (CropScan: `/healthz`), and gave up after 20 s (a sleeping Render service takes up to a minute) | user ("on the plantdoctor apk the backend isnt working why") | Checked against the live API's `/openapi.json` and one real scan, 19 Nov 2026. The website works because it was written for that API. |
| 98 | **CropScan client** `Assets/Scripts/AI/CropScanAIService.cs`, chosen automatically when the server address is a CropScan one (host `cropscan…` or a path with `/api/scans`); waits 90 s; maps class names through the content database's labels and the diseases' aliases; a scan CropScan calls `uncertain` (no `disease_id`) shows its top guess **only as a possibility** (forced Uncertain band); `looks_like_crop: false` becomes "not a crop leaf" | user ("yup build them") | Test connection on both settings screens uses `/healthz` with the long wait. |
| 99 | CropScan reports no severity, so a CropScan diagnosis starts the simulation at **severity 0.5** (moderate) and says so in the result's warnings | Claude | The leaf tracer's "% of the leaf" figure was measured as unreliable on real photos (docs/TRACER_EVALUATION.md), so it is not used as a severity. |
| 100 | All **68 CropScan classes** are mapped (`tools/data/cropscan_labels.json`, from `GET /api/diseases`) and added as aliases of their diseases, and as dataset `CropScan` in the content database. This also adds PlantVillage's own spellings (`Corn_(maize)___Common_rust_`, `Cherry_(including_sour)___Powdery_mildew`) that the app's aliases lacked. CropScan's generic `Wheat___Rust` goes to leaf (brown) rust, the most widespread wheat rust | user ("is there are any misses like the cotton and add them too") | Tests: every class reaches an authored disease (EditMode `EveryCropScanClassReachesAnAuthoredDisease`, server `test_every_cropscan_class_resolves`). |
| 101 | **New plant: cotton** (*Gossypium hirsutum*) with CropScan's three diseases (bacterial blight, cotton leaf curl disease, Fusarium wilt). Profile from the flora: ~1.05 m annual shrub, palmately lobed leaves 7.5-15 cm on petioles about as long, cream flowers, a new open-boll fruit (four lint locks in a dry four-part burr). Leaf photo: the undamaged leaf of CSIRO ScienceImage 405 (CC BY 3.0). Drawn as one leafy stem without the herb builder's long side shoots, which looked like bare sticks against real cotton | user ("they need to be at the same level(quality) as the other plants") | 37 plants, 142 diseases. |
| 102 | **New diseases of existing plants** from CropScan's classes: rice bacterial leaf blight, brown spot and tungro; wheat leaf blight (spot blotch, *Bipolaris sorokiniana*, the South Asian "leaf blight") and wheat blast (a head disease). Content as the others: general plant-pathology knowledge, not reviewed by a plant pathologist, no product or dose | user | `tools/data/cropscan_extra.py`. |
| 103 | Reference photos for 4 of the 8 new diseases (Commons, each looked at): rice bacterial blight and brown spot (D. Groth, LSU AgCenter), tungro (JIRCAS), wheat spot blotch (J. Lindsey; the photo does not name the host, but it shows *C. sativus* spot blotch on a cereal leaf). None for the cotton diseases and wheat blast: Commons had only fabric, paper figures, micrographs and an unreadable old field photo | Claude | Same rule as decision 83. |
| 104 | Fixed: plant-level aliases for diseases defined in the same data file were dropped (the builder applied aliases before adding the file's diseases), and the database script then filed such labels as *healthy*. Now aliases are applied after the file's diseases, and only labels a file declares healthy become healthy labels | Claude | Found while adding the CropScan classes (e.g. `Mango___Anthracnose` resolved to healthy mango). |
| 105 | Image tooling: on 19 Nov Smart App Control blocked the Python image libraries; the user turned it off, and the existing Python pipeline was used. numpy 1.26.4, Pillow 10.4.0 and opencv-python-headless 4.10 were installed into the user's Python site-packages while testing this | user | — |
| 106 | The PlayMode walk through every plant now takes just over 3 min, so the two flow tests get their own time limit (10 and 15 min) instead of Unity's default 180 s, which had cut the run off at the last scenario | Claude | The failure looked like a stuck scan but was the test runner's timeout (duration exactly 180.0 s). |
| 107 | Archived the previous build before rebuilding | user rule | `Builds/Archive/PlantDoctor_2026-11-11_content-db-connected.apk` |
