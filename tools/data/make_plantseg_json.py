"""
Writes Assets/Editor/Data/PlantSeg/<plant>.json from the content files (plantseg_existing.py, plantseg_new.py), which the Unity
editor turns into disease assets (Assets/Editor/PlantSegDataBuilder.cs).  Run:  py -3.11 tools/data/make_plantseg_json.py
Each disease gets its PlantSeg label(s) as aliases, a reference photo name (<plant>_<id>_1, picked by plantseg_photos.py) and a
sources note.
"""
import json
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
import plantseg_existing  # noqa: E402
import plantseg_new  # noqa: E402
import plantseg_extra  # noqa: E402
import cropscan_extra  # noqa: E402

CROPSCAN = json.loads((HERE / "cropscan_labels.json").read_text(encoding="utf-8"))["labels"]

OUT = HERE.parents[1] / "Assets" / "Editor" / "Data" / "PlantSeg"
PHOTOS = HERE.parents[1] / "Assets" / "Art" / "Photos" / "diseases"
SOURCES = "Content: general plant-pathology knowledge (APS disease compendia, university extension guides, UC IPM); not reviewed by a plant pathologist."


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    plants = {}
    plants.update(plantseg_existing.PLANTS)
    plants.update(plantseg_new.PLANTS)
    plants.update(plantseg_extra.PLANTS)
    plants.update(cropscan_extra.PLANTS)
    # New diseases of existing plants, and every CropScan XR class name as an alias of its disease (or healthy).
    for pid, ext in cropscan_extra.EXTEND.items():
        plants[pid] = dict(plants[pid], diseases=plants[pid]["diseases"] + ext["diseases"], healthy=plants[pid].get("healthy", []) + ext.get("healthy", []))
    for label, hit in CROPSCAN.items():
        p = plants.get(hit["plant"])
        if p is None:
            continue
        if hit["disease"] == "healthy":
            if label not in p.setdefault("healthy", []):
                p["healthy"] = p["healthy"] + [label]
            continue
        own = next((d for d in p["diseases"] if d["id"] == hit["disease"]), None)
        if own is not None and label in own["labels"]:
            continue
        al = dict(p.get("aliases", {}))
        if label not in al.get(hit["disease"], []):
            al[hit["disease"]] = al.get(hit["disease"], []) + [label]
        p["aliases"] = al
    count = 0
    for pid, p in plants.items():
        path = OUT / f"{pid}.json"
        base = json.loads(path.read_text(encoding="utf-8")) if path.exists() else {}
        diseases = []
        for d in p["diseases"]:
            d = dict(d)
            photo = f"{pid}_{d['id']}_1"
            # Only reviewed Commons photos (tools/assets/disease_photos_commons.py); diseases without a good one show none.
            d["photos"] = [photo] if (PHOTOS / f"{photo}.jpg").exists() else []
            dataset = {"mango": "MangoLeafBD (Mendeley Data)", "sugarcane": "Sugarcane Leaf Disease Dataset (Mendeley Data)",
                       "chilli": "chili v2 (Roboflow) / Chilli Leaf Disease Dataset (Mendeley Data)",
                       "cotton": "CropScan XR model"}.get(pid, "PlantSeg (Wei et al. 2024, CC BY 4.0)")
            if any(l in CROPSCAN for l in d["labels"]) and pid != "cotton":
                dataset = "CropScan XR model"
            d["sources"] = [f"{dataset} class: {', '.join(d['labels'])}", SOURCES]
            diseases.append(d)
            count += 1
        out = {
            "plantId": pid,
            "plantName": p["plantName"],
            "scientificName": p.get("scientificName", base.get("scientificName", "")),
            "description": p.get("description", base.get("description", "")),
            "diseases": diseases,
            "aliases": [{"disease": k, "labels": v} for k, v in p.get("aliases", {}).items()],
            "healthyLabels": p.get("healthy", []),
        }
        path.write_text(json.dumps(out, indent=1, ensure_ascii=False), encoding="utf-8")
    print(f"wrote {len(plants)} plant files with {count} new diseases to {OUT}")


if __name__ == "__main__":
    main()
