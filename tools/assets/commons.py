"""
Small helper for finding and downloading openly licensed photos from Wikimedia Commons.

    python commons.py cat "Alternaria solani" --n 40         # list files in a category
    python commons.py search "pruning shears tomato" --n 30  # full-text search of files
    python commons.py sheet <out.jpg> <title> <title> ...    # contact sheet to look at candidates
    python commons.py get <title> <out.jpg> --width 1200     # download one image + record credit

Every downloaded image is appended to credits.json (title, author, licence, source URL) so
docs/CREDITS.md can be generated from it. Only licences that allow reuse are accepted
(CC0, public domain, CC BY, CC BY-SA); anything else is refused.
"""
from __future__ import annotations

import argparse
import io
import json
import re
import sys
import time
import urllib.error
import urllib.parse
import urllib.request
from pathlib import Path

API = "https://commons.wikimedia.org/w/api.php"
UA = {"User-Agent": "PlantDoctorAssets/1.0 (educational app; contact via project owner)"}
OK_LICENCES = re.compile(r"^(CC0|Public domain|PD|CC[ -]BY(?!-N)|CC[ -]BY-SA|Attribution)", re.I)
CREDITS = Path(__file__).with_name("credits.json")


_last_call = [0.0]


def _open(req, timeout=60):
    """Polite access: at most one request every 1.5 s, and back off when told to slow down."""
    for attempt in range(6):
        wait = 1.5 - (time.time() - _last_call[0])
        if wait > 0:
            time.sleep(wait)
        _last_call[0] = time.time()
        try:
            with urllib.request.urlopen(req, timeout=timeout) as r:
                return r.read()
        except urllib.error.HTTPError as e:
            if e.code not in (429, 503) or attempt == 5:
                raise
            time.sleep(float(e.headers.get("Retry-After", 8 * (attempt + 1))))


def api(**params):
    q = urllib.parse.urlencode({"format": "json", **params})
    return json.loads(_open(urllib.request.Request(f"{API}?{q}", headers=UA)).decode("utf-8"))


def info_for(pages: dict) -> list[dict]:
    out = []
    for p in sorted(pages.values(), key=lambda p: p.get("index", 0)):
        ii = (p.get("imageinfo") or [None])[0]
        if not ii or not ii.get("mime", "").startswith("image/jpeg") and not ii.get("mime", "").startswith("image/png"):
            continue
        m = ii.get("extmetadata", {})
        strip = lambda k: re.sub(r"<[^>]+>", "", (m.get(k, {}) or {}).get("value", "")).strip()
        out.append({"title": p["title"], "w": ii["width"], "h": ii["height"], "url": ii["url"],
                    "thumb": ii.get("thumburl"), "licence": strip("LicenseShortName"),
                    "author": strip("Artist") or strip("Credit"), "page": ii.get("descriptionurl"),
                    "ok": bool(OK_LICENCES.search(strip("LicenseShortName")))})
    return out


IIPROPS = dict(prop="imageinfo", iiprop="url|size|mime|extmetadata", iiurlwidth=500)


def category(name: str, n: int) -> list[dict]:
    name = name if name.startswith("Category:") else f"Category:{name}"
    d = api(action="query", generator="categorymembers", gcmtitle=name, gcmtype="file", gcmlimit=n, **IIPROPS)
    return info_for(d.get("query", {}).get("pages", {}))


def search(query: str, n: int) -> list[dict]:
    d = api(action="query", generator="search", gsrnamespace=6, gsrsearch=query + " filetype:bitmap",
            gsrlimit=n, **IIPROPS)
    return info_for(d.get("query", {}).get("pages", {}))


def show(items: list[dict]) -> None:
    for i in items:
        flag = " " if i["ok"] else "X"
        print(f"{flag} {i['w']:5d}x{i['h']:<5d} {i['licence'][:14]:14} {i['title'][5:80]}")


def fetch_bytes(url: str) -> bytes:
    return _open(urllib.request.Request(url, headers=UA), timeout=120)


def lookup(title: str, width: int = 500) -> dict:
    d = api(action="query", titles=title if title.startswith("File:") else f"File:{title}",
            prop="imageinfo", iiprop="url|size|mime|extmetadata", iiurlwidth=width)
    items = info_for(d["query"]["pages"])
    if not items:
        raise SystemExit(f"Not found or not an image: {title}")
    return items[0]


