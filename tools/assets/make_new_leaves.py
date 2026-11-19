"""
Leaf cut-outs for the plants added from the PlantSeg dataset. Run after make_commons_leaves.py, before make_plant_textures.py:

    py -3.11 make_new_leaves.py

Three kinds of source, chosen per plant (docs/DECISIONS.md, 10 Nov 2026):
  * cut out of an openly licensed Wikimedia Commons photo of the real leaf (eggplant, ginger, lettuce, basil, coffee, carrot,
    maple, tobacco), credited in credits.json by commons.py;
  * built from a real leaf-surface photo of the species with the leaf's outline drawn from the flora description, where Commons
    has no photo of a whole, clean leaf (banana with its wind-tears, broccoli / cabbage / cauliflower, cucumber on the courgette
    surface, celery leaflets);
  * reused from a close relative with the same leaf (bean <- soybean leaflets, plum <- sweet cherry, both Prunus; garlic, rice and
    wheat <- the corn blade strips, narrowed and re-tinted).
"""
import shutil
from pathlib import Path

import cv2
import numpy as np

HERE = Path(__file__).resolve().parent
SRC = HERE / "_cache" / "newleaves"
OUT = HERE / "_cache" / "leaves"
RNG = np.random.default_rng(7)

from make_commons_leaves import largest, clean, cutout, save, variants  # noqa: E402


def load(name, max_side=2400):
    img = cv2.imread(str(SRC / f"{name}.jpg"))
    if img is None:
        raise SystemExit(f"missing {name}.jpg (download it with commons.py get)")
    s = max_side / max(img.shape[:2])
    return cv2.resize(img, None, fx=s, fy=s, interpolation=cv2.INTER_AREA) if s < 1 else img


def box(img, fx0, fy0, fx1, fy1):
    h, w = img.shape[:2]
    return int(fx0 * w), int(fy0 * h), int(fx1 * w), int(fy1 * h)


def green_on_soil(img):
    lab = cv2.cvtColor(img, cv2.COLOR_BGR2LAB)
    return (lab[..., 1] < 120) & (lab[..., 0] > 40)


def bright_on_dark(img, thr=48):
    hsv = cv2.cvtColor(img, cv2.COLOR_BGR2HSV)
    return (hsv[..., 2] > thr) & (hsv[..., 1] > 40)


