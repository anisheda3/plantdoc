"""
Numbered contact sheet of the usable (openly licensed) images in one or more Wikimedia Commons categories.

    py commons_sheet.py _cache/cand/orange_cats.jpg "Citrus leaves" "Citrus sinensis" --n 60

Writes the sheet and a .json listing number -> file title, licence and size, for picking leaf photos.
"""
import argparse
import io
import json
from pathlib import Path

from PIL import Image, ImageDraw

import commons


def main() -> None:
    ap = argparse.ArgumentParser()
    ap.add_argument("out")
    ap.add_argument("cats", nargs="+")
    ap.add_argument("--n", type=int, default=60)
    ap.add_argument("--min-width", type=int, default=600)
    a = ap.parse_args()
    items, seen = [], set()
    for cat in a.cats:
        try:
            got = commons.category(cat, a.n)
        except Exception as exc:
            print("category failed:", cat, exc)
            continue
        for it in got:
            if it["ok"] and it["w"] >= a.min_width and it["title"] not in seen:
                seen.add(it["title"]); items.append(it)
    cols, cw, ch = 6, 250, 210
    rows = max(1, (len(items) + cols - 1) // cols)
    canvas = Image.new("RGB", (cols * cw, rows * ch), (28, 28, 28))
    dr = ImageDraw.Draw(canvas)
    listing = []
    for k, it in enumerate(items):
        try:
            im = Image.open(io.BytesIO(commons.fetch_bytes(it["thumb"] or it["url"]))).convert("RGB")
        except Exception:
            continue
        im.thumbnail((cw - 8, ch - 30))
        x, y = (k % cols) * cw + 4, (k // cols) * ch + 4
        canvas.paste(im, (x, y))
        dr.rectangle([x, y, x + 30, y + 14], fill=(0, 0, 0))
        dr.text((x + 3, y + 1), str(k), fill=(255, 255, 0))
        dr.text((x, y + ch - 26), f"{it['w']}x{it['h']} {it['licence'][:14]}", fill=(170, 220, 170))
        listing.append({"n": k, "title": it["title"], "licence": it["licence"], "w": it["w"], "h": it["h"]})
    canvas.save(a.out, quality=85)
    Path(a.out).with_suffix(".json").write_text(json.dumps(listing, indent=1, ensure_ascii=False), encoding="utf-8")
    print(f"saved {a.out}: {len(listing)} images")


if __name__ == "__main__":
    main()
