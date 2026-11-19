"""
Step 3 - train (memo Section 8).

    python -m plantdoc_ai.train                       # full run, ~1-2 h on an RTX 5050 laptop GPU (estimate)
    python -m plantdoc_ai.train --epochs 2 --limit 3000 --run quick   # 10-minute sanity run

Stage 1: backbone frozen, heads only (3 epochs, lr 1e-3).
Stage 2: everything unfrozen, AdamW (heads 3e-4 / backbone 1e-4), 1 warm-up epoch then cosine,
         EMA weights (0.999); the best epoch by top-3 accuracy on field (non-lab) validation
         images is kept; stops after 6 epochs without improvement.
Writes runs/<run>/best.pt, last.pt, metrics.csv and TensorBoard logs (tensorboard --logdir runs).
"""
from __future__ import annotations

import argparse
import copy
import csv
import datetime as dt
import math
import random
import sys
import time
from pathlib import Path

import numpy as np
import torch
import torch.nn.functional as F
from torch.utils.data import DataLoader
from tqdm import tqdm

from . import taxonomy as tx
from .config import data_dirs, load_config
from .data import PlantDataset, balanced_sampler, load_manifest
from .model import build_model


def seed_everything(seed: int) -> None:
    random.seed(seed)
    np.random.seed(seed)
    torch.manual_seed(seed)
    torch.cuda.manual_seed_all(seed)


def worker_init(worker_id: int) -> None:
    info = torch.utils.data.get_worker_info()
    info.dataset.rng.seed(torch.initial_seed() % 2**32 + worker_id)


def losses(out: dict, t: dict, cfg: dict) -> tuple[torch.Tensor, dict]:
    tc = cfg["train"]
    w = tc["loss_weights"]
    zero = out["condition"].sum() * 0.0
    parts = {}
    m = t["condition"] >= 0
    parts["condition"] = F.cross_entropy(out["condition"][m].float(), t["condition"][m],
                                         label_smoothing=tc["label_smoothing"]) if m.any() else zero
    m = t["species"] >= 0
    parts["species"] = F.cross_entropy(out["species"][m].float(), t["species"][m]) if m.any() else zero
    sm = t["signs"] >= 0
    if sm.any():
        bce = F.binary_cross_entropy_with_logits(out["signs"].float(), t["signs"].clamp(min=0), reduction="none")
        parts["signs"] = (bce * sm).sum() / sm.sum()
    else:
        parts["signs"] = zero
    m = t["severity"] >= 0
    parts["severity"] = F.l1_loss(torch.sigmoid(out["severity"][m].float()), t["severity"][m].float()) if m.any() else zero
    m = t["quality"] >= 0
    parts["quality"] = F.cross_entropy(out["quality"][m].float(), t["quality"][m]) if m.any() else zero
    total = sum(w[k] * v for k, v in parts.items())
    return total, {k: float(v.detach()) for k, v in parts.items()}


@torch.no_grad()
def validate(model, loader, device, amp_dtype) -> dict:
    """Condition top-1 / top-3 on field (non-lab) validation images, plus species accuracy."""
    model.eval()
    top1 = top3 = n = sp_ok = sp_n = 0
    for x, t in loader:
        x = x.to(device, non_blocking=True).to(memory_format=torch.channels_last)
        with torch.autocast(device.type, dtype=amp_dtype, enabled=device.type == "cuda"):
            out = model(x)
        cond = t["condition"].to(device)
        m = cond >= 0
        if m.any():
            k = out["condition"][m].float().topk(3, dim=1).indices
            top1 += (k[:, 0] == cond[m]).sum().item()
            top3 += (k == cond[m, None]).any(1).sum().item()
            n += int(m.sum())
        sp = t["species"].to(device)
        m = sp >= 0
        sp_ok += (out["species"][m].argmax(1) == sp[m]).sum().item()
        sp_n += int(m.sum())
    return {"top1": top1 / max(n, 1), "top3": top3 / max(n, 1), "species_acc": sp_ok / max(sp_n, 1), "n": n}


def make_scheduler(opt, steps_per_epoch: int, epochs: int, warmup_epochs: int):
    total, warm = max(1, steps_per_epoch * epochs), steps_per_epoch * warmup_epochs

    def f(step):
        if step < warm:
            return (step + 1) / max(1, warm)
        p = (step - warm) / max(1, total - warm)
        return 0.01 + 0.99 * 0.5 * (1 + math.cos(math.pi * min(1.0, p)))
    return torch.optim.lr_scheduler.LambdaLR(opt, f)


