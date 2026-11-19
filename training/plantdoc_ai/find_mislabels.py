"""
Step 3b - find likely mislabels after the first training run (memo Section 5, step 9).

    python -m plantdoc_ai.find_mislabels --run <run>          # writes the review page
    python -m plantdoc_ai.find_mislabels --apply              # after reviewing

Scores every training image with the trained model and lists the 2% the model disagrees
with most in data/reports/suspects.csv plus a contact sheet (suspects.html). Fill the
`action` column with `drop` or `relabel:<Label>` (e.g. relabel:Tomato___Septoria_leaf_spot),
save, run with --apply, then run prepare and train again.
"""
from __future__ import annotations

import argparse
import html
import sys
from pathlib import Path

import pandas as pd
import torch
import torch.nn.functional as F

from . import taxonomy as tx
from .config import data_dirs, load_config
from .data import load_manifest
from .evaluate import collect
from .model import load_checkpoint


def main(argv=None) -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--config")
    ap.add_argument("--run")
    ap.add_argument("--top", type=float, default=0.02, help="share of training images to review")
    ap.add_argument("--apply", action="store_true")
    args = ap.parse_args(argv)
    cfg = load_config(args.config)
    dirs = data_dirs(cfg)
    csv_path = dirs["reports"] / "suspects.csv"

    if args.apply:
        s = pd.read_csv(csv_path, keep_default_na=False)
        drops = s.loc[s["action"].str.strip().str.lower() == "drop", "path"].tolist()
        rel = s[s["action"].str.startswith("relabel:")]
        bad = [a for a in rel["action"] if a.split(":", 1)[1] not in tx.COND_INDEX]
        if bad:
            raise SystemExit(f"Unknown labels in relabel actions: {bad}")
        if drops:
            with open(dirs["root"] / "drop_list.txt", "a", encoding="utf-8") as fh:
                fh.write("\n".join(drops) + "\n")
        if len(rel):
            rl_path = dirs["root"] / "relabel.csv"
            new = pd.DataFrame({"path": rel["path"], "condition": [a.split(":", 1)[1] for a in rel["action"]]})
            if rl_path.exists():
                new = pd.concat([pd.read_csv(rl_path), new]).drop_duplicates("path", keep="last")
            new.to_csv(rl_path, index=False)
        print(f"{len(drops)} dropped, {len(rel)} relabelled. Now run prepare and train again.")
        return 0

    if not args.run:
        raise SystemExit("--run is required (or --apply)")
    device = torch.device("cuda" if torch.cuda.is_available() else "cpu")
    model, _ = load_checkpoint(cfg["paths"]["runs"] / args.run / "best.pt", device)
    model = model.to(memory_format=torch.channels_last)
    df = load_manifest(dirs["root"] / "manifest.csv", ["train"])
    df = df[df["condition"] != ""].reset_index(drop=True)
    r = collect(model, df, cfg["model"]["input_size"], device, 6)
    logits, y = r["out"]["condition"], r["t"]["condition"]
    df["loss"] = F.cross_entropy(logits, y, reduction="none").numpy()
    probs = torch.softmax(logits, 1)
    df["predicted"] = [tx.CONDITIONS[i] for i in probs.argmax(1).tolist()]
    df["p_predicted"] = probs.max(1).values.numpy().round(3)
    top = df.sort_values("loss", ascending=False).head(max(1, int(len(df) * args.top))).copy()
    top["action"] = ""
    top[["path", "source", "orig_label", "condition", "predicted", "p_predicted", "loss", "action"]].to_csv(csv_path, index=False)

    cards = "".join(
        f"<figure><img loading=lazy src='{Path(p).as_uri()}'><figcaption><b>{html.escape(c)}</b><br>"
        f"model: {html.escape(pr)} ({pp:.0%})<br><small>{html.escape(src)} · {html.escape(Path(p).name)}</small>"
        f"</figcaption></figure>"
        for p, c, pr, pp, src in zip(top["path"], top["condition"], top["predicted"], top["p_predicted"], top["source"]))
    page = ("<!doctype html><meta charset=utf-8><title>Suspected mislabels</title><style>"
            "body{font:13px system-ui;background:#111;color:#ddd;margin:16px}"
            "main{display:grid;grid-template-columns:repeat(auto-fill,minmax(220px,1fr));gap:12px}"
            "figure{margin:0;background:#1c1c1c;padding:8px;border-radius:8px}img{width:100%;border-radius:4px}"
            f"</style><h1>{len(top)} images the model disagrees with</h1>"
            f"<p>Mark each in {html.escape(str(csv_path))}: <code>drop</code> or <code>relabel:&lt;Label&gt;</code>.</p>"
            f"<main>{cards}</main>")
    (dirs["reports"] / "suspects.html").write_text(page, encoding="utf-8")
    print(f"Review {dirs['reports'] / 'suspects.html'} and fill the action column in {csv_path}, then --apply.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
