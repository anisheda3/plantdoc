# Plant Doctor: Plan for Measurements, Depth and 3D on Ordinary Android Phones

*Written 28 Oct 2026. Companion to `CROP_3D_SENSING_FINDINGS.md`. Timings and prices are rough estimates and haven't been measured. Licences and package versions marked "verify" must be checked before adoption.*

---

## 0. Assumptions and what changes without ARCore

**Target phone:** any Android 8+ phone with a rear camera that can record at least 1080p. It has **no ARCore, no LiDAR and no depth sensor**. A gyroscope and accelerometer are used if present but are optional.

The app already handles this case: `AppModeController` falls back to non-AR mode, and `WebCamFrameSource` supplies the camera.

**What we lose:** AR tracking, ARCore depth, AR raycasts and AR occlusion. The earlier "real leaves hide the AR twin" idea (occlusion) is **dropped**.

**What replaces each missing ARCore capability:**

| Need | ARCore would give | Replacement in this plan |
|---|---|---|
| Real-world scale (mm, cm) | Depth, raycasts | A **printed scale card** with ArUco markers placed in the photo |
| Depth and background removal | Depth API | A **depth-estimating AI model** (Depth Anything V2 Small) plus leaf segmentation, run on the server |
| Camera positions for 3D | Motion tracking | **Structure-from-Motion** (COLMAP / GLOMAP) on the server, which works out camera positions from the photos themselves |
| Ground plane and "up" direction | Plane detection | The scale card lying flat on the ground |
| Camera lens data (focal length) | ARCore camera intrinsics | Android Camera2 camera characteristics, plus COLMAP self-calibration |

**Guiding principles**

1. **The phone captures and displays; the server computes.** Cheap phones can't run the 3D pipeline.
2. **Everything falls back gracefully.** With no card or no server, the app behaves as it does today: "% of leaf" results and the labelled Demo AI.
3. **De-risk before building.** The 3D pipeline is proven as a command-line tool on the laptop before any app work.

---

## 1. The scale card (the foundation)

Without depth hardware, **a known-size reference in the photo is the only reliable source of real units.** Everything in Phases 1–3 depends on it.

### Design (printable PDF, credit-card size or A6)

- **4 ArUco markers** (OpenCV dictionary `DICT_4X4_50`), one in each corner, each 20 mm wide, at known positions. Any 1–2 can be covered and it still works.
- **6 colour patches** in the middle (white, mid-grey, black, red, green, blue) for **white-balance and colour correction**. This also helps the colour-based `LeafFeatureTracer`.
- A **mm ruler** along one edge so people can check the print.
- A card ID and version, so the server knows the layout.

### Printing rules

- Print at **100% / "actual size"**. Printer scaling is the most common source of error, so the app asks the user to check the ruler once.
- Use **matte paper**. Glossy paper or lamination causes glare that hides the markers.

### Why ArUco markers

They are built into OpenCV (`cv2.aruco`, Apache-2.0) and robust to viewing angle. One detection gives:

