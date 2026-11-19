"""
Minimal pytest suite for the inference API.

Run:  cd server && pytest

These tests intentionally do not require a trained model — the
MODEL_UNAVAILABLE path is exercised on purpose, matching inference.py's
"never fabricate a diagnosis" contract.
"""
import os

# Deterministic environment regardless of the developer's shell or a local
# .env file, so these tests are not flaky.
os.environ["PLANTDOCTOR_API_KEY"] = ""
os.environ["PLANTDOCTOR_RATE_LIMIT_PER_MINUTE"] = "60"

from fastapi.testclient import TestClient

import main as main_module

client = TestClient(main_module.app)


def _tiny_png_bytes() -> bytes:
    # 1x1 white PNG — valid enough to pass the content-type/size checks.
    return bytes.fromhex(
        "89504e470d0a1a0a0000000d4948445200000001000000010802000000907753"
        "de0000000c4944415478da6360606060000000050001a5f645400000000049454e44ae426082"
    )


def test_health_is_open_without_api_key():
    resp = client.get("/v1/health")
    assert resp.status_code == 200
    body = resp.json()
    assert body["status"] == "ok"
    assert "model_loaded" in body


def test_deprecated_unprefixed_health_alias_still_works():
    resp = client.get("/health")
    assert resp.status_code == 200


def test_analyze_rejects_non_image_content_type():
    resp = client.post("/v1/analyze", files={"image": ("leaf.txt", b"not an image", "text/plain")})
    assert resp.status_code == 415
    assert resp.json()["error_code"] == "INVALID_IMAGE"


def test_analyze_rejects_empty_image():
    resp = client.post("/v1/analyze", files={"image": ("leaf.png", b"", "image/png")})
    assert resp.status_code == 400
    assert resp.json()["error_code"] == "INVALID_IMAGE"


def test_analyze_reports_model_unavailable_without_a_trained_checkpoint():
    resp = client.post("/v1/analyze", files={"image": ("leaf.png", _tiny_png_bytes(), "image/png")})
    assert resp.status_code == 503
    assert resp.json()["error_code"] == "MODEL_UNAVAILABLE"


def test_api_key_enforced_when_configured(monkeypatch):
    monkeypatch.setattr(main_module, "API_KEY", "secret123")

    resp = client.post("/v1/analyze", files={"image": ("leaf.png", _tiny_png_bytes(), "image/png")})
    assert resp.status_code == 401

    resp = client.post(
        "/v1/analyze",
        files={"image": ("leaf.png", _tiny_png_bytes(), "image/png")},
        headers={"X-API-Key": "secret123"},
    )
    # Still 503 (no model installed) — the point here is that auth let the
    # request reach the handler at all.
    assert resp.status_code == 503


def test_rate_limiter_blocks_after_the_configured_limit(monkeypatch):
    monkeypatch.setattr(main_module, "_rate_limiter", main_module.SlidingWindowRateLimiter(limit_per_minute=2))

    ok1 = client.get("/v1/health")
    ok2 = client.get("/v1/health")
    blocked = client.get("/v1/health")

    assert ok1.status_code == 200
    assert ok2.status_code == 200
    assert blocked.status_code == 429
    assert blocked.json()["error_code"] == "RATE_LIMITED"

def test_split_label_maps_dataset_names_to_app_plant_ids():
    from inference import split_label

    assert split_label("Tomato___Early_blight") == ("tomato", "early_blight", False)
    assert split_label("Potato___healthy") == ("potato", "healthy", True)
    assert split_label("Pepper,_bell___Bacterial_spot") == ("pepper", "bacterial_spot", False)
    assert split_label("Apple___Cedar_apple_rust") == ("apple", "cedar_apple_rust", False)
