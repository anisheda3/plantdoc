"""
Downloads Lucide icons (ISC licence, https://lucide.dev) and renders them to white, transparent
PNGs with the installed Microsoft Edge (headless), ready to be tinted in Unity.

    python make_icons.py            # writes Assets/Art/Icons/<name>.png (192 px) and the SVGs next to them

The icon names are the Lucide names. To add one, append it to ICONS and run again.
"""
from __future__ import annotations

import subprocess
import sys
import tempfile
import urllib.request
from pathlib import Path

VERSION = "1.57.0"
CDN = f"https://cdn.jsdelivr.net/npm/lucide-static@{VERSION}/icons/{{}}.svg"
OUT = Path(__file__).resolve().parents[2] / "Assets" / "Art" / "Icons"
EDGE = r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe"
CELL = 192
COLS = 8

ICONS = [
    # navigation and controls
    "chevron-left", "chevron-right", "arrow-right", "x", "check", "circle-check", "circle", "plus",
    "share-2", "download", "file-down", "file-text", "refresh-cw", "settings", "info", "triangle-alert",
    "zap", "zap-off", "image", "camera", "play", "pause", "skip-back", "skip-forward",
    # home and tabs
    "house", "scan-line", "book-open", "user", "clipboard-list", "history", "sprout", "leaf",
    # quick cards and AR
    "spray-can", "box", "rotate-3d", "move", "maximize-2", "zoom-in", "layers", "eye",
    # diagnosis and treatment
    "lightbulb", "scissors", "wind", "droplets", "droplet", "shield-check", "microscope", "bug",
    "search", "sun", "thermometer", "cloud-rain", "sparkles", "flask-conical", "calendar", "trees",
    "flower-2", "list-checks", "clock", "bandage", "scan", "skip-back", "skip-forward",
]


def fetch(name: str) -> str:
    url = CDN.format(name)
    with urllib.request.urlopen(urllib.request.Request(url, headers={"User-Agent": "PlantDoctorAssets/1.0"}), timeout=30) as r:
        return r.read().decode("utf-8")


def main() -> None:
    OUT.mkdir(parents=True, exist_ok=True)
    svgs = {}
    for n in ICONS:
        try:
            svgs[n] = fetch(n)
        except Exception as exc:                      # a renamed icon must not stop the rest
            print("missing:", n, exc)
    names = list(svgs)
    rows = (len(names) + COLS - 1) // COLS

    cells = []
    for i, n in enumerate(names):
        svg = svgs[n].replace('stroke="currentColor"', 'stroke="#ffffff"')
        svg = svg.replace('width="24"', f'width="{CELL}"').replace('height="24"', f'height="{CELL}"')
        x, y = (i % COLS) * CELL, (i // COLS) * CELL
        cells.append(f'<div style="position:absolute;left:{x}px;top:{y}px;width:{CELL}px;height:{CELL}px">{svg}</div>')
    html = ('<!doctype html><html><body style="margin:0;background:transparent">'
            + "".join(cells) + "</body></html>")

    with tempfile.TemporaryDirectory() as tmp:
        page = Path(tmp) / "icons.html"
        page.write_text(html, encoding="utf-8")
        shot = Path(tmp) / "sheet.png"
        subprocess.run([EDGE, "--headless=new", "--disable-gpu", "--hide-scrollbars", "--force-device-scale-factor=1",
                        f"--window-size={COLS * CELL},{rows * CELL + 120}", "--default-background-color=00000000",
                        f"--screenshot={shot}", page.as_uri()], check=True, timeout=120,
                       stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
        from PIL import Image
        sheet = Image.open(shot).convert("RGBA")
        for i, n in enumerate(names):
            x, y = (i % COLS) * CELL, (i // COLS) * CELL
            sheet.crop((x, y, x + CELL, y + CELL)).save(OUT / f"{n}.png")
            (OUT / f"{n}.svg").write_text(svgs[n], encoding="utf-8")
    print(f"wrote {len(names)} icons to {OUT}")
    (OUT / "LICENSE-lucide.txt").write_text(
        "Lucide icons - ISC License\nCopyright (c) for portions of Lucide are held by Cole Bemis 2013-2022 as part of Feather (MIT). "
        "All other copyright (c) for Lucide are held by Lucide Contributors 2022.\n"
        "Permission to use, copy, modify, and/or distribute this software for any purpose with or without fee is hereby granted, "
        "provided that the above copyright notice and this permission notice appear in all copies.\nhttps://lucide.dev/license\n",
        encoding="utf-8")


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    main()
