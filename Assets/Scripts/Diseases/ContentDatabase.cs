using System;
using System.Collections.Generic;
using UnityEngine;

namespace PlantDoctor.Diseases
{
    /// <summary>
    /// The app's view of the content database (Database/plant_doctor_content.db). Unity cannot open SQLite on Android without a native
    /// plugin, so tools/data/make_content_db.py publishes the database as Resources/Content/plant_doctor_content.json; this reads that
    /// snapshot. Project setup applies it to the disease assets (descriptions, symptoms, steps, prevention), and at run time the screens
    /// ask it for what the assets do not carry: method limitations, follow-up checks, the expected outcome and the sources.
    /// </summary>
    public static class ContentDatabase
    {
        public const string ResourcePath = "Content/plant_doctor_content";

        [Serializable] public class Step { public string stepId, title, instruction, rationale; }
        [Serializable] public class Measure { public string category, title, detail; }
        [Serializable] public class Method { public string name, purpose, limitations; }
        [Serializable] public class Followup { public string prompt, finding, escalation; }
        [Serializable] public class Evidence { public string publisher, url; }
        [Serializable] public class Label { public string label, plantId, diseaseId; }

        [Serializable]
        public class Disease
        {
            public string plantId, diseaseId, name, category, pathogen, affectedPart, description, causes, effect, untreated, outcome, recoveryNote;
            public string[] symptoms, lifecycle, conditions;
            public Step[] steps;
            public Measure[] prevention;
            public Method[] methods;
            public Followup[] followups;
            public Evidence[] evidence;
        }

        [Serializable] class Snapshot { public string version; public Disease[] diseases; public Label[] labels; }

        static Snapshot _data;
        static Dictionary<string, Disease> _byKey;
        static Dictionary<string, List<Label>> _byLabel;

        /// <summary>The snapshot's content version, or null when the app was built without it.</summary>
        public static string Version => Load() ? _data.version : null;

        public static bool Available => Load();

        static bool Load()
        {
            if (_data != null) return true;
            var text = Resources.Load<TextAsset>(ResourcePath);
            if (text == null) return false;
            return Parse(text.text);
        }

        /// <summary>Loads a snapshot from JSON text (used by the editor importer and by tests).</summary>
        public static bool Parse(string json)
        {
            try { _data = JsonUtility.FromJson<Snapshot>(json); }
            catch (Exception e) { Debug.LogWarning($"[AI Plant Doctor] Content database snapshot unreadable: {e.Message}"); _data = null; return false; }
            if (_data?.diseases == null) { _data = null; return false; }
            _byKey = new Dictionary<string, Disease>(StringComparer.OrdinalIgnoreCase);
            foreach (var d in _data.diseases) _byKey[Key(d.plantId, d.diseaseId)] = d;
            _byLabel = new Dictionary<string, List<Label>>(StringComparer.OrdinalIgnoreCase);
            foreach (var l in _data.labels ?? new Label[0])
            {
                if (!_byLabel.TryGetValue(l.label, out var list)) _byLabel[l.label] = list = new List<Label>();
                list.Add(l);
            }
            return true;
        }

        static string Key(string plant, string disease) => $"{plant}/{disease}";

        /// <summary>All content of one disease, or null.</summary>
        public static Disease Find(string plantId, string diseaseId) =>
            Load() && plantId != null && diseaseId != null && _byKey.TryGetValue(Key(plantId, diseaseId), out var d) ? d : null;

        /// <summary>Every disease in the database.</summary>
        public static IEnumerable<Disease> All => Load() ? _data.diseases : Array.Empty<Disease>();

        /// <summary>
        /// A model's class name (PlantVillage, PlantSeg, PlantDoc, MangoLeafBD, sugarcane or chilli datasets) -> plant and disease
        /// ("healthy" for healthy classes). With a plant hint, a name shared by several plants ("Anthracnose") resolves for that plant.
        /// </summary>
        public static bool TryResolveLabel(string label, string plantHint, out string plantId, out string diseaseId)
        {
            plantId = diseaseId = null;
            if (string.IsNullOrEmpty(label) || !Load() || !_byLabel.TryGetValue(label, out var list) || list.Count == 0) return false;
            var hit = list[0];
            if (!string.IsNullOrEmpty(plantHint))
                foreach (var l in list) if (string.Equals(l.plantId, plantHint, StringComparison.OrdinalIgnoreCase)) { hit = l; break; }
            plantId = hit.plantId;
            diseaseId = hit.diseaseId;
            return true;
        }
    }
}
