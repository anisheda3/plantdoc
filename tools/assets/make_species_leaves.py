"""Turns PlantVillage leaf photos (see fetch_plantvillage.py) into transparent leaf cutouts for the plant twins:
background removed, the leaf turned tip-up with its base (stalk) at the bottom, colours defringed so no grey halo
shows at the edge. Writes tools/assets/_cache/leaves/<crop>/leaf_<n>.png (RGBA; make_plant_textures.py packs them into atlases) and a contact sheet for review.

    py -3.11 tools/assets/make_species_leaves.py                    # every crop
    py -3.11 tools/assets/make_species_leaves.py - blueberry corn   # only these crops (- = no contact sheet)

The first four crops come from tools/assets/_cache/plantvillage/<crop>_NN.jpg (fetch_plantvillage.py), the rest from
tools/assets/_cache/pv/<PlantVillage class>/NN.jpg (fetch_pv_classes.py). PlantVillage has no healthy orange or squash class, so their
leaves are taken from the diseased classes and the scoring below picks the photos with the fewest visible symptoms.
Corn photos are close-ups of part of a leaf, not whole leaves, so corn gets a strap texture instead of a cut-out (corn_strap).
"""
import sys
from pathlib import Path

import cv2
import numpy as np

ROOT = Path(__file__).resolve().parents[2]
CACHE = Path(__file__).resolve().parent / "_cache" / "plantvillage"
OUT = Path(__file__).resolve().parent / "_cache" / "leaves"   # intermediates; the atlases in Assets/Art/Plants are what the app uses
SHEET = Path(sys.argv[1]) if len(sys.argv) > 1 and sys.argv[1] != "-" else None
ONLY = set(sys.argv[2:])
PV = Path(__file__).resolve().parent / "_cache" / "pv"

# Photos that segment badly (torn, crumpled or half-hidden leaves) and are skipped.
EXCLUDE = {"tomato_04.jpg", "apple_17.jpg"}
PER_CROP = {"tomato": 4, "potato": 4, "pepper": 3, "apple": 3,
            "blueberry": 3, "cherry": 3, "corn": 3, "grape": 3, "orange": 1, "peach": 3,
            "raspberry": 4, "soybean": 4, "squash": 3, "strawberry": 4}
# Where each new crop's photos are (folder names as written by fetch_pv_classes.py).
PV_FOLDER = {"blueberry": "Blueberry___healthy", "cherry": "Cherry___healthy", "corn": "Corn___healthy",
             "grape": "Grape___healthy", "orange": "Orange___Haunglongbing__Citrus_greening", "peach": "Peach___healthy",
             "raspberry": "Raspberry___healthy", "soybean": "Soybean___healthy", "squash": "Squash___Powdery_mildew",
             "strawberry": "Strawberry___healthy"}


def photos(crop):
    if crop in PV_FOLDER:
        return sorted((PV / PV_FOLDER[crop]).glob("*.jpg"))
    return sorted(CACHE.glob(f"{crop}_*.jpg"))
HEIGHT = 512


def leaf_mask(bgr):
    """Green-dominant, saturated pixels = leaf. Shadows and the grey/purple table are not."""
    hsv = cv2.cvtColor(bgr, cv2.COLOR_BGR2HSV).astype(np.float32)
    h, s, v = hsv[..., 0] * 2.0, hsv[..., 1] / 255.0, hsv[..., 2] / 255.0
    b, g, r = [bgr[..., i].astype(np.float32) for i in range(3)]
    green = (h > 45) & (h < 175) & (s > 0.17) & (v > 0.10)
    exg = (2 * g - r - b) > 14
    m = (green & exg).astype(np.uint8) * 255
    m = cv2.morphologyEx(m, cv2.MORPH_CLOSE, np.ones((5, 5), np.uint8))
    n, labels, stats, _ = cv2.connectedComponentsWithStats(m)
    if n <= 1:
        return None
    biggest = 1 + int(np.argmax(stats[1:, cv2.CC_STAT_AREA]))
    m = (labels == biggest).astype(np.uint8) * 255
    # Fill holes (insect bites stay as the leaf's own holes only if they touch the border; small ones are closed).
    inv = 255 - m
    n2, lab2, st2, _ = cv2.connectedComponentsWithStats(inv)
    holes = 0
    for i in range(1, n2):
        x, y, w, hgt, area = st2[i]
        touches = x == 0 or y == 0 or x + w >= m.shape[1] or y + hgt >= m.shape[0]
        if not touches:
            holes += area
            if area < 260:
                m[lab2 == i] = 255
    return m, holes


