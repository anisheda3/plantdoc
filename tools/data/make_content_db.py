"""
Builds the content database of the app: Database/plant_doctor_content.db (SQLite) and Database/plant_doctor_content.sql (the same
as SQL text, reviewable and diffable). One row per plant and per disease, everything the app teaches about each disease, keyed by
(plant_id, disease_id) - the same ids the AI model / Demo AI reports, so whatever names the disease can look its content up here.

The database is the source of truth: edit it (any SQLite tool), then run this script with no arguments. That writes
  * Assets/Resources/Content/plant_doctor_content.json - the snapshot the app reads (Unity cannot open SQLite on Android without a
    native plugin), applied to the disease assets at project setup and read at run time for follow-ups and sources;
  * Database/plant_doctor_content.sql (SQL text) and server/data/plant_doctor_content.db (the server's copy).

To rebuild the database from the app's current content instead (this overwrites edits made in the database):
    1. Unity:  -executeMethod PlantDoctor.EditorTools.ContentExport.Run   (writes Database/_export/content.json)
    2. py -3.11 tools/data/make_content_db.py --from-app

Modelled on the user's treatment_content.sql (Database/source/), WITHOUT its interactive add-ons (3D environments and fixtures,
tools, scene targets, quiz options, step prerequisites, treatment sliders and their rules). For the 24 diseases that file covers, its
method limitations, follow-up checks, severity profile and reviewed source links are merged in, marked source = 'treatment_content'.
"""
import json
import re
import sqlite3
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
EXPORT = ROOT / "Database" / "_export" / "content.json"
REFERENCE = ROOT / "Database" / "source" / "treatment_content.sql"
DB = ROOT / "Database" / "plant_doctor_content.db"
SQL = ROOT / "Database" / "plant_doctor_content.sql"
PLANTSEG = ROOT / "Assets" / "Editor" / "Data" / "PlantSeg"
REF_PLANT = {"bell_pepper": "pepper"}

