"""The content database as the server reads it (stdlib only: runs without FastAPI installed)."""
import os
import sys
import unittest

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
from content import ContentDB  # noqa: E402


class ContentDBTests(unittest.TestCase):
    db = ContentDB()

    def setUp(self):
        if not self.db.available:
            self.skipTest("Database/plant_doctor_content.db not built (tools/data/make_content_db.py)")

    def test_labels_of_every_dataset_resolve(self):
        cases = {
            "Tomato___Early_blight": ("tomato", "early_blight"),            # PlantVillage
            "soybean frog eye leaf spot": ("soybean", "frogeye_leaf_spot"),  # PlantSeg
            "Tomato leaf yellow virus": ("tomato", "yellow_leaf_curl_virus"),  # PlantDoc
            "RedRot": ("sugarcane", "red_rot"),                             # sugarcane dataset
            "Tomato___healthy": ("tomato", "healthy"),
        }
        for label, expected in cases.items():
            self.assertEqual(expected, self.db.resolve_label(label), label)
        self.assertEqual(("mango", "anthracnose"), self.db.resolve_label("Anthracnose", "mango"))   # same name on several plants
        self.assertIsNone(self.db.resolve_label("no such class"))

    def test_disease_content_is_complete(self):
        d = self.db.disease("tomato", "early_blight")
        self.assertEqual("Early Blight", d["name"])
        self.assertGreaterEqual(len(d["symptoms"]), 3)
        self.assertGreaterEqual(len(d["treatment_steps"]), 3)
        self.assertTrue(d["prevention"])
        self.assertTrue(d["followups"], "the reviewed follow-ups are merged for the 24 treatment_content cases")
        self.assertTrue(any(e["url"] for e in d["evidence"]))
        self.assertIsNone(self.db.disease("tomato", "nonexistent"))

    def test_every_plant_has_diseases(self):
        plants = self.db.plants()
        self.assertEqual(37, len(plants))
        for p in plants:
            self.assertTrue(self.db.plant(p["plant_id"])["diseases"], p["plant_id"])

    def test_every_cropscan_class_resolves(self):
        """The 68 classes of the deployed CropScan XR model (tools/data/cropscan_labels.json) map to the app's plant and disease."""
        import json
        path = os.path.join(os.path.dirname(__file__), "..", "..", "tools", "data", "cropscan_labels.json")
        labels = json.load(open(path, encoding="utf-8"))["labels"]
        self.assertEqual(68, len(labels))
        for label, hit in labels.items():
            self.assertEqual((hit["plant"], hit["disease"]), self.db.resolve_label(label), label)
            if hit["disease"] != "healthy":
                self.assertIsNotNone(self.db.disease(hit["plant"], hit["disease"]), label)


if __name__ == "__main__":
    unittest.main()
