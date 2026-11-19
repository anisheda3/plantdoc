"""Fetches PlantVillage photos (CC0 1.0 mirror on Hugging Face: DScomp380/plant_village) for any classes, into
tools/assets/_cache/pv/<Class_name>/<nn>.jpg. Used for the leaf textures of the twins (healthy classes), the reference photo of
each disease, and the tracer's diseased-vs-healthy check.

The dataset is sorted by class, so each class is found by bisecting on the label. Photos are taken every <stride>th row so they
do not all come from one plant. The Hugging Face datasets-server is flaky: every request retries, and photos already on disk are kept.

    py -3.11 tools/assets/fetch_pv_classes.py <per class> <Class> [<Class> ...]
    py -3.11 tools/assets/fetch_pv_classes.py 24 Blueberry___healthy Cherry___healthy
"""
import json
import sys
import time
import urllib.request
from pathlib import Path

DATASET = "DScomp380/plant_village"
CACHE = Path(__file__).resolve().parent / "_cache" / "pv"
TOTAL_ROWS = 55447


def get_json(url, tries=15):
    for attempt in range(tries):
        try:
            with urllib.request.urlopen(url, timeout=90) as r:
                return json.load(r)
        except Exception:
            time.sleep(min(10, 1.5 + attempt))
    raise RuntimeError("request failed: " + url)


def rows(offset, length=1):
    url = f"https://datasets-server.huggingface.co/rows?dataset={DATASET}&config=default&split=train&offset={offset}&length={length}"
    return get_json(url)["rows"]


def first_index(label):
    lo, hi = 0, TOTAL_ROWS - 1
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
            with urllib.request.urlopen(url, timeout=90) as r:
                data = r.read()
            dest.write_bytes(data)
            return True
        except Exception:
            time.sleep(min(10, 1.5 + attempt))
    return False


def folder_name(cls):
    return "".join(c if c.isalnum() else "_" for c in cls).strip("_")


def main(per_class, classes, stride=5):
    names = get_json(f"https://datasets-server.huggingface.co/info?dataset={DATASET}")["dataset_info"]["default"]["features"]["label"]["names"]
    for cls in classes:
        out = CACHE / folder_name(cls)
        out.mkdir(parents=True, exist_ok=True)
        have = len(list(out.glob("*.jpg")))
        if have >= per_class:
            print(cls, "already has", have)
            continue
        label = names.index(cls)
        start = first_index(label)
        got = []
        span = per_class * stride
        for offset in range(0, span, 100):
            batch = rows(start + offset, min(100, span - offset))
            got += [(offset + i, r) for i, r in enumerate(batch)]
            if batch and batch[-1]["row"]["label"] != label:
                break
        taken = 0
        for i, row in got:
            if i % stride or row["row"]["label"] != label:
                continue
            dest = out / f"{taken:02d}.jpg"
            if dest.exists() or download(row["row"]["image"]["src"], dest):
                taken += 1
            if taken >= per_class:
                break
        print(cls, "label", label, "from row", start, "->", taken, "photos")


if __name__ == "__main__":
    main(int(sys.argv[1]), sys.argv[2:])
