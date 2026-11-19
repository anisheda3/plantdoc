# AI Plant Doctor inference API.
#
# Build (from repo root):
#   docker build -t plantdoctor-api -f server/Dockerfile server
# Run:
#   docker run --rm -p 8000:8000 --env-file server/.env plantdoctor-api
#
# Without a mounted/baked model, the container starts fine and /v1/analyze
# reports MODEL_UNAVAILABLE — this is the documented, intentional behaviour
# from inference.py, not a bug.

FROM python:3.11-slim

WORKDIR /app

COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

COPY . .

# Uncomment to bake a trained model into the image instead of mounting it:
# COPY models/ /app/models/

# The content database (data/plant_doctor_content.db, copied by tools/data/make_content_db.py) comes in with COPY . .

EXPOSE 8000

ENV PLANTDOCTOR_MODEL=models/plant_disease.onnx \
    PLANTDOCTOR_LABELS=models/labels.json \
    PLANTDOCTOR_INPUT_SIZE=224

CMD ["uvicorn", "main:app", "--host", "0.0.0.0", "--port", "8000"]