class EMA:
    """Exponential moving average of the weights (decay ramps up early so it isn't stuck at init)."""

    def __init__(self, model, decay: float):
        self.module = copy.deepcopy(model).eval()
        for p in self.module.parameters():
            p.requires_grad_(False)
        self.decay, self.updates = decay, 0

    @torch.no_grad()
    def update(self, model):
        self.updates += 1
        d = min(self.decay, (1 + self.updates) / (10 + self.updates))
        msd = model.state_dict()
        for k, v in self.module.state_dict().items():
            if v.dtype.is_floating_point:
                v.mul_(d).add_(msd[k].detach(), alpha=1 - d)
            else:
                v.copy_(msd[k])


def main(argv=None) -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--config")
    ap.add_argument("--run", default=dt.datetime.now().strftime("%Y%m%d-%H%M"))
    ap.add_argument("--epochs", type=int, help="stage-2 epochs (default from config)")
    ap.add_argument("--stage1-epochs", type=int)
    ap.add_argument("--batch-size", type=int)
    ap.add_argument("--workers", type=int)
    ap.add_argument("--limit", type=int, help="use only this many training images (quick tests)")
    ap.add_argument("--no-pretrained", action="store_true", help="offline tests only")
    args = ap.parse_args(argv)

    cfg = load_config(args.config)
    tc = cfg["train"]
    for k in ("epochs", "stage1_epochs", "batch_size", "workers"):
        if getattr(args, k) is not None:
            tc[k] = getattr(args, k)
    seed_everything(cfg["data"]["seed"])
    dirs = data_dirs(cfg)
    run_dir = cfg["paths"]["runs"] / args.run
    run_dir.mkdir(parents=True, exist_ok=True)

    device = torch.device("cuda" if torch.cuda.is_available() else "cpu")
    if device.type == "cpu":
        print("WARNING: no CUDA GPU visible - training on the CPU will take days.")
    amp_dtype = torch.bfloat16 if device.type == "cuda" and torch.cuda.is_bf16_supported() else torch.float16
    torch.backends.cudnn.benchmark = True
    torch.backends.cuda.matmul.allow_tf32 = True
    torch.backends.cudnn.allow_tf32 = True

    manifest = dirs["root"] / "manifest.csv"
    train_df = load_manifest(manifest, ["train"])
    val_df = load_manifest(manifest, ["val"])
    val_df = val_df[val_df["lab"] == 0].reset_index(drop=True)          # select on field photos only
    if args.limit:
        train_df = train_df.sample(min(args.limit, len(train_df)), random_state=1).reset_index(drop=True)
        val_df = val_df.sample(min(max(500, args.limit // 5), len(val_df)), random_state=1).reset_index(drop=True)
    size = cfg["model"]["input_size"]
    train_ds = PlantDataset(train_df, size, train=True, degrade_prob=tc["degrade_prob"])
    val_ds = PlantDataset(val_df, size, train=False)
    common = dict(num_workers=tc["workers"], pin_memory=device.type == "cuda",
                  persistent_workers=tc["workers"] > 0, worker_init_fn=worker_init)
    if tc["workers"] > 0:
        common["prefetch_factor"] = 4
    train_dl = DataLoader(train_ds, batch_size=tc["batch_size"], drop_last=True,
                          sampler=balanced_sampler(train_df, cfg["data"]["lab_batch_share"], cfg["data"]["seed"]), **common)
    val_dl = DataLoader(val_ds, batch_size=tc["batch_size"] * 2, shuffle=False, **common)
    print(f"train {len(train_ds):,} images | field val {len(val_ds):,} | device {device} "
          f"({torch.cuda.get_device_name(0) if device.type == 'cuda' else 'cpu'}) | {amp_dtype}")

    model = build_model(cfg, pretrained=not args.no_pretrained).to(device).to(memory_format=torch.channels_last)
    ema = EMA(model, tc["ema_decay"])
    writer = None
    try:
        from torch.utils.tensorboard import SummaryWriter
        writer = SummaryWriter(str(run_dir / "tb"))
    except Exception:
        pass
    metrics_file = open(run_dir / "metrics.csv", "w", newline="")
    log = csv.writer(metrics_file)
    log.writerow(["stage", "epoch", "loss", "val_top1", "val_top3", "val_species", "lr", "seconds", "img_per_s"])

    best, best_epoch, stale, global_epoch = -1.0, -1, 0, 0

    def save(name, extra=None):
        torch.save({"model": ema.module.state_dict(), "config": cfg, "conditions": tx.CONDITIONS,
                    "species": tx.SPECIES, "signs": tx.SIGNS, "quality": tx.QUALITY,
                    "epoch": global_epoch, "best_top3": best, **(extra or {})}, run_dir / name)

    stages = [("stage1", tc["stage1_epochs"]), ("stage2", tc["epochs"])]
    for stage, epochs in stages:
        if epochs <= 0:
            continue
        frozen = stage == "stage1"
        for p in model.backbone.parameters():
            p.requires_grad_(not frozen)
        if frozen:
            opt = torch.optim.AdamW([p for h in model.heads() for p in h.parameters()], lr=tc["stage1_lr"],
                                    weight_decay=tc["weight_decay"])
            sched = make_scheduler(opt, len(train_dl), epochs, 0)
        else:
            opt = torch.optim.AdamW([
                {"params": model.backbone.parameters(), "lr": tc["lr_backbone"]},
                {"params": [p for h in model.heads() for p in h.parameters()], "lr": tc["lr_heads"]},
            ], weight_decay=tc["weight_decay"])
            sched = make_scheduler(opt, len(train_dl), epochs, tc["warmup_epochs"])
        scaler = torch.amp.GradScaler(enabled=device.type == "cuda" and amp_dtype == torch.float16)

        for epoch in range(epochs):
            model.train()
            if frozen:
                model.backbone.eval()             # keep pretrained BatchNorm statistics while frozen
            t0, seen, running = time.time(), 0, 0.0
            bar = tqdm(train_dl, desc=f"{stage} {epoch + 1}/{epochs}", unit="batch", leave=False)
            for x, t in bar:
                x = x.to(device, non_blocking=True).to(memory_format=torch.channels_last)
                t = {k: v.to(device, non_blocking=True) for k, v in t.items()}
                with torch.autocast(device.type, dtype=amp_dtype, enabled=device.type == "cuda"):
                    out = model(x)
                loss, parts = losses(out, t, cfg)
                opt.zero_grad(set_to_none=True)
                scaler.scale(loss).backward()
                scaler.unscale_(opt)
                torch.nn.utils.clip_grad_norm_(model.parameters(), 5.0)
                scaler.step(opt)
                scaler.update()
                sched.step()
                ema.update(model)
                seen += x.size(0)
                running = 0.98 * running + 0.02 * loss.item() if running else loss.item()
                bar.set_postfix(loss=f"{running:.3f}", cond=f"{parts['condition']:.3f}")
            secs = time.time() - t0
            v = validate(ema.module, val_dl, device, amp_dtype)
            global_epoch += 1
            lr = opt.param_groups[-1]["lr"]
            print(f"{stage} epoch {epoch + 1}/{epochs}: loss {running:.3f} | field val top-1 {v['top1']:.3f} "
                  f"top-3 {v['top3']:.3f} species {v['species_acc']:.3f} | {secs / 60:.1f} min ({seen / secs:.0f} img/s)")
            log.writerow([stage, epoch + 1, f"{running:.4f}", f"{v['top1']:.4f}", f"{v['top3']:.4f}",
                          f"{v['species_acc']:.4f}", f"{lr:.2e}", f"{secs:.0f}", f"{seen / secs:.0f}"])
            metrics_file.flush()
            if writer:
                writer.add_scalar("train/loss", running, global_epoch)
                for k in ("top1", "top3", "species_acc"):
                    writer.add_scalar(f"val/{k}", v[k], global_epoch)
            score = v["top3"] + 1e-3 * v["top1"]
            if score > best:
                best, best_epoch, stale = score, global_epoch, 0
                save("best.pt", {"val": v})
            elif not frozen:
                stale += 1
            save("last.pt")
            if not frozen and stale >= tc["early_stop_patience"]:
                print(f"No improvement for {stale} epochs - stopping early.")
                break

    metrics_file.close()
    print(f"\nBest epoch {best_epoch} (field val top-3 {best:.3f}). Saved {run_dir / 'best.pt'}")
    print(f"Next: python -m plantdoc_ai.evaluate --run {args.run}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
