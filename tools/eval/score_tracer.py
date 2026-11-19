"""Scores the leaf tracer against the human masks.

    py -3.11 -I tools/eval/score_tracer.py <folder with img_*, gt_*, pred_*, manifest.csv, results.csv> [--baselines] [--gallery N]

For every photo the tracer's "affected" pixels (lesions + yellow patches) are compared with the human mask: pixel IoU and Dice
(summed over all photos, so big lesions count more), precision and recall; the mean per-photo IoU; and how well the affected
share of the picture agrees (mean absolute error in percentage points, and correlation). Results are split by crop and by
whether the disease is one the app teaches (it shows spots) or one the tracer is not built for (viruses, mildew, leaf mould).
With --baselines the same photos are also scored for: predicting nothing, predicting every pixel, and a classical colour
baseline (Otsu on the Lab a* channel inside a GrabCut leaf).
"""
import csv
import sys

sys.path.insert(0, r"C:\Users\Anish\.claude\jobs\0a9d9ea4\tmp\img")   # numpy / opencv (trusted)
from collections import defaultdict
from pathlib import Path

import cv2
import numpy as np

# Diseases whose damage the app shows as spots/patches and that the tracer is meant to find.
SPOT_DISEASES = {
    "tomato early blight", "tomato late blight", "tomato septoria leaf spot", "tomato bacterial leaf spot",
    "potato early blight", "potato late blight", "apple scab", "apple rust", "apple black rot",
    "bell pepper bacterial spot", "bell pepper frogeye leaf spot", "bell pepper cercospora leaf spot",
}


def load_mask(path):
    m = cv2.imread(str(path), cv2.IMREAD_GRAYSCALE)
    return None if m is None else m > 127


class Tally:
    def __init__(self):
        self.tp = self.fp = self.fn = 0
        self.ious = []
        self.pred_share, self.gt_share = [], []

    def add(self, pred, gt):
        tp = int((pred & gt).sum()); fp = int((pred & ~gt).sum()); fn = int((~pred & gt).sum())
        self.tp += tp; self.fp += fp; self.fn += fn
        union = tp + fp + fn
        self.ious.append(tp / union if union else 1.0)
        self.pred_share.append(pred.mean() * 100); self.gt_share.append(gt.mean() * 100)

    def row(self):
        iou = self.tp / max(1, self.tp + self.fp + self.fn)
        dice = 2 * self.tp / max(1, 2 * self.tp + self.fp + self.fn)
        prec = self.tp / max(1, self.tp + self.fp)
        rec = self.tp / max(1, self.tp + self.fn)
        p, g = np.array(self.pred_share), np.array(self.gt_share)
        mae = float(np.abs(p - g).mean())
        corr = float(np.corrcoef(p, g)[0, 1]) if len(p) > 2 and p.std() > 0 and g.std() > 0 else float("nan")
        return dict(n=len(self.ious), iou=iou, dice=dice, precision=prec, recall=rec, mean_photo_iou=float(np.mean(self.ious)), area_mae_pp=mae, area_corr=corr)


