"""
Step 4 - evaluate against the memo's ship targets and calibrate (memo Section 9).

    python -m plantdoc_ai.evaluate --run <run>

Writes runs/<run>/eval/report.md, metrics.json, per_class.csv, confusion.csv and
runs/<run>/calibration.json (temperature, energy threshold, quality threshold) that
export_onnx puts into model_meta.json for the server.
"""
from __future__ import annotations

import argparse
import json
import sys

import albumentations as A
import cv2
import numpy as np
import pandas as pd
import torch
import torch.nn.functional as F
from sklearn.metrics import confusion_matrix, f1_score
from torch.utils.data import DataLoader
from tqdm import tqdm

from . import taxonomy as tx
from .config import data_dirs, load_config
from .data import PlantDataset, load_manifest
from .model import load_checkpoint

LOOKALIKES = [("Tomato___Early_blight", "Tomato___Septoria_leaf_spot"),
              ("Tomato___Early_blight", "Tomato___Target_spot"),
              ("Tomato___Septoria_leaf_spot", "Tomato___Target_spot"),
              ("Tomato___Yellow_leaf_curl_virus", "Any___Abiotic_stress")]
REJECT = tx.COND_INDEX["Reject___not_a_leaf"]


class CenterFraction(A.ImageOnlyTransform):
    def __init__(self, frac: float):
        super().__init__(p=1.0)
        self.frac = frac

    def apply(self, img, **_):
        h, w = img.shape[:2]
        ch, cw = int(h * self.frac), int(w * self.frac)
        y, x = (h - ch) // 2, (w - cw) // 2
        return img[y:y + ch, x:x + cw]


class Rot90(A.ImageOnlyTransform):
    def __init__(self):
        super().__init__(p=1.0)

    def apply(self, img, **_):
        return cv2.rotate(img, cv2.ROTATE_90_CLOCKWISE)


@torch.no_grad()
def collect(model, df, size, device, workers, **ds_kwargs) -> dict:
    ds = PlantDataset(df, size, train=False, **ds_kwargs)
    dl = DataLoader(ds, batch_size=128, shuffle=False, num_workers=workers, pin_memory=device.type == "cuda")
    keys = ["species", "condition", "signs", "severity", "quality"]
    outs = {k: [] for k in keys}
    tgts = {k: [] for k in keys}
    for x, t in tqdm(dl, leave=False, unit="batch"):
        with torch.autocast(device.type, dtype=torch.bfloat16, enabled=device.type == "cuda"):
            o = model(x.to(device).to(memory_format=torch.channels_last))
        for k in keys:
            outs[k].append(o[k].float().cpu())
            tgts[k].append(t[k])
    return {"out": {k: torch.cat(v) for k, v in outs.items()}, "t": {k: torch.cat(v) for k, v in tgts.items()}}


def fit_temperature(logits: torch.Tensor, labels: torch.Tensor) -> float:
    log_t = torch.zeros(1, requires_grad=True)
    opt = torch.optim.LBFGS([log_t], lr=0.1, max_iter=200)

    def closure():
        opt.zero_grad()
        loss = F.cross_entropy(logits / log_t.exp(), labels)
        loss.backward()
        return loss
    opt.step(closure)
    return float(log_t.exp())


def ece(probs: np.ndarray, labels: np.ndarray, bins: int = 15) -> float:
    conf, pred = probs.max(1), probs.argmax(1)
    edges = np.linspace(0, 1, bins + 1)
    total = 0.0
    for lo, hi in zip(edges[:-1], edges[1:]):
        m = (conf > lo) & (conf <= hi)
        if m.any():
            total += m.mean() * abs((pred[m] == labels[m]).mean() - conf[m].mean())
    return float(total)


def masked_condition_probs(species_logits, cond_logits, temperature) -> np.ndarray:
    """What the server does: keep only conditions of the predicted species, then softmax."""
    sp = species_logits.argmax(1).numpy()
    masked = torch.full_like(cond_logits, float("-inf"))
    for s_idx, s_name in enumerate(tx.SPECIES):
        rows = np.nonzero(sp == s_idx)[0]
        if len(rows):
            cols = tx.allowed_conditions(s_name)
            masked[np.ix_(rows, cols)] = cond_logits[np.ix_(rows, cols)]
    return torch.softmax(masked / temperature, 1).numpy()


