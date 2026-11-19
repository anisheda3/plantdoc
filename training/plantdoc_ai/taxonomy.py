"""
Label taxonomy (memo Section 4): 42 condition classes, 5 species, 11 visual signs,
5 photo-quality classes. The order here IS the model's output order, so never reorder
a list after a model has been trained on it - append instead.

Condition labels use the "Plant___condition" format so the server's split_label() and
the app's DiseaseData.aiLabelAliases keep working unchanged.
"""
from __future__ import annotations

CONDITIONS: list[str] = [
    # Tomato (17) + shared catch-alls (memo table "Tomato and shared classes")
    "Tomato___Early_blight",
    "Tomato___Late_blight",
    "Tomato___Septoria_leaf_spot",
    "Tomato___Leaf_mold",
    "Tomato___Target_spot",
    "Tomato___Grey_leaf_spot",
    "Tomato___Powdery_mildew",
    "Tomato___Verticillium_wilt",
    "Tomato___Leaf_blight_unspecified",
    "Tomato___Bacterial_spot",
    "Tomato___Yellow_leaf_curl_virus",
    "Tomato___Mosaic_virus",
    "Tomato___Spider_mites",
    "Tomato___Leaf_miner",
    "Tomato___Chewing_insect_damage",
    "Any___Abiotic_stress",
    "Tomato___healthy",
    "Other___unknown_disease",
    "Reject___not_a_leaf",
    # Potato (8)
    "Potato___Early_blight",
    "Potato___Late_blight",
    "Potato___Bacterial_disease",
    "Potato___Viral_disease",
    "Potato___Fungal_leaf_spot_unspecified",
    "Potato___Pest_damage",
    "Potato___Nematode_damage",
    "Potato___healthy",
    # Pepper (7)
    "Pepper___Bacterial_spot",
    "Pepper___Cercospora_leaf_spot",
    "Pepper___Anthracnose",
    "Pepper___Leaf_curl_virus",
    "Pepper___Powdery_mildew",
    "Pepper___Nutrient_deficiency",
    "Pepper___healthy",
    # Apple (8)
    "Apple___Scab",
    "Apple___Rust",
    "Apple___Frog_eye_leaf_spot",
    "Apple___Powdery_mildew",
    "Apple___Alternaria_leaf_spot",
    "Apple___Mosaic_virus",
    "Apple___Multiple_diseases",
    "Apple___healthy",
]
assert len(CONDITIONS) == 42 and len(set(CONDITIONS)) == 42

SPECIES: list[str] = ["tomato", "potato", "pepper", "apple", "other"]

SIGNS: list[str] = [
    "uniform_yellowing",
    "interveinal_yellowing",
    "edge_scorch",
    "spots_lesions",
    "target_rings",
    "wilting",
    "leaf_curl",
    "stippling",
    "powdery_coating",
    "mosaic_mottling",
    "purpling",
]

QUALITY: list[str] = ["good", "blurry", "too_dark", "too_bright", "too_small"]

# Shared labels that are allowed whatever the species head says.
SHARED = {"Any___Abiotic_stress", "Other___unknown_disease", "Reject___not_a_leaf"}

COND_INDEX = {c: i for i, c in enumerate(CONDITIONS)}
SPECIES_INDEX = {s: i for i, s in enumerate(SPECIES)}
SIGN_INDEX = {s: i for i, s in enumerate(SIGNS)}
QUALITY_INDEX = {q: i for i, q in enumerate(QUALITY)}

IGNORE = -1   # "no label" for a head (loss is masked out)


def species_of(condition: str) -> str | None:
    """Species a condition belongs to, or None for the shared labels."""
    if condition in SHARED:
        return None
    return condition.split("___", 1)[0].lower()


def allowed_conditions(species: str) -> list[int]:
    """Condition indices the server keeps when the species head picks `species`."""
    if species == "other":
        return [COND_INDEX[c] for c in ("Other___unknown_disease", "Any___Abiotic_stress", "Reject___not_a_leaf")]
    return [i for i, c in enumerate(CONDITIONS) if c in SHARED or species_of(c) == species]


