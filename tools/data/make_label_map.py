"""
Writes training/dataset_label_map.json: every class name of PlantVillage, PlantSeg and PlantDoc -> the app's plant id and disease id
("healthy" for healthy classes). Built from the generated disease assets (their aiLabelAliases) and the PlantSeg data files, so it
always matches what the app resolves. A model trained on any of the three datasets can map its output through this file.

    py -3.11 tools/data/make_label_map.py
"""
import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
PLANT_OF = {"Tomato": "tomato", "Potato": "potato", "Pepper": "pepper", "Apple": "apple", "Blueberry": "blueberry", "Cherry": "cherry",
            "Corn": "corn", "Grape": "grape", "Orange": "orange", "Peach": "peach", "Raspberry": "raspberry", "Soybean": "soybean",
            "Squash": "squash", "Strawberry": "strawberry"}


def main():
    out = {}
    for f in sorted((ROOT / "Assets" / "Data" / "Diseases").glob("Disease_*.asset")):
        text = f.read_text(encoding="utf-8")
        did = re.search(r"diseaseId: (\S+)", text).group(1)
        prefix = re.match(r"Disease_([A-Z][a-z]+)", f.stem).group(1)
        plant = PLANT_OF.get(prefix, prefix.lower())
        aliases = re.findall(r"^  - (.+)$", text.split("aiLabelAliases:")[1].split("pathogenType")[0], re.M)
        for a in aliases:
            a = a.strip().strip("'\"")
            if a and not a.startswith(plant + "_"):
                out[a] = {"plant": plant, "disease": did}
    for f in sorted((ROOT / "Assets" / "Editor" / "Data" / "PlantSeg").glob("*.json")):
        p = json.loads(f.read_text(encoding="utf-8"))
        for h in p.get("healthyLabels", []):
            out[h] = {"plant": p["plantId"], "disease": "healthy"}
    pv_healthy = ["Apple___healthy", "Blueberry___healthy", "Cherry_(including_sour)___healthy", "Corn_(maize)___healthy", "Grape___healthy",
                  "Peach___healthy", "Pepper,_bell___healthy", "Potato___healthy", "Raspberry___healthy", "Soybean___healthy",
                  "Strawberry___healthy", "Tomato___healthy"]
    for label in pv_healthy:
        key = label.split("___")[0].split(",")[0].split("_(")[0]
        out[label] = {"plant": PLANT_OF.get(key, key.lower()), "disease": "healthy"}
    dest = ROOT / "training" / "dataset_label_map.json"
    dest.write_text(json.dumps(dict(sorted(out.items())), indent=1, ensure_ascii=False), encoding="utf-8")
    print("wrote", dest, len(out), "labels")


if __name__ == "__main__":
    main()
