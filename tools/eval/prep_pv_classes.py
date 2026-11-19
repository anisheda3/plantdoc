"""Turns PlantVillage photos fetched by tools/assets/fetch_pv_classes.py into 224 px centre squares (like a scan) for the tracer checks.
Healthy classes go to one folder, diseased classes to another; files are named img_<Class>_<nn>.png so score_detect.py can group them.

    py -3.11 -I tools/eval/prep_pv_classes.py <pv cache> <healthy out> <diseased out>
"""
import sys

sys.path.insert(0, r"C:\Users\Anish\.claude\jobs\0a9d9ea4\tmp\img")   # numpy / opencv (trusted)
from pathlib import Path

import cv2


def square(img):
    h, w = img.shape[:2]
    s = min(h, w)
    return cv2.resize(img[(h - s) // 2:(h - s) // 2 + s, (w - s) // 2:(w - s) // 2 + s], (224, 224), interpolation=cv2.INTER_AREA)


def main(src: Path, healthy_out: Path, diseased_out: Path):
    healthy_out.mkdir(parents=True, exist_ok=True)
    diseased_out.mkdir(parents=True, exist_ok=True)
    counts = {}
    for folder in sorted(p for p in src.iterdir() if p.is_dir()):
        healthy = folder.name.endswith("healthy")
        out = healthy_out if healthy else diseased_out
        n = 0
        for f in sorted(folder.glob("*.jpg")):
            img = cv2.imread(str(f))
            if img is None:
                continue
            cv2.imwrite(str(out / f"img_{folder.name}_{int(f.stem):02d}.png"), square(img))
            n += 1
        counts[folder.name] = n
    for k, v in counts.items():
        print(f"{k:50s} {v}")


if __name__ == "__main__":
    main(Path(sys.argv[1]), Path(sys.argv[2]), Path(sys.argv[3]))
