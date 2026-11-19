"""
AI Plant Doctor — inference API.

Run: uvicorn main:app --host 0.0.0.0 --port 8000 --reload
Docs: http://localhost:8000/docs

Configuration (env vars — optionally loaded from a .env file, see .env.example):
    PLANTDOCTOR_API_KEY               If set, all /v1/* routes except /v1/health
                                       require header "X-API-Key: <value>".
                                       If unset, auth is DISABLED (local/dev mode).
    PLANTDOCTOR_RATE_LIMIT_PER_MINUTE Max requests per client IP per rolling 60s
                                       window, across all routes. Default 60.
                                       Set to 0 to disable.
    PLANTDOCTOR_MODEL, PLANTDOCTOR_LABELS, PLANTDOCTOR_INPUT_SIZE — see inference.py.
"""
from __future__ import annotations

import logging
import os
import time
from collections import defaultdict, deque
from typing import Deque, Dict

try:
    # Optional: lets `.env` populate os.environ. The server runs fine without
    # python-dotenv installed — env vars can always be set by the shell/host.
    from dotenv import load_dotenv
    load_dotenv()
except ImportError:  # pragma: no cover - exercised only when the package is absent
    pass

from fastapi import APIRouter, Depends, FastAPI, File, Form, HTTPException, Request, UploadFile
from fastapi.responses import JSONResponse
from fastapi.security import APIKeyHeader

from content import CONTENT
from inference import MODEL, ImageUnusable, ModelUnavailable, UnsupportedPlant
from schema import (AnalysisResponse, Candidate, ErrorResponse, FeatureReading, FeatureStatus, QualityInfo,
                    Recommendation)

logging.basicConfig(level=logging.INFO, format="%(levelname)s %(name)s: %(message)s")
log = logging.getLogger("plantdoctor.api")

app = FastAPI(
    title="AI Plant Doctor Inference API",
    version="1.0.0",
    description="Plant identification, disease classification and severity estimation for the Unity AR/VR simulator.",
)

MAX_IMAGE_BYTES = 8 * 1024 * 1024
OPTIMAL = {"leaf_color": 0.95, "leaf_spots": 0.05, "yellowing": 0.10, "wilting": 0.05}
HIGHER_IS_BETTER = {"leaf_color"}

# ---------------------------------------------------------------------------
# Config
# ---------------------------------------------------------------------------
API_KEY = os.getenv("PLANTDOCTOR_API_KEY", "").strip()
RATE_LIMIT_PER_MINUTE = int(os.getenv("PLANTDOCTOR_RATE_LIMIT_PER_MINUTE", "60"))

if not API_KEY:
    log.warning(
        "PLANTDOCTOR_API_KEY is not set — API key authentication is DISABLED. "
        "Set it before exposing this server outside a trusted network."
    )


def _error(code: str, message: str, status: int) -> JSONResponse:
    return JSONResponse(status_code=status, content=ErrorResponse(error_code=code, message=message).model_dump())


# ---------------------------------------------------------------------------
# Auth — a single shared key for the Unity client. Swap for per-client keys /
# OAuth later without touching route handlers, since they only depend on
# require_api_key.
# ---------------------------------------------------------------------------
_api_key_header = APIKeyHeader(name="X-API-Key", auto_error=False)


def require_api_key(supplied: str | None = Depends(_api_key_header)) -> None:
    """No-op when PLANTDOCTOR_API_KEY is unset (local/dev mode)."""
    if not API_KEY:
        return
    if supplied != API_KEY:
        raise HTTPException(status_code=401, detail="Missing or invalid API key.")


