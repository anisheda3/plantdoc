"""
Step 2 - turn data/raw into one clean, split manifest (memo Section 5, steps 2-10).

    python -m plantdoc_ai.prepare

  1. scan every enabled source and map its folder names to our labels
     (an unmapped folder stops the run and is listed in data/reports/unmapped_labels.csv)
  2. drop images under 128 px, resize the rest once into data/cache (short side 384)
  3. perceptual-hash every image: distance <= 6 is the same photo (keep one; if two
     sources disagree on its label, drop both), distance <= 12 shares a group
  4. split by group 70/15/15, stratified by label; held-out sources go to "holdout"
  5. write data/manifest.csv and data/DATA_CARD.md

Re-running is incremental: images already in the cache are not decoded again.
Honours data/drop_list.txt (one cache path per line) and data/relabel.csv
(columns path,condition) written after reviewing find_mislabels output.
"""
from __future__ import annotations

import argparse
import collections
import concurrent.futures as cf
import hashlib
import os
import random
import re
import sys
from pathlib import Path

import numpy as np
import pandas as pd
from tqdm import tqdm

from . import taxonomy as tx
from .config import data_dirs, load_config
from .download import IMAGES, label_of
from .sources import OD, OH, Source, enabled_sources

MAX_GROUP = 200   # near-copy links never grow a group past this (plain lab backgrounds look alike)


# ---------------------------------------------------------------------------
# 1. scan + label
# ---------------------------------------------------------------------------

def resolve(src: Source, mapped: str) -> tuple[str | None, str | None, str | None]:
    """Mapped value -> (condition, species, abiotic cause). Species None = not trained."""
    if mapped == OH:
        return None, "other", None
    if mapped == OD:
        return "Other___unknown_disease", "other", None
    value, _, species = mapped.partition("|")
    if value.startswith("ABIOTIC:"):
        return "Any___Abiotic_stress", species or src.species, value.split(":", 1)[1]
    if value not in tx.COND_INDEX:
        raise SystemExit(f"[{src.id}] maps to unknown label '{value}' - fix sources.py")
    if value == "Reject___not_a_leaf":
        return value, None, None
    return value, species or tx.species_of(value) or src.species, None


def scan_source(src: Source, raw: Path) -> tuple[list[dict], set[str]]:
    root = raw / src.id
    if not root.exists():
        print(f"  [{src.id}] not downloaded - skipped (run download --only {src.id})")
        return [], set()
    rows, unmapped, caps = [], set(), collections.Counter()
    for dirpath, dirnames, filenames in os.walk(root):
        dirnames[:] = sorted(d for d in dirnames if not d.startswith(("_", ".")) and d != "__MACOSX")
        for name in sorted(filenames):
            if not name.lower().endswith(IMAGES) or name.startswith("._"):
                continue
            full = Path(dirpath) / name
            rel = full.relative_to(root).as_posix()
            if src.include and not re.search(src.include, rel, re.I):
                continue
            if src.exclude and re.search(src.exclude, rel, re.I):
                continue
            label = label_of(src, rel)
            mapped = src.map_label(label)
            if mapped is None:
                unmapped.add(label)
                continue
            if mapped == "skip":
                continue
            condition, species, cause = resolve(src, mapped)
            if condition == "Other___unknown_disease" or mapped == OH:
                key = label.split(" ")[0] if src.label_mode == "plantseg" else label
                if src.cap_other and caps[key] >= src.cap_other:
                    continue
                caps[key] += 1
            mask = None
            if src.label_mode == "plantseg":
                m = full.parent.parent.parent / "annotations" / full.parent.name / (full.stem + ".png")
                mask = str(m) if m.exists() else None
            rows.append({"source": src.id, "raw": str(full), "rel": rel, "orig_label": label,
                         "condition": condition or "", "species": species or "", "abiotic_cause": cause or "",
                         "lab": int(src.lab), "mask": mask or ""})
    return rows, unmapped


# ---------------------------------------------------------------------------
# 2. resize into the cache (runs in worker processes)
# ---------------------------------------------------------------------------

def process_image(raw: str, out: str, short_side: int, min_side: int, mask: str) -> dict:
    from PIL import Image, ImageOps
    import imagehash
    try:
        with Image.open(raw) as im:
            im.draft("RGB", (short_side * 2, short_side * 2))   # fast JPEG decode at reduced size
            im = ImageOps.exif_transpose(im).convert("RGB")
            w0, h0 = im.size
            if min(w0, h0) < min_side and im.format != "JPEG":
                return {"ok": False, "reason": "too_small"}
            scale = short_side / min(w0, h0)
            if scale < 1:
                im = im.resize((max(1, round(w0 * scale)), max(1, round(h0 * scale))), Image.BICUBIC)
            if min(im.size) < min_side:
                return {"ok": False, "reason": "too_small"}
            Path(out).parent.mkdir(parents=True, exist_ok=True)
            im.save(out, quality=92)
            severity = severity_from_mask(im, mask) if mask else -1.0
            return {"ok": True, "w": im.size[0], "h": im.size[1], "phash": str(imagehash.phash(im)),
                    "severity": severity}
    except Exception as exc:   # corrupt or unreadable files are reported, not fatal
        return {"ok": False, "reason": f"unreadable: {type(exc).__name__}"}


