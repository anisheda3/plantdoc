"""Prepares the PlantSeg photos of our four crops for scoring the leaf tracer.

Every photo is cut to the centre square and resized to 224 x 224 - what the app does with a scan (ImagePreprocessor.Prepare) - and
its human-drawn mask is cut and resized the same way (nearest neighbour, so it stays a mask). The dataset's own split is used:
Training + Validation photos (a sample) are for tuning, the Test photos are held out and only used for the reported numbers.

    py -3.11 -I tools/eval/prep_plantseg.py <PlantSeg folder> <output folder>

Run with -I: the dataset is untrusted data and must not be able to put code on the import path.
"""
import csv
import random
import sys

sys.path.insert(0, r"C:\Users\Anish\.claude\jobs\0a9d9ea4\tmp\img")   # numpy / opencv, installed there (trusted)
from pathlib import Path

import cv2
import numpy as np

CROPS = ("Apple", "Tomato", "Potato", "Bell pepper")
SIZE = 224
TUNE_PER_CROP = 90


def square_224(img, interpolation):
    h, w = img.shape[:2]
    s = min(h, w)
    y0, x0 = (h - s) // 2, (w - s) // 2
    return cv2.resize(img[y0:y0 + s, x0:x0 + s], (SIZE, SIZE), interpolation=interpolation)


def main(src: Path, out: Path):
    rows = [r for r in csv.DictReader(open(src / "Metadata.csv", encoding="utf-8")) if r["Plant"] in CROPS]
    rng = random.Random(2026)
    by_split = {"tune": [], "test": []}
    for r in rows:
        if r["Split"] == "Test":
            by_split["test"].append(r)
        else:
            by_split["tune"].append(r)
    for crop in CROPS:   # a fixed-size, shuffled tuning sample per crop
        pool = [r for r in by_split["tune"] if r["Plant"] == crop]
        rng.shuffle(pool)
        by_split["tune"] = [r for r in by_split["tune"] if r["Plant"] != crop] + pool[:TUNE_PER_CROP]

    for split, items in by_split.items():
        folder = out / split
        folder.mkdir(parents=True, exist_ok=True)
        manifest = [["id", "name", "plant", "disease", "dataset_split", "mask_ratio", "license", "url"]]
        kept = 0
        for r in items:
            sub = {"Training": "train", "Validation": "val", "Test": "test"}[r["Split"]]
            img = cv2.imread(str(src / "images" / sub / r["Name"]))
            mask = cv2.imread(str(src / "annotations" / sub / r["Label file"]), cv2.IMREAD_UNCHANGED)
            if img is None or mask is None:
                continue
            if mask.ndim == 3:
                mask = mask[..., 0]
            if mask.shape[:2] != img.shape[:2]:
                mask = cv2.resize(mask, (img.shape[1], img.shape[0]), interpolation=cv2.INTER_NEAREST)
            gt = ((mask > 0) * 255).astype(np.uint8)
            i = f"{kept:04d}"
            cv2.imwrite(str(folder / f"img_{i}.png"), square_224(img, cv2.INTER_AREA))
            cv2.imwrite(str(folder / f"gt_{i}.png"), square_224(gt, cv2.INTER_NEAREST))
            manifest.append([i, r["Name"], r["Plant"], r["Disease"], r["Split"], r["Mask ratio"], r["License"], r["URL"]])
            kept += 1
        with open(folder / "manifest.csv", "w", newline="", encoding="utf-8") as f:
            csv.writer(f).writerows(manifest)
        print(split, kept, "photos")


if __name__ == "__main__":
    main(Path(sys.argv[1]), Path(sys.argv[2]))
