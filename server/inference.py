"""
Model loading and inference.

This file deliberately does NOT invent predictions. Until a trained checkpoint is
placed at MODEL_PATH the server reports MODEL_UNAVAILABLE, so Unity falls back to
Demo Mode rather than displaying a fabricated diagnosis.

To go live, train the model in training/ (see training/README.md) and run
  python -m plantdoc_ai.export_onnx --run <run> --install
which writes models/plant_disease.onnx, models/labels.json and models/model_meta.json.

Two model formats are understood:
  * multi-head (training/): outputs species, condition, signs, severity and quality;
    labels.json is an object and model_meta.json carries the calibration. Photos are
    checked by the quality head (Gate 2) and the unknown-image test (Gate 3), and the
    top 3 candidates are returned.
  * legacy single-output classifier: labels.json is a list of "Plant___disease" names.
"""
from __future__ import annotations

import json
import logging
import os
from dataclasses import dataclass
from typing import Dict, List, Optional, Tuple

log = logging.getLogger("plantdoctor.inference")

MODEL_PATH = os.getenv("PLANTDOCTOR_MODEL", "models/plant_disease.onnx")
LABELS_PATH = os.getenv("PLANTDOCTOR_LABELS", "models/labels.json")
META_PATH = os.getenv("PLANTDOCTOR_META", os.path.join(os.path.dirname(MODEL_PATH) or ".", "model_meta.json"))
INPUT_SIZE = int(os.getenv("PLANTDOCTOR_INPUT_SIZE", "224"))


class ModelUnavailable(RuntimeError):
    """Raised when no trained model is installed."""


class ImageUnusable(ValueError):
    """The photo failed a quality or 'is this a leaf' check; the user should retake it."""

    MESSAGES = {
        "blurry": "Too blurry. Hold the phone steady and tap to focus.",
        "too_dark": "Too dark. Find more light.",
        "too_bright": "Too bright. Avoid direct glare on the leaf.",
        "too_small": "Move closer so one leaf fills the frame.",
        "not_a_leaf": "That doesn't look like a leaf we know. Point at one leaf and try again.",
    }

    def __init__(self, reason: str):
        self.reason = reason
        super().__init__(self.MESSAGES.get(reason, "Please retake the photo."))


class UnsupportedPlant(ValueError):
    """The leaf is from a plant (or shows a disease) the model was not trained on."""


@dataclass
class Prediction:
    plant_id: str
    plant_name: str
    disease_id: Optional[str]
    disease_name: Optional[str]
    confidence: float
    severity: float
    is_healthy: bool
    features: Dict[str, float]
    regions: List[dict]
    model_version: str
    candidates: Optional[List[dict]] = None   # top 3: {"label", "confidence"}
    quality: Optional[dict] = None            # {"label", "score"}
    label: Optional[str] = None               # the model's own class name, e.g. "Tomato___Early_blight"