# ---------------------------------------------------------------------------
# Rate limiting — simple in-process sliding window, no external dependency.
# Adequate for a single-process inference server. Behind multiple workers or
# instances, replace with a shared store (e.g. Redis) behind the same
# .allow(client_key) interface; nothing else needs to change.
# ---------------------------------------------------------------------------
class SlidingWindowRateLimiter:
    def __init__(self, limit_per_minute: int) -> None:
        self.limit = limit_per_minute
        self._hits: Dict[str, Deque[float]] = defaultdict(deque)

    def allow(self, client_key: str) -> bool:
        if self.limit <= 0:
            return True
        now = time.monotonic()
        window = self._hits[client_key]
        while window and now - window[0] > 60.0:
            window.popleft()
        if len(window) >= self.limit:
            return False
        window.append(now)
        return True


_rate_limiter = SlidingWindowRateLimiter(RATE_LIMIT_PER_MINUTE)


@app.middleware("http")
async def rate_limit_middleware(request: Request, call_next):
    client_key = request.client.host if request.client else "unknown"
    if not _rate_limiter.allow(client_key):
        return _error("RATE_LIMITED", "Too many requests. Please slow down.", 429)
    return await call_next(request)


# ---------------------------------------------------------------------------
# Shared analyze implementation, used by both the versioned and deprecated
# unprefixed routes so behaviour is identical either way.
# ---------------------------------------------------------------------------
async def _analyze_impl(image: UploadFile, client_version: str):
    if image.content_type not in ("image/jpeg", "image/png"):
        return _error("INVALID_IMAGE", "Upload a JPEG or PNG image.", 415)

    payload = await image.read()
    if not payload:
        return _error("INVALID_IMAGE", "The uploaded image was empty.", 400)
    if len(payload) > MAX_IMAGE_BYTES:
        return _error("INVALID_IMAGE", "Image exceeds the 8 MB limit.", 400)

    try:
        prediction = MODEL.predict(payload)
    except ModelUnavailable as exc:
        log.warning("Inference requested but no model is installed.")
        return _error("MODEL_UNAVAILABLE", str(exc), 503)
    except ImageUnusable as exc:
        log.info("Photo rejected: %s", exc.reason)
        return _error("IMAGE_UNUSABLE", str(exc), 422)
    except UnsupportedPlant as exc:
        return _error("UNSUPPORTED_PLANT", str(exc), 422)
    except Exception:  # noqa: BLE001
        log.exception("Inference failed.")
        return _error("MODEL_ERROR", "Inference failed on the server.", 500)

    features = {}
    for key, value in prediction.features.items():
        optimal = OPTIMAL.get(key, 0.5)
        abnormal = (optimal - value) > 0.10 if key in HIGHER_IS_BETTER else (value - optimal) > 0.10
        features[key] = FeatureReading(
            value=max(0.0, min(1.0, value)),
            optimal=optimal,
            status=FeatureStatus.abnormal if abnormal else FeatureStatus.normal,
        )

    # The content database knows every dataset's class names (PlantVillage, PlantSeg, PlantDoc, mango, sugarcane, chilli): use it to
    # turn the model's label into the app's plant and disease ids, and send that disease's content along.
    plant_id, disease_id, is_healthy = prediction.plant_id, prediction.disease_id, prediction.is_healthy
    plant_name, disease_name = prediction.plant_name, prediction.disease_name
    resolved = CONTENT.resolve_label(prediction.label, prediction.plant_id) if prediction.label else None
    if resolved:
        plant_id, disease_id = resolved
        is_healthy = disease_id == "healthy"
        if is_healthy:
            disease_id, disease_name = None, None
    plant_row = CONTENT.plant(plant_id)
    if plant_row:
        plant_name = plant_row["name"]
    content = CONTENT.disease(plant_id, disease_id) if (disease_id and not is_healthy) else None
    if content:
        disease_name = content["name"]

    treatment_id = None if is_healthy else f"{plant_id}_{disease_id}_treatment"

    warnings = []
    if prediction.confidence < 0.60:
        warnings.append("Low confidence: the client should present this as an uncertain result, not a diagnosis.")

    log.info(
        "client=%s plant=%s disease=%s conf=%.2f",
        client_version, prediction.plant_id, prediction.disease_id, prediction.confidence,
    )

    return AnalysisResponse(
        model_version=prediction.model_version,
        plant_id=plant_id,
        plant_name=plant_name,
        disease_id=disease_id,
        disease_name=disease_name,
        confidence=prediction.confidence,
        severity=prediction.severity,
        is_healthy=is_healthy,
        features=features,
        regions=prediction.regions,
        candidates=[Candidate(**c) for c in (prediction.candidates or [])],
        quality=QualityInfo(**prediction.quality) if prediction.quality else None,
        recommendation=Recommendation(treatment_id=treatment_id),
        warnings=warnings,
        content=content,
    )


