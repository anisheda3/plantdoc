"""Small helpers that keep the disease content files short; make_plantseg_json.py turns them into the app's JSON layout."""


def S(title, instruction, rationale, mode, days, effect, chemical=False):
    """A treatment step. effect: Remove | Cultural | Protect | Small | Rogue (how far it moves the simulated plant)."""
    return {"title": title, "instruction": instruction, "rationale": rationale, "mode": mode, "days": days, "effect": effect, "chemical": chemical}


def M(category, title, detail):
    """A prevention measure. category: CropHygiene | WaterManagement | AirCirculation | Monitoring | EnvironmentalManagement | CropManagement."""
    return {"category": category, "title": title, "detail": detail}


def L(mask, yellow_mask, lesion, halo, yellow, lesions=0.9, yellowing=0.7, wilting=0.4, damage=0.6, seconds=7.0):
    """How the disease looks on the twin: lesion mask and chlorosis mask (Assets/Textures/Generated), colours, how far each goes."""
    return {"mask": mask, "yellowMask": yellow_mask, "lesion": list(lesion), "halo": list(halo), "yellow": list(yellow),
            "lesions": lesions, "yellowing": yellowing, "wilting": wilting, "damage": damage, "seconds": seconds}


def D(id, name, type, part, pathogen, labels, description, causes, symptoms, effect, untreated, steps, prevention, look,
      callouts, lifecycle, conditions, severity_floor=0.15, damage_floor=0.10):
    """One disease. part: Leaf | Fruit | Head | Root | WholePlant. type: Fungal | Oomycete | Bacterial | Viral | Pest | Abiotic."""
    assert len(symptoms) >= 3 and len(lifecycle) == 4 and len(conditions) == 4, id
    return {"id": id, "name": name, "type": type, "part": part, "pathogen": pathogen, "labels": labels,
            "description": description, "causes": causes, "symptoms": symptoms, "effect": effect, "untreated": untreated,
            "severityFloor": severity_floor, "damageFloor": damage_floor, "steps": steps, "prevention": prevention, "look": look,
            "lesionLabel": callouts[0], "haloLabel": callouts[1], "areaLabel": callouts[2],
            "lifecycle": lifecycle, "conditions": conditions}