class DiseaseModel:
    """Thin wrapper over an ONNX Runtime session."""

    def __init__(self) -> None:
        self.session = None
        self.labels: List[str] = []
        self.multi_head = False
        self.meta: dict = {}
        self.species: List[str] = []
        self.signs: List[str] = []
        self.quality_labels: List[str] = []
        self.model_version = "not-loaded"
        self._try_load()

    @property
    def is_loaded(self) -> bool:
        return self.session is not None and bool(self.labels)

    def _try_load(self) -> None:
        if not os.path.exists(MODEL_PATH) or not os.path.exists(LABELS_PATH):
            log.warning("No model at %s — server will report MODEL_UNAVAILABLE.", MODEL_PATH)
            return
        try:
            import onnxruntime as ort  # imported lazily so the API runs without it

            session = ort.InferenceSession(MODEL_PATH, providers=["CPUExecutionProvider"])
            with open(LABELS_PATH, "r", encoding="utf-8") as fh:
                labels = json.load(fh)
            outputs = {o.name: o.shape for o in session.get_outputs()}
            if isinstance(labels, dict):                       # multi-head model from training/
                with open(META_PATH, "r", encoding="utf-8") as fh:
                    meta = json.load(fh)
                expected = {"species_logits": labels["species"], "condition_logits": labels["conditions"],
                            "signs": labels["signs"], "quality_logits": labels["quality"]}
                for name, names in expected.items():
                    if name not in outputs or outputs[name][-1] != len(names):
                        raise ValueError(f"output {name} does not match labels.json ({len(names)} labels)")
                self.meta = meta
                self.labels, self.species = labels["conditions"], labels["species"]
                self.signs, self.quality_labels = labels["signs"], labels["quality"]
                self.multi_head = True
                self.model_version = meta.get("model_version", os.path.basename(MODEL_PATH))
            else:
                size = list(outputs.values())[0][-1]
                if isinstance(size, int) and size != len(labels):
                    raise ValueError("model output size does not match labels.json")
                self.labels = labels
                self.model_version = os.path.basename(MODEL_PATH)
            self.session = session
            log.info("Loaded model %s with %d labels (%s).", self.model_version, len(self.labels),
                     "multi-head" if self.multi_head else "single output")
        except Exception:                       # noqa: BLE001 - report, never crash the API
            log.exception("Model failed to load; continuing without inference.")
            self.session = None

    # -- preprocessing -------------------------------------------------
    def preprocess(self, image_bytes: bytes):
        import numpy as np
        from PIL import Image
        import io

        img = Image.open(io.BytesIO(image_bytes)).convert("RGB").resize((INPUT_SIZE, INPUT_SIZE))
        arr = np.asarray(img, dtype="float32") / 255.0
        mean = np.array([0.485, 0.456, 0.406], dtype="float32")
        std = np.array([0.229, 0.224, 0.225], dtype="float32")
        arr = (arr - mean) / std
        return np.transpose(arr, (2, 0, 1))[None, ...], img

    # -- inference -----------------------------------------------------
    def predict(self, image_bytes: bytes) -> Prediction:
        if not self.is_loaded:
            raise ModelUnavailable("No trained model is installed on this server.")

        import numpy as np

        tensor, pil_image = self.preprocess(image_bytes)
        if self.multi_head:
            return self._predict_multi_head(tensor)
        outputs = self.session.run(None, {self.session.get_inputs()[0].name: tensor})
        logits = np.asarray(outputs[0]).reshape(-1)
        exp = np.exp(logits - logits.max())
        probs = exp / exp.sum()
        idx = int(probs.argmax())
        confidence = float(probs[idx])

        label = self.labels[idx]                       # e.g. "Tomato___Early_blight"
        plant_id, disease_id, is_healthy = split_label(label)
        severity, features, regions = estimate_severity(pil_image, is_healthy)

        return Prediction(
            label=label,
            plant_id=plant_id,
            plant_name=plant_id.replace("_", " ").title(),
            disease_id=None if is_healthy else disease_id,
            disease_name=None if is_healthy else disease_id.replace("_", " ").title(),
            confidence=confidence,
            severity=0.0 if is_healthy else severity,
            is_healthy=is_healthy,
            features=features,
            regions=regions,
            model_version=self.model_version,
        )


    # -- multi-head model (memo Section 10: server changes 1-5) ----------
    def _predict_multi_head(self, tensor) -> Prediction:
        import numpy as np

        names = ["species_logits", "condition_logits", "signs", "severity", "quality_logits"]
        species_logits, cond_logits, signs, severity, quality_logits = (
            np.asarray(o)[0] for o in self.session.run(names, {"image": tensor}))

        # Gate 2: is the photo good enough?
        q = _softmax(quality_logits)
        good = float(q[self.quality_labels.index("good")])
        if good < float(self.meta.get("quality_threshold", 0.6)):
            worst = max((i for i, n in enumerate(self.quality_labels) if n != "good"), key=lambda i: q[i])
            raise ImageUnusable(self.quality_labels[worst])

        # Gate 3: a leaf we know? (the reject class, or out-of-distribution by energy)
        reject = self.labels.index("Reject___not_a_leaf")
        top_logit = float(cond_logits.max())
        energy = -(top_logit + float(np.log(np.exp(cond_logits - top_logit).sum())))
        threshold = self.meta.get("energy_threshold")
        if int(cond_logits.argmax()) == reject or (threshold is not None and energy > float(threshold)):
            raise ImageUnusable("not_a_leaf")

        # Species first, then only that species' conditions (plus the shared ones).
        species = self.species[int(species_logits.argmax())]
        allowed = [i for i in self.meta["allowed_conditions"][species] if i != reject]
        temperature = float(self.meta.get("temperature", 1.0))
        p = _softmax(cond_logits[allowed] / temperature)
        order = np.argsort(-p)
        top = [allowed[i] for i in order[:3]]
        label = self.labels[top[0]]
        if label == "Other___unknown_disease" or species == "other":
            raise UnsupportedPlant("This plant or disease isn't one the model knows yet.")

        plant_id, disease_id, is_healthy = split_label(label)
        if plant_id == "any":                     # Any___Abiotic_stress belongs to the identified species
            plant_id = species
        sign_values = {n: float(v) for n, v in zip(self.signs, signs)}
        return Prediction(
            label=label,
            plant_id=plant_id,
            plant_name=plant_id.replace("_", " ").title(),
            disease_id=None if is_healthy else disease_id,
            disease_name=None if is_healthy else disease_id.replace("_", " ").title(),
            confidence=float(p[order[0]]),
            severity=0.0 if is_healthy else round(float(np.asarray(severity).reshape(-1)[0]), 3),
            is_healthy=is_healthy,
            features=app_features(sign_values),
            regions=[],
            model_version=self.model_version,
            candidates=[{"label": self.labels[i], "confidence": round(float(p[j]), 4)}
                        for i, j in zip(top, order[:3])],
            quality={"label": "good", "score": round(good, 3)},
        )


