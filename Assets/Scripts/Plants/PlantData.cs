using UnityEngine;
using PlantDoctor.Diseases;

namespace PlantDoctor.Plants
{
    [CreateAssetMenu(menuName = "PlantDoctor/Plant Data", fileName = "Plant_")]
    public class PlantData : ScriptableObject
    {
        public string plantId = "tomato";
        public string plantName = "Tomato";
        [TextArea] public string description;
        [Tooltip("Latin name shown under the common name, e.g. Solanum lycopersicum.")]
        public string scientificName;

        [Tooltip("TODO[ASSET]: replace placeholder primitive prefab with the real model.")]
        public GameObject healthyModel;
        public GameObject leafPrefab;
        public Sprite thumbnail;

        [Tooltip("How the plant twin looks for this species (built at runtime; no 3D model needed).")]
        public SpeciesProfile species;

        public FeatureData[] features;
        public DiseaseData[] diseases;

        public DiseaseData FindDisease(string diseaseId)
        {
            if (diseases == null || string.IsNullOrEmpty(diseaseId)) return null;
            foreach (var d in diseases)
                if (d != null && d.Matches(diseaseId)) return d;
            // A class name the disease's own aliases miss: ask the content database which disease it means.
            if (ContentDatabase.TryResolveLabel(diseaseId, plantId, out var plant, out var resolved) && plant == plantId && resolved != diseaseId)
                foreach (var d in diseases)
                    if (d != null && d.diseaseId == resolved) return d;
            return null;
        }

        public FeatureData FindFeature(string featureId)
        {
            if (features == null || string.IsNullOrEmpty(featureId)) return null;
            foreach (var f in features)
                if (f != null && f.featureId == featureId) return f;
            return null;
        }
    }
}
