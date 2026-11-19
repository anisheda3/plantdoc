"""Pydantic models mirroring the Unity-side AI contract (schema_version 1.0)."""
from enum import Enum
from typing import Any, Dict, List, Optional

from pydantic import BaseModel, Field

SCHEMA_VERSION = "1.2"   # 1.1 adds candidates and quality; 1.2 adds content (all optional; older clients ignore them)


class FeatureStatus(str, Enum):
    normal = "normal"
    abnormal = "abnormal"
    unknown = "unknown"


class MeasurementSource(str, Enum):
    visual_ai = "visual_ai"
    sensor = "sensor"
    user_input = "user_input"


class FeatureReading(BaseModel):
    value: float = Field(..., ge=0.0, le=1.0)
    optimal: float = Field(..., ge=0.0, le=1.0)
    status: FeatureStatus = FeatureStatus.unknown
    # Camera imagery yields estimates, never physical sensor measurements.
    source: MeasurementSource = MeasurementSource.visual_ai


class DetectedRegion(BaseModel):
    type: str
    bbox: List[float] = Field(..., min_length=4, max_length=4)  # normalised x,y,w,h
    score: float = Field(0.0, ge=0.0, le=1.0)


class Candidate(BaseModel):
    label: str                                    # e.g. "Tomato___Early_blight"
    confidence: float = Field(..., ge=0.0, le=1.0)


class QualityInfo(BaseModel):
    label: str
    score: float = Field(..., ge=0.0, le=1.0)


class Recommendation(BaseModel):
    treatment_id: Optional[str] = None


class AnalysisResponse(BaseModel):
    schema_version: str = SCHEMA_VERSION
    model_version: str
    plant_id: str
    plant_name: str
    disease_id: Optional[str] = None
    disease_name: Optional[str] = None
    confidence: float = Field(..., ge=0.0, le=1.0)
    severity: float = Field(0.0, ge=0.0, le=1.0)
    is_healthy: bool = False
    features: Dict[str, FeatureReading] = {}
    regions: List[DetectedRegion] = []
    candidates: List[Candidate] = []              # top 3 for the identified species, most likely first
    quality: Optional[QualityInfo] = None
    recommendation: Recommendation = Recommendation()
    warnings: List[str] = []
    # The disease's content from the content database (Database/plant_doctor_content.db): description, symptoms, treatment
    # steps, prevention, follow-ups, sources. None when the database is missing or does not know the disease.
    content: Optional[Dict[str, Any]] = None


class ErrorResponse(BaseModel):
    error_code: str
    message: str