def _softmax(x):
    import numpy as np
    e = np.exp(x - np.max(x))
    return e / e.sum()


def app_features(signs: Dict[str, float]) -> Dict[str, float]:
    """The 11 visual signs folded into the four features the Unity app shows
    (kept in step with training/plantdoc_ai/taxonomy.py: app_features)."""
    yellow = max(signs.get("uniform_yellowing", 0.0), signs.get("interveinal_yellowing", 0.0))
    return {
        "leaf_color": round(1.0 - max(yellow, signs.get("edge_scorch", 0.0), signs.get("purpling", 0.0)), 3),
        "leaf_spots": round(max(signs.get("spots_lesions", 0.0), signs.get("stippling", 0.0)), 3),
        "yellowing": round(yellow, 3),
        "wilting": round(max(signs.get("wilting", 0.0), signs.get("leaf_curl", 0.0) * 0.5), 3),
    }


# Dataset plant names -> the app's plant ids (PlantVillage writes "Pepper,_bell").
PLANT_ALIASES = {
    "pepper,_bell": "pepper",
    "pepper_bell": "pepper",
    "bell_pepper": "pepper",
    "capsicum": "pepper",
}


def split_label(label: str) -> Tuple[str, str, bool]:
    """'Tomato___Early_blight' -> ('tomato', 'early_blight', False)."""
    parts = label.split("___") if "___" in label else [label, "healthy"]
    plant = parts[0].strip().lower().replace(" ", "_")
    plant = PLANT_ALIASES.get(plant, plant)
    disease = parts[1].strip().lower().replace(" ", "_")
    return plant, disease, disease in ("healthy", "background")


def estimate_severity(pil_image, is_healthy: bool):
    """
    Colour-space lesion/chlorosis estimate. This is an explicit, inspectable
    heuristic used for severity and visual features until a dedicated severity
    model is trained — it is reported to the client as a visual AI estimate.
    """
    import numpy as np

    arr = np.asarray(pil_image, dtype="float32") / 255.0
    r, g, b = arr[..., 0], arr[..., 1], arr[..., 2]

    green_mask = (g > r * 1.05) & (g > b * 1.05)
    yellow_mask = (r > 0.45) & (g > 0.45) & (b < 0.4) & ~green_mask
    brown_mask = (r > 0.25) & (r < 0.65) & (g < r * 0.85) & (b < g)

    total = float(arr.shape[0] * arr.shape[1]) or 1.0
    green_frac = float(green_mask.sum()) / total
    yellow_frac = float(yellow_mask.sum()) / total
    lesion_frac = float(brown_mask.sum()) / total

    features = {
        "leaf_color": round(min(1.0, green_frac / 0.75), 3),
        "leaf_spots": round(min(1.0, lesion_frac * 2.5), 3),
        "yellowing": round(min(1.0, yellow_frac * 2.5), 3),
        "wilting": 0.0,   # not visually derivable from a single still with confidence
    }
    severity = 0.0 if is_healthy else round(min(1.0, 0.6 * features["leaf_spots"] + 0.4 * features["yellowing"]), 3)
    regions = []
    return severity, features, regions


MODEL = DiseaseModel()
