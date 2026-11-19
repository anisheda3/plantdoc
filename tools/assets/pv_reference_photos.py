"""Copies one PlantVillage photo per new disease into Assets/Art/Photos/diseases/<key>.jpg as the disease's reference photo, and
records the credit (CC0, PlantVillage) in credits.json. The photo picked is a sharp one with clearly visible symptoms: the share of
the leaf that is not healthy green, preferring a middle amount (a nearly dead leaf teaches less than a clearly spotted one).

    py -3.11 tools/assets/pv_reference_photos.py
"""
import json
import sys

sys.path.insert(0, r"C:\\Users\\Anish\\.claude\\jobs\\0a9d9ea4\\tmp\\img")   # numpy / opencv (trusted)
from pathlib import Path

import cv2
import numpy as np

HERE = Path(__file__).resolve().parent
PV = HERE / "_cache" / "pv"
OUT = HERE.parents[1] / "Assets" / "Art" / "Photos" / "diseases"
CREDITS = HERE / "credits.json"
PAGE = "https://huggingface.co/datasets/DScomp380/plant_village"

PHOTOS = {
    "apple_black_rot_1": "Apple___Black_rot",
    "cherry_powdery_mildew_1": "Cherry___Powdery_mildew",
    "corn_gray_leaf_spot_1": "Corn___Cercospora_leaf_spot_Gray_leaf_spot",
    "corn_common_rust_1": "Corn___Common_rust",
    "corn_northern_leaf_blight_1": "Corn___Northern_Leaf_Blight",
    "grape_black_rot_1": "Grape___Black_rot",
    "grape_esca_1": "Grape___Esca__Black_Measles",
    "grape_leaf_blight_1": "Grape___Leaf_blight__Isariopsis_Leaf_Spot",
    "orange_citrus_greening_1": "Orange___Haunglongbing__Citrus_greening",
    "peach_bacterial_spot_1": "Peach___Bacterial_spot",
    "squash_powdery_mildew_1": "Squash___Powdery_mildew",
    "strawberry_leaf_scorch_1": "Strawberry___Leaf_scorch",
    "tomato_bacterial_spot_1": "Tomato___Bacterial_spot",
    "tomato_leaf_mold_1": "Tomato___Leaf_Mold",
    "tomato_septoria_leaf_spot_1": "Tomato___Septoria_leaf_spot",
    "tomato_spider_mites_1": "Tomato___Spider_mites_Two_spotted_spider_mite",
    "tomato_target_spot_1": "Tomato___Target_Spot",
    "tomato_yellow_leaf_curl_1": "Tomato___Tomato_Yellow_Leaf_Curl_Virus",
    "tomato_mosaic_virus_1": "Tomato___Tomato_mosaic_virus",
}


def yellowing(bgr):
    """For diseases that show as yellow mottling or stippling (citrus greening, spider mites): the yellow share of the leaf."""
    hsv = cv2.cvtColor(bgr, cv2.COLOR_BGR2HSV)
    leaf = hsv[..., 1] > 50
    hue = hsv[..., 0].astype(np.float32) * 2
    yellow = leaf & (hue > 38) & (hue < 75)
    return yellow.sum() / max(1, leaf.sum()) + 0.2 * min(cv2.Laplacian(cv2.cvtColor(bgr, cv2.COLOR_BGR2GRAY), cv2.CV_64F).var(), 600) / 600


def score(bgr):
    hsv = cv2.cvtColor(bgr, cv2.COLOR_BGR2HSV)
    leaf = hsv[..., 1] > 50                                   # coloured pixels, not the grey background
    if leaf.mean() < 0.15:
        return -1.0
    hue = hsv[..., 0].astype(np.float32) * 2
    healthy = leaf & (hue > 70) & (hue < 160)
    symptoms = 1 - healthy.sum() / max(1, leaf.sum())
    sharp = min(cv2.Laplacian(cv2.cvtColor(bgr, cv2.COLOR_BGR2GRAY), cv2.CV_64F).var(), 600) / 600
    return sharp * 0.6 + (1 - abs(symptoms - 0.3) / 0.3) * 0.8 + leaf.mean() * 0.4


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    credits = json.loads(CREDITS.read_text(encoding="utf-8")) if CREDITS.exists() else {}
    for key, folder in PHOTOS.items():
        files = sorted((PV / folder).glob("*.jpg"))
        if not files:
            print("no photos for", key)
            continue
        rank = yellowing if ("greening" in key or "mites" in key) else score
        best = max(files, key=lambda f: rank(cv2.imread(str(f))))
        dest = OUT / f"{key}.jpg"
        cv2.imwrite(str(dest), cv2.imread(str(best)), [cv2.IMWRITE_JPEG_QUALITY, 92])
        credits[str(dest).replace("\\", "/")] = {
            "title": f"PlantVillage {folder.replace('___', ' - ').replace('_', ' ')} (photo {best.stem})",
            "author": "PlantVillage (Hughes & Salathe 2015)", "licence": "CC0 1.0", "page": PAGE,
            "role": f"disease reference photo: {key}"}
        print(key, "<-", best.name)
    CREDITS.write_text(json.dumps(credits, indent=2, ensure_ascii=False), encoding="utf-8")


if __name__ == "__main__":
    main()
