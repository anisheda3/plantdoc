using System.Collections.Generic;
using UnityEngine;

namespace PlantDoctor.Plants
{
    /// <summary>
    /// The single, carefully made leaf shown in the zoomed view: the species' own leaf as it grows (a whole compound leaf, three
    /// leaflets on their stalk, a grass blade or one simple leaf; see <see cref="HeroLeafBuilder"/>). It lives in the viewer stage
    /// (never in the AR scene), shares the plant's health state through <see cref="PlantVisualBinder"/>, and stays perfectly still:
    /// its material has no wind, and the only motion is whatever the user does by dragging.
    /// </summary>
    public class LeafSpecimen : MonoBehaviour
    {
        [SerializeField] Material material;
        [Tooltip("Leaf stalks (rachis, petiole) of the zoomed leaf: a still material, no wind.")]
        [SerializeField] Material stalkMaterial;
        [Tooltip("Photographic leaf with transparent background (alpha = outline), stalk at the bottom; used without species photos.")]
        [SerializeField] Texture2D outlineTexture;
        [SerializeField] PlantVisualBinder visualBinder;
        [SerializeField] string layerName = "Viewer";
        [Tooltip("Size of the leaf in the stage's units (its length for a simple leaf).")]
        [SerializeField] float height = 1f;
        [Tooltip("The default photo is a slim leaf; this widens it into the broader blade of a crop leaf.")]
        [SerializeField] float widthStretch = 1.5f;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        static readonly int BaseMapStId = Shader.PropertyToID("_BaseMap_ST");

        MeshFilter _filter, _stalkFilter;
        MeshRenderer _renderer, _stalkRenderer;
        MaterialPropertyBlock _block, _stalkBlock;
        HeroLeafModel _model;
        SpeciesProfile _species;
        Diseases.AffectedPart _part = Diseases.AffectedPart.Leaf;
        Color _tint = Color.white;
        float _lift = 1.35f;

        public Renderer LeafRenderer => _renderer;
        public Transform LeafTransform => _renderer != null ? _renderer.transform : transform;
        public float Height => height;
        /// <summary>Radius of a sphere round the whole leaf, in the stage's units (for framing the camera).</summary>
        public float FrameRadius => height * (_model != null ? _model.Radius : 0.56f);
        public HeroLeafModel Model => _model;

        void Awake() => EnsureBuilt();

