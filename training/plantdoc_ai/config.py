from __future__ import annotations

from pathlib import Path

import yaml

TRAINING_DIR = Path(__file__).resolve().parent.parent


def load_config(path: str | Path | None = None) -> dict:
    """Reads config.yaml and turns every entry under `paths` into an absolute Path."""
    path = Path(path) if path else TRAINING_DIR / "config.yaml"
    with open(path, "r", encoding="utf-8") as fh:
        cfg = yaml.safe_load(fh)
    cfg["paths"] = {k: (TRAINING_DIR / v).resolve() for k, v in cfg["paths"].items()}
    cfg["_file"] = str(path)
    return cfg


def data_dirs(cfg: dict) -> dict[str, Path]:
    root = cfg["paths"]["data"]
    dirs = {"root": root, "raw": root / "raw", "cache": root / "cache", "reports": root / "reports"}
    for d in dirs.values():
        d.mkdir(parents=True, exist_ok=True)
    return dirs
