"""Builds the textures the plant twins are made of:

  * Assets/Art/Plants/<crop>/atlas.png + atlas.json  the crop's leaf cutouts (make_species_leaves.py) packed into one
    texture, so a whole compound leaf (or a whole tree canopy) is one mesh with one material;
  * Assets/Art/Plants/shared/{stem,bark,soil,root}.png  tileable, hand-built from noise (no photos).

    py -3.11 tools/assets/make_plant_textures.py
"""
import json
from pathlib import Path

import cv2
import numpy as np

ROOT = Path(__file__).resolve().parents[2]
PLANTS = ROOT / "Assets" / "Art" / "Plants"
LEAVES = Path(__file__).resolve().parent / "_cache" / "leaves"
rng = np.random.default_rng(7)

# ----------------------------------------------------------------------------------------------
# Leaf atlases
# ----------------------------------------------------------------------------------------------

ATLAS_HEIGHT = 512
GUTTER = 12            # transparent-but-coloured border between leaves so mip levels do not bleed
ATLAS_WIDTH = 2048


def pack_atlas(crop: str):
    folder = PLANTS / crop
    folder.mkdir(parents=True, exist_ok=True)
    files = sorted((LEAVES / crop).glob("leaf_*.png"))
    if not files:
        return
    leaves = [cv2.imread(str(f), cv2.IMREAD_UNCHANGED) for f in files]
    # Wide leaves (four broad leaflets) may not fit side by side at full height: shrink the row until they do.
    height = ATLAS_HEIGHT
    while sum(int(l.shape[1] * height / l.shape[0]) + 2 * GUTTER for l in leaves) + GUTTER > ATLAS_WIDTH:
        height -= 32
    atlas = np.zeros((height + 2 * GUTTER, ATLAS_WIDTH, 4), np.uint8)
    rects = []
    x = GUTTER
    for leaf in leaves:
        h, w = leaf.shape[:2]
        scale = height / h
        leaf = cv2.resize(leaf, (max(8, int(w * scale)), height), interpolation=cv2.INTER_AREA)
        w = leaf.shape[1]
        if x + w + GUTTER > ATLAS_WIDTH:
            raise SystemExit(f"{crop}: leaves do not fit in the atlas")
        # Colour in the gutters comes from the leaf's edge so filtering never pulls in black.
        pad = cv2.copyMakeBorder(leaf, 0, 0, GUTTER, GUTTER, cv2.BORDER_REPLICATE)
        pad[..., 3] = np.pad(leaf[..., 3], ((0, 0), (GUTTER, GUTTER)), mode="constant")
        atlas[GUTTER:GUTTER + height, x - GUTTER:x + w + GUTTER] = np.where(
            atlas[GUTTER:GUTTER + height, x - GUTTER:x + w + GUTTER, 3:4] > 0,
            atlas[GUTTER:GUTTER + height, x - GUTTER:x + w + GUTTER], pad)
        rects.append({"x": x / ATLAS_WIDTH, "y": GUTTER / atlas.shape[0], "w": w / ATLAS_WIDTH,
                      "h": height / atlas.shape[0], "aspect": w / height})
        x += w + 2 * GUTTER
    # Fill the rest with the average leaf colour (alpha 0) so nothing is ever black.
    mean = leaves[0][leaves[0][..., 3] > 128][:, :3].mean(axis=0)
    empty = atlas[..., 3] == 0
    for c in range(3):
        channel = atlas[..., c]
        channel[empty & (channel == 0)] = int(mean[c])
    cv2.imwrite(str(folder / "atlas.png"), atlas)
    (folder / "atlas.json").write_text(json.dumps({"width": ATLAS_WIDTH, "height": atlas.shape[0], "leaves": rects}, indent=1))
    print(crop, "atlas", atlas.shape[1], "x", atlas.shape[0], "with", len(rects), "leaves")


# ----------------------------------------------------------------------------------------------
# Tileable noise helpers
# ----------------------------------------------------------------------------------------------

