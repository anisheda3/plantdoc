"""
End-to-end check of the whole pipeline on synthetic images, in a few minutes:
prepare -> train (1+1 epochs) -> evaluate -> export -> the server loads the model and answers.

    python smoke_test.py

Uses real source ids and folder names (so label mapping, de-duplication and splitting run
for real) but generated pictures, a temporary data folder and no downloads.
"""
from __future__ import annotations

import json
import random
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path

import numpy as np
import yaml
from PIL import Image, ImageDraw, ImageFilter

HERE = Path(__file__).resolve().parent

# source id -> {folder path inside raw/<id>: (leaf colour, spot colour or None)}
FAKE = {
    "tomato_bangladesh": {"Tomato Leaf/Early Blight": ((60, 140, 50), (70, 45, 20)),
                          "Tomato Leaf/Late Blight": ((60, 140, 50), (30, 30, 25)),
                          "Tomato Leaf/Healthy": ((50, 150, 55), None),
                          "Tomato Leaf/Spider Mites": ((120, 150, 60), (230, 230, 200))},
    "potato_uncontrolled": {"Phytopthora": ((40, 110, 45), (25, 25, 20)), "Healthy": ((45, 120, 50), None)},
    "apple_jk": {"Train_Set/Alternaria": ((70, 130, 40), (110, 60, 30)), "Train_Set/Healthy": ((75, 135, 45), None)},
    "abiotic_banana": {"Version-2-RAW Images of Banana leaves deficient in Nutrients/Boron": ((150, 160, 60), None)},
    "reject_coco": {"not a leaf": (None, None)},
}


def leaf(rng: random.Random, colour, spots, size=320) -> Image.Image:
    bg = tuple(rng.randint(60, 200) for _ in range(3))
    img = Image.new("RGB", (size, size), bg)
    d = ImageDraw.Draw(img)
    if colour is None:                                   # "not a leaf": random shapes
        for _ in range(12):
            x, y = rng.randint(0, size), rng.randint(0, size)
            d.rectangle([x, y, x + rng.randint(20, 120), y + rng.randint(20, 120)],
                        fill=tuple(rng.randint(0, 255) for _ in range(3)))
        return img
    jitter = lambda c: tuple(max(0, min(255, v + rng.randint(-15, 15))) for v in c)
    m = rng.randint(20, 50)
    d.ellipse([m, m + 30, size - m, size - m - 30], fill=jitter(colour))
    d.line([size // 2, m + 30, size // 2, size - m - 30], fill=(200, 220, 150), width=3)
    if spots:
        for _ in range(rng.randint(8, 20)):
            x, y = rng.randint(m + 30, size - m - 30), rng.randint(m + 50, size - m - 50)
            r = rng.randint(4, 12)
            d.ellipse([x - r, y - r, x + r, y + r], fill=jitter(spots))
    return img.filter(ImageFilter.GaussianBlur(0.6))


def run(*args) -> None:
    print("\n$ python -m", " ".join(args))
    subprocess.run([sys.executable, "-m", *args], cwd=HERE, check=True)


def main() -> int:
    tmp = Path(tempfile.mkdtemp(prefix="plantdoc_smoke_"))
    print("Working in", tmp)
    rng = random.Random(0)
    raw = tmp / "data" / "raw"
    for sid, folders in FAKE.items():
        for folder, (colour, spots) in folders.items():
            out = raw / sid / folder
            out.mkdir(parents=True, exist_ok=True)
            for i in range(60):
                leaf(rng, colour, spots).save(out / f"img_{i:03d}.jpg", quality=90)
    # an exact duplicate (must be removed) and a tiny image (must be dropped)
    dup = raw / "tomato_bangladesh" / "Tomato Leaf/Early Blight"
    shutil.copy(dup / "img_000.jpg", dup / "img_000_copy.jpg")
    Image.new("RGB", (60, 60), (50, 150, 50)).save(dup / "tiny.jpg")

    cfg = yaml.safe_load(open(HERE / "config.yaml", encoding="utf-8"))
    cfg["paths"] = {"data": str(tmp / "data"), "runs": str(tmp / "runs"), "server_models": str(tmp / "server_models")}
    cfg["data"]["holdout"] = ["apple_jk"]
    cfg["train"].update({"batch_size": 16, "workers": 2, "stage1_epochs": 1, "epochs": 1})
    cfg_path = tmp / "config.yaml"
    yaml.safe_dump(cfg, open(cfg_path, "w"))
    c = ["--config", str(cfg_path)]

    run("plantdoc_ai.prepare", *c, "--only", *FAKE.keys(), "--workers", "4")
    manifest = (tmp / "data" / "manifest.csv").read_text(encoding="utf-8")
    assert "img_000_copy" not in manifest or "img_000.jpg" not in manifest, "duplicate was not removed"
    assert "tiny.jpg" not in manifest, "tiny image was not dropped"
    run("plantdoc_ai.train", *c, "--run", "smoke", "--no-pretrained")
    run("plantdoc_ai.evaluate", *c, "--run", "smoke", "--workers", "2", "--quality-sample", "100")
    run("plantdoc_ai.export_onnx", *c, "--run", "smoke", "--install")

    # The real server code loads the exported files and answers.
    sys.path.insert(0, str(HERE.parent / "server"))
    import inference
    inference.MODEL_PATH = str(tmp / "server_models" / "plant_disease.onnx")
    inference.LABELS_PATH = str(tmp / "server_models" / "labels.json")
    inference.META_PATH = str(tmp / "server_models" / "model_meta.json")
    model = inference.DiseaseModel()
    assert model.is_loaded and model.multi_head, "server could not load the exported model"
    img = raw / "tomato_bangladesh" / "Tomato Leaf/Early Blight" / "img_005.jpg"
    try:
        p = model.predict(img.read_bytes())
        print(f"\nServer answer: {p.plant_id} / {p.disease_id} ({p.confidence:.2f}); top 3 = "
              + ", ".join(f"{c['label']} {c['confidence']:.2f}" for c in p.candidates))
    except (inference.ImageUnusable, inference.UnsupportedPlant) as exc:
        print(f"\nServer answer: rejected ({exc}) - fine for a 2-epoch model on fake data")
    print(f"\nSMOKE TEST PASSED. Files kept in {tmp}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
