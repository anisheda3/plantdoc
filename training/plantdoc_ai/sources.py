"""
Every dataset the model learns from, with where to get it and how its folder names map
onto our 42 condition labels. Folder names below were read from the live dataset
listings on 2026-10-12.

Mapped values:
  "<Condition>"            a label from taxonomy.CONDITIONS (species from the label)
  "<Condition>|<species>"  same, with an explicit species (needed for shared labels)
  "ABIOTIC:<cause>"        Any___Abiotic_stress, the cause picks the visual signs
  "OTHER_DISEASE"          Other___unknown_disease on a non-target plant (species "other")
  "OTHER_HEALTHY"          healthy non-target plant: trains species "other" only
  "skip"                   not used (fruit photos, classes we don't model, ...)

A folder that is not in the map stops build_manifest with a list of what's missing, so
nothing is dropped silently.
"""
from __future__ import annotations

import re
from dataclasses import dataclass, field

OD, OH = "OTHER_DISEASE", "OTHER_HEALTHY"


def norm(label: str) -> str:
    """'Tomato_leaf_late_blight' / 'Tomato leaf-late blight' -> 'tomato leaf late blight'."""
    return re.sub(r"[^a-z0-9]+", " ", label.lower()).strip()


@dataclass
class Source:
    id: str
    kind: str                      # mendeley | github_zip | zenodo_zip | hf_parquet | coco
    ref: str                       # dataset id, or URL
    title: str
    url: str
    licence: str
    species: str                   # default species for shared labels
    labels: dict[str, str]         # norm(folder label) -> mapped value
    label_parts: int = 1           # how many folder levels above the file form the label
    include: str | None = None     # regex (case-insensitive) on the image's path inside raw/<id>/
    exclude: str | None = None
    download_include: str | None = None   # regex on the remote path: which files to fetch at all
    lab: bool = False              # lab photos (plain background): capped at 20% of a batch
    cap_other: int | None = None   # max images per OTHER_* folder
    label_mode: str = "dirs"       # "dirs" or "plantseg"
    enabled: bool = True
    note: str = ""
    citation: str = ""
    extra: dict = field(default_factory=dict)

    def map_label(self, raw: str) -> str | None:
        key = norm(raw)
        if self.label_mode == "plantseg":
            for prefix, value in self.labels.items():
                if key.startswith(prefix):
                    return value
            return OD   # every other PlantSeg host/disease is "not one we know"
        return self.labels.get(key)


