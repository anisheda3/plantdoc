"""PlantSeg diseases for plants without PlantVillage diseases (blueberry, raspberry, soybean) and for the new PlantSeg plants."""
from plantseg_new_a import PLANTS as A
from plantseg_new_b import PLANTS as B

PLANTS = {**A, **B}
