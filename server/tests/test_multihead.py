"""
The multi-head model path (training/ export format): gates, species masking and top-3.

Builds a tiny stand-in ONNX model whose outputs are fixed biases, so each test controls
exactly what the "model" says. Skipped when onnx / onnxruntime are not installed.
"""
import io
import json
import os

import pytest

os.environ["PLANTDOCTOR_API_KEY"] = ""
onnx = pytest.importorskip("onnx")
pytest.importorskip("onnxruntime")
np = pytest.importorskip("numpy")
from onnx import TensorProto, helper  # noqa: E402

import inference  # noqa: E402

CONDITIONS = ["Tomato___Early_blight", "Tomato___Late_blight", "Tomato___healthy", "Other___unknown_disease",
              "Reject___not_a_leaf", "Potato___Early_blight", "Any___Abiotic_stress"]
SPECIES = ["tomato", "potato", "pepper", "apple", "other"]
SIGNS = ["uniform_yellowing", "interveinal_yellowing", "edge_scorch", "spots_lesions", "target_rings", "wilting",
         "leaf_curl", "stippling", "powdery_coating", "mosaic_mottling", "purpling"]
QUALITY = ["good", "blurry", "too_dark", "too_bright", "too_small"]
ALLOWED = {"tomato": [0, 1, 2, 3, 4, 6], "potato": [5, 3, 4, 6], "pepper": [3, 4, 6], "apple": [3, 4, 6],
           "other": [3, 4, 6]}


def build_model(path, species, condition, quality, signs=None, severity=0.4):
    """image -> mean (N,1) -> each output = mean * 0 + fixed bias (broadcast to the batch)."""
    signs = signs if signs is not None else [0.1] * len(SIGNS)
    nodes = [helper.make_node("ReduceMean", ["image"], ["m"], axes=[1, 2, 3], keepdims=0),
             helper.make_node("Unsqueeze", ["m", "ax"], ["m2"]),
             helper.make_node("Mul", ["m2", "zero"], ["z"])]
    inits = [helper.make_tensor("ax", TensorProto.INT64, [1], [1]),
             helper.make_tensor("zero", TensorProto.FLOAT, [], [0.0])]
    outputs = []
    for name, values in [("species_logits", species), ("condition_logits", condition), ("signs", signs),
                         ("severity", [severity]), ("quality_logits", quality)]:
        inits.append(helper.make_tensor(name + "_b", TensorProto.FLOAT, [len(values)], values))
        nodes.append(helper.make_node("Add", ["z", name + "_b"], [name if name != "severity" else "sev2"]))
        if name == "severity":
            nodes.append(helper.make_node("Squeeze", ["sev2", "ax"], ["severity"]))
            outputs.append(helper.make_tensor_value_info("severity", TensorProto.FLOAT, ["batch"]))
        else:
            outputs.append(helper.make_tensor_value_info(name, TensorProto.FLOAT, ["batch", len(values)]))
    graph = helper.make_graph(nodes, "stub", [helper.make_tensor_value_info("image", TensorProto.FLOAT, ["batch", 3, 224, 224])],
                              outputs, inits)
    model = helper.make_model(graph, opset_imports=[helper.make_opsetid("", 17)])
    model.ir_version = 8
    onnx.save(model, str(path))


def load(tmp_path, monkeypatch, **outs):
    build_model(tmp_path / "m.onnx", **outs)
    (tmp_path / "labels.json").write_text(json.dumps(
        {"conditions": CONDITIONS, "species": SPECIES, "signs": SIGNS, "quality": QUALITY}))
    (tmp_path / "model_meta.json").write_text(json.dumps(
        {"model_version": "stub-1", "temperature": 1.0, "energy_threshold": 5.0, "quality_threshold": 0.6,
         "allowed_conditions": ALLOWED}))
    monkeypatch.setattr(inference, "MODEL_PATH", str(tmp_path / "m.onnx"))
    monkeypatch.setattr(inference, "LABELS_PATH", str(tmp_path / "labels.json"))
    monkeypatch.setattr(inference, "META_PATH", str(tmp_path / "model_meta.json"))
    model = inference.DiseaseModel()
    assert model.is_loaded and model.multi_head
    return model


