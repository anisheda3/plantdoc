"""
The content database (Database/plant_doctor_content.db, built by tools/data/make_content_db.py): what the app teaches about each
plant and disease, keyed by (plant_id, disease_id). The server uses it to turn whatever class name the model outputs into the app's
plant and disease, and to send the disease's content with every diagnosis.

Standard library only (sqlite3), so it works wherever the server runs. Path: PLANTDOCTOR_CONTENT_DB (default ../Database/...).
"""
from __future__ import annotations

import os
import sqlite3
import threading
from pathlib import Path
from typing import Dict, List, Optional, Tuple

# server/data/ is what the Docker image contains (make_content_db.py copies the database there); ../Database/ when run from the repo.
_HERE = Path(__file__).resolve().parent
DEFAULT_PATH = next((p for p in (_HERE / "data" / "plant_doctor_content.db", _HERE.parent / "Database" / "plant_doctor_content.db") if p.exists()),
                    _HERE / "data" / "plant_doctor_content.db")


class ContentDB:
    def __init__(self, path: Optional[str] = None) -> None:
        self.path = Path(path or os.getenv("PLANTDOCTOR_CONTENT_DB", "") or DEFAULT_PATH)
        self._local = threading.local()

    @property
    def available(self) -> bool:
        return self.path.exists()

    def _con(self) -> sqlite3.Connection:
        con = getattr(self._local, "con", None)
        if con is None:
            # Read-only: the server never changes the content.
            con = sqlite3.connect(f"file:{self.path.as_posix()}?mode=ro", uri=True, check_same_thread=False)
            con.row_factory = sqlite3.Row
            self._local.con = con
        return con

    def _rows(self, sql: str, args: tuple = ()) -> List[dict]:
        if not self.available:
            return []
        return [dict(r) for r in self._con().execute(sql, args)]

    # ------------------------------------------------------------------
    def resolve_label(self, label: str, plant_hint: Optional[str] = None) -> Optional[Tuple[str, str]]:
        """A dataset class name (PlantVillage, PlantSeg, PlantDoc, MangoLeafBD, ...) -> (plant_id, disease_id or 'healthy')."""
        rows = self._rows("SELECT plant_id, disease_id FROM labels WHERE label = ? COLLATE NOCASE", (label,))
        if not rows:
            return None
        if plant_hint:
            for r in rows:
                if r["plant_id"] == plant_hint:
                    return r["plant_id"], r["disease_id"]
        return rows[0]["plant_id"], rows[0]["disease_id"]

    def plants(self) -> List[dict]:
        return self._rows("SELECT plant_id, name, scientific_name, description FROM plants ORDER BY name")

    def plant(self, plant_id: str) -> Optional[dict]:
        rows = self._rows("SELECT plant_id, name, scientific_name, description FROM plants WHERE plant_id = ?", (plant_id,))
        if not rows:
            return None
        p = rows[0]
        p["diseases"] = self._rows("SELECT disease_id, name, category, affected_part FROM diseases WHERE plant_id = ? ORDER BY name", (plant_id,))
        return p

    def disease(self, plant_id: str, disease_id: str) -> Optional[Dict]:
        """Everything about one disease, or None when the database does not have it."""
        key = (plant_id, disease_id)
        rows = self._rows("SELECT * FROM diseases WHERE plant_id = ? AND disease_id = ?", key)
        if not rows:
            return None
        d = rows[0]
        texts = lambda table: [r["text"] for r in self._rows(f"SELECT text FROM {table} WHERE plant_id = ? AND disease_id = ? ORDER BY ord", key)]
        d["symptoms"] = texts("symptoms")
        d["lifecycle"] = texts("lifecycle_stages")
        d["conditions"] = texts("condition_factors")
        d["treatment_steps"] = self._rows(
            "SELECT step_id, title, instruction, rationale, simulated_days, involves_product FROM treatment_steps "
            "WHERE plant_id = ? AND disease_id = ? ORDER BY ord", key)
        d["prevention"] = self._rows(
            "SELECT category, title, detail FROM prevention_measures WHERE plant_id = ? AND disease_id = ? ORDER BY ord", key)
        d["methods"] = self._rows("SELECT method_id, name, purpose, limitations FROM methods WHERE plant_id = ? AND disease_id = ?", key)
        d["followups"] = self._rows("SELECT checkpoint_id, prompt, finding, escalation FROM followups WHERE plant_id = ? AND disease_id = ?", key)
        prof = self._rows("SELECT outcome, initial_severity, initial_health, max_severity_drop, max_health_gain, recovery_note "
                          "FROM severity_profiles WHERE plant_id = ? AND disease_id = ?", key)
        d["severity_profile"] = prof[0] if prof else None
        d["evidence"] = self._rows("SELECT publisher, url, reviewed, region, note FROM evidence WHERE plant_id = ? AND disease_id = ? "
                                   "AND url IS NOT NULL", key)
        return d


CONTENT = ContentDB()
