"""Downloads a few healthy-leaf photos per crop from the PlantVillage dataset (CC0 1.0 mirror on Hugging Face:
DScomp380/plant_village) through the public datasets-server, into tools/assets/_cache/plantvillage/.

The dataset is stored sorted by class, so each class is found by bisecting on the label and then read in a block.
Used to build realistic leaf textures for the plant twins (see make_species_leaves.py).

    py -3.11 tools/assets/fetch_plantvillage.py            # tomato, potato, pepper, apple; 24 images each
"""
import json
import sys
import time
import urllib.request
from pathlib import Path

DATASET = "DScomp380/plant_village"
CACHE = Path(__file__).resolve().parent / "_cache" / "plantvillage"
CLASSES = {
    "tomato": "Tomato___healthy",
    "potato": "Potato___healthy",
    "pepper": "Pepper,_bell___healthy",
    "apple": "Apple___healthy",
}
PER_CLASS = int(sys.argv[1]) if len(sys.argv) > 1 else 24


def get_json(url, tries=8):
    for attempt in range(tries):
        try:
            with urllib.request.urlopen(url, timeout=90) as r:
                return json.load(r)
        except Exception:
            time.sleep(1.5 + attempt)
    raise RuntimeError("request failed: " + url)


def rows(offset, length=1):
    url = f"https://datasets-server.huggingface.co/rows?dataset={DATASET}&config=default&split=train&offset={offset}&length={length}"
    return get_json(url)["rows"]


def first_index(label):
    lo, hi = 0, 55446
    while lo < hi:
        mid = (lo + hi) // 2
        if rows(mid)[0]["row"]["label"] < label:
            lo = mid + 1
        else:
            hi = mid
    return lo


def download(url, dest, tries=8):
    for attempt in range(tries):
        try:
            with urllib.request.urlopen(url, timeout=90) as r, open(dest, "wb") as f:
                f.write(r.read())
            return True
        except Exception:
            time.sleep(1.5 + attempt)
    return False


def main():
    CACHE.mkdir(parents=True, exist_ok=True)
    info = get_json(f"https://datasets-server.huggingface.co/info?dataset={DATASET}")
    names = info["dataset_info"]["default"]["features"]["label"]["names"]
    for crop, class_name in CLASSES.items():
        label = names.index(class_name)
        have = len(list(CACHE.glob(f"{crop}_*.jpg")))
        if have >= PER_CLASS:
            print(crop, "already has", have)
            continue
        start = first_index(label)
        got = []
        for offset in range(0, PER_CLASS, 20):
            got += rows(start + offset, min(20, PER_CLASS - offset))
        ok = 0
        for i, row in enumerate(got):
            if row["row"]["label"] != label:
                continue
            dest = CACHE / f"{crop}_{i:02d}.jpg"
            if dest.exists() or download(row["row"]["image"]["src"], dest):
                ok += 1
        print(crop, class_name, "label", label, "starts at", start, "->", ok, "images")


if __name__ == "__main__":
    main()