def leaf_mask_grabcut(bgr):
    """For dark, glossy, wavy leaves whose highlights and shadows break the plain colour mask (peach). The plain PlantVillage background
    is grey, so anything clearly more green than the background's own colour (CIELAB a*, b*) is leaf; a large closing and hole filling
    bring back the glossy highlights inside it, and GrabCut tidies the outline."""
    h, w = bgr.shape[:2]
    lab = cv2.cvtColor(bgr, cv2.COLOR_BGR2LAB).astype(np.float32)
    border = np.concatenate([lab[:6].reshape(-1, 3), lab[-6:].reshape(-1, 3), lab[:, :6].reshape(-1, 3), lab[:, -6:].reshape(-1, 3)])
    bg = np.median(border, axis=0)
    green = (bg[1] - lab[..., 1]) > 7                     # greener than the background
    m = (green * 255).astype(np.uint8)
    m = cv2.morphologyEx(m, cv2.MORPH_OPEN, np.ones((3, 3), np.uint8))
    m = cv2.morphologyEx(m, cv2.MORPH_CLOSE, cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (17, 17)))
    n, labels, stats, _ = cv2.connectedComponentsWithStats(m)
    if n <= 1:
        return None
    m = ((labels == 1 + int(np.argmax(stats[1:, cv2.CC_STAT_AREA]))) * 255).astype(np.uint8)
    inv = 255 - m
    n2, lab2, st2, _ = cv2.connectedComponentsWithStats(inv)
    for i in range(1, n2):
        x, y, ww, hh, area = st2[i]
        if not (x == 0 or y == 0 or x + ww >= w or y + hh >= h):
            m[lab2 == i] = 255
    gc = np.where(cv2.erode(m, np.ones((9, 9), np.uint8)) > 0, cv2.GC_FGD,
                  np.where(cv2.dilate(m, np.ones((9, 9), np.uint8)) > 0, cv2.GC_PR_FGD, cv2.GC_BGD)).astype(np.uint8)
    bgd, fgd = np.zeros((1, 65)), np.zeros((1, 65))
    try:
        cv2.grabCut(bgr, gc, None, bgd, fgd, 4, cv2.GC_INIT_WITH_MASK)
        m = (((gc == cv2.GC_FGD) | (gc == cv2.GC_PR_FGD)) * 255).astype(np.uint8)
    except cv2.error:
        pass
    return m, 0


def score(bgr, m, holes=0.0, min_area=0.22):
    """Higher is a better texture source: a whole, sharp, healthy leaf of good size."""
    area = (m > 0).mean()
    if area < min_area or area > 0.78:
        return -1.0
    border = np.concatenate([m[0], m[-1], m[:, 0], m[:, -1]])
    cut = (border > 0).mean()
    gray = cv2.cvtColor(bgr, cv2.COLOR_BGR2GRAY)
    sharp = cv2.Laplacian(gray, cv2.CV_64F)[m > 0].var()
    # Even colour: penalise brown blotches (disease or damage) inside the leaf.
    hsv = cv2.cvtColor(bgr, cv2.COLOR_BGR2HSV)
    hue = hsv[..., 0].astype(np.float32) * 2.0
    off = ((hue < 60) & (m > 0)).sum() / max(1, (m > 0).sum())
    # Yellow-green mottling (citrus greening) and pale powdery patches count against a photo too.
    leaf = m > 0
    mottle = ((hue >= 60) & (hue < 85) & leaf).sum() / max(1, leaf.sum())
    pale = ((hsv[..., 1] < 70) & (hsv[..., 2] > 150) & leaf).sum() / max(1, leaf.sum())
    return area * 2.0 + min(sharp, 400.0) / 400.0 - cut * 6.0 - off * 4.0 - mottle * 3.0 - pale * 5.0 - holes * 0.02


