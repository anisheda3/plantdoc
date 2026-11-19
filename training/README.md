# Training the Plant Doctor model

One EfficientNet-B0 with five heads (species, 42 conditions, 11 visual signs, severity,
photo quality) for tomato, potato, pepper and apple, trained on ~17 public field datasets.
Design: the "Plant Doctor — AI Model Project Memo".

Your laptop (RTX 5050 Laptop 8 GB, 24 GB RAM, i7-13620H) is enough. Budget ~60 GB of
disk, an evening for downloads, and roughly 1–2 hours per full training run (estimate;
`train` prints the real speed after the first epoch).

## 0. Unblock Python (once)

Windows **Smart App Control** is on and blocks PyTorch's unsigned DLLs
(`WinError 4551 ... Application Control policy has blocked this file`). Pick one:

**A. Turn it off** (simplest; permanent - turning it back on needs a Windows reset):
Windows Security → App & browser control → Smart App Control settings → Off. Then:

```powershell
cd E:\plant
powershell -ExecutionPolicy Bypass -File training\setup_env.ps1   # re-checks / finishes the install
training\.venv\Scripts\Activate.ps1
cd training
```

**B. Keep it on and use Linux (WSL2)**. In PowerShell:

```powershell
wsl --install -d Ubuntu-24.04      # pick a Linux username/password when it asks; reboot if told to
```

Then in the Ubuntu window:

```bash
bash /mnt/e/plant/training/setup_env.sh
source ~/plantdoc-venv/bin/activate
cd /mnt/e/plant/training
```

On WSL, reading images from `/mnt/e` is slow. Put the data on the Linux disk by editing
`config.yaml`: `data: /home/<your-linux-name>/plantdoc-data`.

**Anaconda instead of the venv** (still needs option A - conda's PyTorch DLLs are
unsigned too). In the Anaconda PowerShell Prompt:

```powershell
conda create -n plantdoc python=3.11 -y
conda activate plantdoc
pip install torch torchvision --index-url https://download.pytorch.org/whl/cu128
cd E:\plant
pip install -r training\requirements.txt -r server\requirements.txt
python -c "import torch; print(torch.__version__, torch.cuda.get_device_name(0))"
cd training
```

Each new prompt: `conda activate plantdoc` then `cd E:\plant\training`.

The setup must end with `GPU: NVIDIA GeForce RTX 5050 Laptop GPU`. Every command below
is run from the `training` folder with the environment active (each new terminal:
activate again).

## 1. Smoke test (5 minutes)

```
python smoke_test.py
```

Runs the whole pipeline on fake images and ends with `SMOKE TEST PASSED`. Also run the
server tests: `cd ..\server` then `python -m pytest -q` (all should pass), then come back.

## 2. Download the datasets (~45 GB, hours, resumable)

```
python -m plantdoc_ai.download --list     # what will be fetched, with licences
python -m plantdoc_ai.download            # everything; just run it again if it stops
```

To start small: `python -m plantdoc_ai.download --only tomato_bangladesh potato_uncontrolled plantdoc`.
The two big ones are `potato_pldd` (8.8 GB) and `apple_pp2021` (16 GB).

## 3. Prepare (clean, de-duplicate, split)

```
python -m plantdoc_ai.prepare
```

Read `data/DATA_CARD.md` afterwards (images per label and split, gaps). If it stops with
"folder labels have no mapping", open `data/reports/unmapped_labels.csv` and add those
names to `plantdoc_ai/sources.py` (or rerun with `--allow-unmapped` to skip them).

## 4. Train

Quick run first (about 10–15 minutes) to see the speed and that everything works:

```
python -m plantdoc_ai.train --run quick --limit 3000 --stage1-epochs 1 --epochs 2
```

Then the real run:

```
python -m plantdoc_ai.train --run v1
tensorboard --logdir runs        # optional, in a second terminal: open http://localhost:6006
```

The best weights go to `runs/v1/best.pt`. If you get "CUDA out of memory", add
`--batch-size 32`; if Windows runs out of RAM, add `--workers 4`.

## 5. Evaluate

```
python -m plantdoc_ai.evaluate --run v1
```

Prints the memo's checks with pass/fail (accuracy, held-out sources, rare classes,
look-alikes, robustness, quality gate, not-a-leaf, calibration) and saves
`runs/v1/eval/report.md`.

## 6. Clean labels and train again (once)

```
python -m plantdoc_ai.find_mislabels --run v1
```

Open `data/reports/suspects.html`, and in `data/reports/suspects.csv` write `drop` or
`relabel:<Label>` in the `action` column for the wrong ones. Then:

```
python -m plantdoc_ai.find_mislabels --apply
python -m plantdoc_ai.prepare
python -m plantdoc_ai.train --run v2
python -m plantdoc_ai.evaluate --run v2
```

## 7. Put it in the app

```
python -m plantdoc_ai.export_onnx --run v2 --install
cd ..\server
uvicorn main:app --host 0.0.0.0 --port 8000
```

Export checks the ONNX file against PyTorch and times it on the CPU (target ≤ 150 ms),
then copies `plant_disease.onnx`, `labels.json`, `model_meta.json` to `server/models/`.
On the phone: Settings → Server AI → `http://<your PC's IP>:8000/v1/analyze` →
Test connection (it should say a trained model is loaded). Phone and PC on the same
Wi-Fi; allow port 8000 in Windows Firewall. On WSL, run the server inside Ubuntu.

## Files

| File | What it does |
| --- | --- |
| `config.yaml` | every setting for a run |
| `plantdoc_ai/taxonomy.py` | the 42 labels, species, signs, quality classes (output order - append only) |
| `plantdoc_ai/sources.py` | each dataset, its licence, and folder-name → label maps |
| `plantdoc_ai/download.py` | step 2 |
| `plantdoc_ai/prepare.py` | step 3 |
| `plantdoc_ai/data.py`, `model.py`, `train.py` | step 4 |
| `plantdoc_ai/evaluate.py` | step 5 |
| `plantdoc_ai/find_mislabels.py` | step 6 |
| `plantdoc_ai/export_onnx.py` | step 7 |

Before releasing the app publicly, confirm the licences flagged "confirm" in
`--list` (Plant Pathology 2021, PlantSeg, Chili Rangpur) and credit the CC BY authors.