def accuracy_block(r: dict, temperature: float) -> dict:
    y = r["t"]["condition"].numpy()
    m = y >= 0
    if not m.any():
        return {}
    logits = r["out"]["condition"][m]
    probs = masked_condition_probs(r["out"]["species"][m], logits, temperature)
    y = y[m]
    top3_raw = logits.topk(3, 1).indices.numpy()
    top3 = np.argsort(-probs, 1)[:, :3]
    pred = top3[:, 0]
    present = sorted(set(y))
    per_class = f1_score(y, pred, labels=present, average=None, zero_division=0)
    sp_t = r["t"]["species"].numpy()[m]
    sp_p = r["out"]["species"][m].argmax(1).numpy()
    per_species = {s: float((pred[sp_t == i] == y[sp_t == i]).mean()) for i, s in enumerate(tx.SPECIES) if (sp_t == i).any()}
    return {"n": int(m.sum()), "top1": float((pred == y).mean()), "top3": float((top3 == y[:, None]).any(1).mean()),
            "top3_unmasked": float((top3_raw == y[:, None]).any(1).mean()),
            "macro_f1": float(per_class.mean()), "per_class_f1": {tx.CONDITIONS[c]: float(f) for c, f in zip(present, per_class)},
            "species_acc": float((sp_p[sp_t >= 0] == sp_t[sp_t >= 0]).mean()) if (sp_t >= 0).any() else None,
            "top1_by_species": per_species, "_y": y, "_pred": pred}


