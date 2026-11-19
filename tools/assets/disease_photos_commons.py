"""
Reference photos for the PlantSeg diseases, from Wikimedia Commons (openly licensed; many are Bugwood / university extension photos).
PlantSeg's own images are NOT shipped: the newest PlantSeg release is CC BY-NC and its images were gathered from many websites, so
it stays an evaluation set only (docs/DECISIONS.md).

    py -3.11 tools/assets/disease_photos_commons.py search   # candidate sheet per disease: _cache/dphotos/<key>.jpg/.json
    py -3.11 tools/assets/disease_photos_commons.py get      # download the picks in dphotos/picks.json to Assets/Art/Photos/diseases

The search tries the pathogen's scientific name with the host, then the disease's common name with the host.
"""
import json
import re
import sys
from pathlib import Path

import commons

HERE = Path(__file__).resolve().parent
DATA = HERE.parents[1] / "Assets" / "Editor" / "Data" / "PlantSeg"
OUT = HERE.parents[1] / "Assets" / "Art" / "Photos" / "diseases"
CACHE = HERE / "_cache" / "dphotos"
BAD = re.compile(r"map|diagram|drawing|plate|book|journal|micrograph|microscop|conidi|spore|SEM|culture|petri|logo|chart|graph|range", re.I)


def diseases():
    for f in sorted(DATA.glob("*.json")):
        plant = json.loads(f.read_text(encoding="utf-8"))
        for d in plant["diseases"]:
            pathogen = re.split(r"[(,;]| and | syn\.", d["pathogen"])[0].strip()
            yield d["photos"][0], plant["plantName"], d["name"], pathogen


def search():
    CACHE.mkdir(parents=True, exist_ok=True)
    found = {}
    for key, plant, name, pathogen in diseases():
        host = plant.lower()
        items, seen = [], set()
        for q in (f"{pathogen} {host}", f"{name} {host}", pathogen):
            try:
                for it in commons.search(q, 12):
                    if it["ok"] and it["w"] >= 500 and it["title"] not in seen and not BAD.search(it["title"]):
                        seen.add(it["title"]); items.append(it)
            except Exception as exc:
                print("search failed", q, exc)
            if len(items) >= 6:
                break
        found[key] = [{"title": i["title"], "licence": i["licence"], "thumb": i["thumb"]} for i in items[:6]]
        print(key, len(found[key]), flush=True)
    (CACHE / "candidates.json").write_text(json.dumps(found, indent=1, ensure_ascii=False), encoding="utf-8")


def get():
    picks = json.loads((CACHE / "picks.json").read_text(encoding="utf-8"))
    for key, title in picks.items():
        if title:
            commons.get(title, OUT / f"{key}.jpg", 1024, role="disease")


if __name__ == "__main__":
    {"search": search, "get": get}[sys.argv[1]]()
