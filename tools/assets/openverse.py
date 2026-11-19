"""
Find and download openly licensed photos through Openverse (api.openverse.org).

    python openverse.py cand <sheet.jpg> --q "pruning shears" --q "watering can garden" [--per 6]
    python openverse.py get <sheet.json> <n> <out.jpg> [--width 1400] [--role "treatment: prune"]

`cand` builds one numbered contact sheet (and <sheet>.json) from several queries; `get` downloads
candidate <n> from that list and appends its credit to credits.json (shared with commons.py).
Only CC0, public domain, CC BY and CC BY-SA are accepted: no NonCommercial or NoDerivatives.
"""
from __future__ import annotations

import argparse
import io
import json
import sys
import time
import urllib.error
import urllib.parse
import urllib.request
from pathlib import Path

API = "https://api.openverse.org/v1/images/"
UA = {"User-Agent": "PlantDoctorAssets/1.0 (educational app)"}
ACCEPT = {"cc0", "pdm", "by", "by-sa"}
CREDITS = Path(__file__).with_name("credits.json")
_last = [0.0]


def _get(url: str, timeout: int = 60) -> bytes:
    for attempt in range(5):
        wait = 1.2 - (time.time() - _last[0])
        if wait > 0:
            time.sleep(wait)
        _last[0] = time.time()
        try:
            with urllib.request.urlopen(urllib.request.Request(url, headers=UA), timeout=timeout) as r:
                return r.read()
        except urllib.error.HTTPError as e:
            if e.code not in (429, 503) or attempt == 4:
                raise
            time.sleep(float(e.headers.get("Retry-After", 10 * (attempt + 1))))
    raise RuntimeError("unreachable")


def search(q: str, pages: int = 1) -> list[dict]:
    out = []
    for page in range(1, pages + 1):
        params = urllib.parse.urlencode({"q": q, "license_type": "commercial", "page_size": 20, "page": page,
                                         "mature": "false", "category": "photograph"})
        d = json.loads(_get(f"{API}?{params}").decode("utf-8"))
        out += [r for r in d.get("results", []) if r["license"] in ACCEPT]
    return out


def cand(out: Path, queries: list[str], per: int, min_w: int) -> None:
    from PIL import Image, ImageDraw
    seen, picked = set(), []
    for q in queries:
        n = 0
        try:
            results = search(q)
        except Exception as exc:
            print("query failed:", q, exc)
            continue
        for r in results:
            if r["id"] in seen or (r.get("width") or 0) < min_w:
                continue
            seen.add(r["id"]); picked.append((q, r)); n += 1
            if n >= per:
                break
    cols, cw, ch = 5, 300, 250
    rows = (len(picked) + cols - 1) // cols
    canvas = Image.new("RGB", (cols * cw, rows * ch), (28, 28, 28))
    dr = ImageDraw.Draw(canvas)
    listing = []
    for r in picked:
        q, it = r
        k = len(listing)
        try:
            thumb = it.get("thumbnail") or it["url"]
            im = Image.open(io.BytesIO(_get(thumb, 90))).convert("RGB")
        except Exception:
            continue
        im.thumbnail((cw - 8, ch - 34))
        x, y = (k % cols) * cw + 4, (k // cols) * ch + 4
        canvas.paste(im, (x, y))
        dr.rectangle([x, y, x + 34, y + 16], fill=(0, 0, 0))
        dr.text((x + 3, y + 2), str(k), fill=(255, 255, 0))
        dr.text((x, y + ch - 30), (it["title"] or "")[:44], fill=(255, 255, 255))
        dr.text((x, y + ch - 17), f"{it['width']}x{it['height']} {it['license']} {it['source']}", fill=(170, 220, 170))
        listing.append({"n": k, "id": it["id"], "title": it["title"], "url": it["url"], "creator": it.get("creator"),
                        "license": it["license"], "license_version": it.get("license_version"),
                        "license_url": it.get("license_url"), "page": it.get("foreign_landing_url"),
                        "source": it["source"], "width": it["width"], "height": it["height"], "query": q})
    canvas.save(out, quality=85)
    out.with_suffix(".json").write_text(json.dumps(listing, indent=1), encoding="utf-8")
    print(f"saved {out} with {len(listing)} candidates")


def get(sheet_json: Path, n: int, out: Path, width: int, role: str) -> None:
    from PIL import Image
    items = json.loads(sheet_json.read_text(encoding="utf-8"))
    it = next(i for i in items if i["n"] == n)
    data = _get(it["url"], 180)
    im = Image.open(io.BytesIO(data)).convert("RGB")
    if im.width > width:
        im = im.resize((width, round(im.height * width / im.width)), Image.LANCZOS)
    out.parent.mkdir(parents=True, exist_ok=True)
    im.save(out, quality=90)
    credits = json.loads(CREDITS.read_text(encoding="utf-8")) if CREDITS.exists() else {}
    lic = f"{it['license'].upper()} {it.get('license_version') or ''}".strip()
    credits[str(out).replace("\\", "/")] = {"title": it["title"], "author": it.get("creator") or "", "licence": lic,
                                            "page": it.get("page") or it["url"], "role": role}
    CREDITS.write_text(json.dumps(credits, indent=2, ensure_ascii=False), encoding="utf-8")
    print(f"saved {out} {im.size} - {lic} - {(it.get('creator') or '')[:40]}")


def main() -> None:
    ap = argparse.ArgumentParser()
    sub = ap.add_subparsers(dest="cmd", required=True)
    c = sub.add_parser("cand"); c.add_argument("out"); c.add_argument("--q", action="append", required=True)
    c.add_argument("--per", type=int, default=5); c.add_argument("--min-width", type=int, default=900)
    g = sub.add_parser("get"); g.add_argument("sheet"); g.add_argument("n", type=int); g.add_argument("out")
    g.add_argument("--width", type=int, default=1400); g.add_argument("--role", default="")
    a = ap.parse_args()
    if a.cmd == "cand": cand(Path(a.out), a.q, a.per, a.min_width)
    else: get(Path(a.sheet), a.n, Path(a.out), a.width, a.role)


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    main()