SCHEMA = """
CREATE TABLE content_info (key TEXT PRIMARY KEY, value TEXT NOT NULL);
CREATE TABLE plants (plant_id TEXT PRIMARY KEY, name TEXT NOT NULL, scientific_name TEXT, description TEXT,
  body_plan TEXT, fruit_form TEXT, twin_height_m REAL);
CREATE TABLE diseases (plant_id TEXT NOT NULL REFERENCES plants(plant_id), disease_id TEXT NOT NULL, name TEXT NOT NULL,
  category TEXT NOT NULL, pathogen TEXT, affected_part TEXT NOT NULL CHECK (affected_part IN ('Leaf','Fruit','Head','Root','WholePlant')),
  description TEXT, causes TEXT, effect_on_plant TEXT, untreated_outcome TEXT,
  moderate_threshold REAL, severe_threshold REAL, residual_severity_floor REAL, residual_damage_floor REAL,
  PRIMARY KEY (plant_id, disease_id));
CREATE TABLE symptoms (plant_id TEXT NOT NULL, disease_id TEXT NOT NULL, ord INTEGER NOT NULL, text TEXT NOT NULL,
  PRIMARY KEY (plant_id, disease_id, ord), FOREIGN KEY (plant_id, disease_id) REFERENCES diseases(plant_id, disease_id));
CREATE TABLE lifecycle_stages (plant_id TEXT NOT NULL, disease_id TEXT NOT NULL, ord INTEGER NOT NULL, text TEXT NOT NULL,
  PRIMARY KEY (plant_id, disease_id, ord), FOREIGN KEY (plant_id, disease_id) REFERENCES diseases(plant_id, disease_id));
CREATE TABLE condition_factors (plant_id TEXT NOT NULL, disease_id TEXT NOT NULL, ord INTEGER NOT NULL, text TEXT NOT NULL,
  PRIMARY KEY (plant_id, disease_id, ord), FOREIGN KEY (plant_id, disease_id) REFERENCES diseases(plant_id, disease_id));
CREATE TABLE treatment_steps (plant_id TEXT NOT NULL, disease_id TEXT NOT NULL, step_id TEXT NOT NULL, ord INTEGER NOT NULL,
  title TEXT NOT NULL, instruction TEXT NOT NULL, rationale TEXT, interaction TEXT, simulated_days INTEGER,
  involves_product INTEGER NOT NULL DEFAULT 0, severity_change REAL, lesion_change REAL, yellowing_change REAL, wilting_change REAL,
  damage_change REAL, growth_change REAL,
  PRIMARY KEY (plant_id, disease_id, step_id), FOREIGN KEY (plant_id, disease_id) REFERENCES diseases(plant_id, disease_id));
CREATE TABLE prevention_measures (plant_id TEXT NOT NULL, disease_id TEXT NOT NULL, ord INTEGER NOT NULL, category TEXT NOT NULL,
  title TEXT NOT NULL, detail TEXT,
  PRIMARY KEY (plant_id, disease_id, ord), FOREIGN KEY (plant_id, disease_id) REFERENCES diseases(plant_id, disease_id));
CREATE TABLE methods (plant_id TEXT NOT NULL, disease_id TEXT NOT NULL, method_id TEXT NOT NULL, name TEXT NOT NULL,
  purpose TEXT NOT NULL, limitations TEXT NOT NULL, source TEXT NOT NULL,
  PRIMARY KEY (plant_id, disease_id, method_id), FOREIGN KEY (plant_id, disease_id) REFERENCES diseases(plant_id, disease_id));
CREATE TABLE followups (plant_id TEXT NOT NULL, disease_id TEXT NOT NULL, checkpoint_id TEXT NOT NULL, prompt TEXT NOT NULL,
  finding TEXT NOT NULL, escalation TEXT NOT NULL, source TEXT NOT NULL,
  PRIMARY KEY (plant_id, disease_id, checkpoint_id), FOREIGN KEY (plant_id, disease_id) REFERENCES diseases(plant_id, disease_id));
CREATE TABLE severity_profiles (plant_id TEXT NOT NULL, disease_id TEXT NOT NULL, outcome TEXT, initial_severity REAL,
  initial_health REAL, max_severity_drop REAL, max_health_gain REAL, max_worsen REAL, recovery_note TEXT,
  full_lesions REAL, full_yellowing REAL, full_wilting REAL, full_damage REAL, source TEXT NOT NULL,
  PRIMARY KEY (plant_id, disease_id), FOREIGN KEY (plant_id, disease_id) REFERENCES diseases(plant_id, disease_id));
CREATE TABLE labels (label TEXT NOT NULL, dataset TEXT NOT NULL, plant_id TEXT NOT NULL REFERENCES plants(plant_id),
  disease_id TEXT NOT NULL, PRIMARY KEY (label, plant_id));
CREATE TABLE evidence (plant_id TEXT NOT NULL, disease_id TEXT NOT NULL, publisher TEXT NOT NULL, url TEXT, reviewed TEXT,
  region TEXT, note TEXT, source TEXT NOT NULL);
CREATE INDEX labels_by_label ON labels(label);
"""

PLANTDOC = {"Apple Scab Leaf", "Apple leaf", "Apple rust leaf", "Bell_pepper leaf spot", "Bell_pepper leaf", "Blueberry leaf", "Cherry leaf",
            "Corn Gray leaf spot", "Corn leaf blight", "Corn rust leaf", "Peach leaf", "Potato leaf early blight", "Potato leaf late blight",
            "Raspberry leaf", "Soyabean leaf", "Squash Powdery mildew leaf", "Strawberry leaf", "Tomato Early blight leaf",
            "Tomato Septoria leaf spot", "Tomato leaf bacterial spot", "Tomato leaf late blight", "Tomato leaf mosaic virus",
            "Tomato leaf yellow virus", "Tomato leaf", "Tomato mold leaf", "Tomato two spotted spider mites leaf", "grape leaf black rot", "grape leaf"}
# The 38 PlantVillage classes as the dataset spells them (CropScan uses these too, plus its own).
PV_CLASSES = {"Apple___Apple_scab", "Apple___Black_rot", "Apple___Cedar_apple_rust", "Apple___healthy", "Blueberry___healthy",
              "Cherry_(including_sour)___Powdery_mildew", "Cherry_(including_sour)___healthy", "Corn_(maize)___Cercospora_leaf_spot Gray_leaf_spot",
              "Corn_(maize)___Common_rust_", "Corn_(maize)___Northern_Leaf_Blight", "Corn_(maize)___healthy", "Grape___Black_rot",
              "Grape___Esca_(Black_Measles)", "Grape___Leaf_blight_(Isariopsis_Leaf_Spot)", "Grape___healthy", "Orange___Haunglongbing_(Citrus_greening)",
              "Peach___Bacterial_spot", "Peach___healthy", "Pepper,_bell___Bacterial_spot", "Pepper,_bell___healthy", "Potato___Early_blight",
              "Potato___Late_blight", "Potato___healthy", "Raspberry___healthy", "Soybean___healthy", "Squash___Powdery_mildew",
              "Strawberry___Leaf_scorch", "Strawberry___healthy", "Tomato___Bacterial_spot", "Tomato___Early_blight", "Tomato___Late_blight",
              "Tomato___Leaf_Mold", "Tomato___Septoria_leaf_spot", "Tomato___Spider_mites Two-spotted_spider_mite", "Tomato___Target_Spot",
              "Tomato___Tomato_Yellow_Leaf_Curl_Virus", "Tomato___Tomato_mosaic_virus", "Tomato___healthy"}
