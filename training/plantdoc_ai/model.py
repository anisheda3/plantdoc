"""One pretrained backbone, five small heads (memo Section 7)."""
from __future__ import annotations

import timm
import torch
from torch import nn

from . import taxonomy as tx

OUTPUT_NAMES = ["species_logits", "condition_logits", "signs", "severity", "quality_logits"]


class PlantDoctorNet(nn.Module):
    def __init__(self, backbone: str = "efficientnet_b0", pretrained: bool = True, dropout: float = 0.3):
        super().__init__()
        self.backbone = timm.create_model(backbone, pretrained=pretrained, num_classes=0)   # pooled features
        n = self.backbone.num_features
        self.dropout = nn.Dropout(dropout)
        self.species = nn.Linear(n, len(tx.SPECIES))
        self.condition = nn.Linear(n, len(tx.CONDITIONS))
        self.signs = nn.Linear(n, len(tx.SIGNS))
        self.severity = nn.Linear(n, 1)
        self.quality = nn.Linear(n, len(tx.QUALITY))

    def heads(self):
        return [self.species, self.condition, self.signs, self.severity, self.quality]

    def forward(self, x):
        f = self.dropout(self.backbone(x))
        return {"species": self.species(f), "condition": self.condition(f), "signs": self.signs(f),
                "severity": self.severity(f).squeeze(-1), "quality": self.quality(f)}


class ExportWrapper(nn.Module):
    """What goes into the ONNX file: logits for the softmax heads, probabilities for the rest."""

    def __init__(self, net: PlantDoctorNet):
        super().__init__()
        self.net = net

    def forward(self, image):
        o = self.net(image)
        return (o["species"], o["condition"], torch.sigmoid(o["signs"]),
                torch.sigmoid(o["severity"]), o["quality"])


def build_model(cfg: dict, pretrained: bool = True) -> PlantDoctorNet:
    m = cfg["model"]
    return PlantDoctorNet(m["backbone"], pretrained=pretrained, dropout=m.get("dropout", 0.3))


def load_checkpoint(path, device="cpu") -> tuple[PlantDoctorNet, dict]:
    ckpt = torch.load(path, map_location=device, weights_only=False)
    if ckpt.get("conditions") != tx.CONDITIONS:
        raise SystemExit("This checkpoint was trained with a different label list than taxonomy.py.")
    net = build_model(ckpt["config"], pretrained=False)
    net.load_state_dict(ckpt["model"])
    return net.to(device).eval(), ckpt