# ---------------------------------------------------------------------------
# Versioned routes (/v1) — this is the API new clients should target.
# ---------------------------------------------------------------------------
v1 = APIRouter(prefix="/v1")


@v1.get("/health")
def health_v1():
    return {"status": "ok", "model_loaded": MODEL.is_loaded, "model_version": MODEL.model_version}


@v1.get("/labels", dependencies=[Depends(require_api_key)])
def labels_v1():
    return {"labels": MODEL.labels, "count": len(MODEL.labels)}


@v1.post(
    "/analyze",
    response_model=AnalysisResponse,
    responses={400: {"model": ErrorResponse}, 415: {"model": ErrorResponse}, 422: {"model": ErrorResponse},
               503: {"model": ErrorResponse}},
    dependencies=[Depends(require_api_key)],
)
async def analyze_v1(image: UploadFile = File(...), client_version: str = Form("unknown")):
    return await _analyze_impl(image, client_version)


@v1.get("/plants", dependencies=[Depends(require_api_key)])
def plants_v1():
    """Every plant in the content database."""
    return {"plants": CONTENT.plants(), "content_available": CONTENT.available}


@v1.get("/plants/{plant_id}", dependencies=[Depends(require_api_key)])
def plant_v1(plant_id: str):
    plant = CONTENT.plant(plant_id)
    if plant is None:
        raise HTTPException(status_code=404, detail=f"Unknown plant '{plant_id}'.")
    return plant


@v1.get("/content/{plant_id}/{disease_id}", dependencies=[Depends(require_api_key)])
def content_v1(plant_id: str, disease_id: str):
    """Everything the app teaches about one disease: description, symptoms, treatment, prevention, follow-ups, sources."""
    d = CONTENT.disease(plant_id, disease_id)
    if d is None:
        raise HTTPException(status_code=404, detail=f"No content for {plant_id}/{disease_id}.")
    return d


@v1.get("/resolve", dependencies=[Depends(require_api_key)])
def resolve_v1(label: str, plant: str | None = None):
    """A dataset class name (e.g. 'Tomato___Early_blight', 'soybean frog eye leaf spot', 'RedRot') -> plant and disease."""
    r = CONTENT.resolve_label(label, plant)
    if r is None:
        raise HTTPException(status_code=404, detail=f"Unknown label '{label}'.")
    return {"label": label, "plant_id": r[0], "disease_id": r[1]}


app.include_router(v1)


# ---------------------------------------------------------------------------
# Deprecated unprefixed aliases — kept ONLY so the current Unity
# RemoteAIService.cs (which calls /analyze directly) keeps working unchanged.
# Remove these once RemoteAIService.cs is updated to call /v1/analyze with the
# X-API-Key header (see note at the end of this batch).
# ---------------------------------------------------------------------------
@app.get("/health", include_in_schema=False)
def health():
    return health_v1()


@app.get("/labels", include_in_schema=False, dependencies=[Depends(require_api_key)])
def labels():
    return labels_v1()


@app.post(
    "/analyze",
    response_model=AnalysisResponse,
    include_in_schema=False,
    dependencies=[Depends(require_api_key)],
)
async def analyze(image: UploadFile = File(...), client_version: str = Form("unknown")):
    return await _analyze_impl(image, client_version)