def severity_from_mask(im, mask_path: str) -> float:
    """Share of the leaf covered by lesions: PlantSeg lesion pixels / (plant-coloured pixels + lesions)."""
    import cv2
    from PIL import Image
    mask = np.array(Image.open(mask_path).convert("L").resize(im.size, Image.NEAREST)) > 0
    hsv = cv2.cvtColor(np.array(im), cv2.COLOR_RGB2HSV)
    h, s, v = hsv[..., 0], hsv[..., 1], hsv[..., 2]
    plant = ((h >= 8) & (h <= 95) & (s >= 40) & (v >= 40)) | mask
    area = plant.sum()
    return float(min(1.0, mask.sum() / area)) if area > 0 else -1.0


def cache_path(cache: Path, row: dict) -> Path:
    digest = hashlib.sha1(row["rel"].encode("utf-8")).hexdigest()[:20]
    return cache / row["source"] / f"{digest}.jpg"


# ---------------------------------------------------------------------------
# 3. duplicates and groups
# ---------------------------------------------------------------------------

class UnionFind:
    def __init__(self, n):
        self.p = list(range(n))
        self.size = [1] * n

    def find(self, a):
        while self.p[a] != a:
            self.p[a] = self.p[self.p[a]]
            a = self.p[a]
        return a

    def union(self, a, b, max_size=None):
        ra, rb = self.find(a), self.find(b)
        if ra == rb or (max_size and self.size[ra] + self.size[rb] > max_size):
            return False
        if self.size[ra] < self.size[rb]:
            ra, rb = rb, ra
        self.p[rb] = ra
        self.size[ra] += self.size[rb]
        return True


def close_pairs(hashes: np.ndarray, max_dist: int, chunk: int = 256):
    """All (i, j, distance) with i < j and Hamming distance <= max_dist (vectorised)."""
    n = len(hashes)
    out = []
    for start in tqdm(range(0, n, chunk), desc="  comparing hashes", unit="blk"):
        block = hashes[start:start + chunk]
        dist = np.bitwise_count(block[:, None] ^ hashes[None, :])
        ii, jj = np.nonzero(dist <= max_dist)
        ii = ii + start
        keep = jj > ii
        out.append(np.stack([ii[keep], jj[keep], dist[ii[keep] - start, jj[keep]]], axis=1))
    return np.concatenate(out) if out else np.zeros((0, 3), dtype=np.int64)


def label_key(row) -> str:
    return row["condition"] or f"species:{row['species']}"


# ---------------------------------------------------------------------------
# 4. split
# ---------------------------------------------------------------------------

def holdout_mask(df: pd.DataFrame, specs: list[str]) -> np.ndarray:
    mask = np.zeros(len(df), dtype=bool)
    for spec in specs:
        sid, _, pattern = spec.partition(":")
        m = (df["source"] == sid).to_numpy()
        if pattern:
            m &= df["rel"].str.contains(pattern, case=False, regex=True).to_numpy()
        mask |= m
    return mask


def assign_splits(df: pd.DataFrame, ratios, holdout: list[str], seed: int) -> pd.Series:
    split = pd.Series("", index=df.index)
    held_groups = set(df.loc[holdout_mask(df, holdout), "group"])
    split[df["group"].isin(held_groups)] = "holdout"
    rest = df[split == ""]
    group_label = rest.groupby("group")["key"].agg(lambda s: s.value_counts().index[0])
    group_size = rest.groupby("group").size()
    rng = random.Random(seed)
    for label, groups in group_label.groupby(group_label):
        gs = list(groups.index)
        rng.shuffle(gs)
        total = group_size[gs].sum()
        cut_train, cut_val = ratios[0] * total, (ratios[0] + ratios[1]) * total
        seen = 0
        for k, g in enumerate(gs):
            n = group_size[g]
            # small classes: make sure val and test each get at least one group
            if len(gs) >= 3 and k == len(gs) - 1:
                s = "test"
            elif len(gs) >= 3 and k == len(gs) - 2 and not (split[rest.index[rest["group"].isin(gs)]] == "val").any():
                s = "val"
            else:
                s = "train" if seen + n / 2 <= cut_train else "val" if seen + n / 2 <= cut_val else "test"
            split[rest.index[rest["group"] == g]] = s
            seen += n
    return split