def baseline_otsu_lab(img):
    """Classical baseline: GrabCut finds the leaf, Otsu on the Lab a* channel (green to red) splits healthy from diseased inside it."""
    h, w = img.shape[:2]
    mask = np.zeros((h, w), np.uint8)
    rect = (w // 10, h // 10, w - 2 * (w // 10), h - 2 * (h // 10))
    bgd, fgd = np.zeros((1, 65)), np.zeros((1, 65))
    try:
        cv2.grabCut(img, mask, rect, bgd, fgd, 3, cv2.GC_INIT_WITH_RECT)
        leaf = (mask == cv2.GC_FGD) | (mask == cv2.GC_PR_FGD)
    except cv2.error:
        leaf = np.ones((h, w), bool)
    a = cv2.cvtColor(img, cv2.COLOR_BGR2LAB)[..., 1]
    if leaf.sum() < 50:
        return np.zeros((h, w), bool)
    thr, _ = cv2.threshold(a[leaf].reshape(-1, 1), 0, 255, cv2.THRESH_BINARY + cv2.THRESH_OTSU)
    return leaf & (a > thr)


def main():
    folder = Path(sys.argv[1])
    predf = folder / sys.argv[sys.argv.index("--pred") + 1] if "--pred" in sys.argv else folder
    baselines = "--baselines" in sys.argv
    manifest = {r["id"]: r for r in csv.DictReader(open(folder / "manifest.csv", encoding="utf-8"))}
    results = {r["name"]: r for r in csv.DictReader(open(predf / "results.csv", encoding="utf-8"))}

    groups = defaultdict(Tally)
    base = {k: defaultdict(Tally) for k in ("nothing", "everything", "otsu_lab")} if baselines else {}
    shares = []
    no_leaf = [0]
    ms = []
    for i, meta in manifest.items():
        gt = load_mask(folder / f"gt_{i}.png")
        lesion = load_mask(predf / f"pred_lesion_{i}.png")
        yellow = load_mask(predf / f"pred_yellow_{i}.png")
        leaf = load_mask(predf / f"pred_leaf_{i}.png")
        if gt is None or lesion is None:
            continue
        pred = lesion | yellow
        spot = meta["disease"] in SPOT_DISEASES
        keys = ["ALL", f"crop:{meta['plant']}", "spot diseases" if spot else "other diseases", f"disease:{meta['disease']}"]
        for k in keys:
            groups[k].add(pred, gt)
        leaf_px = int(leaf.sum())
        if leaf_px >= 200:
            shares.append((float(pred.sum()) / leaf_px * 100, float(gt.sum()) / leaf_px * 100, leaf_px / pred.size))
        else:
            no_leaf[0] += 1
        ms.append(float(results[i]["milliseconds"]) if i in results else 0)
        if baselines:
            img = cv2.imread(str(folder / f"img_{i}.png"))
            outs = {"nothing": np.zeros_like(gt), "everything": np.ones_like(gt), "otsu_lab": baseline_otsu_lab(img)}
            for name, p in outs.items():
                for k in keys[:3]:
                    base[name][k].add(p, gt)

    def fmt(r):
        return f"n={r['n']:4d}  IoU {r['iou']:.3f}  Dice {r['dice']:.3f}  precision {r['precision']:.3f}  recall {r['recall']:.3f}  mean per-photo IoU {r['mean_photo_iou']:.3f}  area MAE {r['area_mae_pp']:.1f} pp  corr {r['area_corr']:.2f}"

    print(f"== Leaf tracer vs human masks ({folder.name}) ==")
    for k in sorted(groups, key=lambda k: (k != "ALL", not k.startswith("crop"), not k.endswith("diseases"), k)):
        print(f"{k:42s} {fmt(groups[k].row())}")
    if baselines:
        print("\n== Baselines on the same photos ==")
        for name in base:
            for k in ("ALL", "spot diseases", "other diseases"):
                print(f"{name:11s} {k:16s} {fmt(base[name][k].row())}")
    s = np.array(shares)
    print(f"\nPercent of the traced leaf that is affected: tracer vs human mask, MAE {np.abs(s[:, 0] - s[:, 1]).mean():.1f} pp, "
          f"corr {np.corrcoef(s[:, 0], s[:, 1])[0, 1]:.2f} (leaf = the tracer's own leaf)")
    print(f"Tracer time per 224 px photo in the Editor: mean {np.mean(ms):.1f} ms, max {np.max(ms):.0f} ms")

    if "--gallery" in sys.argv:
        n = int(sys.argv[sys.argv.index("--gallery") + 1])
        rng = np.random.default_rng(1)
        pick = rng.choice(sorted(manifest), size=min(n, len(manifest)), replace=False)
        tiles = []
        for i in pick:
            img = cv2.imread(str(folder / f"img_{i}.png"))
            gt = load_mask(folder / f"gt_{i}.png")
            pr = load_mask(predf / f"pred_lesion_{i}.png") | load_mask(predf / f"pred_yellow_{i}.png")
            over = img.copy()
            over[gt & ~pr] = (0, 0, 255)         # red: human marked, tracer missed
            over[pr & ~gt] = (255, 120, 0)       # blue: tracer marked, human did not
            over[pr & gt] = (0, 220, 0)          # green: both
            tiles.append(np.hstack([img, over]))
        cols = 2
        rows = [np.hstack(tiles[r:r + cols]) for r in range(0, len(tiles) - len(tiles) % cols, cols)]
        cv2.imwrite(str(folder / "gallery.png"), np.vstack(rows))
        print("gallery ->", folder / "gallery.png", "(left: photo; right: green both, red missed, blue extra)")


if __name__ == "__main__":
    main()