def apex_and_base(m):
    """The apex is the most pointed part of the outline at a large scale; the base is the outline point farthest from it
    (the stalk end when the photo includes one)."""
    cnts, _ = cv2.findContours((m > 127).astype(np.uint8), cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_NONE)
    c = max(cnts, key=cv2.contourArea).reshape(-1, 2).astype(np.float32)
    n = len(c)
    sigma = max(2.0, n / 90.0)
    half = int(sigma * 3)
    kernel = np.exp(-0.5 * (np.arange(-half, half + 1) / sigma) ** 2)
    kernel /= kernel.sum()
    def smooth(a):   # circular: the outline is a closed loop
        padded = np.concatenate([a[-half:], a, a[:half]])
        return np.convolve(padded, kernel, mode="valid")
    cs = np.column_stack([smooth(c[:, 0]), smooth(c[:, 1])])
    k = max(8, n // 14)
    idx = np.arange(n)
    v1 = cs[(idx - k) % n] - cs
    v2 = cs[(idx + k) % n] - cs
    cosang = (v1 * v2).sum(1) / (np.linalg.norm(v1, axis=1) * np.linalg.norm(v2, axis=1) + 1e-6)
    apex = cs[int(np.argmax(cosang))]            # largest cosine = smallest opening angle
    base = c[int(np.argmax(np.linalg.norm(c - apex, axis=1)))]
    return apex, base


def orient(bgr, m):
    """Rotate so the apex is up and the base (stalk) is down."""
    apex, base = apex_and_base(m)
    d = apex - base                                  # should point up on screen, i.e. (0, -1) in image coordinates
    ang = np.degrees(np.arctan2(d[0], -d[1]))        # clockwise angle of the leaf axis from "up"
    centre = ((apex + base) / 2).tolist()
    h, wd = m.shape
    size = int(np.hypot(h, wd))
    mat = cv2.getRotationMatrix2D(tuple(centre), ang, 1.0)
    mat[0, 2] += size / 2 - centre[0]
    mat[1, 2] += size / 2 - centre[1]
    img = cv2.warpAffine(bgr, mat, (size, size), flags=cv2.INTER_CUBIC, borderMode=cv2.BORDER_REPLICATE)
    msk = cv2.warpAffine(m, mat, (size, size), flags=cv2.INTER_LINEAR)

    # When a stalk is in the photo it is the base, whatever the outline says: it must end up at the bottom.
    rows = np.nonzero((msk > 127).any(axis=1))[0]
    widths = (msk[rows[0]:rows[-1] + 1] > 127).sum(axis=1).astype(np.float32)
    thin = widths < 0.16 * widths.max()
    def run(flags, w):
        n = 0
        for f in flags:
            if not f: break
            n += 1
        if n < 4: return 0
        seg = w[:n]
        # A stalk has an even width; a tip tapers to nothing, so its width varies a lot.
        return n if seg.std() / max(seg.mean(), 1e-3) < 0.32 else 0
    top_stalk, bottom_stalk = run(thin, widths), run(thin[::-1], widths[::-1])
    if top_stalk > bottom_stalk + 0.02 * len(widths):
        img = cv2.rotate(img, cv2.ROTATE_180)
        msk = cv2.rotate(msk, cv2.ROTATE_180)
    return img, msk


def cutout(bgr, m):
    img, msk = orient(bgr, m)
    ys, xs = np.nonzero(msk > 127)
    pad = 3
    y0, y1, x0, x1 = ys.min() - pad, ys.max() + pad + 1, xs.min() - pad, xs.max() + pad + 1
    img, msk = img[max(0, y0):y1, max(0, x0):x1], msk[max(0, y0):y1, max(0, x0):x1]
    scale = HEIGHT / img.shape[0]
    img = cv2.resize(img, None, fx=scale, fy=scale, interpolation=cv2.INTER_CUBIC)
    msk = cv2.resize(msk, (img.shape[1], img.shape[0]), interpolation=cv2.INTER_CUBIC)

    # A crisp but anti-aliased edge, pulled in a little so none of the table or shadow is left on it.
    msk = cv2.GaussianBlur(msk, (0, 0), 1.1)
    core = (msk > 140).astype(np.uint8)
    core = cv2.erode(core, cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (5, 5)))
    alpha = cv2.GaussianBlur(core.astype(np.float32), (0, 0), 1.3)
    alpha = np.clip((alpha - 0.18) / 0.64, 0, 1)

    # Defringe: outside the solid leaf, carry the colour of the leaf's own edge outward.
    solid = cv2.erode(core, cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (7, 7))).astype(np.float32)
    rgb = img.astype(np.float32)
    num = cv2.GaussianBlur(rgb * solid[..., None], (0, 0), 6.0)
    den = cv2.GaussianBlur(solid, (0, 0), 6.0)[..., None]
    filled = num / np.maximum(den, 1e-4)
    keep = solid[..., None]
    rgb = rgb * keep + filled * (1 - keep)

    # A touch of local contrast restores the veins lost to upscaling.
    blur = cv2.GaussianBlur(rgb, (0, 0), 2.0)
    rgb = np.clip(rgb + (rgb - blur) * 0.25, 0, 255)
    return np.dstack([rgb, alpha * 255]).astype(np.uint8)   # BGRA, the order cv2 writes


