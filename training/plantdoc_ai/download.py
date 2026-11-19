"""
Step 1 - download every source into data/raw/<source>/ and unpack it.

    python -m plantdoc_ai.download                 # everything enabled (~45 GB, resumable)
    python -m plantdoc_ai.download --only tomato_bangladesh potato_uncontrolled
    python -m plantdoc_ai.download --list          # what will be fetched, licences

Safe to re-run: finished files are skipped (Mendeley files are checked against their
SHA-256), and archives are only unpacked once. Each source gets a SOURCE.md with its URL,
version, licence and download date (memo: "Download" step of the pipeline).
"""
from __future__ import annotations

import argparse
import collections
import concurrent.futures as cf
import datetime as dt
import hashlib
import io
import json
import re
import shutil
import subprocess
import sys
import time
import zipfile
from pathlib import Path

import requests
from tqdm import tqdm

from .config import data_dirs, load_config
from .sources import OD, OH, SOURCES, Source, enabled_sources

UA = {"User-Agent": "Mozilla/5.0 PlantDoctor-training/1.0"}   # Mendeley returns 403 to python's default
MENDELEY = "https://data.mendeley.com/public-api/datasets"
ARCHIVES = (".zip", ".7z", ".rar")
IMAGES = (".jpg", ".jpeg", ".png", ".bmp", ".webp", ".tif", ".tiff")

_session = requests.Session()
_session.headers.update(UA)


# ---------------------------------------------------------------------------
# HTTP helpers
# ---------------------------------------------------------------------------

def get_json(url: str):
    for attempt in range(5):
        try:
            r = _session.get(url, timeout=120, headers={"Accept": "application/json"})
            r.raise_for_status()
            return r.json()
        except (requests.RequestException, ValueError):
            if attempt == 4:
                raise
            time.sleep(2 ** attempt)