def cut_in_box(img, mask, b, rot=None, close=11, open_=7):
    x0, y0, x1, y1 = b
    sub = np.zeros_like(mask)
    sub[y0:y1, x0:x1] = mask[y0:y1, x0:x1]
    m = largest(clean(sub, close, open_), ((x0 + x1) // 2, (y0 + y1) // 2))
    leaf = cutout(img, m)
    return cv2.rotate(leaf, rot) if rot is not None else leaf


def from_plain(img, b, rot=None):
    border = np.concatenate([img[:10].reshape(-1, 3), img[-10:].reshape(-1, 3)])
    bg = np.median(border, axis=0)
    diff = np.linalg.norm(img.astype(np.float32) - bg, axis=2)
    return cut_in_box(img, diff > 45, b, rot)


# ------------------------------------------------------------------ built leaves: a real surface inside a drawn outline

def fill_surface(surface, w, h, mirror_tile=True):
    """The surface photo scaled to the leaf's width and repeated (mirrored) down its length."""
    s = cv2.resize(surface, (w, int(surface.shape[0] * w / surface.shape[1])), interpolation=cv2.INTER_AREA)
    rows, k = [], 0
    while sum(r.shape[0] for r in rows) < h:
        rows.append(s if k % 2 == 0 or not mirror_tile else s[::-1])
        k += 1
    return np.vstack(rows)[:h]


def compose(surface, alpha):
    a = np.clip(alpha, 0, 1)
    inside = a > 0.5
    fill = surface.copy()
    fill[~inside] = surface[inside].mean(axis=0)
    return np.dstack([fill, (a * 255).astype(np.uint8)])


def banana_leaf(surface):
    """An oblong banana blade (about 3.3 times as long as wide), rounded at both ends, the midrib running down the middle, torn by
    the wind along its parallel side veins as banana leaves always are."""
    w, h = 520, 1700
    surf = fill_surface(surface, w, h)
    ys, xs = np.mgrid[0:h, 0:w].astype(np.float32)
    t = 1 - ys / h                                                 # 0 at the base (bottom), 1 at the tip
    half = (w / 2 - 6) * np.sqrt(np.clip(t / 0.05, 0, 1)) * np.sqrt(np.clip((1 - t) / 0.09, 0, 1))
    half *= 1 + 0.015 * np.sin(t * 40)
    alpha = (np.abs(xs - w / 2) < half).astype(np.float32)
    # Tears: slits from the margin toward the midrib along the side veins (they leave the midrib at about 70 degrees).
    for side in (-1, 1):
        for _ in range(6):
            t0 = RNG.uniform(0.15, 0.9)
            depth = RNG.uniform(0.35, 0.95)
            y_edge = (1 - t0) * h
            for k in range(int(depth * w / 2)):
                x = w / 2 + side * (w / 2 - k)
                y = int(y_edge + k * 0.36)                        # climbing toward the tip as it nears the midrib
                if 0 <= y < h and 0 <= int(x) < w:
                    alpha[max(0, y - 1):y + 2, max(0, int(x) - 1):int(x) + 2] = 0
    stalk = (np.abs(xs - w / 2) < 9) & (ys > h - 40)               # the midrib carries on into the stalk at the base
    alpha[stalk] = 1
    return compose(surf, cv2.GaussianBlur(alpha, (3, 3), 0))


def brassica_leaf(surface, kind):
    """Broccoli: long, deeply lobed at the base along a long stalk; cabbage: broad, round, wavy; cauliflower: long, oval, upright.
    The waxy blue-green surface and pale midrib come from a real broccoli leaf photo."""
    w, h = {"broccoli": (560, 1100), "cabbage": (760, 820), "cauliflower": (520, 1100)}[kind]
    surf = fill_surface(surface, w, h)
    ys, xs = np.mgrid[0:h, 0:w].astype(np.float32)
    t = 1 - ys / h
    if kind == "broccoli":
        body = np.sin(np.clip((t - 0.22) / 0.78, 0, 1) * np.pi * 0.92) ** 0.7
        lobes = (t < 0.4) * 0.45 * np.clip(np.sin(t * 34), 0, 1) * (t > 0.06)        # small lobes along the stalk
        shape = np.maximum(body, lobes) + 0.04 * np.sin(t * 60)
    elif kind == "cabbage":
        shape = np.sin(np.clip(t / 0.98, 0, 1) * np.pi) ** 0.55 * (1 + 0.05 * np.sin(t * 46)) * np.clip(t / 0.06, 0.15, 1)
    else:
        shape = np.sin(np.clip((t - 0.1) / 0.9, 0, 1) * np.pi * 0.95) ** 0.6 * (1 + 0.035 * np.sin(t * 52))
    half = (w / 2 - 6) * shape
    alpha = (np.abs(xs - w / 2) < np.maximum(half, 10 * (t < 0.3))).astype(np.float32)   # the stalk below the blade
    return compose(surf, cv2.GaussianBlur(alpha, (5, 5), 0))


def ovate_leaf(surface, aspect=0.55, widest=0.42, tip=1.6, base=0.9, ruffle=0.0, teeth=0.0, stalk=0.05, size=1000):
    """A simple leaf drawn from its description: ovate to elliptic (widest a share of the way up), a pointed or rounded tip, a
    tapering or rounded base, an optional ruffled (lettuce) or toothed (basil) edge, a short stalk; filled with the real surface."""
    h = size
    w = int(size * aspect)
    surf = fill_surface(surface, w, h)
    ys, xs = np.mgrid[0:h, 0:w].astype(np.float32)
    t = 1 - ys / h
    blade_t = np.clip((t - stalk) / (1 - stalk), 0, 1)
    up = np.clip(blade_t / widest, 0, 1) ** base
    down = np.clip((1 - blade_t) / (1 - widest), 0, 1) ** (1 / tip)
    shape = np.sin(np.minimum(up, 1) * np.pi / 2) * down
    shape *= 1 + ruffle * np.sin(blade_t * 38) + teeth * (np.abs((blade_t * 46) % 1 - 0.5) * 2 - 0.5)
    half = (w / 2 - 4) * shape
    alpha = (np.abs(xs - w / 2) < np.maximum(half, 5 * (t < stalk + 0.01))).astype(np.float32)
    return compose(surf, cv2.GaussianBlur(alpha, (3, 3), 0))


def palmate_leaf(surface, lobes=5, depth=0.32, notch=0.16, tooth=0.025, size=900):
    """A palmately lobed, heart-based leaf (cucumber: 3-7 shallow, pointed, angular lobes, a deep narrow notch at the stalk, a
    toothed edge), drawn as a polygon round the point where the stalk joins and filled with the real surface."""
    w = h = size
    surf = cv2.resize(surface, (w, h), interpolation=cv2.INTER_AREA)
    cx, cy = w * 0.5, h * 0.72
    R = h * 0.46
    # Lobe tips (angle from straight up, radius) and the sinuses between them, then the heart-shaped base either side of the notch.
    tips = np.linspace(-120, 120, lobes)
    radii = 1.0 - 0.22 * (np.abs(tips) / 120) ** 1.5
    pts = []
    for i, (a, r) in enumerate(zip(tips, radii)):
        if i > 0:
            mid = (tips[i - 1] + a) / 2
            pts.append((mid, (1 - depth) * (radii[i - 1] + r) / 2))
        pts.append((a, r))
    pts = [(-180 + notch * 180, 0.42)] + [(-150, 0.62)] + pts + [(150, 0.62), (180 - notch * 180, 0.42), (180, 0.05)]
    pts = [(-180, 0.05)] + pts
    poly = []
    for (a0, r0), (a1, r1) in zip(pts[:-1], pts[1:]):
        for k in range(12):
            u = k / 12
            a, r = a0 + (a1 - a0) * u, r0 + (r1 - r0) * u
            r *= 1 + tooth * (abs((u * 3) % 1 - 0.5) * 2 - 0.5)          # small teeth along each edge
            rad = np.radians(a)
            poly.append((cx + np.sin(rad) * r * R, cy - np.cos(rad) * r * R))
    mask = np.zeros((h, w), np.uint8)
    cv2.fillPoly(mask, [np.array(poly, np.int32)], 255)
    cv2.line(mask, (int(cx), int(cy)), (int(cx), h - 1), 255, 10)      # the stalk
    alpha = cv2.GaussianBlur(mask.astype(np.float32) / 255, (3, 3), 0)
    return compose(surf, alpha)


def celery_leaflet(surface, size=520):
    """A celery leaflet: rhombic, three-lobed, coarsely toothed, narrowing to a short stalk."""
    w, h = int(size * 0.82), size
    surf = cv2.resize(surface, (w, h), interpolation=cv2.INTER_AREA)
    ys, xs = np.mgrid[0:h, 0:w].astype(np.float32)
    t = 1 - ys / h
    body = np.clip(np.minimum(t / 0.55, (1 - t) / 0.45), 0, 1) ** 0.9                   # a rhombus
    lobes = 1 - 0.25 * np.clip(np.sin((t - 0.45) * 9), 0, 1) * (t > 0.45)              # cuts between the three lobes
    teeth = 1 + 0.08 * (np.abs((t * 22) % 1 - 0.5) * 2 - 0.5)
    half = (w / 2 - 4) * body * lobes * teeth
    alpha = (np.abs(xs - w / 2) < np.maximum(half, 6 * (t < 0.12))).astype(np.float32)
    return compose(surf, cv2.GaussianBlur(alpha, (3, 3), 0))


def strap_from_corn(width_scale, tint, n=3):
    """Grass blades from the real corn blade strips: narrowed (rice and wheat blades are 0.5-2 cm wide, corn's 3-12 cm) and tinted."""
    leaves = []
    for f in sorted((OUT / "corn").glob("leaf_*.png"))[:n]:
        leaf = cv2.imread(str(f), cv2.IMREAD_UNCHANGED)
        h, w = leaf.shape[:2]
        leaf = cv2.resize(leaf, (max(8, int(w * width_scale)), h), interpolation=cv2.INTER_AREA)
        leaf[..., :3] = np.clip(leaf[..., :3].astype(np.float32) * np.array(tint)[::-1], 0, 255).astype(np.uint8)
        leaves.append(leaf)
    return leaves


def copy_from(crop, src):
    folder = OUT / crop
    folder.mkdir(parents=True, exist_ok=True)
    for f in folder.glob("leaf_*.png"):
        f.unlink()
    for f in sorted((OUT / src).glob("leaf_*.png")):
        shutil.copy(f, folder / f.name)
    print(crop, "<-", src)


def main():
    # Cut-outs of real leaves.
    img = load("eggplant_33")
    m = green_on_soil(img)
    save("eggplant", [cut_in_box(img, m, box(img, 0.0, 0.1, 0.56, 0.64), cv2.ROTATE_90_CLOCKWISE),
                      cut_in_box(img, m, box(img, 0.42, 0.62, 0.7, 1.0), cv2.ROTATE_180)])
    img = load("ginger_15")
    save("ginger", [from_plain(img, box(img, 0.1, y0, 0.99, y1), cv2.ROTATE_90_COUNTERCLOCKWISE)
                    for y0, y1 in ((0.22, 0.42), (0.42, 0.6), (0.6, 0.82))])
    img = load("lettuce_30")
    h, w = img.shape[:2]
    lettuce_surface = img[int(h * 0.62):int(h * 0.9), int(w * 0.24):int(w * 0.5)]   # inside the lower-left leaf
    save("lettuce", [ovate_leaf(lettuce_surface, aspect=0.86, widest=0.62, tip=0.6, base=1.4, ruffle=0.05, stalk=0.08),
                     ovate_leaf(lettuce_surface[:, ::-1], aspect=0.8, widest=0.6, tip=0.7, base=1.3, ruffle=0.06, stalk=0.08)])
    img = load("coffee_20")
    leaf = cut_in_box(img, green_on_soil(img) | bright_on_dark(img, 70), box(img, 0.02, 0.0, 0.98, 1.0))
    save("coffee", variants(leaf))
    img = load("carrot_8")
    save("carrot", variants(cut_in_box(img, bright_on_dark(img), box(img, 0.0, 0.0, 1.0, 1.0), close=5, open_=3)))
    leaves = []
    for name in ("maple_54", "maple_56", "maple_52"):
        img = load(name)
        m = bright_on_dark(img, 80)
        if name == "maple_54":
            leaves.append(cut_in_box(img, m, box(img, 0, 0, 1, 1), close=5, open_=3))
        else:   # two leaves side by side: take the left one
            leaves.append(cut_in_box(img, m, box(img, 0.0, 0.0, 0.5, 1.0), close=5, open_=3))
    save("maple", leaves)
    img = load("basil_4")
    h, w = img.shape[:2]
    basil_surface = img[int(h * 0.25):int(h * 0.75), int(w * 0.3):int(w * 0.5)]        # inside the central leaf
    basil_surface = np.clip(basil_surface.astype(np.float32) * 1.35, 0, 255).astype(np.uint8)   # the photo is dark; lift it
    # The only basil photo is too dark and shadowed to cut cleanly; basil's ovate, glossy leaf matches the bell-pepper leaf photos,
    # which are used instead (and tinted to basil's brighter green in the species profile).
    copy_from("basil", "pepper")
    if (SRC / "tobacco_12.jpg").exists():
        img = load("tobacco_12")
        h, w = img.shape[:2]
        tob_surface = img[int(h * 0.32):int(h * 0.55), int(w * 0.4):int(w * 0.64)]
        save("tobacco", [ovate_leaf(tob_surface, aspect=0.5, widest=0.4, tip=1.6, base=0.7, stalk=0.02),
                         ovate_leaf(tob_surface[:, ::-1], aspect=0.46, widest=0.38, tip=1.7, base=0.7, stalk=0.02)])

    # Built leaves: real surface, drawn outline.
    surf = load("banana_33")
    h, w = surf.shape[:2]
    banana_surface = surf[:, int(w * 0.14):int(w * 0.86)]
    save("banana", [banana_leaf(banana_surface) for _ in range(3)])
    if (SRC / "brassica_13.jpg").exists():
        b = cv2.rotate(load("brassica_13"), cv2.ROTATE_90_CLOCKWISE)   # the photo's midrib runs across; turn it upright
        h, w = b.shape[:2]
        bs = b[int(h * 0.05):int(h * 0.95), int(w * 0.18):int(w * 0.82)]
        for kind in ("broccoli", "cabbage", "cauliflower"):
            save(kind, [brassica_leaf(bs, kind), brassica_leaf(bs[:, ::-1], kind)])
    sq = cv2.imread(str(OUT / "squash" / "leaf_1.png"), cv2.IMREAD_UNCHANGED)
    h, w = sq.shape[:2]
    cuc_surface = sq[int(h * 0.15):int(h * 0.6), int(w * 0.2):int(w * 0.8), :3]
    save("cucumber", [palmate_leaf(cuc_surface), palmate_leaf(cuc_surface[:, ::-1], lobes=5, depth=0.26)])
    cof = cv2.imread(str(OUT / "coffee" / "leaf_0.png"), cv2.IMREAD_UNCHANGED)
    h, w = cof.shape[:2]
    cel_surface = cof[int(h * 0.25):int(h * 0.75), int(w * 0.2):int(w * 0.8), :3]
    cel_surface = np.clip(cel_surface.astype(np.float32) * np.array([0.85, 1.0, 0.9]), 0, 255).astype(np.uint8)
    save("celery", [celery_leaflet(cel_surface), celery_leaflet(cel_surface[:, ::-1]), celery_leaflet(cel_surface[::-1])])

    # Mango, sugarcane, chilli (11 Nov 2026).
    img = load("mango_11")
    sat = cv2.cvtColor(img, cv2.COLOR_BGR2HSV)[..., 1] > 55               # the backdrop shades grey to white: cut by colour
    save("mango", variants(cut_in_box(img, sat, box(img, 0.0, 0.0, 1.0, 1.0), cv2.ROTATE_180)))   # the photo hangs it stalk-up
    save("sugarcane", strap_from_corn(0.6, (0.96, 1.0, 0.9)))         # blades 2-6 cm wide on 0.7-1.5 m (corn: 3-12 cm)
    chilli = []
    for f in sorted((OUT / "pepper").glob("leaf_*.png")):
        leaf = cv2.imread(str(f), cv2.IMREAD_UNCHANGED)
        h, w = leaf.shape[:2]
        chilli.append(cv2.resize(leaf, (int(w * 0.72), h), interpolation=cv2.INTER_AREA))   # hot-pepper leaves are narrower, ovate-lanceolate
    save("chilli", chilli)

    # Close relatives.
    copy_from("bean", "soybean")
    copy_from("plum", "cherry")
    save("garlic", strap_from_corn(0.7, (0.86, 0.96, 1.0)))
    save("rice", strap_from_corn(0.45, (1.0, 1.03, 0.86)))
    save("wheat", strap_from_corn(0.5, (0.9, 0.98, 0.95)))

    # Cotton (19 Nov 2026): the undamaged leaf on the right of CSIRO ScienceImage 405 (CC BY 3.0), on a pale dish, base up.
    cotton_leaf()


def cotton_leaf():
    img = load("cotton_1")
    hsv = cv2.cvtColor(img, cv2.COLOR_BGR2HSV)
    leaf = (hsv[..., 1] > 55) & (hsv[..., 0] > 25) & (hsv[..., 0] < 90) & (hsv[..., 2] > 40)
    save("cotton", variants(cut_in_box(img, leaf, box(img, 0.48, 0.04, 0.98, 0.96), cv2.ROTATE_180)))


if __name__ == "__main__":
    import sys
    cotton_leaf() if "--cotton" in sys.argv else main()