OTHER = {"mango": "MangoLeafBD", "sugarcane": "Sugarcane Leaf Disease Dataset", "chilli": "chili v2 / Chilli Leaf Disease Dataset", "cotton": "CropScan"}


def plantseg_labels():
    """Labels written in the PlantSeg data files, by plant: the dataset each came from."""
    out = {}
    for f in PLANTSEG.glob("*.json"):
        p = json.loads(f.read_text(encoding="utf-8"))
        for d in p["diseases"]:
            for l in d["labels"]:
                out[(p["plantId"], l)] = OTHER.get(p["plantId"], "PlantSeg")
        for a in p.get("aliases", []):
            for l in a["labels"]:
                if l not in PLANTDOC:
                    out[(p["plantId"], l)] = "PlantSeg"
        for h in p.get("healthyLabels", []):
            out[(p["plantId"], h)] = ("PlantDoc" if h in PLANTDOC else "PlantVillage" if h in PV_CLASSES else "CropScan" if h in CROPSCAN
                                     else OTHER.get(p["plantId"], "PlantSeg"))
    return out


CROPSCAN = json.loads((Path(__file__).resolve().parent / "cropscan_labels.json").read_text(encoding="utf-8"))["labels"]


def dataset_of(plant, label, seg):
    if label in CROPSCAN and label not in PV_CLASSES:
        return "CropScan"
    if label in PLANTDOC:
        return "PlantDoc"
    if "___" in label:
        return "PlantVillage"
    return seg.get((plant, label))


def main():
    import sys
    if "--from-app" in sys.argv or not DB.exists():
        build_from_app()
    publish()


