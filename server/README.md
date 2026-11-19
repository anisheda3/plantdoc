# AI Plant Doctor — Inference Server

## Run locally
```bash
cd server
python -m venv .venv && source .venv/bin/activate
pip install -r requirements.txt
uvicorn main:app --host 0.0.0.0 --port 8000 --reload
```
Interactive docs: http://localhost:8000/docs

## Endpoints
| Method | Path | Purpose |
|---|---|---|
| GET | `/health` | liveness + whether a model is loaded |
| GET | `/labels` | label list the model can predict |
| POST | `/analyze` | multipart `image` (+ optional `client_version`) → `AnalysisResponse`; the model's class name is resolved through the content database to the app's plant and disease, and the disease's full content comes back in `content` (schema 1.2) |
| GET | `/v1/plants` | every plant in the content database |
| GET | `/v1/plants/{plant_id}` | one plant and its diseases |
| GET | `/v1/content/{plant_id}/{disease_id}` | everything about one disease: symptoms, lifecycle, conditions, treatment steps, prevention, method limits, follow-ups, severity profile, sources |
| GET | `/v1/resolve?label=...&plant=...` | a dataset class name (PlantVillage, PlantSeg, PlantDoc, MangoLeafBD, ...) → plant and disease |

The content comes from `data/plant_doctor_content.db` (copied there by `tools/data/make_content_db.py`; falls back to
`../Database/`; override with `PLANTDOCTOR_CONTENT_DB`). It is opened read-only. Tests: `python -m unittest tests.test_content`.

## Error codes
`INVALID_IMAGE` (400/415) · `MODEL_UNAVAILABLE` (503) · `MODEL_ERROR` (500).
Unity maps each to a user-facing message and can fall back to Demo Mode.

## Installing a real model
1. Train a disease classifier and export to ONNX.
2. Put it at `models/plant_disease.onnx`, labels at `models/labels.json`
   (a JSON array like `["Tomato___Early_blight", "Tomato___Late_blight", "Tomato___healthy"]`).
3. Restart. `/health` will report `model_loaded: true`.

Until then `/analyze` returns `MODEL_UNAVAILABLE` **by design** — the server never
fabricates a prediction.

## Unity side
In the app: **Settings (gear) → Server AI**, address `http://<PC LAN IP>:8000/v1/analyze`, then **Test connection**.
The phone and PC must share a Wi-Fi network and port 8000 must be open in the firewall. Run the
server with `--host 0.0.0.0` so it accepts LAN connections. Release builds already allow plain HTTP.
See `docs/PHONE_AR_GUIDE.md`.
