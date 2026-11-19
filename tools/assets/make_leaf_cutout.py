"""
Cuts a leaf photographed on a white background out into a transparent PNG, straightened so the
leaf stands upright with its tip up and its stalk at the bottom.

    python make_leaf_cutout.py <in.jpg> <out.png> [--height 1100]

Steps: separate leaf from background by colour, keep the largest blob, fill holes, find the
leaf's long axis (PCA) and rotate it vertical (the narrower end becomes the base), crop, then
bleed the leaf colours outward under the transparent edge so no white fringe shows when the
texture is filtered. Prints the final width/height ratio, which the leaf mesh uses.
"""
from __future__ import annotations

import argparse
import sys

import cv2
import numpy as np


def main() -> None:
    ap = argparse.ArgumentParser()
    ap.add_argument("src"); ap.add_argument("dst")
    ap.add_argument("--height", type=int, default=1100)
    ap.add_argument("--margin", type=int, default=6)
    a = ap.parse_args()

    bgr = cv2.imread(a.src, cv2.IMREAD_COLOR)
    if bgr is None:
        sys.exit(f"cannot read {a.src}")
    hsv = cv2.cvtColor(bgr, cv2.COLOR_BGR2HSV)
    s, v = hsv[..., 1].astype(np.float32), hsv[..., 2].astype(np.float32)
    # Background is near-white with a faint gradient: leaf = saturated or dark.
    score = np.maximum((s - 28) / 40.0, (215 - v) / 40.0)
    mask = (score > 0.5).astype(np.uint8) * 255
    mask = cv2.morphologyEx(mask, cv2.MORPH_CLOSE, np.ones((5, 5), np.uint8))
    n, labels, stats, _ = cv2.connectedComponentsWithStats(mask, 8)
    biggest = 1 + int(np.argmax(stats[1:, cv2.CC_STAT_AREA]))
    mask = np.where(labels == biggest, 255, 0).astype(np.uint8)
    # fill interior holes (bright vein highlights)
    inv = cv2.bitwise_not(mask)
    ff = inv.copy()
    h, w = mask.shape
    cv2.floodFill(ff, np.zeros((h + 2, w + 2), np.uint8), (0, 0), 0)
    mask = cv2.bitwise_or(mask, ff)

    # orientation: long axis vertical, stalk (the narrower end) at the bottom
    ys, xs = np.nonzero(mask)
    pts = np.stack([xs, ys], 1).astype(np.float32)
    mean = pts.mean(0)
    vals, vecs = np.linalg.eigh(np.cov((pts - mean).T))
    ax, ay = vecs[:, np.argmax(vals)]                     # long axis in image coordinates (y down)
    phi = np.degrees(np.arctan2(ay, ax))                  # angle of the axis, clockwise from +x
    best = None
    for turn in (phi - 90.0, phi - 90.0 + 180.0):         # OpenCV angles are counter-clockwise
        m = cv2.getRotationMatrix2D((float(mean[0]), float(mean[1])), turn, 1.0)
        t = cv2.warpAffine(mask, m, (w, h), flags=cv2.INTER_NEAREST)
        yy, xx = np.nonzero(t)
        height = yy.max() - yy.min()
        band = max(2, int(height * 0.06))
        bottom_area = np.count_nonzero(t[yy.max() - band:yy.max() + 1])
        if best is None or bottom_area < best[0]:
            best = (bottom_area, m)
    rot = best[1]

    # expand the canvas so nothing is clipped
    corners = np.array([[0, 0, 1], [w, 0, 1], [w, h, 1], [0, h, 1]], np.float32) @ rot.T
    min_xy, max_xy = corners.min(0), corners.max(0)
    rot[:, 2] -= min_xy
    W, H = int(np.ceil(max_xy[0] - min_xy[0])), int(np.ceil(max_xy[1] - min_xy[1]))
    img = cv2.warpAffine(bgr, rot, (W, H), flags=cv2.INTER_CUBIC, borderMode=cv2.BORDER_REPLICATE)
    msk = cv2.warpAffine(mask, rot, (W, H), flags=cv2.INTER_LINEAR)

    ys, xs = np.nonzero(msk > 127)
    x0, x1 = max(0, xs.min() - a.margin), min(W, xs.max() + a.margin)
    y0, y1 = max(0, ys.min() - a.margin), min(H, ys.max() + a.margin)
    img, msk = img[y0:y1, x0:x1], msk[y0:y1, x0:x1]

    # clean soft edge: erode ~1px to drop the white fringe, then feather
    msk = cv2.erode(msk, np.ones((3, 3), np.uint8))
    alpha = cv2.GaussianBlur(msk, (0, 0), 1.1).astype(np.float32) / 255.0
    alpha = np.clip((alpha - 0.15) / 0.7, 0, 1)

    # colour bleed: fill transparent area with the nearest leaf colour (no halos when filtered)
    solid = (msk > 200).astype(np.uint8)
    filled = img.copy()
    for _ in range(40):
        blurred = cv2.blur(np.where(solid[..., None] > 0, filled, 0).astype(np.float32), (5, 5))
        weight = cv2.blur(solid.astype(np.float32), (5, 5))[..., None]
        grow = (weight > 0.05) & (solid[..., None] == 0)
        fill = np.where(weight > 0, blurred / np.maximum(weight, 1e-3), 0)
        filled = np.where(grow, fill, filled).astype(np.uint8)
        solid = np.where(grow[..., 0], 1, solid).astype(np.uint8)
        if solid.all():
            break

    scale = a.height / filled.shape[0]
    size = (max(1, round(filled.shape[1] * scale)), a.height)
    filled = cv2.resize(filled, size, interpolation=cv2.INTER_LANCZOS4)
    alpha = cv2.resize(alpha, size, interpolation=cv2.INTER_CUBIC)
    # mild unsharp mask: the source is 1024 px wide and gets enlarged
    soft = cv2.GaussianBlur(filled, (0, 0), 1.6)
    filled = cv2.addWeighted(filled, 1.35, soft, -0.35, 0)
    out = np.dstack([filled, np.clip(alpha * 255, 0, 255).astype(np.uint8)])
    cv2.imwrite(a.dst, out)
    print(f"saved {a.dst}: {size[0]}x{size[1]} (width/height = {size[0] / size[1]:.3f})")


if __name__ == "__main__":
    main()
