"""False alarms on healthy leaves: how much of a healthy leaf does the tracer wrongly flag?

    py -3.11 -I tools/eval/score_healthy.py <folder with img_*.png> [--pred out_<name>]
"""
import sys

sys.path.insert(0, r"C:\Users\Anish\.claude\jobs\0a9d9ea4\tmp\img")   # numpy / opencv (trusted)
from pathlib import Path

import cv2
import numpy as np


def main():
    folder = Path(sys.argv[1])
    pred = folder / sys.argv[sys.argv.index("--pred") + 1] if "--pred" in sys.argv else folder
    shares, flagged_any, usable, n = [], 0, 0, 0
    for img in sorted(folder.glob("img_*.png")):
        i = img.stem[4:]
        leaf = cv2.imread(str(pred / f"pred_leaf_{i}.png"), 0) > 127
        hit = (cv2.imread(str(pred / f"pred_lesion_{i}.png"), 0) > 127) | (cv2.imread(str(pred / f"pred_yellow_{i}.png"), 0) > 127)
        n += 1
        if leaf.sum() < 200:
            continue
        share = hit.sum() / leaf.sum() * 100
        shares.append(share)
        flagged_any += hit.sum() > 0.01 * leaf.sum()
        usable += 1
    s = np.array(shares)
    print(f"healthy leaves: {n}, with a leaf found: {usable}; flagged share of leaf: mean {s.mean():.1f}%, median {np.median(s):.1f}%, "
          f"photos with more than 1% flagged: {flagged_any} ({flagged_any / max(1, usable) * 100:.0f}%)")


if __name__ == "__main__":
    main()
