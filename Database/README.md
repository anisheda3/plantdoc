# Plant Doctor content database

`plant_doctor_content.db` (SQLite) and `plant_doctor_content.sql` (the same as SQL text) hold everything the app teaches about each
disease, one row per `(plant_id, disease_id)` - the ids the AI model or Demo AI reports. 37 plants, 142 diseases.

| Table | What it holds |
|---|---|
| `plants` | id, name, scientific name, description, body plan, fruit form |
| `diseases` | name, category (Fungal, Oomycete, Bacterial, Viral, Pest, Abiotic), pathogen, affected part (Leaf, Fruit, Head, Root, WholePlant), description, causes, effect, untreated outcome, severity thresholds and treatment floors |
| `symptoms`, `lifecycle_stages`, `condition_factors` | ordered text lists per disease |
| `treatment_steps` | ordered steps: title, instruction, rationale, kind of action, simulated days, whether a product is involved, effect on the simulated plant |
| `prevention_measures` | category, title, detail |
| `methods`, `followups` | method purpose and limitations, follow-up checks (24 diseases, from the reviewed treatment_content.sql) |
| `severity_profiles` | how far the disease goes and can recover; starting severity and recovery limits for the 24 reviewed cases |
| `labels` | every PlantVillage, PlantSeg, PlantDoc, MangoLeafBD, sugarcane, chilli and CropScan XR class name -> plant and disease (or healthy) |
| `evidence` | sources: the app's notes, and the reviewed publisher links for the 24 cases |

Look a disease up by `(plant_id, disease_id)`, or by a model's class name: `SELECT * FROM labels WHERE label = 'Tomato___Early_blight'`.

## How it is connected

* **Server**: `server/content.py` reads `server/data/plant_doctor_content.db`. `/analyze` turns the model's class name into the
  app's plant and disease through `labels` and returns the disease's content; `/v1/plants`, `/v1/content/...` and `/v1/resolve`
  serve it directly.
* **App**: Unity cannot open SQLite on Android without a native plugin, so the database is published as
  `Assets/Resources/Content/plant_doctor_content.json`. Project setup (`ContentDatabaseImporter`) writes its names, descriptions,
  symptoms, lifecycle, conditions, step text and prevention into the disease assets; at run time `ContentDatabase` supplies what the
  assets do not hold - method limitations (treatment intro), follow-up checks and recovery note (recovery and report screens),
  sources (diagnosis) - and resolves dataset class names the disease aliases miss.

## Editing

The database is the source of truth. Edit it with any SQLite tool, then:
1. `py -3.11 tools/data/make_content_db.py` (writes the .sql, the server copy and the app snapshot)
2. Unity project setup (`BuildScript.CommandLineSetup`) to apply it to the assets, then build.

To rebuild the database from the app instead (overwrites database edits):
1. Unity: `-executeMethod PlantDoctor.EditorTools.ContentExport.Run` (writes `_export/content.json`)
2. `py -3.11 tools/data/make_content_db.py --from-app`

Modelled on `source/treatment_content.sql` but without its interactive add-ons (3D environments, fixtures, tools, scene targets,
quiz options, step prerequisites, treatment sliders and rules). Rows marked `source = 'treatment_content'` come from that file;
`source = 'app'` rows are the app's own content (general plant-pathology knowledge, not reviewed by a plant pathologist).
No pesticide products, doses, mixing or intervals anywhere.