# ---------------------------------------------------------------------------
# 5. data card
# ---------------------------------------------------------------------------

def write_data_card(df: pd.DataFrame, sources: list[Source], stats: dict, path: Path) -> None:
    lines = ["# Data card - Plant Doctor 4-species model", "",
             f"{len(df):,} images after cleaning ({stats['duplicates']:,} duplicates and "
             f"{stats['conflicts']:,} label conflicts removed, {stats['small']:,} too small, "
             f"{stats['unreadable']:,} unreadable).", "",
             "## Sources", "", "| Source | Images | Licence | Notes |", "| --- | --- | --- | --- |"]
    per_source = df["source"].value_counts()
    for s in sources:
        lines.append(f"| [{s.title}]({s.url}) | {per_source.get(s.id, 0):,} | {s.licence} | {s.note} |")
    lines += ["", "Credit every CC BY author above in the app's About screen.", "",
              "## Images per label and split", "", "| Label | train | val | test | holdout |",
              "| --- | --- | --- | --- | --- |"]
    table = df.pivot_table(index="key", columns="split", values="path", aggfunc="count", fill_value=0)
    for key in [c for c in tx.CONDITIONS] + ["species:other"]:
        r = table.loc[key] if key in table.index else {}
        lines.append(f"| {key} | " + " | ".join(str(int(r.get(s, 0))) for s in ("train", "val", "test", "holdout")) + " |")
    thin = [k for k in tx.CONDITIONS if (table.loc[k, "train"] if k in table.index and "train" in table else 0) < 50]
    lines += ["", "## Known gaps", ""]
    if thin:
        lines.append("Labels with fewer than 50 training images (expect weak results): " + ", ".join(thin) + ".")
    lines += ["Water stress has no usable public field dataset (the maize water-level set is 48 x 48 px patches); "
              "the app's questions (soil now? watering?) carry that diagnosis.",
              "Severity labels come only from PlantSeg masks (lesion share of plant-coloured pixels).", ""]
    path.write_text("\n".join(lines), encoding="utf-8")


# ---------------------------------------------------------------------------

