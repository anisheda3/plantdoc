using System.Collections.Generic;
using PlantDoctor.AI;
using PlantDoctor.Core;
using PlantDoctor.Plants;
using UnityEngine;

namespace PlantDoctor.AR
{
    /// <summary>A single overlay marker positioned in normalised image space.</summary>
    public class OverlayMarker
    {
        public string RegionType;
        public Rect NormalizedBounds;
        public Color Color;
        public string Label;
        public float Score;
        public string FeatureId;
    }

    /// <summary>
    /// Builds the AR overlay set (bounding boxes, labels, highlight colours) from the
    /// AI result. Rendering itself is delegated to whatever view layer is present, so
    /// this class is pure data and unit-testable.
    /// </summary>
    public class AROverlayRenderer : MonoBehaviour
    {
        [SerializeField] PlantDatabase plantDatabase;
        [SerializeField] Color lesionColor = new Color(0.85f, 0.20f, 0.15f);
        [SerializeField] Color yellowingColor = new Color(0.95f, 0.85f, 0.20f);
        [SerializeField] Color healthyColor = new Color(0.30f, 0.80f, 0.35f);
        [SerializeField] float minimumScoreToShow = 0.15f;

        readonly List<OverlayMarker> _markers = new List<OverlayMarker>();
        public IReadOnlyList<OverlayMarker> Markers => _markers;

        void OnEnable() => GameEvents.OnAIAnalysisCompleted += Rebuild;
        void OnDisable() => GameEvents.OnAIAnalysisCompleted -= Rebuild;

        public void Rebuild(PlantAnalysisResult analysis)
        {
            _markers.Clear();
            if (analysis == null || analysis.HasError) return;

            foreach (var region in analysis.regions)
            {
                if (region == null || region.score < minimumScoreToShow) continue;
                _markers.Add(new OverlayMarker
                {
                    RegionType = region.type,
                    NormalizedBounds = region.bounds,
                    Color = ColorFor(region.type),
                    Label = $"{Prettify(region.type)} {region.score:P0}",
                    Score = region.score,
                    FeatureId = FeatureIdFor(region.type)
                });
            }
        }

        public Color ColorFor(string regionType) => regionType switch
        {
            "lesion" => lesionColor,
            "yellowing" => yellowingColor,
            "healthy" => healthyColor,
            _ => Color.white
        };

        static string FeatureIdFor(string regionType) => regionType switch
        {
            "lesion" => "leaf_spots",
            "yellowing" => "yellowing",
            _ => null
        };

        static string Prettify(string s) =>
            string.IsNullOrEmpty(s) ? "Region" : char.ToUpperInvariant(s[0]) + s.Substring(1).Replace('_', ' ');

        /// <summary>Hit-test in normalised image space so users can tap a highlighted region.</summary>
        public OverlayMarker MarkerAt(Vector2 normalizedPoint)
        {
            for (int i = _markers.Count - 1; i >= 0; i--)
            {
                var b = _markers[i].NormalizedBounds;
                if (normalizedPoint.x >= b.xMin && normalizedPoint.x <= b.xMax &&
                    normalizedPoint.y >= b.yMin && normalizedPoint.y <= b.yMax)
                    return _markers[i];
            }
            return null;
        }

        public void Clear() => _markers.Clear();
    }
}