def jpeg():
    from PIL import Image
    buf = io.BytesIO()
    Image.new("RGB", (64, 64), (40, 140, 50)).save(buf, "JPEG")
    return buf.getvalue()


GOOD = [6.0, 0, 0, 0, 0]
TOMATO = [6.0, 0, 0, 0, 0]


def test_top3_comes_from_the_identified_species_only(tmp_path, monkeypatch):
    # Potato early blight has the highest raw logit, but the species head says tomato.
    m = load(tmp_path, monkeypatch, species=TOMATO, condition=[3.0, 2.0, 1.0, -2, -4, 9.0, 0.0], quality=GOOD)
    p = m.predict(jpeg())
    assert (p.plant_id, p.disease_id) == ("tomato", "early_blight")
    assert [c["label"] for c in p.candidates] == ["Tomato___Early_blight", "Tomato___Late_blight", "Tomato___healthy"]
    assert abs(sum(c["confidence"] for c in p.candidates) - 1.0) < 0.1
    assert p.severity == pytest.approx(0.4, abs=1e-3)
    assert set(p.features) == {"leaf_color", "leaf_spots", "yellowing", "wilting"}


def test_blurry_photo_is_rejected_with_its_reason(tmp_path, monkeypatch):
    m = load(tmp_path, monkeypatch, species=TOMATO, condition=[3.0, 0, 0, 0, 0, 0, 0], quality=[0, 4.0, 0, 0, 0])
    with pytest.raises(inference.ImageUnusable) as e:
        m.predict(jpeg())
    assert e.value.reason == "blurry"


def test_not_a_leaf_is_rejected(tmp_path, monkeypatch):
    m = load(tmp_path, monkeypatch, species=TOMATO, condition=[0, 0, 0, 0, 5.0, 0, 0], quality=GOOD)
    with pytest.raises(inference.ImageUnusable) as e:
        m.predict(jpeg())
    assert e.value.reason == "not_a_leaf"


def test_unfamiliar_image_is_rejected_by_energy(tmp_path, monkeypatch):
    # All logits very negative = low energy confidence -> out of distribution.
    m = load(tmp_path, monkeypatch, species=TOMATO, condition=[-9.0] * 7, quality=GOOD)
    with pytest.raises(inference.ImageUnusable):
        m.predict(jpeg())


def test_other_plant_is_unsupported(tmp_path, monkeypatch):
    m = load(tmp_path, monkeypatch, species=[0, 0, 0, 0, 6.0], condition=[0, 0, 0, 4.0, 0, 0, 0], quality=GOOD)
    with pytest.raises(inference.UnsupportedPlant):
        m.predict(jpeg())


def test_abiotic_stress_is_reported_on_the_identified_plant(tmp_path, monkeypatch):
    m = load(tmp_path, monkeypatch, species=[0, 6.0, 0, 0, 0], condition=[0, 0, 0, 0, 0, 1.0, 4.0], quality=GOOD)
    p = m.predict(jpeg())
    assert (p.plant_id, p.disease_id) == ("potato", "abiotic_stress")


def test_api_returns_candidates_and_422_for_bad_photos(tmp_path, monkeypatch):
    from fastapi.testclient import TestClient
    import main

    client = TestClient(main.app)
    monkeypatch.setattr(main, "MODEL", load(tmp_path, monkeypatch, species=TOMATO,
                                            condition=[3.0, 2.0, 1.0, -2, -4, 0, 0], quality=GOOD))
    r = client.post("/v1/analyze", files={"image": ("leaf.jpg", jpeg(), "image/jpeg")})
    assert r.status_code == 200, r.text
    body = r.json()
    assert body["schema_version"] == "1.1" and len(body["candidates"]) == 3
    assert body["quality"]["label"] == "good"

    monkeypatch.setattr(main, "MODEL", load(tmp_path, monkeypatch, species=TOMATO,
                                            condition=[3.0, 0, 0, 0, 0, 0, 0], quality=[0, 0, 4.0, 0, 0]))
    r = client.post("/v1/analyze", files={"image": ("leaf.jpg", jpeg(), "image/jpeg")})
    assert r.status_code == 422 and r.json()["error_code"] == "IMAGE_UNUSABLE"
    assert "dark" in r.json()["message"].lower()