def main(argv=None) -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--config")
    ap.add_argument("--run", required=True)
    ap.add_argument("--workers", type=int, default=6)
    ap.add_argument("--quality-sample", type=int, default=2000, help="test images used for the quality-gate check")
    args = ap.parse_args(argv)

    cfg = load_config(args.config)
    dirs = data_dirs(cfg)
    run_dir = cfg["paths"]["runs"] / args.run
    out_dir = run_dir / "eval"
    out_dir.mkdir(parents=True, exist_ok=True)
    device = torch.device("cuda" if torch.cuda.is_available() else "cpu")
    model, ckpt = load_checkpoint(run_dir / "best.pt", device)
    model = model.to(memory_format=torch.channels_last)
    size = cfg["model"]["input_size"]
    manifest = dirs["root"] / "manifest.csv"
    W = args.workers

    print("Running the model over validation, test and held-out images ...")
    val_df = load_manifest(manifest, ["val"])
    test_df = load_manifest(manifest, ["test"])
    hold_df = load_manifest(manifest, ["holdout"])
    val = collect(model, val_df[val_df["lab"] == 0].reset_index(drop=True), size, device, W)
    test_field_df = test_df[test_df["lab"] == 0].reset_index(drop=True)
    test = collect(model, test_field_df, size, device, W)
    hold = collect(model, hold_df, size, device, W) if len(hold_df) else None

    # --- calibration (R5) and the unknown-image threshold (Gate 3)
    yv = val["t"]["condition"]
    mv = (yv >= 0) & (yv != REJECT)
    T = fit_temperature(val["out"]["condition"][mv], yv[mv])
    ece_before = ece(torch.softmax(val["out"]["condition"][mv], 1).numpy(), yv[mv].numpy())
    ece_after = ece(torch.softmax(val["out"]["condition"][mv] / T, 1).numpy(), yv[mv].numpy())
    energy_val = -torch.logsumexp(val["out"]["condition"][mv], 1).numpy()
    energy_thr = float(np.quantile(energy_val, 0.95))           # keeps 95% of real leaves
    q_thr = float(cfg["export"]["quality_threshold"])

    # --- accuracy (test field split) and generalisation (held-out sources)
    acc = accuracy_block(test, T)
    acc_hold = accuracy_block(hold, T) if hold else {}

    # --- look-alikes
    y, pred = acc["_y"], acc["_pred"]
    look = {}
    for a, b in LOOKALIKES:
        ia, ib = tx.COND_INDEX[a], tx.COND_INDEX[b]
        na, nb = (y == ia).sum(), (y == ib).sum()
        if na + nb:
            look[f"{a} <-> {b}"] = float((((y == ia) & (pred == ib)).sum() + ((y == ib) & (pred == ia)).sum()) / (na + nb))

    # --- robustness (R2): rotated 90, cropped to 60%, +-30% brightness
    print("Robustness checks ...")
    robust = {}
    for name, tf in [("rotated 90", Rot90()), ("crop 60%", CenterFraction(0.6)),
                     ("brightness +30%", A.RandomBrightnessContrast((0.3, 0.3), (0, 0), p=1)),
                     ("brightness -30%", A.RandomBrightnessContrast((-0.3, -0.3), (0, 0), p=1))]:
        r = collect(model, test_field_df, size, device, W, extra_tf=tf)
        robust[name] = accuracy_block(r, T).get("top3", 0.0)
    worst_drop = max(acc["top3"] - v for v in robust.values())

    # --- quality gate (R3) on synthetic bad photos
    print("Quality-gate checks ...")
    qdf = test_field_df[test_field_df["condition"] != "Reject___not_a_leaf"]
    qdf = qdf.sample(min(args.quality_sample, len(qdf)), random_state=3).reset_index(drop=True)
    gate = {}
    for kind in tx.QUALITY:
        r = collect(model, qdf, size, device, W, force_degrade=kind)
        p_good = torch.softmax(r["out"]["quality"], 1)[:, tx.QUALITY_INDEX["good"]].numpy()
        gate[kind] = float((p_good < q_thr).mean())          # share rejected
    bad_rejected = float(np.mean([gate[k] for k in tx.QUALITY[1:]]))
    good_rejected = gate["good"]

    # --- not a leaf (R4)
    rej = [r for r in (test, hold) if r is not None]
    rej_rates = []
    for r in rej:
        m = r["t"]["condition"] == REJECT
        if m.any():
            lg = r["out"]["condition"][m]
            rejected = (lg.argmax(1) == REJECT) | (-torch.logsumexp(lg, 1) > energy_thr)
            rej_rates.append(rejected.float())
    not_leaf = float(torch.cat(rej_rates).mean()) if rej_rates else None

    # --- severity
    sev_t = test["t"]["severity"]
    sm = sev_t > 0
    sev_mae = float((torch.sigmoid(test["out"]["severity"][sm]) - sev_t[sm]).abs().mean()) if sm.any() else None

    # --- report
    worst_class = min(acc["per_class_f1"].items(), key=lambda kv: kv[1]) if acc["per_class_f1"] else ("", 0)
    checks = [
        ("Accuracy, field test", f"top-1 {acc['top1']:.1%} / top-3 {acc['top3']:.1%}", "≥ 75% / ≥ 90%",
         acc["top1"] >= 0.75 and acc["top3"] >= 0.90),
        ("Generalisation, held-out sources", f"top-3 {acc_hold.get('top3', float('nan')):.1%}", "≥ 80%",
         acc_hold.get("top3", 0) >= 0.80),
        ("Rare classes", f"macro-F1 {acc['macro_f1']:.2f}; worst {worst_class[0]} {worst_class[1]:.2f}",
         "≥ 0.65; none < 0.40", acc["macro_f1"] >= 0.65 and worst_class[1] >= 0.40),
        ("Look-alikes", "; ".join(f"{k.replace('Tomato___', '')} {v:.0%}" for k, v in look.items()) or "n/a",
         "each < 15%", all(v < 0.15 for v in look.values())),
        ("Robustness (R2)", f"worst top-3 drop {worst_drop * 100:.1f} pts", "≤ 3 pts", worst_drop <= 0.03),
        ("Quality gate (R3)", f"bad rejected {bad_rejected:.1%} / good rejected {good_rejected:.1%}", "≥ 95% / ≤ 5%",
         bad_rejected >= 0.95 and good_rejected <= 0.05),
        ("Not a leaf (R4)", f"{not_leaf:.1%} rejected" if not_leaf is not None else "no reject images", "≥ 90%",
         (not_leaf or 0) >= 0.90),
        ("Calibration (R5)", f"ECE {ece_before:.1%} -> {ece_after:.1%} (T = {T:.2f})", "≤ 5%", ece_after <= 0.05),
    ]
    lines = [f"# Evaluation - run {args.run}", "",
             f"Field test images: {acc['n']:,} · held-out images: {acc_hold.get('n', 0):,} · "
             f"species accuracy {acc['species_acc']:.1%}" + (f" · severity MAE {sev_mae:.3f}" if sev_mae is not None else ""), "",
             "| Check | Result | Target | Pass |", "| --- | --- | --- | --- |"]
    lines += [f"| {c} | {r} | {t} | {'yes' if ok else '**no**'} |" for c, r, t, ok in checks]
    lines += ["", "## Top-1 accuracy by species (field test)", ""]
    lines += [f"- {s}: {v:.1%}" for s, v in acc["top1_by_species"].items()]
    lines += ["", "## Weakest classes (F1)", ""]
    lines += [f"- {c}: {f:.2f}" for c, f in sorted(acc["per_class_f1"].items(), key=lambda kv: kv[1])[:10]]
    lines += ["", f"Speed (R6) is measured by export_onnx on the CPU.", ""]
    (out_dir / "report.md").write_text("\n".join(lines), encoding="utf-8")

    pd.DataFrame(sorted(acc["per_class_f1"].items(), key=lambda kv: kv[1]), columns=["label", "f1"]).to_csv(out_dir / "per_class.csv", index=False)
    labels = sorted(set(y) | set(pred))
    cm = confusion_matrix(y, pred, labels=labels)
    pd.DataFrame(cm, index=[tx.CONDITIONS[i] for i in labels], columns=[tx.CONDITIONS[i] for i in labels]).to_csv(out_dir / "confusion.csv")
    strip = lambda d: {k: v for k, v in d.items() if not k.startswith("_")}
    json.dump({"test": strip(acc), "holdout": strip(acc_hold), "lookalikes": look, "robustness_top3": robust,
               "quality_gate_reject_rate": gate, "not_a_leaf_rejected": not_leaf, "severity_mae": sev_mae,
               "ece_before": ece_before, "ece_after": ece_after},
              open(out_dir / "metrics.json", "w"), indent=2)
    json.dump({"temperature": T, "energy_threshold": energy_thr, "quality_threshold": q_thr},
              open(run_dir / "calibration.json", "w"), indent=2)

    print("\n" + "\n".join(lines[4:4 + len(checks) + 2]))
    print(f"\nReport: {out_dir / 'report.md'}\nNext: python -m plantdoc_ai.export_onnx --run {args.run} --install")
    return 0


if __name__ == "__main__":
    sys.exit(main())