def build_from_app():
    dump = json.loads(EXPORT.read_text(encoding="utf-8"))
    if DB.exists():
        DB.unlink()
    con = sqlite3.connect(DB)
    con.execute("PRAGMA foreign_keys = ON")
    con.executescript(SCHEMA)
    q = con.execute

    for p in dump["plants"]:
        q("INSERT INTO plants VALUES (?,?,?,?,?,?,?)", (p["id"], p["name"], p["scientificName"], p["description"], p["archetype"], p["fruit"], round(p["height"], 3)))

    seg = plantseg_labels()
    known = set()
    for d in dump["diseases"]:
        key = (d["plantId"], d["id"])
        known.add(key)
        q("INSERT INTO diseases VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?)", (*key, d["name"], d["pathogenType"], d["pathogen"] or None, d["affectedPart"],
          d["description"], d["causes"], d["effect"], d["untreated"], d["moderateThreshold"], d["severeThreshold"], d["severityFloor"], d["damageFloor"]))
        for table, items in (("symptoms", d["symptoms"]), ("lifecycle_stages", d["lifecycle"]), ("condition_factors", d["conditions"])):
            for i, text in enumerate(items):
                q(f"INSERT INTO {table} VALUES (?,?,?,?)", (*key, (i + 1) * 10, text))
        for s in d["steps"]:
            q("INSERT INTO treatment_steps VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)", (*key, s["id"], s["ord"], s["title"], s["instruction"], s["rationale"],
              s["mode"], s["days"], int(s["chemical"]), s["severityChange"], s["lesionChange"], s["yellowingChange"], s["wiltingChange"],
              s["damageChange"], s["growthChange"]))
        for m in d["prevention"]:
            q("INSERT INTO prevention_measures VALUES (?,?,?,?,?,?)", (*key, m["ord"], m["category"], m["title"], m["detail"]))
        q("INSERT INTO severity_profiles (plant_id, disease_id, full_lesions, full_yellowing, full_wilting, full_damage, source) VALUES (?,?,?,?,?,?,?)",
          (*key, d["fullLesions"], d["fullYellowing"], d["fullWilting"], d["fullDamage"], "app"))
        for a in d["aliases"]:
            ds = dataset_of(d["plantId"], a, seg)
            if ds:
                q("INSERT OR IGNORE INTO labels VALUES (?,?,?,?)", (a, ds, *key))
        for s in d["sources"]:
            q("INSERT INTO evidence (plant_id, disease_id, publisher, note, source) VALUES (?,?,?,?,?)", (*key, "Plant Doctor content", s, "app"))
    # Healthy classes of the datasets.
    pv_healthy = {"Apple___healthy": "apple", "Blueberry___healthy": "blueberry", "Cherry_(including_sour)___healthy": "cherry",
                  "Corn_(maize)___healthy": "corn", "Grape___healthy": "grape", "Peach___healthy": "peach", "Pepper,_bell___healthy": "pepper",
                  "Potato___healthy": "potato", "Raspberry___healthy": "raspberry", "Soybean___healthy": "soybean",
                  "Strawberry___healthy": "strawberry", "Tomato___healthy": "tomato"}
    for label, plant in pv_healthy.items():
        q("INSERT OR IGNORE INTO labels VALUES (?,?,?,?)", (label, "PlantVillage", plant, "healthy"))
    # Healthy classes: only labels a data file declares healthy (an unmatched disease label must never become "healthy").
    healthy = {(json.loads(f.read_text(encoding="utf-8"))["plantId"], h) for f in PLANTSEG.glob("*.json")
               for h in json.loads(f.read_text(encoding="utf-8")).get("healthyLabels", [])}
    for (plant, label), ds in seg.items():
        if (plant, label) not in healthy:
            if not any(con.execute("SELECT 1 FROM labels WHERE label=? AND plant_id=?", (label, plant))):
                print("WARNING: dataset label matches no disease:", plant, label)
            continue
        if not any(r for r in con.execute("SELECT 1 FROM labels WHERE label=? AND plant_id=?", (label, plant))):
            q("INSERT OR IGNORE INTO labels VALUES (?,?,?,?)", (label, ds, plant, "healthy"))

    # Merge the reviewed parts of the user's treatment_content.sql for the diseases it covers.
    merged = 0
    if REFERENCE.exists():
        ref = sqlite3.connect(":memory:")
        ref.executescript(REFERENCE.read_text(encoding="utf-8"))
        def rows(sql):
            for r in ref.execute(sql):
                plant = REF_PLANT.get(r[0], r[0])
                if (plant, r[1]) in known:
                    yield (plant, r[1], *r[2:])
        for r in rows("SELECT plant_id, disease_id, method_id, name, purpose, limitations FROM methods"):
            q("INSERT INTO methods VALUES (?,?,?,?,?,?,?)", (*r, "treatment_content"))
        for r in rows("SELECT plant_id, disease_id, checkpoint_id, prompt, finding, escalation FROM followups"):
            q("INSERT INTO followups VALUES (?,?,?,?,?,?,?)", (*r, "treatment_content"))
        for r in rows("SELECT p.plant_id, p.disease_id, c.outcome, p.initial_severity, p.initial_health, p.max_severity_drop, p.max_health_gain, "
                      "p.max_worsen, p.recovery_note FROM case_profile p JOIN cases c USING (plant_id, disease_id)"):
            q("UPDATE severity_profiles SET outcome=?, initial_severity=?, initial_health=?, max_severity_drop=?, max_health_gain=?, max_worsen=?, "
              "recovery_note=?, source='app + treatment_content' WHERE plant_id=? AND disease_id=?", (*r[2:], r[0], r[1]))
            merged += 1
        for r in rows("SELECT plant_id, disease_id, publisher, url, reviewed, region, limitations FROM evidence"):
            q("INSERT INTO evidence VALUES (?,?,?,?,?,?,?,?)", (*r, "treatment_content"))

    info = {
        "content_version": "1",
        "plants": str(len(dump["plants"])),
        "diseases": str(len(dump["diseases"])),
        "merged_from_treatment_content": str(merged),
        "lookup": "Find a disease by (plant_id, disease_id), or by a dataset class name through the labels table.",
        "rules": "Educational content. No pesticide products, doses, mixing or intervals: those follow the local product label.",
        "review": "App content is general plant-pathology knowledge, not reviewed by a plant pathologist; rows with source = "
                  "'treatment_content' come from the reviewed treatment_content.sql.",
    }
    for k, v in info.items():
        q("INSERT INTO content_info VALUES (?,?)", (k, v))
    con.commit()

    con.close()
    print("built", DB, "from the app's content")


