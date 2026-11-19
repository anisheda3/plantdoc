using System.Collections.Generic;
using PlantDoctor.Diseases;
using PlantDoctor.Utilities;
using UnityEngine;

namespace PlantDoctor.Plants
{
    /// <summary>Registry resolving AI ids to authored data. Missing entries never throw.</summary>
    [CreateAssetMenu(menuName = "PlantDoctor/Plant Database", fileName = "PlantDatabase")]
    public class PlantDatabase : ScriptableObject
    {
        public PlantData[] plants;

        Dictionary<string, PlantData> _byId;

        void OnEnable() => Rebuild();

        public void Rebuild()
        {
            _byId = new Dictionary<string, PlantData>(System.StringComparer.OrdinalIgnoreCase);
            if (plants == null) return;
            foreach (var p in plants)
            {
                if (p == null || string.IsNullOrEmpty(p.plantId)) continue;
                if (!_byId.ContainsKey(p.plantId)) _byId.Add(p.plantId, p);
                else Log.Warn(Log.Channel.Core, $"Duplicate plantId '{p.plantId}' ignored.");
            }
        }

        public IReadOnlyList<PlantData> All => plants ?? System.Array.Empty<PlantData>();

        public bool TryResolve(string plantId, out PlantData plant)
        {
            if (_byId == null) Rebuild();
            plant = null;
            return !string.IsNullOrEmpty(plantId) && _byId.TryGetValue(plantId, out plant);
        }

        public bool TryResolveDisease(string plantId, string diseaseId, out PlantData plant, out DiseaseData disease)
        {
            disease = null;
            if (!TryResolve(plantId, out plant)) return false;
            disease = plant.FindDisease(diseaseId);
            return disease != null;
        }
    }
}