# ---------------------------------------------------------------------------
# Weak labels for the visual-signs head (memo: "weak labels from each class,
# refined by hand on 500 images"). Per condition: (signs present, signs unknown);
# every other sign is taken as absent. None = no sign labels for that class.
# ---------------------------------------------------------------------------
_S = set
SIGNS_BY_CONDITION: dict[str, tuple[set, set] | None] = {
    "Tomato___Early_blight": (_S({"spots_lesions", "target_rings", "uniform_yellowing"}), _S()),
    "Tomato___Late_blight": (_S({"spots_lesions"}), _S({"wilting", "uniform_yellowing"})),
    "Tomato___Septoria_leaf_spot": (_S({"spots_lesions"}), _S({"uniform_yellowing"})),
    "Tomato___Leaf_mold": (_S({"uniform_yellowing"}), _S({"spots_lesions"})),
    "Tomato___Target_spot": (_S({"spots_lesions", "target_rings"}), _S({"uniform_yellowing"})),
    "Tomato___Grey_leaf_spot": (_S({"spots_lesions"}), _S({"uniform_yellowing"})),
    "Tomato___Powdery_mildew": (_S({"powdery_coating"}), _S({"uniform_yellowing"})),
    "Tomato___Verticillium_wilt": (_S({"wilting", "interveinal_yellowing"}), _S({"edge_scorch"})),
    "Tomato___Leaf_blight_unspecified": (_S({"spots_lesions"}), _S({"target_rings", "uniform_yellowing"})),
    "Tomato___Bacterial_spot": (_S({"spots_lesions"}), _S({"uniform_yellowing"})),
    "Tomato___Yellow_leaf_curl_virus": (_S({"leaf_curl", "uniform_yellowing"}), _S()),
    "Tomato___Mosaic_virus": (_S({"mosaic_mottling"}), _S({"leaf_curl"})),
    "Tomato___Spider_mites": (_S({"stippling"}), _S({"uniform_yellowing"})),
    "Tomato___Leaf_miner": None,
    "Tomato___Chewing_insect_damage": None,
    "Tomato___healthy": (_S(), _S()),
    "Potato___Early_blight": (_S({"spots_lesions", "target_rings", "uniform_yellowing"}), _S()),
    "Potato___Late_blight": (_S({"spots_lesions"}), _S({"wilting", "uniform_yellowing"})),
    "Potato___Bacterial_disease": (_S({"spots_lesions"}), _S({"wilting", "uniform_yellowing"})),
    "Potato___Viral_disease": (_S({"mosaic_mottling"}), _S({"leaf_curl", "uniform_yellowing"})),
    "Potato___Fungal_leaf_spot_unspecified": (_S({"spots_lesions"}), _S({"target_rings", "uniform_yellowing"})),
    "Potato___Pest_damage": None,
    "Potato___Nematode_damage": (_S({"uniform_yellowing"}), _S({"wilting"})),
    "Potato___healthy": (_S(), _S()),
    "Pepper___Bacterial_spot": (_S({"spots_lesions"}), _S({"uniform_yellowing"})),
    "Pepper___Cercospora_leaf_spot": (_S({"spots_lesions"}), _S({"target_rings"})),
    "Pepper___Anthracnose": (_S({"spots_lesions"}), _S({"target_rings"})),
    "Pepper___Leaf_curl_virus": (_S({"leaf_curl"}), _S({"uniform_yellowing", "mosaic_mottling"})),
    "Pepper___Powdery_mildew": (_S({"powdery_coating"}), _S({"uniform_yellowing"})),
    "Pepper___Nutrient_deficiency": (_S(), _S({"uniform_yellowing", "interveinal_yellowing", "edge_scorch", "purpling"})),
    "Pepper___healthy": (_S(), _S()),
    "Apple___Scab": (_S({"spots_lesions"}), _S()),
    "Apple___Rust": (_S({"spots_lesions"}), _S()),
    "Apple___Frog_eye_leaf_spot": (_S({"spots_lesions", "target_rings"}), _S()),
    "Apple___Powdery_mildew": (_S({"powdery_coating"}), _S({"leaf_curl"})),
    "Apple___Alternaria_leaf_spot": (_S({"spots_lesions"}), _S({"target_rings"})),
    "Apple___Mosaic_virus": (_S({"mosaic_mottling"}), _S()),
    "Apple___Multiple_diseases": (_S({"spots_lesions"}), set(SIGNS) - {"spots_lesions"}),
    "Other___unknown_disease": None,
    "Reject___not_a_leaf": (_S(), _S()),
    "Any___Abiotic_stress": None,   # resolved per cause, see ABIOTIC_SIGNS
}

# Abiotic stress: the cause (from the source's folder name) decides the signs.
ABIOTIC_SIGNS: dict[str, tuple[set, set]] = {
    "boron": (_S({"edge_scorch", "leaf_curl"}), _S()),
    "calcium": (_S({"edge_scorch", "leaf_curl"}), _S()),
    "iron": (_S({"interveinal_yellowing"}), _S()),
    "magnesium": (_S({"interveinal_yellowing"}), _S({"edge_scorch"})),
    "manganese": (_S({"interveinal_yellowing"}), _S({"spots_lesions"})),
    "zinc": (_S({"interveinal_yellowing"}), _S()),
    "potassium": (_S({"edge_scorch"}), _S({"uniform_yellowing"})),
    "nitrogen": (_S({"uniform_yellowing"}), _S()),
    "sulphur": (_S({"uniform_yellowing"}), _S()),
    "phosphorus": (_S({"purpling"}), _S()),
    "drought": (_S({"wilting", "leaf_curl"}), _S()),
    "sunburn": (_S(), set(SIGNS)),
    "excess_nitrogen": (_S(), set(SIGNS)),
    "unknown": (_S(), set(SIGNS)),
}


def sign_targets(condition: str, abiotic_cause: str | None = None) -> list[int]:
    """Weak sign labels as a list of 1 (present), 0 (absent) or IGNORE (unknown)."""
    if condition == "Any___Abiotic_stress":
        spec = ABIOTIC_SIGNS.get(abiotic_cause or "unknown", ABIOTIC_SIGNS["unknown"])
    else:
        spec = SIGNS_BY_CONDITION.get(condition)
    if spec is None:
        return [IGNORE] * len(SIGNS)
    present, unknown = spec
    return [1 if s in present else IGNORE if s in unknown else 0 for s in SIGNS]


# Signs -> the four feature ids the Unity app already understands
# (Assets/Data/Features: leaf_color, leaf_spots, yellowing, wilting).
def app_features(signs: dict[str, float]) -> dict[str, float]:
    yellow = max(signs.get("uniform_yellowing", 0.0), signs.get("interveinal_yellowing", 0.0))
    return {
        "leaf_color": round(1.0 - max(yellow, signs.get("edge_scorch", 0.0), signs.get("purpling", 0.0)), 3),
        "leaf_spots": round(max(signs.get("spots_lesions", 0.0), signs.get("stippling", 0.0)), 3),
        "yellowing": round(yellow, 3),
        "wilting": round(max(signs.get("wilting", 0.0), signs.get("leaf_curl", 0.0) * 0.5), 3),
    }
