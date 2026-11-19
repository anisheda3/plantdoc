"""Prepares real HEALTHY leaf photos (PlantVillage, CC0, fetched by tools/assets/fetch_plantvillage.py) as a false-alarm test for the tracer:
centre square, 224 x 224, like a scan. The tracer should find (almost) nothing on them.

    py -3.11 -I tools/eval/prep_healthy.py <plantvillage cache folder> <output folder>
"""
import sys

sys.path.insert(0, r"C:\Users\Anish\.claude\jobs\0a9d9ea4\tmp\img")   # numpy / opencv (trusted)
from pathlib import Path

import cv2


def main(src: Path, out: Path):
    out.mkdir(parents=True, exist_ok=True)
    n = 0
    for f in sorted(src.glob("*.jpg")):
        img = cv2.imread(str(f))
        if img is None:
            continue
        h, w = img.shape[:2]
        s = min(h, w)
        crop = img[(h - s) // 2:(h - s) // 2 + s, (w - s) // 2:(w - s) // 2 + s]
        cv2.imwrite(str(out / f"img_{f.stem}.png"), cv2.resize(crop, (224, 224), interpolation=cv2.INTER_AREA))
        n += 1
    print(n, "healthy photos")


if __name__ == "__main__":
    main(Path(sys.argv[1]), Path(sys.argv[2]))
