"""
Step 5 - export for the inference server (memo Section 10).

    python -m plantdoc_ai.export_onnx --run <run> --install

Writes runs/<run>/export/{plant_disease.onnx, labels.json, model_meta.json}, checks that
ONNX Runtime gives the same answers as PyTorch, times it on the CPU (target <= 150 ms),
and with --install copies the three files to server/models/.
"""
from __future__ import annotations

import argparse
import datetime as dt
import json
import shutil
import sys
import time

import numpy as np
import torch

from . import taxonomy as tx
from .config import data_dirs, load_config
from .data import PlantDataset, load_manifest
from .model import OUTPUT_NAMES, ExportWrapper, load_checkpoint


def main(argv=None) -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--config")
    ap.add_argument("--run", required=True)
    ap.add_argument("--install", action="store_true", help="copy the files into server/models/")
    args = ap.parse_args(argv)

    cfg = load_config(args.config)
    run_dir = cfg["paths"]["runs"] / args.run
    out = run_dir / "export"
    out.mkdir(parents=True, exist_ok=True)
    net, ckpt = load_checkpoint(run_dir / "best.pt", "cpu")
    wrapper = ExportWrapper(net).eval()
    size = cfg["model"]["input_size"]

    calib_file = run_dir / "calibration.json"
    if calib_file.exists():
        calib = json.loads(calib_file.read_text())
    else:
        print("WARNING: no calibration.json - run evaluate first. Using T=1 and no energy gate.")
        calib = {"temperature": 1.0, "energy_threshold": None, "quality_threshold": cfg["export"]["quality_threshold"]}

    onnx_path = out / "plant_disease.onnx"
    dummy = torch.randn(1, 3, size, size)
    kwargs = dict(input_names=["image"], output_names=OUTPUT_NAMES, opset_version=cfg["export"]["opset"],
                  dynamic_axes={"image": {0: "batch"}, **{n: {0: "batch"} for n in OUTPUT_NAMES}})
    try:
        torch.onnx.export(wrapper, (dummy,), str(onnx_path), dynamo=False, **kwargs)
    except TypeError:     # older torch without the dynamo switch
        torch.onnx.export(wrapper, (dummy,), str(onnx_path), **kwargs)
    print(f"Exported {onnx_path} ({onnx_path.stat().st_size / 1e6:.1f} MB)")

    labels = {"conditions": tx.CONDITIONS, "species": tx.SPECIES, "signs": tx.SIGNS, "quality": tx.QUALITY}
    (out / "labels.json").write_text(json.dumps(labels, indent=2))
    meta = {
        "model_version": cfg["export"]["model_version"],
        "outputs": OUTPUT_NAMES,
        "input": {"name": "image", "size": size, "mean": [0.485, 0.456, 0.406], "std": [0.229, 0.224, 0.225]},
        "temperature": calib["temperature"],
        "energy_threshold": calib["energy_threshold"],
        "quality_threshold": calib["quality_threshold"],
        "signs": tx.SIGNS,
        "allowed_conditions": {s: tx.allowed_conditions(s) for s in tx.SPECIES},
        "trained": dt.date.today().isoformat(),
        "backbone": cfg["model"]["backbone"],
        "best_epoch": ckpt.get("epoch"),
        "data_card": "training/data/DATA_CARD.md",
    }
    (out / "model_meta.json").write_text(json.dumps(meta, indent=2))

    # --- parity with PyTorch on real validation images, then CPU speed (R6)
    import onnxruntime as ort
    sess = ort.InferenceSession(str(onnx_path), providers=["CPUExecutionProvider"])
    dirs = data_dirs(cfg)
    manifest = dirs["root"] / "manifest.csv"
    if manifest.exists():
        df = load_manifest(manifest, ["val"]).head(16)
        x = torch.stack([PlantDataset(df, size, train=False)[i][0] for i in range(len(df))])
    else:
        x = torch.randn(4, 3, size, size)
    with torch.no_grad():
        ref = [t.numpy() for t in wrapper(x)]
    got = sess.run(None, {"image": x.numpy()})
    worst = max(float(np.abs(a - b).max()) for a, b in zip(ref, got))
    same_top1 = float((ref[1].argmax(1) == got[1].argmax(1)).mean())
    print(f"ONNX vs PyTorch: max difference {worst:.2e}, same top-1 on {same_top1:.0%} of images")
    if worst > 1e-2 or same_top1 < 1.0:
        print("ERROR: the ONNX model does not match PyTorch - not installing.")
        return 1

    one = x[:1].numpy()
    for _ in range(5):
        sess.run(None, {"image": one})
    times = []
    for _ in range(30):
        t0 = time.perf_counter()
        sess.run(None, {"image": one})
        times.append((time.perf_counter() - t0) * 1000)
    ms = float(np.median(times))
    print(f"CPU time per image: {ms:.0f} ms (target <= 150 ms)")
    meta["cpu_ms_per_image"] = round(ms, 1)
    (out / "model_meta.json").write_text(json.dumps(meta, indent=2))

    if args.install:
        dest = cfg["paths"]["server_models"]
        dest.mkdir(parents=True, exist_ok=True)
        for name in ("plant_disease.onnx", "labels.json", "model_meta.json"):
            shutil.copy2(out / name, dest / name)
        print(f"Installed into {dest}. Restart the server: cd server && uvicorn main:app --host 0.0.0.0 --port 8000")
    return 0


if __name__ == "__main__":
    sys.exit(main())