T = "Tomato___"
SOURCES: list[Source] = [
    # ------------------------------------------------------------------ tomato
    Source(
        id="tomato_pakistan", kind="mendeley", ref="3mbnb82mxd",
        title="Tomato Leaf Disease Classification Dataset in Pakistan",
        url="https://data.mendeley.com/datasets/3mbnb82mxd", licence="CC BY 4.0", species="tomato",
        include=r"Dataset \(raw\)/",
        note="Only the raw folder; the 'Augmented Dataset' copies are skipped.",
        labels={
            "tomato early blight leaf": T + "Early_blight",
            "tomato healthy leaf": T + "healthy",
            "tomato septoria leaf spot": T + "Septoria_leaf_spot",
            "tomato leaf late blight": T + "Late_blight",
            "tomato leaf yellow curl virus": T + "Yellow_leaf_curl_virus",
            "tomato mold leaf": T + "Leaf_mold",
        }),
    Source(
        id="ccmt", kind="mendeley", ref="bwh3zbpkpv",
        title="CCMT: Dataset for Crop Pest and Disease Detection (Ghana)",
        url="https://data.mendeley.com/datasets/bwh3zbpkpv", licence="CC BY 4.0", species="tomato",
        include=r"^Raw Data/CCMT Dataset/", label_parts=2, cap_other=250,
        note="Raw images only. Cashew, cassava and maize teach the 'other plant' answers (capped).",
        labels={
            "tomato healthy": T + "healthy",
            "tomato leaf blight": T + "Leaf_blight_unspecified",
            "tomato leaf curl": T + "Yellow_leaf_curl_virus",
            "tomato septoria leaf spot": T + "Septoria_leaf_spot",
            "tomato verticulium wilt": T + "Verticillium_wilt",
            "cashew anthracnose": OD, "cashew gumosis": OD, "cashew leaf miner": OD, "cashew red rust": OD,
            "cashew healthy": OH,
            "cassava bacterial blight": OD, "cassava brown spot": OD, "cassava green mite": OD,
            "cassava mosaic": OD, "cassava healthy": OH,
            "maize fall armyworm": OD, "maize grasshoper": OD, "maize leaf beetle": OD,
            "maize leaf blight": OD, "maize leaf spot": OD, "maize streak virus": OD, "maize healthy": OH,
        }),
    Source(
        id="tomato_bangladesh", kind="mendeley", ref="93h9p62kg4",
        title="Tomato leaf diseases (Bangladesh, iPhone 11)",
        url="https://data.mendeley.com/datasets/93h9p62kg4", licence="CC BY 4.0", species="tomato",
        labels={
            "bacterial spot": T + "Bacterial_spot",
            "cercospora leaf mold": T + "Leaf_mold",
            "early blight": T + "Early_blight",
            "healthy": T + "healthy",
            "insect damage": T + "Chewing_insect_damage",
            "late blight": T + "Late_blight",
            "leaf miner": T + "Leaf_miner",
            "leaf mold": T + "Leaf_mold",
            "spider mites": T + "Spider_mites",
            "tomato leaf curl virus": T + "Yellow_leaf_curl_virus",
        }),
    Source(
        id="tom2024", kind="mendeley", ref="3d4yg89rtr",
        title="TOM2024 (Burkina Faso)",
        url="https://data.mendeley.com/datasets/3d4yg89rtr", licence="CC BY 4.0", species="tomato",
        include=r"^CATEGORY A/CATA-English/", label_parts=2, cap_other=250,
        note="English folders of category A (originals); category B is augmented copies. Fruit classes skipped.",
        labels={
            "tomato diseases alternaria d": T + "Early_blight",
            "tomato diseases mite d": T + "Spider_mites",
            "tomato diseases tomato late blight d": T + "Late_blight",
            "tomato diseases healthy leaf": T + "healthy",
            "tomato pests tuta absoluta p": T + "Leaf_miner",
            "tomato pests helicoverpa armigera p": T + "Chewing_insect_damage",
            "tomato diseases exces nitrogen d": "ABIOTIC:excess_nitrogen",
            "tomato diseases sunburn d": "ABIOTIC:sunburn",
            "tomato diseases healthy fruit": "skip",
            "tomato diseases blossom end rot d": "skip",
            "tomato diseases bacterial floundering d": "skip",   # bacterial wilt: not modelled
            "tomato diseases fusarium d": "skip",
            "tomato diseases virosis d": "skip",                  # virus not named
            "tomato diseases alternaria mite d": "skip",          # two labels at once
            "maize diseases abiotic disease d": "ABIOTIC:unknown|other",
            "maize diseases curvularia d": OD, "maize diseases helminthosporiosis d": OD,
            "maize diseases rust d": OD, "maize diseases stripe d": OD, "maize diseases virosis d": OD,
            "maize diseases healthy leaf": OH,
            "maize pests aphids p": OD, "maize pests spodoptera frugiperda p": OD,
            "maize pests activities spodotera frugiperda a": "skip",
            "onion diseases alternaria d": OD, "onion diseases bulb blight d": OD,
            "onion diseases fusarium d": OD, "onion diseases virosis d": OD,
            "onion diseases healthy leaf": OH, "onion pests caterpillar p": OD,
        }),
    Source(
        id="tomato_taiwan", kind="mendeley", ref="ngdgg79rzb",
        title="Dataset of Tomato Leaves (Taiwan, dataset 2)",
        url="https://data.mendeley.com/datasets/ngdgg79rzb", licence="CC BY 4.0", species="tomato",
        download_include=r"^taiwan\.7z$", include=r"^taiwan/Preprocessed data/",
        note="Original 634 images; the 'data augmentation' folder is skipped.",
        labels={
            "bacterial spot": T + "Bacterial_spot",
            "black mold": T + "Leaf_mold",
            "gray spot": T + "Grey_leaf_spot",
            "late blight": T + "Late_blight",
            "health": T + "healthy",
            "powdery mildew": T + "Powdery_mildew",
        }),
    Source(
        id="plantvillage_tomato", kind="mendeley", ref="ngdgg79rzb",
        title="PlantVillage tomato subset (repackaged, dataset 1)",
        url="https://data.mendeley.com/datasets/ngdgg79rzb", licence="CC BY 4.0", species="tomato",
        download_include=r"^plantvillage/Preprocessed data\.7z$", include=r"^plantvillage/Preprocessed data/", lab=True,
        note="Lab photos on plain backgrounds: at most 20% of every batch.",
        labels={
            "bacterial spot227": T + "Bacterial_spot",
            "early blight227": T + "Early_blight",
            "late blight227": T + "Late_blight",
            "leaf mold227": T + "Leaf_mold",
            "septoria leaf spot227": T + "Septoria_leaf_spot",
            "target spot227": T + "Target_spot",
            "tomato yellow leaf curl virus227": T + "Yellow_leaf_curl_virus",
            "tomato mosaic virus227": T + "Mosaic_virus",
            "two spotted spider mite227": T + "Spider_mites",
            "healthy227": T + "healthy",
        }),
    # ------------------------------------------------------------------ potato
    Source(
        id="potato_pldd", kind="mendeley", ref="3j4nfkvp2n",
        title="PLDD-UP: Potato Leaf Disease Dataset from Uttar Pradesh",
        url="https://data.mendeley.com/datasets/3j4nfkvp2n", licence="CC BY 4.0", species="potato",
        note="8.8 GB of full-resolution photos; delete data/raw/potato_pldd after `prepare` if disk is tight.",
        labels={"eb": "Potato___Early_blight", "lb": "Potato___Late_blight", "healthy": "Potato___healthy"}),
    Source(
        id="potato_uncontrolled", kind="mendeley", ref="ptz377bwb8",
        title="Potato Leaf Disease Dataset in Uncontrolled Environment (Central Java)",
        url="https://data.mendeley.com/datasets/ptz377bwb8", licence="CC BY 4.0", species="potato",
        labels={
            "bacteria": "Potato___Bacterial_disease",
            "fungi": "Potato___Fungal_leaf_spot_unspecified",
            "healthy": "Potato___healthy",
            "nematode": "Potato___Nematode_damage",
            "pest": "Potato___Pest_damage",
            "phytopthora": "Potato___Late_blight",
            "virus": "Potato___Viral_disease",
        }),
    # ------------------------------------------------------------------ pepper
    Source(
        id="pepper_chilli_bd", kind="mendeley", ref="tm3v4zmh7c",
        title="Chilli Leaf Disease Image Dataset (Bangladesh)",
        url="https://data.mendeley.com/datasets/tm3v4zmh7c", licence="CC BY 4.0", species="pepper",
        labels={
            "bacterial spot": "Pepper___Bacterial_spot",
            "cercospora leaf spot": "Pepper___Cercospora_leaf_spot",
            "curl virus": "Pepper___Leaf_curl_virus",
            "healthy leaf": "Pepper___healthy",
            "nutrition deficiency": "Pepper___Nutrient_deficiency",
            "powdery mildew": "Pepper___Powdery_mildew",
        }),
    Source(
        id="pepper_rangpur", kind="mendeley", ref="wzc6r6w5w5",
        title="Chili Leaf Disease Dataset (Rangpur)",
        url="https://data.mendeley.com/datasets/wzc6r6w5w5",
        licence="CC BY 4.0 (page also mentions non-commercial use: confirm before release)", species="pepper",
        download_include=r"\.zip$", exclude=r"__MACOSX",
        labels={
            "anthracnose": "Pepper___Anthracnose",
            "cercospora leaf spot": "Pepper___Cercospora_leaf_spot",
            "fresh leaf": "Pepper___healthy",
            "leaf curl disease": "Pepper___Leaf_curl_virus",
        }),
    Source(
        id="pepper_bell", kind="mendeley", ref="w9drvtxg9g",
        title="Pepper Bell Leaf Disease", url="https://data.mendeley.com/datasets/w9drvtxg9g",
        licence="CC BY 4.0", species="pepper", lab=True, enabled=False,
        exclude=r"(_180deg|_flip|_rot\d)",
        note="Off by default: mostly PlantVillage copies with pre-made rotations, and its 'Cercospora' "
             "folder holds files named B.Spot. Inspect before enabling.",
        labels={
            "pepperbell bacterial spot": "Pepper___Bacterial_spot",
            "pepperbell cercospora leaf spot": "Pepper___Cercospora_leaf_spot",
            "pepperbell healthy": "Pepper___healthy",
            "pepperbell leaf curl": "Pepper___Leaf_curl_virus",
            "pepperbell nutrition deficiency": "Pepper___Nutrient_deficiency",
            "pepperbell powdery mildew": "Pepper___Powdery_mildew",
        }),
    # ------------------------------------------------------------------ apple
    Source(
        id="apple_pp2021", kind="hf_parquet", ref="timm/plant-pathology-2021",
        title="Plant Pathology 2021 (FGVC8)", url="https://huggingface.co/datasets/timm/plant-pathology-2021",
        licence="Apache-2.0 on the mirror; original Kaggle competition terms: confirm before release",
        species="apple",
        note="16 GB of parquet; images are re-saved at 640 px and the parquet deleted as it goes.",
        labels={
            "scab": "Apple___Scab", "rust": "Apple___Rust",
            "frog eye leaf spot": "Apple___Frog_eye_leaf_spot",
            "powdery mildew": "Apple___Powdery_mildew", "healthy": "Apple___healthy",
            "complex": "Apple___Multiple_diseases", "multiple": "Apple___Multiple_diseases",
        }),
    Source(
        id="apple_jk", kind="mendeley", ref="9m2dcb5mmr",
        title="Indigenous Dataset for Apple Leaf Disease Detection (Jammu and Kashmir)",
        url="https://data.mendeley.com/datasets/9m2dcb5mmr", licence="CC BY 4.0", species="apple",
        labels={"alternaria": "Apple___Alternaria_leaf_spot", "apple mosaic": "Apple___Mosaic_virus",
                "healthy": "Apple___healthy"}),
    # ------------------------------------------------------------------ web photos
    Source(
        id="plantdoc", kind="github_zip",
        ref="https://codeload.github.com/pratikkayal/PlantDoc-Dataset/zip/refs/heads/master",
        title="PlantDoc", url="https://github.com/pratikkayal/PlantDoc-Dataset", licence="CC BY 4.0",
        species="tomato", cap_other=250,
        labels={
            "apple scab leaf": "Apple___Scab", "apple leaf": "Apple___healthy", "apple rust leaf": "Apple___Rust",
            "bell pepper leaf spot": "Pepper___Bacterial_spot", "bell pepper leaf": "Pepper___healthy",
            "potato leaf early blight": "Potato___Early_blight", "potato leaf late blight": "Potato___Late_blight",
            "tomato early blight leaf": T + "Early_blight", "tomato septoria leaf spot": T + "Septoria_leaf_spot",
            "tomato leaf bacterial spot": T + "Bacterial_spot", "tomato leaf late blight": T + "Late_blight",
            "tomato leaf mosaic virus": T + "Mosaic_virus", "tomato leaf yellow virus": T + "Yellow_leaf_curl_virus",
            "tomato leaf": T + "healthy", "tomato mold leaf": T + "Leaf_mold",
            "tomato two spotted spider mites leaf": T + "Spider_mites",
            "corn gray leaf spot": OD, "corn leaf blight": OD, "corn rust leaf": OD,
            "squash powdery mildew leaf": OD, "grape leaf black rot": OD,
            "blueberry leaf": OH, "cherry leaf": OH, "peach leaf": OH, "raspberry leaf": OH,
            "soyabean leaf": OH, "strawberry leaf": OH, "grape leaf": OH,
        }),
    Source(
        id="plantseg", kind="zenodo_zip", ref="https://zenodo.org/records/16665770/files/Plantseg.zip",
        title="PlantSeg (pixel lesion masks)", url="https://zenodo.org/records/16665770",
        licence="CC BY 4.0 on the current record (the paper said CC BY-NC-ND 4.0): confirm before release",
        species="other", label_mode="plantseg", cap_other=40, include=r"/images/",
        note="Lesion masks train the severity head; the four target crops also feed the condition head.",
        labels={
            "apple black rot": "Other___unknown_disease|apple",
            "apple mosaic virus": "Apple___Mosaic_virus", "apple rust": "Apple___Rust", "apple scab": "Apple___Scab",
            "bell pepper bacterial spot": "Pepper___Bacterial_spot",
            "bell pepper blossom end rot": "skip",
            "bell pepper frogeye leaf spot": "Pepper___Cercospora_leaf_spot",
            "bell pepper powdery mildew": "Pepper___Powdery_mildew",
            "potato early blight": "Potato___Early_blight", "potato late blight": "Potato___Late_blight",
            "tomato bacterial leaf spot": T + "Bacterial_spot", "tomato early blight": T + "Early_blight",
            "tomato late blight": T + "Late_blight", "tomato leaf mold": T + "Leaf_mold",
            "tomato mosaic virus": T + "Mosaic_virus", "tomato septoria leaf spot": T + "Septoria_leaf_spot",
            "tomato yellow leaf curl virus": T + "Yellow_leaf_curl_virus",
        }),
    # ------------------------------------------------------------------ abiotic stress
    Source(
        id="abiotic_banana", kind="mendeley", ref="7vpdrbdkd4",
        title="Images of Nutrient Deficient Banana Plant Leaves (Karnataka)",
        url="https://data.mendeley.com/datasets/7vpdrbdkd4", licence="CC BY 4.0", species="other",
        include=r"^Version-2-RAW Images", cap_other=400,
        note="RAW folders only (the augmented copies are skipped).",
        labels={
            "boron": "ABIOTIC:boron|other", "calcium": "ABIOTIC:calcium|other", "iron": "ABIOTIC:iron|other",
            "magnesium": "ABIOTIC:magnesium|other", "manganese": "ABIOTIC:manganese|other",
            "potassium": "ABIOTIC:potassium|other", "sulphur": "ABIOTIC:sulphur|other",
            "zinc": "ABIOTIC:zinc|other", "healthy": OH,
        }),
    Source(
        id="abiotic_coleaf", kind="mendeley", ref="brfgw46wzb",
        title="CoLeaf-DB (coffee leaf nutrient deficiencies, Peru)",
        url="https://data.mendeley.com/datasets/brfgw46wzb", licence="CC BY 4.0", species="other",
        download_include=r"\.zip$",
        labels={
            "boron b": "ABIOTIC:boron|other", "calcium ca": "ABIOTIC:calcium|other",
            "iron fe": "ABIOTIC:iron|other", "magnesium mg": "ABIOTIC:magnesium|other",
            "manganese mn": "ABIOTIC:manganese|other", "nitrogen n": "ABIOTIC:nitrogen|other",
            "phosphorus p": "ABIOTIC:phosphorus|other", "potasium k": "ABIOTIC:potassium|other",
            "more deficiencies": "ABIOTIC:unknown|other", "healthy": OH,
        }),
    # ------------------------------------------------------------------ "not a leaf"
    Source(
        id="reject_coco", kind="coco", ref="http://images.cocodataset.org/zips/val2017.zip",
        title="COCO 2017 validation images (non-plant photos)", url="https://cocodataset.org",
        licence="Annotations CC BY 4.0; images under their Flickr licences (mostly CC BY / BY-SA / BY-NC)",
        species="other",
        note="Stands in for the memo's Open Images sample: same purpose, one 1 GB download. "
             "Photos containing potted plants, broccoli or vases are left out.",
        extra={"annotations": "http://images.cocodataset.org/annotations/annotations_trainval2017.zip",
               "exclude_categories": ["potted plant", "broccoli", "vase"], "max_images": 5000},
        labels={"not a leaf": "Reject___not_a_leaf"}),
]

BY_ID = {s.id: s for s in SOURCES}


def enabled_sources(only: list[str] | None = None) -> list[Source]:
    if only:
        missing = [i for i in only if i not in BY_ID]
        if missing:
            raise SystemExit(f"Unknown source(s): {', '.join(missing)}. Known: {', '.join(BY_ID)}")
        return [BY_ID[i] for i in only]
    return [s for s in SOURCES if s.enabled]