def sha256_of(path: Path) -> str:
    h = hashlib.sha256()
    with open(path, "rb") as fh:
        for chunk in iter(lambda: fh.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


def fetch(url: str, dest: Path, size: int | None = None, sha256: str | None = None, progress: bool = False) -> Path:
    """Downloads url to dest (resumable via .part), verifying size and SHA-256 when known."""
    if dest.exists() and (size is None or dest.stat().st_size == size):
        return dest
    dest.parent.mkdir(parents=True, exist_ok=True)
    part = dest.with_name(dest.name + ".part")
    for attempt in range(6):
        try:
            have = part.stat().st_size if part.exists() else 0
            headers = {"Range": f"bytes={have}-"} if have else {}
            with _session.get(url, stream=True, timeout=300, headers=headers) as r:
                if r.status_code == 416:          # .part is already complete
                    break
                r.raise_for_status()
                mode = "ab" if have and r.status_code == 206 else "wb"
                total = int(r.headers.get("Content-Length", 0)) + (have if mode == "ab" else 0)
                bar = tqdm(total=total or None, initial=have if mode == "ab" else 0, unit="B", unit_scale=True,
                           desc=dest.name[:40], leave=False, disable=not progress)
                with open(part, mode) as fh:
                    for chunk in r.iter_content(1 << 20):
                        fh.write(chunk)
                        bar.update(len(chunk))
                bar.close()
            break
        except requests.RequestException:
            if attempt == 5:
                raise
            time.sleep(2 ** attempt)
    if size is not None and part.stat().st_size != size:
        part.unlink(missing_ok=True)
        raise IOError(f"{dest.name}: size mismatch, deleted the partial file - run again")
    if sha256 and sha256_of(part) != sha256:
        part.unlink(missing_ok=True)
        raise IOError(f"{dest.name}: checksum mismatch, deleted - run again")
    part.replace(dest)
    return dest


# ---------------------------------------------------------------------------
# Archives
# ---------------------------------------------------------------------------

def extract(archive: Path, keep: bool) -> None:
    """Unpacks next to the archive, once (a .extracted marker remembers it)."""
    marker = archive.with_name(archive.name + ".extracted")
    if marker.exists():
        return
    out = archive.parent
    name = archive.name.lower()
    print(f"  unpacking {archive.name} ...")
    if name.endswith(".zip"):
        with zipfile.ZipFile(archive) as z:
            for info in z.infolist():
                if "__MACOSX" in info.filename or info.is_dir():
                    continue
                z.extract(info, out)
    elif name.endswith(".7z"):
        import py7zr
        with py7zr.SevenZipFile(archive, "r") as z:
            z.extractall(out)
    elif name.endswith(".rar"):
        tar = Path(r"C:\Windows\System32\tar.exe")   # Windows' bsdtar reads rar
        subprocess.run([str(tar) if tar.exists() else "tar", "-xf", str(archive), "-C", str(out)], check=True)
    marker.write_text(dt.datetime.now().isoformat())
    if not keep:
        archive.unlink()


# ---------------------------------------------------------------------------
# Label filtering while downloading single files
# ---------------------------------------------------------------------------

def label_of(src: Source, rel: str) -> str:
    parts = rel.replace("\\", "/").split("/")
    if src.label_mode == "plantseg":
        return plantseg_label(parts[-1])
    return " ".join(parts[-1 - src.label_parts:-1])


def plantseg_label(filename: str) -> str:
    stem = filename.rsplit(".", 1)[0]
    stem = re.sub(r"[ _]*\(?\d+\)?$", "", stem)                    # trailing number
    stem = re.sub(r"_(google|bing|baidu).*$", "", stem, flags=re.I)  # search-engine suffix
    return stem


def wanted(src: Source, rel: str, counts: collections.Counter) -> bool:
    """For plain (non-archive) files: path filters, 'skip' labels and per-folder caps."""
    if src.include and not re.search(src.include, rel, re.I):
        return False
    if src.exclude and re.search(src.exclude, rel, re.I):
        return False
    if not rel.lower().endswith(IMAGES):
        return False
    label = label_of(src, rel)
    mapped = src.map_label(label)
    if mapped == "skip":
        return False
    if mapped in (OD, OH) or (mapped or "").startswith("Other___"):
        key = label.split(" ")[0] if src.label_mode == "plantseg" else label
        if src.cap_other and counts[key] >= src.cap_other:
            return False
        counts[key] += 1
    return True


# ---------------------------------------------------------------------------
# Source kinds
# ---------------------------------------------------------------------------

def download_mendeley(src: Source, dest: Path, keep: bool, workers: int) -> dict:
    record = get_json(f"{MENDELEY}/{src.ref}")          # the unversioned record lists every file
    version = record.get("version")
    folders = get_json(f"{MENDELEY}/{src.ref}/folders/{version}") if version else []
    by_id = {f["id"]: f for f in folders}

    def folder_path(fid):
        parts = []
        while fid and fid in by_id:
            parts.append(by_id[fid]["name"])
            fid = by_id[fid].get("parent_id")
        return "/".join(reversed(parts))

    jobs, counts = [], collections.Counter()
    for f in sorted(record.get("files", []), key=lambda f: (folder_path(f.get("folder_id")), f["filename"])):
        rel = "/".join(p for p in (folder_path(f.get("folder_id")), f["filename"]) if p)
        if src.download_include and not re.search(src.download_include, rel, re.I):
            continue
        if not rel.lower().endswith(ARCHIVES) and not wanted(src, rel, counts):
            continue
        cd = f.get("content_details", {})
        jobs.append((rel, cd.get("download_url"), f.get("size"), cd.get("sha256_hash")))

    total = sum(j[2] or 0 for j in jobs)
    print(f"  {len(jobs)} files, {total / 1e9:.2f} GB (version {version})")
    archives = [j for j in jobs if j[0].lower().endswith(ARCHIVES)]
    singles = [j for j in jobs if j not in archives]

    for rel, url, size, sha in archives:                 # big files: one at a time, with a progress bar
        path = dest / rel
        if not path.with_name(path.name + ".extracted").exists():
            fetch(url, path, size, sha, progress=True)
            extract(path, keep)

    if singles:
        with cf.ThreadPoolExecutor(workers) as pool:
            futs = [pool.submit(fetch, url, dest / rel, size, sha) for rel, url, size, sha in singles]
            for fut in tqdm(cf.as_completed(futs), total=len(futs), desc="  files", unit="img"):
                fut.result()

    doi = (record.get("doi") or {}).get("id", "")
    return {"version": version, "doi": doi, "files": len(jobs), "bytes": total,
            "licence": (record.get("data_licence") or {}).get("short_name", src.licence)}


def download_zip(src: Source, dest: Path, keep: bool, workers: int) -> dict:
    path = dest / (src.id + ".zip")
    if not path.with_name(path.name + ".extracted").exists():
        fetch(src.ref, path, progress=True)
        extract(path, keep)
    return {"version": src.ref.rsplit("/", 1)[-1], "files": 1}


def download_hf_parquet(src: Source, dest: Path, keep: bool, workers: int) -> dict:
    """Plant Pathology 2021: parquet shards -> <label>/<image_id>.jpg at 640 px (shards deleted)."""
    import pyarrow.parquet as pq
    from PIL import Image

    tree = get_json(f"https://huggingface.co/api/datasets/{src.ref}/tree/main/data")
    shards = sorted(t["path"] for t in tree if t["path"].endswith(".parquet"))
    done_dir = dest / "_shards_done"
    done_dir.mkdir(parents=True, exist_ok=True)
    written = 0
    for shard in shards:
        marker = done_dir / (Path(shard).name + ".done")
        if marker.exists():
            continue
        local = dest / "_shards" / Path(shard).name
        fetch(f"https://huggingface.co/datasets/{src.ref}/resolve/main/{shard}", local, progress=True)
        pf = pq.ParquetFile(local)
        for batch in pf.iter_batches(batch_size=32, columns=["image", "label_names", "image_id"]):
            for row in batch.to_pylist():
                names = row["label_names"] or []
                folder = names[0] if len(names) == 1 else "multiple"
                out = dest / folder / f"{row['image_id'].rsplit('.', 1)[0]}.jpg"
                if out.exists():
                    continue
                img = Image.open(io.BytesIO(row["image"]["bytes"])).convert("RGB")
                img.thumbnail((640, 640))
                out.parent.mkdir(parents=True, exist_ok=True)
                img.save(out, quality=92)
                written += 1
        marker.write_text(dt.datetime.now().isoformat())
        if not keep:
            local.unlink()
    return {"version": "main", "files": written}


def download_coco(src: Source, dest: Path, keep: bool, workers: int) -> dict:
    out_dir = dest / "not a leaf"
    if out_dir.exists() and any(out_dir.iterdir()):
        return {"version": "2017-val", "files": len(list(out_dir.iterdir()))}
    images = fetch(src.ref, dest / "val2017.zip", progress=True)
    ann_zip = fetch(src.extra["annotations"], dest / "annotations_trainval2017.zip", progress=True)
    with zipfile.ZipFile(ann_zip) as z:
        ann = json.loads(z.read("annotations/instances_val2017.json"))
    banned = {c["id"] for c in ann["categories"] if c["name"] in src.extra["exclude_categories"]}
    bad_images = {a["image_id"] for a in ann["annotations"] if a["category_id"] in banned}
    keepers = [im["file_name"] for im in ann["images"] if im["id"] not in bad_images]
    keepers = sorted(keepers)[: src.extra["max_images"]]
    out_dir.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(images) as z:
        for name in tqdm(keepers, desc="  COCO", unit="img"):
            with z.open(f"val2017/{name}") as fh, open(out_dir / name, "wb") as o:
                shutil.copyfileobj(fh, o)
    if not keep:
        images.unlink()
        ann_zip.unlink()
    return {"version": "2017-val", "files": len(keepers)}


KINDS = {"mendeley": download_mendeley, "github_zip": download_zip, "zenodo_zip": download_zip,
         "hf_parquet": download_hf_parquet, "coco": download_coco}


def write_source_md(src: Source, dest: Path, info: dict) -> None:
    lines = [f"# {src.title}", "", f"- Source id: `{src.id}`", f"- URL: {src.url}",
             f"- Licence: {info.get('licence', src.licence)}", f"- Version: {info.get('version', '')}"]
    if info.get("doi"):
        lines.append(f"- DOI: https://doi.org/{info['doi']}")
    lines += [f"- Downloaded: {dt.date.today().isoformat()}", f"- Files fetched: {info.get('files', '')}"]
    if src.note:
        lines += ["", src.note]
    (dest / "SOURCE.md").write_text("\n".join(lines) + "\n", encoding="utf-8")


def main(argv=None) -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--config")
    ap.add_argument("--only", nargs="+", help="source ids (see --list)")
    ap.add_argument("--list", action="store_true")
    ap.add_argument("--keep-archives", action="store_true", help="keep .zip/.7z/.parquet after unpacking")
    ap.add_argument("--workers", type=int, default=8, help="parallel downloads for single-image sources")
    args = ap.parse_args(argv)

    cfg = load_config(args.config)
    raw = data_dirs(cfg)["raw"]
    sources = enabled_sources(args.only)

    if args.list:
        for s in enabled_sources(None) if not args.only else sources:
            print(f"{s.id:22} {s.kind:11} {s.title[:58]:58} {s.licence[:40]}")
        print("\nOff by default:", ", ".join(x.id for x in SOURCES if not x.enabled))
        return 0

    failed = []
    for src in sources:
        print(f"\n== {src.id}: {src.title}")
        dest = raw / src.id
        dest.mkdir(parents=True, exist_ok=True)
        try:
            info = KINDS[src.kind](src, dest, args.keep_archives, args.workers)
            write_source_md(src, dest, info)
            print("  done")
        except Exception as exc:   # keep going; report at the end
            print(f"  FAILED: {exc}")
            failed.append(src.id)
    if failed:
        print(f"\nSome sources failed (re-run to resume): {', '.join(failed)}")
        return 1
    print("\nAll downloads complete. Next: python -m plantdoc_ai.prepare")
    return 0


if __name__ == "__main__":
    sys.exit(main())