def tile_blur(a, sx, sy):
    ky, kx = int(sy * 3) | 1, int(sx * 3) | 1
    pad_y, pad_x = ky, kx
    p = np.pad(a, ((pad_y, pad_y), (pad_x, pad_x)) + ((0, 0),) * (a.ndim - 2), mode="wrap")
    p = cv2.GaussianBlur(p, (kx * 2 + 1, ky * 2 + 1), sigmaX=max(sx, 0.01), sigmaY=max(sy, 0.01))
    return p[pad_y:-pad_y, pad_x:-pad_x]


def fbm(h, w, scales, sx_ratio=1.0, weights=None):
    out = np.zeros((h, w), np.float32)
    weights = weights or [1.0 / (i + 1) for i in range(len(scales))]
    for s, wt in zip(scales, weights):
        n = rng.standard_normal((h, w)).astype(np.float32)
        n = tile_blur(n, s * sx_ratio, s)
        n /= max(n.std(), 1e-6)
        out += n * wt
    out -= out.min()
    out /= max(out.max(), 1e-6)
    return out


def ramp(t, stops):
    """Colour ramp: stops = [(t, (r,g,b)), ...] with colours 0..255; t is an array."""
    ts = np.array([s[0] for s in stops], np.float32)
    cols = np.array([s[1] for s in stops], np.float32)
    out = np.zeros(t.shape + (3,), np.float32)
    for c in range(3):
        out[..., c] = np.interp(t, ts, cols[:, c])
    return out


def save_rgb(path, rgb):
    path.parent.mkdir(parents=True, exist_ok=True)
    cv2.imwrite(str(path), np.clip(rgb[..., ::-1], 0, 255).astype(np.uint8))


# ----------------------------------------------------------------------------------------------
# Stem, bark, soil, root
# ----------------------------------------------------------------------------------------------

def make_stem(path):
    h = w = 256
    fibres = fbm(h, w, [1.2, 3.0], sx_ratio=0.18)                # long vertical streaks
    fibres = tile_blur(fibres, 0.6, 14.0)
    body = fbm(h, w, [6.0, 14.0])
    t = 0.55 * fibres + 0.45 * body
    rgb = ramp(t, [(0.0, (88, 112, 52)), (0.5, (128, 158, 76)), (1.0, (168, 190, 108))])
    # Fine pale hairs, as on a real tomato stem.
    hair = np.zeros((h, w), np.float32)
    for _ in range(420):
        x, y = rng.integers(0, w), rng.integers(0, h)
        length = rng.integers(3, 9)
        ang = np.radians(rng.normal(0, 55))
        for k in range(length):
            xx, yy = int(x + np.sin(ang) * k) % w, int(y - np.cos(ang) * k) % h
            hair[yy, xx] = 1.0
    hair = cv2.GaussianBlur(hair, (3, 3), 0.7)
    rgb = rgb * (1 - hair[..., None] * 0.35) + np.array([215, 225, 190], np.float32) * hair[..., None] * 0.35
    save_rgb(path, rgb)


def make_bark(path):
    h = w = 256
    ridges = fbm(h, w, [1.6, 4.0], sx_ratio=0.25)
    ridges = tile_blur(ridges, 0.8, 20.0)
    cracks = np.abs(ridges - 0.5) * 2                              # 0 in the middle of a fissure line
    base = fbm(h, w, [4.0, 10.0])
    t = np.clip(0.35 + 0.5 * cracks ** 0.8 + 0.25 * base - 0.2, 0, 1)
    rgb = ramp(t, [(0.0, (66, 52, 44)), (0.45, (126, 106, 90)), (0.8, (166, 146, 126)), (1.0, (192, 172, 150))])
    # Lichen-ish pale flecks and a hint of green.
    fleck = (fbm(h, w, [2.0, 5.0]) > 0.82).astype(np.float32)
    fleck = cv2.GaussianBlur(fleck, (5, 5), 1.2)
    rgb = rgb * (1 - fleck[..., None] * 0.45) + np.array([150, 160, 130], np.float32) * fleck[..., None] * 0.45
    save_rgb(path, rgb)


