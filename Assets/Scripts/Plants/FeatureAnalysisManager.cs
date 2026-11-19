using System.Collections.Generic;
using PlantDoctor.AI;
using PlantDoctor.Core;
using UnityEngine;

namespace PlantDoctor.Plants
{
    /// <summary>One row of the OBSERVED vs OPTIMAL comparison table.</summary>
    public class FeatureComparisonRow
    {
        public string FeatureId;
        public string DisplayName;
        public float Observed;
        public float Optimal;
        public FeatureStatus Status;
        public string SourceLabel;
        public Color OverlayColor = Color.yellow;
        public string Meaning;
        public string WhyItMatters;

        public string StatusGlyph => Status switch
        {
            FeatureStatus.Abnormal => "!",
            FeatureStatus.Normal => "OK",
            _ => "?"
        };
    }

    /// <summary>Joins AI readings with authored FeatureData to build the educational table.</summary>
    public class FeatureAnalysisManager : MonoBehaviour
    {
        readonly List<FeatureComparisonRow> _rows = new List<FeatureComparisonRow>();
        public IReadOnlyList<FeatureComparisonRow> Rows => _rows;

        public IReadOnlyList<FeatureComparisonRow> Build(SessionContext context)
        {
            _rows.Clear();
            if (context?.Analysis == null) return _rows;

            foreach (var reading in context.Analysis.features)
            {
                if (reading == null) continue;
                var data = context.Plant != null ? context.Plant.FindFeature(reading.featureId) : null;

                _rows.Add(new FeatureComparisonRow
                {
                    FeatureId = reading.featureId,
                    DisplayName = data != null ? data.displayName : Prettify(reading.featureId),
                    Observed = reading.value,
                    Optimal = data != null ? data.optimalValue : reading.optimal,
                    Status = data != null ? data.Evaluate(reading.value) : reading.status,
                    SourceLabel = reading.SourceLabel,
                    OverlayColor = data != null ? data.overlayColor : Color.yellow,
                    Meaning = data != null ? data.meaning : string.Empty,
                    WhyItMatters = data != null ? data.whyItMatters : string.Empty
                });
            }
            return _rows;
        }

        public FeatureComparisonRow Get(string featureId)
        {
            foreach (var r in _rows) if (r.FeatureId == featureId) return r;
            return null;
        }

        static string Prettify(string id) =>
            string.IsNullOrEmpty(id) ? "Unknown" : id.Replace('_', ' ');
    }
}