SNAPSHOT = ROOT / "Assets" / "Resources" / "Content" / "plant_doctor_content.json"


def publish():
    """From the database: the SQL text, the server's copy, and the snapshot the app reads."""
    con = sqlite3.connect(DB)
    con.row_factory = sqlite3.Row
    header = ["-- Plant Doctor content database: plants, diseases and how to treat and prevent them, one row per (plant_id, disease_id).",
              "-- Generated by tools/data/make_content_db.py from the app's data (Unity ContentExport) and the reviewed parts of",
              "-- Database/source/treatment_content.sql. Educational only: no pesticide products, doses, mixing or intervals.",
              "PRAGMA foreign_keys = ON;"]
    SQL.write_text("\n".join(header + [line for line in con.iterdump() if not line.startswith(("BEGIN", "COMMIT"))]) + "\n", encoding="utf-8")
    counts = {t: con.execute(f"SELECT count(*) FROM {t}").fetchone()[0] for t in
              ("plants", "diseases", "symptoms", "lifecycle_stages", "condition_factors", "treatment_steps", "prevention_measures",
               "methods", "followups", "severity_profiles", "labels", "evidence")}
    # The server's Docker image is built from server/, so it gets its own copy.
    serve = ROOT / "server" / "data" / DB.name
    serve.parent.mkdir(parents=True, exist_ok=True)
    serve.write_bytes(DB.read_bytes())
    print("wrote", DB, "and", SQL, "(and a copy in server/data for the server)")
    print(counts)



    # The app's snapshot: one record per disease with everything the screens show, and the label table.
    rows = lambda sql, args=(): [dict(r) for r in con.execute(sql, args)]
    diseases = []
    for d in rows("SELECT * FROM diseases ORDER BY plant_id, disease_id"):
        key = (d["plant_id"], d["disease_id"])
        texts = lambda t: [r["text"] for r in rows(f"SELECT text FROM {t} WHERE plant_id=? AND disease_id=? ORDER BY ord", key)]
        prof = rows("SELECT * FROM severity_profiles WHERE plant_id=? AND disease_id=?", key)
        prof = prof[0] if prof else {}
        diseases.append({
            "plantId": d["plant_id"], "diseaseId": d["disease_id"], "name": d["name"], "category": d["category"], "pathogen": d["pathogen"] or "",
            "affectedPart": d["affected_part"], "description": d["description"] or "", "causes": d["causes"] or "",
            "effect": d["effect_on_plant"] or "", "untreated": d["untreated_outcome"] or "",
            "symptoms": texts("symptoms"), "lifecycle": texts("lifecycle_stages"), "conditions": texts("condition_factors"),
            "steps": rows("SELECT step_id AS stepId, title, instruction, rationale FROM treatment_steps WHERE plant_id=? AND disease_id=? ORDER BY ord", key),
            "prevention": rows("SELECT category, title, detail FROM prevention_measures WHERE plant_id=? AND disease_id=? ORDER BY ord", key),
            "methods": rows("SELECT name, purpose, limitations FROM methods WHERE plant_id=? AND disease_id=?", key),
            "followups": rows("SELECT prompt, finding, escalation FROM followups WHERE plant_id=? AND disease_id=?", key),
            "evidence": rows("SELECT publisher, url FROM evidence WHERE plant_id=? AND disease_id=? AND url IS NOT NULL", key),
            "outcome": prof.get("outcome") or "", "recoveryNote": prof.get("recovery_note") or "",
        })
    labels = rows("SELECT label, plant_id AS plantId, disease_id AS diseaseId FROM labels ORDER BY label")
    version = dict(con.execute("SELECT key, value FROM content_info").fetchall()).get("content_version", "1")
    SNAPSHOT.parent.mkdir(parents=True, exist_ok=True)
    SNAPSHOT.write_text(json.dumps({"version": version, "diseases": diseases, "labels": labels}, ensure_ascii=False, separators=(",", ":")),
                        encoding="utf-8")
    con.close()
    print("wrote the app snapshot", SNAPSHOT, f"({len(diseases)} diseases, {len(labels)} labels)")


if __name__ == "__main__":
    main()