        public void EnsureBuilt()
        {
            if (_renderer != null) return;
            int layer = string.IsNullOrEmpty(layerName) ? -1 : LayerMask.NameToLayer(layerName);

            var go = new GameObject("Leaf", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(transform, false);
            if (layer >= 0) go.layer = layer;
            _filter = go.GetComponent<MeshFilter>();
            _renderer = go.GetComponent<MeshRenderer>();
            _renderer.sharedMaterial = material;
            _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
            _block = new MaterialPropertyBlock();

            var stalks = new GameObject("LeafStalks", typeof(MeshFilter), typeof(MeshRenderer));
            stalks.transform.SetParent(go.transform, false);
            if (layer >= 0) stalks.layer = layer;
            _stalkFilter = stalks.GetComponent<MeshFilter>();
            _stalkRenderer = stalks.GetComponent<MeshRenderer>();
            _stalkRenderer.sharedMaterial = stalkMaterial;
            _stalkRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _stalkRenderer.receiveShadows = false;
            _stalkBlock = new MaterialPropertyBlock();

            Rebuild(null);
            visualBinder?.SetRenderers(this, new Renderer[] { _renderer });
        }

        void OnDestroy()
        {
            if (visualBinder != null) visualBinder.SetRenderers(this, null);
            Release();
        }

        void Release()
        {
            if (_model == null) return;
            Kill(_model.Blades);
            Kill(_model.Stalks);
            _model = null;
        }

        static void Kill(Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Destroy(o); else DestroyImmediate(o);
        }

        /// <summary>Slightly tints the leaf for the species (a potato leaf is a little darker than an apple leaf).</summary>
        public void SetTint(Color tint)
        {
            EnsureBuilt();
            _tint = tint;
            if (_model != null && _model.IsOrgan) return;   // a fruit keeps its own colour
            _renderer.GetPropertyBlock(_block);
            _block.SetColor(BaseColorId, Color.Lerp(Color.white, tint, 0.6f) * _lift);   // the default photo is dark; lift it so the veins read
            _renderer.SetPropertyBlock(_block);
        }

        /// <summary>Shows the species' own leaf, built from its leaf photographs with the leaf's real layout and proportions.</summary>
        public void SetSpecies(SpeciesProfile species)
        {
            EnsureBuilt();
            if (species == _species && _model != null) return;
            _species = species;
            Rebuild(species);
        }

        /// <summary>
        /// Which part to show: a leaf, or for a fruit, head or root disease that organ (when the species has one). Returns true when
        /// the specimen changed, so the caller can frame it again.
        /// </summary>
        public bool SetPart(Diseases.AffectedPart part)
        {
            EnsureBuilt();
            if (part == _part) return false;
            _part = part;
            Rebuild(_species);
            return true;
        }

        public bool ShowsOrgan => _model != null && _model.IsOrgan;

        void Rebuild(SpeciesProfile species)
        {
            Release();
            bool photos = FoliageMeshes.HasPhotos(species);
            float fallbackAspect = outlineTexture != null ? outlineTexture.width / (float)outlineTexture.height * widthStretch : 0.45f;
            bool organ = species != null && species.fruit != FruitForm.None
                         && (_part == Diseases.AffectedPart.Fruit || _part == Diseases.AffectedPart.Head || _part == Diseases.AffectedPart.Root);
            _model = organ ? HeroLeafBuilder.BuildOrgan(species) : HeroLeafBuilder.Build(species, outlineTexture, fallbackAspect);
            _filter.sharedMesh = _model.Blades;
            _stalkFilter.sharedMesh = _model.Stalks;
            _stalkRenderer.enabled = _model.Stalks != null && stalkMaterial != null;

            // Centre the leaf on the stage origin (cameras orbit its middle); a grass blade lies across the frame.
            var leaf = _renderer.transform;
            var turn = Quaternion.Euler(0f, _model.Turn, 0f);
            leaf.localRotation = turn;
            leaf.localScale = Vector3.one * height;
            leaf.localPosition = -(turn * _model.Centre) * height;

            _lift = photos ? 1.08f : 1.35f;
            _renderer.GetPropertyBlock(_block);
            if (organ)
            {
                // The fruit in its own colour (and surface, for heads), the calyx or husk in its colour.
                var surface = CropMeshes.Texture(species.fruit);
                _block.SetTexture(BaseMapId, surface != null ? (Texture)surface : Texture2D.whiteTexture);
                _block.SetColor(BaseColorId, species.fruitColors != null && species.fruitColors.Length > 0 ? species.fruitColors[0] : Color.white);
            }
            else
            {
                _block.SetTexture(BaseMapId, photos ? (Texture)species.leafAtlas : outlineTexture != null ? outlineTexture : Texture2D.whiteTexture);
                _block.SetColor(BaseColorId, Color.Lerp(Color.white, _tint, 0.6f) * _lift);
            }
            _block.SetVector(BaseMapStId, new Vector4(1f, 1f, 0f, 0f));   // the mesh's UVs already point into the atlas
            _renderer.SetPropertyBlock(_block);

            _stalkRenderer.GetPropertyBlock(_stalkBlock);
            _stalkBlock.SetColor(BaseColorId, organ ? PlantTwinBuilder.CalyxColor(species.fruit)
                : species != null ? species.stemColor : new Color(0.62f, 0.72f, 0.42f));
            _stalkRenderer.SetPropertyBlock(_stalkBlock);
        }

        /// <summary>World position of a point of the disease masks (UV1) on the leaf's surface.</summary>
        public Vector3 WorldPointAtMask(Vector2 maskUv)
        {
            EnsureBuilt();
            return LeafTransform.TransformPoint(_model.LocalPoint(maskUv));
        }

        /// <summary>Old single-sheet mapping (mask = 0.05 + 0.9 * leaf); still how the scan trace lays its spots out (TraceTextures).</summary>
        public static Vector2 MaskToLeaf(Vector2 maskUv) => new Vector2((maskUv.x - 0.05f) / 0.9f, (maskUv.y - 0.05f) / 0.9f);

        /// <summary>
        /// Finds where the chosen disease actually shows on the leaf, so annotations point at real lesions: the strongest lesion
        /// centres well inside a blade, the edge of one of them (its halo), and the centre of the yellowed area.
        /// </summary>
        public LesionSpots FindSpots(Texture2D lesionMask, Texture2D yellowMask)
        {
            EnsureBuilt();
            var spots = new LesionSpots();
            if (lesionMask == null || !lesionMask.isReadable || _model == null) return spots;

            // The labels must always find something to point at (user, 11 Nov 2026: "the marker pointing on the leaf is gone"). Try the
            // best places first - strong lesions well inside a blade - and relax step by step: nearer the margin, weaker lesions, any
            // leaf tissue. Narrow grass blades, the gaps between leaflets and a scan's own spots used to leave no candidate at all.
            const int grid = 96;
            var middle = _model.Middle;
            var tiers = new (int ring, float minValue, float apart)[] { (3, 0.7f, 0.18f), (2, 0.5f, 0.14f), (1, 0.3f, 0.1f), (0, 0.12f, 0.08f) };
            foreach (var (ring, minValue, apart) in tiers)
            {
                var peaks = new List<(Vector2 uv, float value)>();
                for (int y = 1; y < grid - 1; y++)
                    for (int x = 1; x < grid - 1; x++)
                    {
                        var uv = new Vector2((x + 0.5f) / grid, (y + 0.5f) / grid);
                        if (!OnLeaf(uv, ring)) continue;
                        if (ring > 0 && Vector2.Distance(uv, _model.BaseMask) < 0.12f) continue;   // away from the stalk
                        float value = lesionMask.GetPixelBilinear(uv.x, uv.y).r;
                        if (value > minValue) peaks.Add((uv, value - 0.6f * Vector2.Distance(uv, middle)));
                    }
                peaks.Sort((a, b) => b.value.CompareTo(a.value));
                foreach (var peak in peaks)
                {
                    bool far = true;
                    foreach (var kept in spots.LesionMaskUv)
                        if (Vector2.Distance(kept, peak.uv) < apart) { far = false; break; }
                    if (!far) continue;
                    spots.LesionMaskUv.Add(peak.uv);
                    if (spots.LesionMaskUv.Count >= 3) break;
                }
                if (spots.LesionMaskUv.Count >= 2) break;
            }

            if (spots.LesionMaskUv.Count > 0)
            {
                // The halo sits just outside a lesion: walk out from it until the mask drops away, staying on the leaf.
                var c = spots.LesionMaskUv[Mathf.Min(2, spots.LesionMaskUv.Count - 1)];   // a different lesion from the first label
                for (int k = 0; k < 8 && !spots.HaloMaskUv.HasValue; k++)
                {
                    float a = k * Mathf.PI / 4f;
                    var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    float edge = 0.01f;
                    while (edge < 0.2f && lesionMask.GetPixelBilinear(c.x + dir.x * edge, c.y + dir.y * edge).r > 0.55f) edge += 0.004f;
                    var halo = c + dir * (edge + 0.012f);
                    if (OnLeaf(halo, 0)) spots.HaloMaskUv = halo;
                }
            }

            // The yellowed (or otherwise affected) area: the strongest patch of the chlorosis mask on the leaf, with the same relaxing.
            if (yellowMask != null && yellowMask.isReadable)
                foreach (int ring in new[] { 4, 2, 0 })
                {
                    float best = -1f;
                    for (int y = 3; y < grid - 3; y += 2)
                        for (int x = 3; x < grid - 3; x += 2)
                        {
                            var uv = new Vector2((x + 0.5f) / grid, (y + 0.5f) / grid);
                            if (!OnLeaf(uv, ring) || (ring > 0 && Vector2.Distance(uv, _model.BaseMask) < 0.15f)) continue;
                            float sum = 0f;
                            for (int dy = -3; dy <= 3; dy += 3)
                                for (int dx = -3; dx <= 3; dx += 3)
                                    sum += yellowMask.GetPixelBilinear(uv.x + dx / (float)grid, uv.y + dy / (float)grid).r;
                            sum -= Vector2.Distance(uv, middle) * 0.5f;
                            foreach (var kept in spots.LesionMaskUv)
                                if (Vector2.Distance(kept, uv) < 0.06f) sum -= 2f;                    // not on top of a lesion label
                            if (sum > best) { best = sum; spots.AreaMaskUv = uv; }
                        }
                    if (spots.AreaMaskUv.HasValue) break;
                }

            // Last resort: whatever was found, there is always at least one label on the leaf, near its middle.
            if (spots.LesionMaskUv.Count == 0 && !spots.AreaMaskUv.HasValue)
            {
                float best = float.MaxValue;
                for (int y = 1; y < grid - 1; y++)
                    for (int x = 1; x < grid - 1; x++)
                    {
                        var uv = new Vector2((x + 0.5f) / grid, (y + 0.5f) / grid);
                        float d = Vector2.Distance(uv, middle);
                        if (d < best && OnLeaf(uv, 0)) { best = d; spots.AreaMaskUv = uv; }
                    }
            }
            return spots;
        }

        /// <summary>A mask position on leaf tissue, at least <paramref name="ring"/> grid cells in from the margin (0 = anywhere on the leaf).</summary>
        bool OnLeaf(Vector2 uv, int ring) => ring > 0 ? _model.Interior(uv, ring) : _model.Coverage(uv) >= 0.5f;

        public class LesionSpots
        {
            public readonly List<Vector2> LesionMaskUv = new List<Vector2>();
            public Vector2? HaloMaskUv;
            public Vector2? AreaMaskUv;
        }
    }
}
