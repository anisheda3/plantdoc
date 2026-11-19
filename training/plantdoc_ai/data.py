"""Dataset, augmentation (memo Section 8), bad-photo synthesis for the quality head, sampler."""
from __future__ import annotations

import random

import albumentations as A
import cv2
import numpy as np
import pandas as pd
import torch
from albumentations.pytorch import ToTensorV2
from torch.utils.data import Dataset, WeightedRandomSampler

from . import taxonomy as tx

MEAN, STD = (0.485, 0.456, 0.406), (0.229, 0.224, 0.225)   # same as server/inference.py


def train_transform(size: int) -> A.Compose:
    return A.Compose([
        A.RandomResizedCrop(size=(size, size), scale=(0.35, 1.0), ratio=(0.75, 1.33)),   # zoom + partial crops
        A.HorizontalFlip(), A.VerticalFlip(), A.RandomRotate90(),
        A.Rotate(limit=30, border_mode=cv2.BORDER_CONSTANT, p=0.5),                       # tilted phones
        A.Perspective(scale=(0.02, 0.06), p=0.3),                                         # leaf seen at an angle
        A.RandomBrightnessContrast(0.3, 0.3, p=0.7),                                      # sun vs shade
        A.HueSaturationValue(hue_shift_limit=4, sat_shift_limit=25, val_shift_limit=20, p=0.5),
        A.RandomShadow(p=0.2),
        A.GaussNoise(std_range=(0.02, 0.08), p=0.2),
        A.ImageCompression(quality_range=(50, 95), p=0.3),                                # messaging-app JPEGs
        A.Normalize(mean=MEAN, std=STD), ToTensorV2(),
    ])


def eval_transform(size: int) -> A.Compose:
    # The server squashes the whole photo to size x size (no crop), so evaluation does too.
    return A.Compose([A.Resize(size, size), A.Normalize(mean=MEAN, std=STD), ToTensorV2()])


# ---------------------------------------------------------------------------
# Bad photos for the quality head (memo Section 6, Gate 2)
# ---------------------------------------------------------------------------

def degrade(img: np.ndarray, kind: str, rng: random.Random) -> np.ndarray:
    """Turns a good RGB uint8 photo into a typical bad one."""
    h, w = img.shape[:2]
    if kind == "blurry":
        if rng.random() < 0.5:
            k = rng.choice([9, 13, 17, 21])
            return cv2.GaussianBlur(img, (k, k), 0)
        length = rng.randint(15, 35)                       # camera shake
        kernel = np.zeros((length, length), np.float32)
        kernel[length // 2, :] = 1.0 / length
        rot = cv2.getRotationMatrix2D((length / 2, length / 2), rng.uniform(0, 180), 1.0)
        kernel = cv2.warpAffine(kernel, rot, (length, length))
        return cv2.filter2D(img, -1, kernel / max(kernel.sum(), 1e-6))
    if kind == "too_dark":
        out = (img.astype(np.float32) / 255.0) ** rng.uniform(2.2, 3.5) * rng.uniform(0.25, 0.5)
        return np.clip(out * 255 + np.random.normal(0, 3, img.shape), 0, 255).astype(np.uint8)
    if kind == "too_bright":
        out = img.astype(np.float32) * rng.uniform(2.2, 3.2) + rng.uniform(40, 90)
        return np.clip(out, 0, 255).astype(np.uint8)
    if kind == "too_small":                                # leaf far away: small in a big blurry frame
        s = rng.uniform(0.18, 0.32)
        small = cv2.resize(img, (max(8, int(w * s)), max(8, int(h * s))), interpolation=cv2.INTER_AREA)
        canvas = cv2.GaussianBlur(cv2.resize(img, (w // 8 or 1, h // 8 or 1)), (5, 5), 0)
        canvas = cv2.resize(canvas, (w, h))
        canvas = (canvas * rng.uniform(0.5, 0.9)).astype(np.uint8)
        y, x = rng.randint(0, h - small.shape[0]), rng.randint(0, w - small.shape[1])
        canvas[y:y + small.shape[0], x:x + small.shape[1]] = small
        return canvas
    return img


# ---------------------------------------------------------------------------

def load_manifest(path, splits=None) -> pd.DataFrame:
    df = pd.read_csv(path, keep_default_na=False)
    if splits:
        df = df[df["split"].isin(splits)].reset_index(drop=True)
    return df


class PlantDataset(Dataset):
    """Returns (image tensor, targets). Every target uses tx.IGNORE (-1) when unknown."""

    def __init__(self, df: pd.DataFrame, size: int, train: bool, degrade_prob: float = 0.0,
                 force_degrade: str | None = None, extra_tf: A.BasicTransform | None = None):
        self.paths = df["path"].tolist()
        self.species = [tx.SPECIES_INDEX.get(s, tx.IGNORE) for s in df["species"]]
        self.condition = [tx.COND_INDEX.get(c, tx.IGNORE) for c in df["condition"]]
        self.signs = np.array([[float(v) for v in s.split(",")] for s in df["signs"]], dtype=np.float32)
        self.severity = df["severity"].astype(float).to_numpy(np.float32)
        self.reject = [c == "Reject___not_a_leaf" for c in df["condition"]]
        self.train = train
        self.degrade_prob = degrade_prob
        self.force_degrade = force_degrade
        self.tf = train_transform(size) if train else eval_transform(size)
        self.extra_tf = extra_tf
        self.rng = random.Random()

    def __len__(self):
        return len(self.paths)

    def __getitem__(self, i):
        img = cv2.cvtColor(cv2.imread(self.paths[i], cv2.IMREAD_COLOR), cv2.COLOR_BGR2RGB)
        if self.extra_tf is not None:
            img = self.extra_tf(image=img)["image"]
        t = {"species": self.species[i], "condition": self.condition[i], "signs": self.signs[i].copy(),
             "severity": self.severity[i], "quality": tx.QUALITY_INDEX["good"]}
        kind = self.force_degrade
        if kind is None and self.train and not self.reject[i] and self.rng.random() < self.degrade_prob:
            kind = self.rng.choice(tx.QUALITY[1:])
        if kind and kind != "good":
            img = degrade(img, kind, self.rng)
            # a bad photo only teaches the quality head: the diagnosis on it is not trusted
            t = {"species": tx.IGNORE, "condition": tx.IGNORE, "signs": np.full(len(tx.SIGNS), -1, np.float32),
                 "severity": -1.0, "quality": tx.QUALITY_INDEX[kind]}
        elif self.reject[i]:
            t["quality"] = tx.IGNORE
        x = self.tf(image=img)["image"]
        return x, {k: torch.as_tensor(v) for k, v in t.items()}


def balanced_sampler(df: pd.DataFrame, lab_share: float, seed: int) -> WeightedRandomSampler:
    """Each label seen about equally often per epoch; lab photos capped at `lab_share` of draws."""
    key = np.where(df["condition"] != "", df["condition"], "species:" + df["species"])
    counts = pd.Series(key).value_counts()
    w = 1.0 / counts[key].to_numpy(dtype=np.float64)
    lab = df["lab"].astype(int).to_numpy() == 1
    if lab.any() and (~lab).any():
        share = w[lab].sum() / w.sum()
        if share > lab_share:
            w[lab] *= (lab_share / (1 - lab_share)) * w[~lab].sum() / w[lab].sum()
    g = torch.Generator().manual_seed(seed)
    return WeightedRandomSampler(torch.as_tensor(w), num_samples=len(df), replacement=True, generator=g)