def sheet(out: Path, titles: list[str]) -> None:
    from PIL import Image, ImageDraw
    thumbs = []
    for t in titles:
        it = lookup(t, 360)
        im = Image.open(io.BytesIO(fetch_bytes(it["thumb"] or it["url"]))).convert("RGB")
        thumbs.append((it, im))
    cols = 4
    cw, ch = 360, 300
    rows = (len(thumbs) + cols - 1) // cols
    canvas = Image.new("RGB", (cols * cw, rows * ch), (30, 30, 30))
    dr = ImageDraw.Draw(canvas)
    for k, (it, im) in enumerate(thumbs):
        im.thumbnail((cw - 8, ch - 40))
        x, y = (k % cols) * cw + 4, (k // cols) * ch + 4
        canvas.paste(im, (x, y))
        dr.text((x, y + ch - 34), f"{k + 1}. {it['title'][5:48]}", fill=(255, 255, 255))
        dr.text((x, y + ch - 20), f"{it['w']}x{it['h']} {it['licence'][:20]}", fill=(180, 220, 180))
    canvas.save(out, quality=88)
    print("saved", out)


def get(title: str, out: Path, width: int, role: str = "") -> None:
    it = lookup(title, width)
    if not it["ok"]:
        raise SystemExit(f"Licence not accepted: {it['licence']} ({title})")
    data = fetch_bytes(it["thumb"] if it["w"] > width and it["thumb"] else it["url"])
    out.parent.mkdir(parents=True, exist_ok=True)
    out.write_bytes(data)
    credits = json.loads(CREDITS.read_text(encoding="utf-8")) if CREDITS.exists() else {}
    credits[str(out).replace("\\", "/")] = {"title": it["title"], "author": it["author"], "licence": it["licence"],
                                            "page": it["page"], "role": role}
    CREDITS.write_text(json.dumps(credits, indent=2, ensure_ascii=False), encoding="utf-8")
    print(f"saved {out} ({len(data) // 1024} KB) - {it['licence']} - {it['author'][:50]}")


def candidates(out: Path, queries: list[str], per: int, min_w: int) -> None:
    """Runs several searches, keeps only usable photos, and writes one numbered contact sheet
    (out) plus out.json mapping each number to its file title."""
    from PIL import Image, ImageDraw
    seen, picked = set(), []
    for q in queries:
        try:
            items = search(q, 30)
        except Exception as exc:                      # keep going if one query fails
            print("query failed:", q, exc)
            continue
        n = 0
        for it in items:
            if not it["ok"] or it["w"] < min_w or it["title"] in seen:
                continue
            if re.search(r"\((18|19)\d\d\)|(19|18)\d\d|report|journal|catalog|book|annual|plate|bulletin", it["title"], re.I):
                continue                              # old scanned book pages
            seen.add(it["title"]); picked.append((q, it)); n += 1
            if n >= per:
                break
    cols, cw, ch = 5, 300, 250
    rows = (len(picked) + cols - 1) // cols
    canvas = Image.new("RGB", (cols * cw, rows * ch), (28, 28, 28))
    dr = ImageDraw.Draw(canvas)
    listing = []
    for k, (q, it) in enumerate(picked):
        try:
            im = Image.open(io.BytesIO(fetch_bytes(it["thumb"] or it["url"]))).convert("RGB")
        except Exception:
            continue
        im.thumbnail((cw - 8, ch - 34))
        x, y = (k % cols) * cw + 4, (k // cols) * ch + 4
        canvas.paste(im, (x, y))
        dr.rectangle([x, y, x + 34, y + 16], fill=(0, 0, 0))
        dr.text((x + 3, y + 2), str(k), fill=(255, 255, 0))
        dr.text((x, y + ch - 30), it["title"][5:46], fill=(255, 255, 255))
        dr.text((x, y + ch - 17), f"{it['w']}x{it['h']} {it['licence'][:16]}", fill=(170, 220, 170))
        listing.append({"n": k, "title": it["title"], "licence": it["licence"], "query": q})
    canvas.save(out, quality=85)
    out.with_suffix(".json").write_text(json.dumps(listing, indent=1), encoding="utf-8")
    print(f"saved {out} with {len(listing)} candidates")


def main() -> None:
    ap = argparse.ArgumentParser()
    sub = ap.add_subparsers(dest="cmd", required=True)
    c = sub.add_parser("cat"); c.add_argument("name"); c.add_argument("--n", type=int, default=40)
    s = sub.add_parser("search"); s.add_argument("query"); s.add_argument("--n", type=int, default=30)
    sh = sub.add_parser("sheet"); sh.add_argument("out"); sh.add_argument("titles", nargs="+")
    cd = sub.add_parser("cand"); cd.add_argument("out"); cd.add_argument("--q", action="append", required=True)
    cd.add_argument("--per", type=int, default=5); cd.add_argument("--min-width", type=int, default=1200)
    g = sub.add_parser("get"); g.add_argument("title"); g.add_argument("out")
    g.add_argument("--width", type=int, default=1200); g.add_argument("--role", default="")
    a = ap.parse_args()
    if a.cmd == "cat": show(category(a.name, a.n))
    elif a.cmd == "search": show(search(a.query, a.n))
    elif a.cmd == "sheet": sheet(Path(a.out), a.titles)
    elif a.cmd == "cand": candidates(Path(a.out), a.q, a.per, a.min_width)
    else: get(a.title, Path(a.out), a.width, a.role)


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    main()