def make_soil(path):
    h = w = 512
    grain = fbm(h, w, [0.8, 2.0, 6.0, 18.0], weights=[0.5, 0.7, 0.5, 0.35])
    rgb = ramp(grain, [(0.0, (30, 22, 17)), (0.4, (66, 48, 34)), (0.75, (98, 74, 52)), (1.0, (128, 102, 76))])
    # Small pebbles and light mineral specks.
    layer = rgb.copy()
    for _ in range(150):
        x, y = rng.integers(0, w), rng.integers(0, h)
        r = int(rng.integers(2, 6))
        tone = rng.uniform(0.55, 1.1)
        col = np.array([112, 100, 88], np.float32) * tone
        for dy in range(-r, r + 1):
            for dx in range(-r, r + 1):
                if dx * dx + dy * dy <= r * r:
                    shade = 1.0 - 0.35 * ((dx + dy) / (2.0 * r))
                    layer[(y + dy) % h, (x + dx) % w] = col * shade
    rgb = layer
    save_rgb(path, rgb)


def make_root(path):
    """Root colour along its length (v: 0 at the crown, 1 at the tip) in four tints side by side (u quarters, one per
    primary root): old roots are dark brown at the crown and turn tan, then cream at the fine tips; with a fibrous
    grain along the length and a dusting of soil."""
    h, w = 512, 256
    bands = [  # crown, middle, tip
        [(0.0, (112, 84, 58)), (0.45, (168, 134, 96)), (1.0, (224, 204, 162))],   # tan
        [(0.0, (88, 64, 44)), (0.45, (134, 100, 68)), (1.0, (206, 182, 138))],    # dark brown
        [(0.0, (126, 98, 70)), (0.45, (176, 144, 104)), (1.0, (230, 212, 172))], # light tan
        [(0.0, (108, 70, 48)), (0.45, (158, 108, 72)), (1.0, (218, 188, 142))],   # reddish brown
    ]
    v = np.linspace(0, 1, h, dtype=np.float32)[:, None] * np.ones((1, w // 4), np.float32)
    out = np.zeros((h, w, 3), np.float32)
    for b, stops in enumerate(bands):
        fibres = fbm(h, w // 4, [0.7, 2.0], sx_ratio=0.2)
        fibres = tile_blur(fibres, 0.5, 9.0)
        fibres = (fibres - fibres.min()) / max(fibres.max() - fibres.min(), 1e-6)
        mottle = fbm(h, w // 4, [5.0, 14.0])
        tone = np.clip(v + (mottle - 0.5) * 0.14, 0, 1)
        rgb = ramp(tone, stops)
        rgb *= (0.80 + 0.40 * fibres)[..., None]
        # Soil dust, mostly near the crown.
        dust = (fbm(h, w // 4, [1.5, 4.0]) > 0.66).astype(np.float32) * np.clip(1.0 - v * 1.3, 0, 1)
        dust = cv2.GaussianBlur(dust, (3, 3), 0.8)
        rgb = rgb * (1 - dust[..., None] * 0.55) + np.array([74, 56, 40], np.float32) * dust[..., None] * 0.55
        out[:, b * (w // 4):(b + 1) * (w // 4)] = rgb
    # Image row 0 is the top of the texture (v = 1): flip so the crown (v = 0) sits at the bottom row.
    save_rgb(path, out[::-1])


def main():
    # Every crop with leaf cut-outs: the PlantVillage crops (make_species_leaves.py) and the PlantSeg ones (make_new_leaves.py).
    for crop in sorted(p.name for p in LEAVES.iterdir() if p.is_dir() and any(p.glob("leaf_*.png"))):
        pack_atlas(crop)
    shared = PLANTS / "shared"
    make_stem(shared / "stem.png")
    make_bark(shared / "bark.png")
    make_soil(shared / "soil.png")
    make_root(shared / "root.png")
    print("textures written to", shared)


if __name__ == "__main__":
    main()
