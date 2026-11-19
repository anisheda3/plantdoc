"""Writes the credits shown in the app (Assets/Data/Config/Credits.txt) and docs/CREDITS.md from credits.json."""
import json
from pathlib import Path

here = Path(__file__).resolve().parent
root = here.parents[1]
credits = json.loads((here / "credits.json").read_text(encoding="utf-8"))

groups = {"Disease reference photos": [], "Treatment photos": [], "Backgrounds and leaf photos": []}
for path, c in sorted(credits.items()):
    key = ("Disease reference photos" if "/diseases/" in path else "Treatment photos" if "/treatment/" in path
           else "Backgrounds and leaf photos")
    author = (c.get("author") or "unknown").replace("\n", " ")[:70]
    groups[key].append(f"{c['title'].replace('File:', '')[:70]} - {author} - {c['licence']} - {c.get('page', '')}")

fixed = [
    "Icons: Lucide (https://lucide.dev), ISC licence.",
    "Font: Inter by Rasmus Andersson, SIL Open Font License 1.1.",
    "Plant leaf textures: healthy-leaf photographs from the PlantVillage dataset (Hughes & Salathe 2015; CC0 1.0, via the Hugging Face mirror DScomp380/plant_village), cut out and packed into atlases by tools/assets/make_plant_textures.py. Stem, bark, soil and root textures are generated from noise.",
    "Photographs are used under the licences shown; CC BY and CC BY-SA images are credited here.",
]

lines = ["# Credits", ""] + fixed + [""]
for title, items in groups.items():
    lines += [f"## {title}", ""] + [f"- {i}" for i in items] + [""]
(root / "docs" / "CREDITS.md").write_text("\n".join(lines), encoding="utf-8")

short = ["CREDITS", ""] + fixed + [""]
for title, items in groups.items():
    short += [title.upper()] + [f"- {i.split(' - http')[0]}" for i in items] + [""]
out = root / "Assets" / "Data" / "Config" / "Credits.txt"
out.parent.mkdir(parents=True, exist_ok=True)
out.write_text("\n".join(short), encoding="utf-8")
print("wrote", out, "and docs/CREDITS.md,", sum(len(v) for v in groups.values()), "images")
