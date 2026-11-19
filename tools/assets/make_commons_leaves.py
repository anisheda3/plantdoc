"""
Leaf cut-outs from openly licensed Wikimedia Commons photos, for crops whose PlantVillage leaves are poor or missing.
Run AFTER make_species_leaves.py (it overwrites those crops' leaf_*.png) and BEFORE make_plant_textures.py.

    py -3.11 make_commons_leaves.py

Sources (downloaded with commons.py get, credited in credits.json):
  peach   File:Folla Pexegueiro 006eue.jpg (Lmbuga, CC BY-SA 3.0): one peach leaf on white
  orange  File:Clementine012.png (asdfVic, CC BY-SA 3.0): one citrus (clementine) leaf on a plain ground; sweet-orange
          leaves have the same elliptic, pointed, short-winged form
  squash  File:-2020-05-25 Courgette plant, Trimingham (2).JPG (CC BY-SA 4.0): a young courgette (zucchini) plant on bare soil;
          its lobed true leaves are cut out by colour
"""
from pathlib import Path

import cv2
import numpy as np

HERE = Path(__file__).resolve().parent
SRC = HERE / "_cache" / "newleaves"
OUT = HERE / "_cache" / "leaves"


def largest(mask, near=None):
    n, lab, stats, cent = cv2.connectedComponentsWithStats(mask.astype(np.uint8), 8)
    if n <= 1:
        raise SystemExit("nothing found")
    if near is not None:
        k = lab[near[1], near[0]]
        if k > 0:
            return lab == k
    k = 1 + int(np.argmax(stats[1:, cv2.CC_STAT_AREA]))
    return lab == k


def clean(mask, close=9, open_=5):
    m = mask.astype(np.uint8) * 255
    m = cv2.morphologyEx(m, cv2.MORPH_CLOSE, cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (close, close)))
    m = cv2.morphologyEx(m, cv2.MORPH_OPEN, cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (open_, open_)))
    # fill holes
    inv = cv2.bitwise_not(m)
    h, w = m.shape
    ff = inv.copy()
    cv2.floodFill(ff, np.zeros((h + 2, w + 2), np.uint8), (0, 0), 0)
    return (m > 0) | (ff > 0)


def cutout(bgr, mask, max_side=900):
    ys, xs = np.nonzero(mask)
    y0, y1, x0, x1 = ys.min(), ys.max() + 1, xs.min(), xs.max() + 1
    pad = 6
    y0, x0 = max(0, y0 - pad), max(0, x0 - pad)
    y1, x1 = min(mask.shape[0], y1 + pad), min(mask.shape[1], x1 + pad)
    rgb = bgr[y0:y1, x0:x1]
    a = (mask[y0:y1, x0:x1].astype(np.float32))
    a = cv2.GaussianBlur(a, (3, 3), 0)
    # colour bleed under the transparent part, so filtering never pulls in the old background
    inside = mask[y0:y1, x0:x1]
    mean = rgb[inside].mean(axis=0)
    fill = rgb.copy()
    fill[~inside] = mean
    out = np.dstack([fill, (a * 255).astype(np.uint8)])
    s = max_side / max(out.shape[:2])
    if s < 1:
        out = cv2.resize(out, (int(out.shape[1] * s), int(out.shape[0] * s)), interpolation=cv2.INTER_AREA)
    return out


def save(crop, leaves):
    folder = OUT / crop
    folder.mkdir(parents=True, exist_ok=True)
    for f in folder.glob("leaf_*.png"):
        f.unlink()
    for i, leaf in enumerate(leaves):
        cv2.imwrite(str(folder / f"leaf_{i}.png"), leaf)
    print(crop, len(leaves), "leaves", [l.shape[:2] for l in leaves])


def variants(leaf):
    """The same leaf mirrored, and a slightly lighter/darker copy, so a canopy of them is not all clones."""
    m = leaf[:, ::-1].copy()
    l = leaf.copy()
    l[..., :3] = np.clip(l[..., :3].astype(np.int16) * np.array([0.96, 1.04, 1.0]), 0, 255).astype(np.uint8)
    return [leaf, m, l[:, ::-1]]


def on_plain(img):
    """A leaf on a plain light background: everything clearly darker or more coloured than the background."""
    hsv = cv2.cvtColor(img, cv2.COLOR_BGR2HSV)
    border = np.concatenate([img[:20].reshape(-1, 3), img[-20:].reshape(-1, 3), img[:, :20].reshape(-1, 3), img[:, -20:].reshape(-1, 3)])
    bg = np.median(border, axis=0)
    diff = np.linalg.norm(img.astype(np.float32) - bg, axis=2)
    return clean((diff > 40) | (hsv[..., 1] > 70))


def peach():
    img = cv2.imread(str(SRC / "peach_a.jpg"))
    leaf = cutout(img, largest(on_plain(img)))
    leaf = cv2.rotate(leaf, cv2.ROTATE_90_CLOCKWISE)           # stalk (on the right) to the bottom
    save("peach", variants(leaf))


def orange():
    img = cv2.imread(str(SRC / "orange_a.jpg"))
    s = 1600 / img.shape[1]
    img = cv2.resize(img, None, fx=s, fy=s, interpolation=cv2.INTER_AREA)
    leaf = cutout(img, largest(on_plain(img)))
    leaf = cv2.rotate(leaf, cv2.ROTATE_90_CLOCKWISE)
    # A clementine leaf is narrower (about 0.3 wide for its length) than a sweet-orange leaf (about 0.5: 7.5-11 cm long,
    # 4-6 cm wide, Flora of Pakistan / FNA), so the photo is widened to the sweet orange's proportion. Recorded in DECISIONS.md.
    leaf = cv2.resize(leaf, (int(leaf.shape[1] * 1.6), leaf.shape[0]), interpolation=cv2.INTER_CUBIC)
    save("orange", variants(leaf))


def squash():
    img = cv2.imread(str(SRC / "squash_b.jpg"))
    lab = cv2.cvtColor(img, cv2.COLOR_BGR2LAB)
    # Green in Lab (a* well below neutral 128): the brown soil and its crumbs sit at or above neutral, even in shade.
    green = (lab[..., 1] < 118) & (lab[..., 0] > 45)
    leaves = []
    # (box x0, y0, x1, y1 in the full photo; a point on the leaf; how to turn it so the stalk is at the bottom)
    for box, rot in [((1190, 1520, 2560, 2640), cv2.ROTATE_180), ((40, 760, 1200, 1700), cv2.ROTATE_90_CLOCKWISE)]:
        x0, y0, x1, y1 = box
        sub = np.zeros_like(green)
        sub[y0:y1, x0:x1] = green[y0:y1, x0:x1]
        m = largest(clean(sub, 15, 9), ((x0 + x1) // 2, (y0 + y1) // 2))
        leaf = cutout(img, m)
        leaves.append(cv2.rotate(leaf, rot))
    save("squash", [leaves[0], leaves[1], leaves[0][:, ::-1].copy()])


if __name__ == "__main__":
    peach()
    orange()
    squash()