def main(argv=None) -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--config")
    ap.add_argument("--only", nargs="+", help="limit to these sources (quick experiments)")
    ap.add_argument("--allow-unmapped", action="store_true", help="skip unmapped folders instead of stopping")
    ap.add_argument("--workers", type=int, default=max(2, (os.cpu_count() or 4) - 2))
    args = ap.parse_args(argv)

    cfg = load_config(args.config)
    dc = cfg["data"]
    dirs = data_dirs(cfg)
    sources = enabled_sources(args.only)

    # 1. scan
    print("Scanning sources ...")
    rows, unmapped = [], {}
    for src in sources:
        r, u = scan_source(src, dirs["raw"])
        rows += r
        if u:
            unmapped[src.id] = sorted(u)
        print(f"  {src.id:22} {len(r):7,} images")
    if unmapped:
        report = dirs["reports"] / "unmapped_labels.csv"
        pd.DataFrame([(s, l) for s, ls in unmapped.items() for l in ls], columns=["source", "folder_label"]).to_csv(report, index=False)
        msg = f"{sum(map(len, unmapped.values()))} folder labels have no mapping (see {report}); add them to sources.py."
        if not args.allow_unmapped:
            print("\nSTOP: " + msg)
            return 1
        print("WARNING: " + msg)
    if not rows:
        print("Nothing to prepare - download first: python -m plantdoc_ai.download")
        return 1
    df = pd.DataFrame(rows)

    # 2. cache (incremental)
    index_path = dirs["cache"] / "index.parquet"
    index = pd.read_parquet(index_path) if index_path.exists() else pd.DataFrame(columns=["raw", "raw_size"])
    known = {r.raw: r for r in index.itertuples(index=False)} if len(index) else {}
    df["path"] = [str(cache_path(dirs["cache"], r)) for r in rows]
    df["raw_size"] = [os.path.getsize(p) for p in df["raw"]]
    todo, results = [], {}
    for i, r in enumerate(df.itertuples(index=False)):
        k = known.get(r.raw)
        if k is not None and k.raw_size == r.raw_size and (not k.ok or Path(r.path).exists()):
            results[i] = k._asdict()
        else:
            todo.append(i)
    print(f"Resizing {len(todo):,} new images into the cache ({len(df) - len(todo):,} already done) ...")
    if todo:
        with cf.ProcessPoolExecutor(args.workers) as pool:
            futs = {pool.submit(process_image, df.at[i, "raw"], df.at[i, "path"], dc["cache_short_side"],
                                dc["min_side"], df.at[i, "mask"]): i for i in todo}
            for fut in tqdm(cf.as_completed(futs), total=len(futs), unit="img"):
                i = futs[fut]
                results[i] = {"raw": df.at[i, "raw"], "raw_size": int(df.at[i, "raw_size"]), **fut.result()}
    res = pd.DataFrame([results[i] for i in range(len(df))])
    res.reindex(columns=["raw", "raw_size", "ok", "reason", "w", "h", "phash", "severity"]).to_parquet(index_path, index=False)
    for col in ("ok", "reason", "w", "h", "phash", "severity"):
        df[col] = res[col].values if col in res else None
    stats = {"small": int((df["reason"] == "too_small").sum()),
             "unreadable": int(df["reason"].astype(str).str.startswith("unreadable").sum())}
    df = df[df["ok"] == True].reset_index(drop=True)   # noqa: E712

    # review results from find_mislabels
    drop_file = dirs["root"] / "drop_list.txt"
    if drop_file.exists():
        drops = {l.strip() for l in drop_file.read_text(encoding="utf-8").splitlines() if l.strip()}
        df = df[~df["path"].isin(drops)].reset_index(drop=True)
    relabel_file = dirs["root"] / "relabel.csv"
    if relabel_file.exists():
        rl = pd.read_csv(relabel_file).set_index("path")["condition"].to_dict()
        hit = df["path"].isin(rl)
        df.loc[hit, "condition"] = df.loc[hit, "path"].map(rl)
        df.loc[hit, "species"] = [tx.species_of(c) or s for c, s in zip(df.loc[hit, "condition"], df.loc[hit, "species"])]
    df["key"] = [label_key(r) for r in df.to_dict("records")]

    # 3. duplicates and groups
    print("Finding duplicates and near-copies ...")
    hashes = np.array([int(h, 16) for h in df["phash"]], dtype=np.uint64)
    pairs = close_pairs(hashes, dc["group_distance"])
    dup_uf, grp_uf = UnionFind(len(df)), UnionFind(len(df))
    for i, j, d in pairs:
        if d <= dc["duplicate_distance"]:
            dup_uf.union(int(i), int(j))
            grp_uf.union(int(i), int(j))
    for i, j, d in pairs:
        if d > dc["duplicate_distance"]:
            grp_uf.union(int(i), int(j), max_size=MAX_GROUP)
    clusters = collections.defaultdict(list)
    for i in range(len(df)):
        clusters[dup_uf.find(i)].append(i)
    drop, conflicts = set(), []
    keys, labs, areas = df["key"].tolist(), df["lab"].tolist(), (df["w"] * df["h"]).tolist()
    for members in clusters.values():
        if len(members) == 1:
            continue
        if len({keys[m] for m in members}) > 1:            # same photo, different labels: trust neither
            drop.update(members)
            conflicts.append([df.at[m, "source"] + ":" + df.at[m, "rel"] + "=" + keys[m] for m in members])
            continue
        best = max(members, key=lambda m: (not labs[m], areas[m]))
        drop.update(m for m in members if m != best)
    if conflicts:
        pd.DataFrame({"images": [" | ".join(c) for c in conflicts]}).to_csv(dirs["reports"] / "label_conflicts.csv", index=False)
    stats["duplicates"] = len(drop) - sum(len(c) for c in conflicts)
    stats["conflicts"] = sum(len(c) for c in conflicts)
    df["group"] = [grp_uf.find(i) for i in range(len(df))]
    df = df.drop(index=sorted(drop)).reset_index(drop=True)

    # 4. split
    df["split"] = assign_splits(df, dc["split"], dc.get("holdout", []), dc["seed"])
    df["signs"] = [",".join(map(str, tx.sign_targets(c, a))) if c else ",".join(["-1"] * len(tx.SIGNS))
                   for c, a in zip(df["condition"], df["abiotic_cause"])]
    df["severity"] = df["severity"].fillna(-1.0)
    # severity is only meaningful for diseased leaves; healthy ones are 0 by definition
    df.loc[df["condition"].str.endswith("___healthy"), "severity"] = 0.0

    cols = ["path", "source", "rel", "orig_label", "species", "condition", "abiotic_cause", "signs",
            "severity", "lab", "group", "split", "w", "h"]
    out = dirs["root"] / "manifest.csv"
    df[cols].to_csv(out, index=False)
    write_data_card(df, sources, stats, dirs["root"] / "DATA_CARD.md")

    print(f"\nManifest: {out}  ({len(df):,} images)")
    print(df.groupby("split").size().to_string())
    print(f"Removed {stats['duplicates']:,} duplicates, {stats['conflicts']:,} conflicting copies, "
          f"{stats['small']:,} too small, {stats['unreadable']:,} unreadable.")
    print("Data card: data/DATA_CARD.md   Next: python -m plantdoc_ai.train")
    return 0


if __name__ == "__main__":
    sys.exit(main())
