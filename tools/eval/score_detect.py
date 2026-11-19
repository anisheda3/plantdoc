"""Does the tracer flag more of a diseased leaf than of a healthy one? (Lab-style PlantVillage close-ups.)

    py -3.11 -I tools/eval/score_detect.py <healthy folder> <diseased folder>

Both folders hold img_*.png plus the tracer's pred_*.png. The "affected share" is the flagged area as a share of the leaf the tracer found.
Reports the AUC (chance that a random diseased leaf gets a higher share than a random healthy one), and sensitivity / specificity at 5% and 10%.
"""
import sys

sys.path.insert(0, r"C:\Users\Anish\.claude\jobs\0a9d9ea4\tmp\img")   # numpy / opencv (trusted)
from collections import defaultdict
from pathlib import Path

import cv2
import numpy as np


def shares(folder: Path, pred: Path = None, parity: str = "all"):
    out = []
    pred = pred or folder
    for img in sorted(folder.glob("img_*.png")):
        i = img.stem[4:]
        number = int(i.rsplit("_", 1)[1])
        if (parity == "even" and number % 2) or (parity == "odd" and not number % 2):
            continue
        leaf = cv2.imread(str(pred / f"pred_leaf_{i}.png"), 0) > 127
        hit = (cv2.imread(str(pred / f"pred_lesion_{i}.png"), 0) > 127) | (cv2.imread(str(pred / f"pred_yellow_{i}.png"), 0) > 127)
        out.append((i, hit.sum() / leaf.sum() * 100 if leaf.sum() >= 200 else float("nan")))
    return out


def auc(neg, pos):
    neg, pos = np.array(neg), np.array(pos)
    wins = (pos[:, None] > neg[None, :]).sum() + 0.5 * (pos[:, None] == neg[None, :]).sum()
    return wins / (len(neg) * len(pos))


def main():
    sub = sys.argv[sys.argv.index("--pred") + 1] if "--pred" in sys.argv else None
    parity = sys.argv[sys.argv.index("--parity") + 1] if "--parity" in sys.argv else "all"
    hf, df = Path(sys.argv[1]), Path(sys.argv[2])
    healthy = [s for _, s in shares(hf, hf / sub if sub else None, parity) if s == s]
    rows = shares(df, df / sub if sub else None, parity)
    diseased = [s for _, s in rows if s == s]
    print(f"healthy n={len(healthy)} median share {np.median(healthy):.1f}%; diseased n={len(diseased)} median share {np.median(diseased):.1f}%")
    print(f"AUC (diseased vs healthy by affected share): {auc(healthy, diseased):.3f}")
    for t in (5, 10):
        print(f"at {t}%: sensitivity {np.mean(np.array(diseased) > t) * 100:.0f}%, specificity {np.mean(np.array(healthy) <= t) * 100:.0f}%")
    by = defaultdict(list)
    for i, s in rows:
        if s == s:
            by[i.rsplit("_", 1)[0]].append(s)
    for k, v in sorted(by.items()):
        print(f"  {k:36s} n={len(v):2d} median affected share {np.median(v):5.1f}%   >5%: {np.mean(np.array(v) > 5) * 100:3.0f}%")


if __name__ == "__main__":
    main()
