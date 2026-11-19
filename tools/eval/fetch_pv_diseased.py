"""Fetches DISEASED leaf photos from PlantVillage (CC0 mirror DScomp380/plant_village on Hugging Face), 224 px centre squares, for the
tracer's "diseased vs healthy" check on lab-style close-ups (the easiest case for the app: one leaf, plain background).
These photos have class labels but no lesion masks, so they only test whether the tracer flags more of a diseased leaf than a healthy one.

    py -3.11 tools/eval/fetch_pv_diseased.py <output folder> [per class]
"""
import io
import json
import sys
import time
import urllib.request

sys.path.insert(0, r"C:\Users\Anish\.claude\jobs\0a9d9ea4\tmp\img")   # numpy / opencv (trusted)
from pathlib import Path

import cv2
import numpy as np

DATASET = "DScomp380/plant_village"
CLASSES = [
    "Tomato___Early_blight", "Tomato___Late_blight", "Tomato___Bacterial_spot", "Tomato___Septoria_leaf_spot",
    "Potato___Early_blight", "Potato___Late_blight", "Pepper,_bell___Bacterial_spot", "Apple___Apple_scab", "Apple___Cedar_apple_rust",
]


def get_json(url, tries=15):
    for attempt in range(tries):
        try:
            with urllib.request.urlopen(url, timeout=90) as r:
                return json.load(r)
        except Exception:
            time.sleep(min(10, 1.5 + attempt))
    raise RuntimeError("request failed: " + url)


def rows(offset, length=1):
    return get_json(f"https://datasets-server.huggingface.co/rows?dataset={DATASET}&config=default&split=train&offset={offset}&length={length}")["rows"]


def first_index(label):
    lo, hi = 0, 55446
    while lo < hi:
        mid = (lo + hi) // 2
        if rows(mid)[0]["row"]["label"] < label:
            lo = mid + 1
        else:
            hi = mid
    return lo


def main(out: Path, per_class: int):
    out.mkdir(parents=True, exist_ok=True)
    names = get_json(f"https://datasets-server.huggingface.co/info?dataset={DATASET}")["dataset_info"]["default"]["features"]["label"]["names"]
    for cls in CLASSES:
        tag = cls.replace(",", "").replace("___", "_")
        if len(list(out.glob(f"img_{tag}_*.png"))) >= per_class:
            print(cls, "already fetched")
            continue
        label = names.index(cls)
        start = first_index(label)
        # Spread the picks over the class (every 7th photo) so they are not near-duplicates from one plant.
        got = []
        for offset in range(0, per_class * 7, 100):
            got += [(offset + i, r) for i, r in enumerate(rows(start + offset, min(100, per_class * 7 - offset)))]
        taken = 0
        for i, row in got:
            if i % 7 or row["row"]["label"] != label or taken >= per_class:
                continue
            for attempt in range(6):
                try:
                    with urllib.request.urlopen(row["row"]["image"]["src"], timeout=90) as r:
                        data = np.frombuffer(r.read(), np.uint8)
                    break
                except Exception:
                    time.sleep(1.5 + attempt)
            else:
                continue
            img = cv2.imdecode(data, cv2.IMREAD_COLOR)
            h, w = img.shape[:2]
            s = min(h, w)
            crop = img[(h - s) // 2:(h - s) // 2 + s, (w - s) // 2:(w - s) // 2 + s]
            tag = cls.replace(",", "").replace("___", "_")
            cv2.imwrite(str(out / f"img_{tag}_{taken:02d}.png"), cv2.resize(crop, (224, 224), interpolation=cv2.INTER_AREA))
            taken += 1
        print(cls, taken, "photos")


if __name__ == "__main__":
    main(Path(sys.argv[1]), int(sys.argv[2]) if len(sys.argv) > 2 else 30)