- the **homography** (the mapping from the photo to the card's flat surface), which gives **mm per pixel** on that surface;
- **distance and tilt** of the camera relative to the card;
- in 3D, the **ground plane and true scale**.

### How the card is used

| Scan | Card placement |
|---|---|
| Leaf | Behind or beside the leaf, roughly in the same plane (slide it under the leaf) |
| Plant height (Phase 2) | Standing upright at the plant base, facing the camera |
| 3D scan (Phase 3) | Lying flat on the soil next to the stem |

### Fallbacks

1. **A4 / Letter sheet behind the leaf.** The server detects the largest bright rectangle, which gives both scale and a clean background. Leaf scans only.
2. **No reference at all:** show percentages only, as now, and say so in the result.

### Accuracy rule

Measurements are exact **only on the card's plane**. A leaf that sits `d` cm off that plane, viewed from `Z` cm away, has a scale error of about `d / Z`. For example, 2 cm off the plane at 25 cm away gives about an 8% error. The app tells users to keep the leaf flat against or near the card.

---

## 2. Architecture

```
┌────────────────────────── Android phone (Unity app, no AR) ──────────────────────────┐
│ Camera preview (WebCamTexture)    High-res stills / frames (NativeCamera plugin)     │
│ On-device: quality gate (blur, light), LeafFeatureTracer, gyro coverage ring,        │
│            3D model viewer (glTFast), results UI, offline fallback                   │
└──────────────────────────────────────┬───────────────────────────────────────────────┘
                                       │ HTTPS + X-API-Key   (LAN HTTP in development)
┌──────────────────────────────────────▼───────────────────────────────────────────────┐
│ API service (FastAPI, CPU)  ── existing /v1/analyze                                  │
│   /v1/measure/leaf  /v1/measure/plant   → synchronous, under ~3 s, CPU ONNX models   │
│   /v1/reconstructions/*                 → asynchronous jobs                          │
│   job records: SQLite (later Postgres)        files: ./data volume (later S3/R2)     │
└──────────────────────────────────────┬───────────────────────────────────────────────┘
                                       │ Redis + RQ job queue
┌──────────────────────────────────────▼───────────────────────────────────────────────┐
│ GPU worker:  frame filter → COLMAP/GLOMAP → scale from card → OpenMVS mesh           │
│              (later: gsplat Gaussian splatting) → clean/decimate → GLB + measurements│
└──────────────────────────────────────────────────────────────────────────────────────┘
```

### Architecture decisions

| Decision | Choice | Why | Alternative considered |
|---|---|---|---|
| API framework | **Keep FastAPI** (`server/main.py`) | It already exists and has tests, API-key auth, rate limiting and Docker | — |
| How requests run | **Waiting for the result** for measurements; **background jobs** for 3D | Measurements take seconds; 3D takes 10–30 min | Everything as jobs (unnecessary complexity for measurements) |
| Job queue | **Redis + RQ** | Simple, Python-native, one extra container | Celery (heavier); SQLite polling (fine for one worker, but no retries or visibility) |
| Job records | **SQLite** on a volume, moving to **Postgres** in production | No setup for development | Postgres from day one |
| File storage | **Local `data/` volume**, later an **S3-compatible store** (Cloudflare R2 / MinIO) | Simple now, cloud-ready later | — |
| Model runtime for measurements | **onnxruntime on the CPU** inside the API container | Same runtime as `inference.py`; runs on a cheap CPU server | GPU worker (adds queue latency to every measurement) |
| 3D output | **Textured mesh (GLB) first**, Gaussian splats second | Meshes display easily on phones (glTFast) and can be measured; splats are heavy on mobile and harder to measure | Splats first (prettier, but worse for phones and measurements) |
| 3D format limits | GLB, ≤100k triangles, ≤2048² texture, ≤10 MB | Fits low-end phone memory | — |
| Phone capture | **NativeCamera** (yasirkula, MIT; verify) for stills; WebCamTexture for preview and frame bursts | Full-sensor photos with EXIF data for fine detail; WebCamTexture is limited to about 1080p | WebCamTexture only (simpler, less detail) |
| Containers | **docker compose**: `api`, `redis`, `worker` (GPU), plus `caddy` in production | One command to start everything; the same setup on the laptop and in the cloud | Bare-metal installs |
| Development hosting | **This laptop** (RTX 5050 Laptop, 8 GB VRAM, 24 GB RAM, Docker Desktop with WSL2 installed) | Free; the GPU is good enough for single-plant reconstructions | — |
| Windows Smart App Control | Run all Python and 3D tools **inside WSL2/Docker** | Smart App Control blocks unsigned Windows DLLs; Linux binaries inside WSL2 aren't affected | Turning Smart App Control off |

---

## 3. Phase 0: Server foundations (do first)

### 3.1 Make the GPU usable in Docker

1. Start Docker Desktop. Under Settings → Resources → WSL integration, make sure the WSL 2 backend is on.
2. Check that a container can see the GPU:
   `docker run --rm --gpus all nvidia/cuda:12.8.0-base-ubuntu22.04 nvidia-smi`
3. **Blackwell GPU gotcha.** The RTX 5050 has compute capability **12.0** (the `sm_120` architecture), so every GPU library must be built for **CUDA 12.8 or newer**:
   - **PyTorch ≥ 2.7** with `cu128` wheels;
   - **COLMAP** built from source with `CMAKE_CUDA_ARCHITECTURES=120`. Older prebuilt images may fail on GPU feature extraction; COLMAP's CPU feature extraction still works, just slower;
   - **gsplat** compiled with `TORCH_CUDA_ARCH_LIST="12.0"`.

### 3.2 Code layout

```
server/
  main.py                    existing app; includes the new routers
  inference.py, schema.py    existing
  routers/measure.py         /v1/measure/leaf, /v1/measure/plant
  routers/reconstructions.py /v1/reconstructions/*
  jobs/store.py              SQLite job records
  jobs/queue.py              RQ enqueue helpers
  vision/scale_card.py       ArUco detection, homography, colour correction
  vision/depth.py            Depth Anything V2 Small (ONNX)
  vision/segment.py          leaf/plant mask (depth + colour, later a trained model)
  vision/measure.py          mm/px, areas, distance, height/width
  worker/recon_pipeline.py   the 3D pipeline (also runnable as a command-line tool)
  worker/Dockerfile.gpu      CUDA 12.8 + COLMAP + GLOMAP + OpenMVS + PyTorch + gsplat
  docker-compose.yml
  models/                    plant_disease.onnx (later), depth_anything_v2_small.onnx
  tests/                     pytest: card photos, measurement fixtures, small 3D fixture (skipped without a GPU)
data/                        (volume) jobs/<id>/{frames,colmap,mvs,out,log.txt}
```

### 3.3 docker-compose sketch

```yaml
services:
  api:
    build: .
    ports: ["8000:8000"]
    env_file: .env
    volumes: ["./data:/data", "./models:/app/models"]
    depends_on: [redis]
  redis:
    image: redis:7-alpine
  worker:
    build: { context: ., dockerfile: worker/Dockerfile.gpu }
    command: rq worker recon --url redis://redis:6379
    volumes: ["./data:/data"]
    depends_on: [redis]
    deploy:
      resources:
        reservations:
          devices: [{ driver: nvidia, count: all, capabilities: [gpu] }]
```

### 3.4 Security and data

- Keep the existing **API key** and **rate limit**.
- Add upload limits: 8 MB per measurement photo; 300 MB and 150 frames per 3D scan.
- Use random UUID4 job IDs.
- Serve **HTTPS** in production through Caddy, which handles certificates automatically.
- **Retention:** delete raw frames after 7 days and models after 30 days (configurable), with a nightly cleanup job.
- **Privacy:** an in-app notice that photos are uploaded for processing, plus a "delete my scan" option (`DELETE` endpoint).

### 3.5 Phase 0 is done when

`docker compose up` serves `/v1/health`, a test job travels through Redis to the worker and back, the worker container sees the GPU, and pytest passes.

---

## 4. Phase 1: Real-size leaf results, background removal, distance check

### User flow

1. The scan screen shows "Place the Plant Doctor card next to the leaf". The step can be skipped.
2. The existing on-device quality gate checks the shot (blur, light), then a **high-resolution still** is taken through NativeCamera.
3. The app uploads the photo plus metadata (focal length, sensor size, image size, phone model, card ID) to `POST /v1/measure/leaf`.
4. The server:
   1. detects the card, computes the homography and **mm per pixel**, and colour-corrects the photo using the patches;
   2. **v1 leaf mask:** runs Depth Anything V2 Small to get relative depth, keeps the foreground blob around the leaf, and refines the edges with colour. **v2:** a trained leaf-segmentation model (e.g. SegFormer-B0 or U-Net fine-tuned on PlantSeg-style data; check dataset licences);
   3. estimates **distance** from the marker's size in pixels and the focal length;
   4. returns warnings: `CARD_NOT_FOUND`, `TOO_FAR` (card under about 120 px wide), `TOO_CLOSE`, `CARD_TILTED` (over 45°), `LEAF_OFF_PLANE` (leaf much larger or smaller than expected).
5. The phone runs `LeafFeatureTracer` **inside the server's leaf mask** and converts lesion pixel counts to **mm²**. Example result: "3 spots, largest 4.2 mm²; 0.8 cm² affected of a 31 cm² leaf".
6. **Distance guide.** v1 checks after capture ("Card too small — move closer and retake"). v2 is a **live** on-device card check, using a C# ArUco port or the paid "OpenCV for Unity" asset (to be decided then).

### Example response

```json
{
  "card": { "found": true, "id": "PD-CARD-1", "mm_per_px": 0.071, "tilt_deg": 18, "distance_cm": 24.5 },
  "leaf": { "area_cm2": 31.2, "mask_png_base64": "…", "bbox": [412, 300, 1630, 1488] },
  "colour_correction": { "applied": true, "white_error_before": 0.11, "white_error_after": 0.02 },
  "warnings": []
}
```

### Depth model choice

- **Depth Anything V2 Small**, licensed **Apache-2.0**. The Base, Large and Giant versions are **CC-BY-NC-4.0 (non-commercial)**, so don't use them in a product.
- Export it to ONNX at a 518 px input size.
- On CPU onnxruntime it takes roughly 0.5–2 s; on a GPU, tens of milliseconds.
- Its output is **relative** depth, not distance. It's used only to separate the leaf from the background. Real scale always comes from the card.

### Unity changes

| Item | Change |
|---|---|
| `NativeCamera` package | Add through its git URL; used for full-resolution stills |
| `CameraIntrinsicsProvider` (new) | Reads Camera2 data through `AndroidJavaObject`: focal lengths, sensor size and, if present, lens calibration |
| `MeasureService` (new) | Follows the `RemoteAIService` pattern: multipart upload through `UnityWebRequest`, error codes, timeout |
| `MeasurementResult` (new) | Parsed response |
| `LeafFeatureTracer` | New overload `Trace(pixels, w, h, bool[] leafMask)`; `TracedFeature` gains `AreaMm2` |
| Result UI | Show mm² and cm² when a card was found, "% of leaf" otherwise; a card hint on the scan screen; a "Print card" button that shares the PDF |
| Settings | "Use scale card" toggle; server address (already exists) |
| Tests | EditMode tests for response parsing, the mm² conversion and the fallback when no card is found |

### Validation (before claiming accuracy)

- Photograph known shapes printed on paper (2×2 cm and 5×5 cm squares, 1 cm dots) next to the card. **Target: area error ≤ 5%** when the shapes are on the card's plane.
- Compare against 20 real leaves measured by hand (graph paper or ImageJ).
- Compare the distance estimate with a tape measure.
- Re-run the existing tracer evaluation (`tools/eval`) with depth masks and check whether IoU rises above 0.172.

### Phase 1 is done when

On the target phone with a card, leaf area is within 5% on the test shapes and results show mm². Without a card or a server, the app behaves exactly as today.

---

## 5. Phase 2: Plant height and spread

### Method A: one photo (ships first)

- **Setup:** the card stands upright at the plant base, facing the camera (a fold-out stand or tape it to a stake). The phone is about 1–1.5 m away at roughly mid-plant height, with the whole plant in frame.
- **On the phone:** read the gravity vector from the accelerometer (Input System `GravitySensor` / `Accelerometer`) and warn if the phone tilts more than 10°.
- **Server endpoint** `POST /v1/measure/plant`: detect the card to get mm per pixel at the card's plane, build a plant mask (depth plus colour), then convert the top-to-bottom and left-to-right pixel extents to **height and spread in cm**.
- **Expected error:** ±5–10%, because leaves in front of or behind the card's plane are scaled wrongly.

### Method B: from the 3D model (Phase 3)

Height along the ground's "up" direction, canopy diameter and volume. When Phase 3 lands, Method B replaces Method A as the accurate option, and A stays as the quick one.

### Growth tracking

- Save each measurement per plant on the phone (JSON in `Application.persistentDataPath`) with date and method.
- Show a simple height-over-time chart.
- Keep the plant ID stable across scans so the same plant can be followed.

### Phase 2 is done when

Height is within 10% of a tape measure on 10 test plants of different sizes.

---

## 6. Phase 3: 3D model of the user's own plant (server reconstruction)

### 6.1 De-risk first (1–2 days, before any app work)

1. Record 2–3 plants with the **target phone's** normal camera app: 60–150 photos, card on the soil, indoors and outdoors.
2. On the laptop, inside WSL2/Docker, run COLMAP and then OpenMVS by hand on those photos.
3. Decide go or no-go:
   - Did the photos connect into one model?
   - Is the model usable?
   - Which settings worked?

   Only then build the API and the app side.

### 6.2 Capture in the app

- **Setup screen:** "Put the card flat on the soil next to the stem. Calm air, soft light, uncluttered background."
- **Two slow circles** around the plant: one at top height looking slightly down, one lower. Each takes about a minute.
- **Automatic frame picking:**
  - grab a frame from WebCamTexture at 1080p about every 0.5 s;
  - keep it only if it passes the existing blur and light checks;
  - aim for 60–150 frames.
- **Coverage ring:** gyroscope yaw (Input System `Gyroscope` / `AttitudeSensor`) fills a ring as the user walks around. It drifts but is fine for about a minute. Phones without a gyro use a frame counter instead.
- **Known limitation:** WebCamTexture can't lock exposure or focus. This is accepted; the capture tips and the server's frame filter cover it.
- **Upload:** JPEG at quality 90, about 300–500 KB per frame, 30–60 MB per scan. Frames are sent **one at a time** with a frame index, so an interrupted upload resumes. There's a Wi-Fi-only option.

### 6.3 Server pipeline (`worker/recon_pipeline.py`, also a command-line tool)

| Stage | Tool | What it does |
|---|---|---|
| 1. Filter | OpenCV | Drop blurry, over- or under-exposed and duplicate frames; keep at most 150 |
| 2. Camera positions (SfM) | **COLMAP** feature extraction and matching (exhaustive for ≤150 frames); **GLOMAP** global mapper (faster), with COLMAP's incremental mapper as fallback | Finds each photo's camera position and builds a sparse point cloud. One shared camera; the starting focal length comes from the Camera2 data. **Keep the background**: SfM needs its texture to place the cameras |
| 3. Scale and orientation | OpenCV ArUco + the COLMAP camera positions | Detect the card corners in every frame and triangulate them in 3D. Then scale the model to **metres**, make the card plane the **ground (z up)** and put the **origin at the card** |
| 4. Dense surface and mesh (v1) | **OpenMVS**: DensifyPointCloud → ReconstructMesh → (RefineMesh) → TextureMesh | Produces a textured mesh. Crop it to a cylinder around the plant and keep a disc of soil |
| 4b. Gaussian splats (v2, optional) | **gsplat** (Apache-2.0), 7k–30k steps on images about 1600 px wide | Better-looking model; 8 GB VRAM is enough at that size. A mesh can be extracted through TSDF fusion of rendered depth |
| 5. Clean and export | Open3D / trimesh | Remove floating fragments, reduce to ≤100k triangles and a ≤2048² texture, export **GLB**, render a thumbnail |
| 6. Measurements | NumPy / Open3D | Height (max z), canopy diameter (convex hull on the ground plane), canopy area, volume (5 mm voxels or alpha shape); written to `measurements.json` |
| 7. Disease overlay (later) | Existing tracer and disease model | Run them on the source frames and project the masks onto the mesh using the known camera positions. In v1 the photo texture already shows the lesions |

**Rough time on the RTX 5050 (not measured):** SfM 2–6 min, OpenMVS 5–15 min, gsplat 10–20 min. A full job takes about **10–30 minutes**.

### 6.4 Reconstruction API

| Method | Path | Purpose |
|---|---|---|
| POST | `/v1/reconstructions` | Create a job; returns `{id, max_frames, max_bytes}` |
| PUT | `/v1/reconstructions/{id}/frames/{index}` | Upload one JPEG frame (resumable) |
| POST | `/v1/reconstructions/{id}/submit` | Send metadata (lens data, phone model, card ID) and start the job |
| GET | `/v1/reconstructions/{id}` | `{status, stage, progress, eta_s, error}` |
| GET | `/v1/reconstructions/{id}/model.glb` · `/measurements.json` · `/thumbnail.jpg` | Results |
| DELETE | `/v1/reconstructions/{id}` | Delete everything for the job |

- **Status values:** `created → uploading → queued → processing(stage) → done | failed`.
- **Error codes:**
  - `TOO_FEW_FRAMES`;
  - `SFM_FAILED` ("photos didn't connect": move slower, more overlap);
  - `NO_SCALE_CARD` (the model is still returned, but without real units);
  - `GPU_OOM` (GPU out of memory; retried automatically at lower resolution);
  - `TIMEOUT` (per stage).

### 6.5 Unity side

| Item | Purpose |
|---|---|
| `ScanCaptureSession` (new) | Frame picking, coverage ring, frame counter |
| `ReconstructionClient` (new) | Upload with retries, check status every 10 s with back-off, remember the job ID in PlayerPrefs so it survives an app restart |
| `ModelViewer` (new) | Load the GLB with **glTFast** (`com.unity.cloud.gltfast`; pick a version that supports 2022.3 and verify its licence), with orbit, zoom and height/width labels |
| "My scans" list | Thumbnails, status, delete |
| Procedural twin | **Kept** for disease simulation. The scan is shown as "Your plant (3D scan)" alongside it; projecting disease masks onto the scan comes later |

### 6.6 Depth fallback (feature 8)

On a phone without ARCore, the server-side depth model from Phase 1 **is** the depth source, so nothing extra is needed. Running it on the phone is a later option:

- Unity Sentis / Inference Engine: verify compatibility with Unity 2022.3, since newer versions target Unity 6;
- ONNX Runtime Mobile through a native plugin.

### 6.7 Phase 3 is done when

A potted plant scanned with the target phone produces a scaled GLB within 30 minutes, the height in `measurements.json` is within 5% of a tape measure, and the model opens in the app on the target phone.

---

## 7. Hosting and running the server

### Options

| Option | Cost | Good for | Limits |
|---|---|---|---|
| **This laptop** | Free | Development and a pilot | Laptop must be on; LAN only unless tunnelled |
| Laptop + **Cloudflare Tunnel** or **Tailscale** | Free tiers | Using the phone outside home Wi-Fi; no router port-forwarding | Still depends on the laptop |
| Rented GPU machine (RunPod, Vast.ai, Lambda, AWS g5, GCP L4) | Paid per hour while running | Demos, batch processing | Must be started and stopped |
| **Cheap CPU server for the API + serverless GPU** (Modal / RunPod Serverless) for 3D jobs | A few €/month + per-second GPU billing | **Production with low or bursty usage** | Cold starts (tens of seconds); the worker image must be adapted to the provider |

**Recommendation:** develop on the laptop, pilot through Cloudflare Tunnel or Tailscale, then run production as a CPU server (API, Redis, storage, Caddy) plus serverless GPU workers. Check current prices when choosing.

### Laptop runbook

1. Start Docker Desktop.
2. `cd E:\plant\server`, then `docker compose up --build`. The first build of the GPU worker is long (COLMAP and OpenMVS compile).
3. Open `http://localhost:8000/docs` and check that `/v1/health` responds.
4. Find the PC's address with `ipconfig` (Wi-Fi IPv4).
5. Allow the port through the Windows firewall (run once, as admin):
   `New-NetFirewallRule -DisplayName "PlantDoctor API" -Direction Inbound -LocalPort 8000 -Protocol TCP -Action Allow`
6. On the phone, in the same Wi-Fi network: **Settings → Server AI → `http://<PC IP>:8000/v1/analyze` → Test connection.**
7. Watch progress with `docker compose logs -f worker`.
8. Stop with `docker compose down`. Data stays in `server/data/`.

### Production checklist

- HTTPS through Caddy, API key, rate limit and upload limits.
- Postgres instead of SQLite; S3/R2 instead of the local volume.
- A retention job and database backups.
- A time limit and retry per stage.
- Monitoring: `/v1/health`, queue length, disk use, failed-job rate.
- A version endpoint, so the app can warn when it's too old for the server.
- A privacy policy covering uploads and retention.

---

## 8. Order of work

Sizes are for one developer: S = days, M = 1–2 weeks, L = 2–4 weeks.

| # | Milestone | Size | Depends on |
|---|---|---|---|
| M0 | Phase 0: Docker + GPU, code layout, Redis/RQ, test job, pytest | S–M | — |
| M1 | Scale card PDF + `vision/scale_card.py` + `/v1/measure/leaf` (card, colour correction, depth mask) | M | M0 |
| M2 | Unity Phase 1: NativeCamera, intrinsics, `MeasureService`, tracer mask overload, mm² UI | M | M1 |
| M3 | Phase 1 validation (printed shapes, 20 leaves, tracer evaluation) | S | M2 |
| M4 | Phase 2 Method A: `/v1/measure/plant`, tilt warning, growth history | S–M | M1 |
| **M5** | **3D de-risk spike (§6.1), then `recon_pipeline.py` as a command-line tool** | **L** | M0 |
| M6 | Reconstruction API + worker + Unity capture, upload and viewer | L | M5 |
| M7 | Measurements from the mesh (Method B) and growth tracking from scans | M | M6 |
| M8 | Hosting: tunnel pilot, then production setup (§7) | M | M3 / M6 |
| M9 (optional) | gsplat models, disease projection onto the scan, live on-device card detection | L | M6 |

**Tip:** run the M5 spike early, even in parallel with M1, because it carries the most risk.

---

## 9. Risks and mitigations

| Risk | Mitigation |
|---|---|
| Users don't have or won't use the card | "Print card" button in the app; A4-sheet fallback; percentages without a reference |
| Wind or moving leaves ruin the 3D model | Capture tips, a "calm air" prompt, `SFM_FAILED` with a clear retry message |
| Repetitive leaves or plain backgrounds stop photos connecting in SfM | Keep a textured background, the card helps, more overlap (slower walk), exhaustive matching |
| Cheap phone cameras (focus hunting, noise, low light) | On-device quality gate, server frame filter, a "more light" prompt |
| Upload size on mobile data | JPEG quality 90, at most 150 frames, Wi-Fi-only option, resumable uploads |
| Blackwell GPU build problems | Pin CUDA 12.8+ images; COLMAP's CPU feature extraction as fallback; serverless GPUs as an alternative |
| GPU memory (8 GB) | Downscale frames to about 1600 px; `GPU_OOM` retries at lower resolution |
| Licences | See §10 |
| Server cost or downtime | Job queue, serverless GPUs, app still works offline with fewer features |
| Privacy (frames may show people or homes) | Upload notice, retention limits, delete endpoint |

---

## 10. Licence notes (verify before shipping)

| Component | Licence | Note |
|---|---|---|
| COLMAP, GLOMAP | BSD-3 | OK |
| **OpenMVS** | **AGPL-3.0** | Fine for research and private use. If modified and offered as a network service, the source must be shared. **Get a review before commercial use**, or swap in COLMAP's own dense reconstruction and Poisson meshing |
| gsplat | Apache-2.0 | OK. **Avoid the original Inria 3DGS code (non-commercial)**; also check 2DGS / SuGaR licences |
| Depth Anything V2 **Small** | Apache-2.0 | OK. Base, Large and Giant are **non-commercial** |
| OpenCV (incl. ArUco), Open3D, trimesh | Apache-2.0 / MIT / MIT | OK |
| NativeCamera (yasirkula) | MIT (verify) | OK |
| glTFast | Verify (Unity package licence) | — |
| PyTorch, onnxruntime, FastAPI, Redis, RQ | BSD / MIT / MIT / BSD-3 or RSAL (by Redis version) / BSD | Check the Redis licence for the version you pin (or use Valkey) |

---

## 11. Not possible on these phones (be upfront in the app)

- Live depth or AR occlusion; a live 3D preview while capturing.
- Sub-mm geometry, leaf hairs (trichomes), and disease before symptoms show (that needs multispectral or thermal cameras).
- Instant 3D results: a scan takes about 10–30 minutes on the server.