def corn_strap(bgr):
    """A whole corn leaf cannot be cut out of a close-up, so the leaf surface is turned until its parallel veins run straight up, a strip
    is cut along the midrib, and a strap outline (long taper to the tip, slightly narrower at the base) becomes its alpha."""
    gray = cv2.cvtColor(bgr, cv2.COLOR_BGR2GRAY).astype(np.float32)
    gx, gy = cv2.Sobel(gray, cv2.CV_32F, 1, 0, ksize=5), cv2.Sobel(gray, cv2.CV_32F, 0, 1, ksize=5)
    jxx, jyy, jxy = (gx * gx).mean(), (gy * gy).mean(), (gx * gy).mean()
    grad_angle = 0.5 * np.degrees(np.arctan2(2 * jxy, jxx - jyy))   # dominant gradient direction = across the veins
    h, w = gray.shape
    mat = cv2.getRotationMatrix2D((w / 2, h / 2), grad_angle, 1.0)     # veins vertical
    rot = cv2.warpAffine(bgr, mat, (w, h), flags=cv2.INTER_CUBIC, borderMode=cv2.BORDER_REFLECT)
    inner = rot[int(h * 0.18):int(h * 0.82), int(w * 0.18):int(w * 0.82)]   # away from the rotated corners
    ih, iw = inner.shape[:2]
    # The midrib is the brightest straight column; centre the strip on it when it is clear, else take the middle.
    col = cv2.GaussianBlur(cv2.cvtColor(inner, cv2.COLOR_BGR2GRAY).astype(np.float32), (0, 0), 3).mean(axis=0)
    centre = int(np.argmax(col)) if col.max() - np.median(col) > 8 else iw // 2
    half = max(8, min(centre, iw - centre, iw // 3))
    strip = inner[:, centre - half:centre + half]
    height, width = HEIGHT, int(HEIGHT * 0.11)
    strip = cv2.resize(strip, (width, height), interpolation=cv2.INTER_AREA)
    v = np.linspace(1, 0, height)[:, None]              # 1 at the top row (tip), 0 at the base
    u = np.abs(np.linspace(-1, 1, width))[None, :]
    taper = np.clip((1 - v) / 0.28, 0, 1) ** 0.6        # the last 28% narrows to a point
    base = 0.82 + 0.18 * np.clip(v / 0.15, 0, 1)        # the sheath end is a little narrower
    edge = taper * base
    alpha = np.clip((edge - u) * width / 2.0, 0, 1)
    return np.dstack([strip, (alpha * 255).astype(np.uint8)])


def squash_leaf(bgr, m, seed):
    """PlantVillage's squash photos are close-ups of parts of big leaves (all with powdery mildew), so no whole leaf can be cut out.
    The cleanest square patch inside the leaf is taken, its white mildew specks are painted out from the surrounding green, it is
    mirrored to fill the texture, the palmate veins are drawn in, and it gets the outline of a squash leaf from its description:
    palmately five-lobed, triangular-ovate, with a heart-shaped notch at the stalk (Flora of China, Cucurbita pepo)."""
    # Largest square fully inside the leaf.
    dist = cv2.distanceTransform((m > 0).astype(np.uint8), cv2.DIST_L2, 5)
    y, x = np.unravel_index(np.argmax(dist), dist.shape)
    half = int(max(24, min(dist[y, x] * 0.7, 140)))
    patch = bgr[max(0, y - half):y + half, max(0, x - half):x + half].copy()
    hsv = cv2.cvtColor(patch, cv2.COLOR_BGR2HSV)
    specks = ((((hsv[..., 1] < 85) & (hsv[..., 2] > 120)) | (hsv[..., 2] < 70) | (hsv[..., 0] < 22)) * 255).astype(np.uint8)   # mildew, dead and brown spots
    specks = cv2.dilate(specks, np.ones((3, 3), np.uint8))
    patch = cv2.inpaint(patch, specks, 3, cv2.INPAINT_TELEA)
    patch = cv2.bilateralFilter(patch, 7, 30, 5)
    tile = np.vstack([np.hstack([patch, patch[:, ::-1]]), np.hstack([patch[::-1], patch[::-1, ::-1]])])
    # Outline from the description of Cucurbita pepo (Prigge & Gibson, UCLA): blade roundish and a little wider than long
    # (110-340 x 120-390 mm), palmately five-lobed with broad lobes and shallow sinuses, the terminal lobe clearest and the
    # basal lobes weak, heart-shaped at the base, toothed on the margin; principal veins fan out from where the stalk joins.
    if seed % 2:
        tile = tile[:, ::-1]                                   # variants: mirrored surface, different teeth
    width = HEIGHT
    tex = cv2.resize(tile, (width, HEIGHT), interpolation=cv2.INTER_CUBIC).astype(np.float32)
    rng = np.random.default_rng(seed)
    yy, xx = np.mgrid[0:HEIGHT, 0:width].astype(np.float32)
    cx, cy = width * 0.5, HEIGHT * 0.47                       # middle of the blade
    dx, dy = (xx - cx) / (HEIGHT * 1.1), (cy - yy) / HEIGHT     # 1.1: wider than long
    r = np.hypot(dx, dy)
    th = np.arctan2(dx, dy)                                   # 0 = toward the tip, +-pi = toward the stalk
    lobes = ((1 + np.cos(6 * th)) / 2) ** 0.6                 # lobes at 0, +-60, +-120 degrees
    weak = 1 - 0.28 * (np.abs(th) / np.pi) ** 2               # the basal lobes are the weakest
    reach = 0.41 * (0.80 + 0.20 * lobes) * weak * (1 + 0.022 * np.sin(th * 41 + rng.uniform(0, 6)))
    sinus = (np.abs(th) > np.pi * 0.92) & (r > 0.10)          # the narrow heart-shaped notch where the stalk joins
    inside = (r < reach) & ~sinus
    joint = (cx, cy + HEIGHT * 0.10)
    veins = np.zeros((HEIGHT, width), np.float32)
    for ang, length in ((0, 0.50), (60, 0.44), (-60, 0.44), (118, 0.30), (-118, 0.30)):
        a_ = np.radians(ang)
        tip = (joint[0] + np.sin(a_) * HEIGHT * 1.1 * length, joint[1] - np.cos(a_) * HEIGHT * length)
        cv2.line(veins, (int(joint[0]), int(joint[1])), (int(tip[0]), int(tip[1])), 1.0, 3)
    veins = cv2.GaussianBlur(veins, (0, 0), 1.5) * inside
    tex = tex * (1 - veins[..., None] * 0.22) + np.array([150, 205, 170], np.float32) * veins[..., None] * 0.22
    alpha = cv2.GaussianBlur(inside.astype(np.float32), (0, 0), 1.2)
    rgba = np.dstack([np.clip(tex, 0, 255).astype(np.uint8), (np.clip((alpha - 0.2) / 0.6, 0, 1) * 255).astype(np.uint8)])
    # Crop to the leaf, so the card's base (where the stalk meets it) is the bottom of the blade at the notch.
    ys, xs = np.nonzero(rgba[..., 3] > 8)
    rgba = rgba[max(0, ys.min() - 2):ys.max() + 3, max(0, xs.min() - 2):xs.max() + 3]
    return cv2.resize(rgba, (int(rgba.shape[1] * HEIGHT / rgba.shape[0]), HEIGHT), interpolation=cv2.INTER_CUBIC)


def main():
    sheets = []
    for crop, count in PER_CROP.items():
        if ONLY and crop not in ONLY:
            continue
        files = photos(crop)
        scored = []
        for f in files:
            if f.name in EXCLUDE:
                continue
            bgr = cv2.imread(str(f))
            res = leaf_mask_grabcut(bgr) if crop == "peach" else leaf_mask(bgr)
            if res is None:
                continue
            m, holes = res
            if crop == "squash":   # close-ups too: pick the photos with the least white mildew on them
                if f.name in ("09.jpg",):   # torn, with dark dead patches
                    continue
                hsv = cv2.cvtColor(bgr, cv2.COLOR_BGR2HSV)
                pale = ((hsv[..., 1] < 70) & (hsv[..., 2] > 140)).mean()
                specks = cv2.Laplacian(cv2.cvtColor(bgr, cv2.COLOR_BGR2GRAY), cv2.CV_64F).var()
                scored.append((1.0 - pale * 6.0 - specks / 4000.0, f, bgr, m))
                continue
            if crop == "corn":   # close-ups: score by how evenly green and sharp the surface is, no outline needed
                hsv = cv2.cvtColor(bgr, cv2.COLOR_BGR2HSV)
                green = ((hsv[..., 0] > 30) & (hsv[..., 0] < 90) & (hsv[..., 1] > 40)).mean()
                spots = (hsv[..., 0] < 28).mean()
                scored.append((green - spots * 4.0, f, bgr, m))
                continue
            scored.append((score(bgr, m, holes, 0.08 if crop == "peach" else 0.22), f, bgr, m))
        scored.sort(key=lambda t: -t[0])
        picks = [t for t in scored if t[0] > 0 or crop in ("squash", "corn")][:count]   # close-ups are ranked, not thresholded
        if crop == "squash" and picks:
            picks = [picks[0]] * count   # one clean surface; the variants differ in mirroring and margin teeth
        folder = OUT / crop
        folder.mkdir(parents=True, exist_ok=True)
        for old in folder.glob("leaf_*.png"):
            old.unlink()
        tiles = []
        for k, (sc, f, bgr, m) in enumerate(picks):
            rgba = corn_strap(bgr) if crop == "corn" else squash_leaf(bgr, m, k) if crop == "squash" else cutout(bgr, m)
            cv2.imwrite(str(folder / f"leaf_{k}.png"), rgba)
            print(f"{crop} leaf_{k}: from {f.name} score {sc:.2f} size {rgba.shape[1]}x{rgba.shape[0]}")
            tile = np.full((HEIGHT, int(HEIGHT * 0.9), 3), (60, 74, 60), np.uint8)
            h, w = rgba.shape[:2]
            s = min(tile.shape[0] / h, tile.shape[1] / w)
            rs = cv2.resize(rgba, None, fx=s, fy=s, interpolation=cv2.INTER_AREA)
            ox, oy = (tile.shape[1] - rs.shape[1]) // 2, (tile.shape[0] - rs.shape[0]) // 2
            a = rs[..., 3:4].astype(np.float32) / 255
            tile[oy:oy + rs.shape[0], ox:ox + rs.shape[1]] = (rs[..., :3] * a + tile[oy:oy + rs.shape[0], ox:ox + rs.shape[1]] * (1 - a)).astype(np.uint8)
            tiles.append(tile)
        sheets.append(np.hstack(tiles) if tiles else None)
    if SHEET:
        rows = [s for s in sheets if s is not None]
        width = max(r.shape[1] for r in rows)
        rows = [np.pad(r, ((0, 0), (0, width - r.shape[1]), (0, 0))) for r in rows]
        cv2.imwrite(str(SHEET), np.vstack(rows))


if __name__ == "__main__":
    main()
