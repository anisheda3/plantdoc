using PlantDoctor.AI;
using PlantDoctor.Core;
using UnityEngine;

namespace PlantDoctor.Plants
{
    /// <summary>
    /// The only component that turns PlantHealthState into visuals.
    /// Pushes state into shader properties via MaterialPropertyBlock (no material instancing),
    /// plus optional blendshapes and VFX. Nothing else touches renderers.
    /// </summary>
    public class PlantVisualBinder : MonoBehaviour
    {
        [SerializeField] Renderer[] targetRenderers;
        [SerializeField] SkinnedMeshRenderer wiltBlendshapeTarget;
        [SerializeField] int wiltBlendshapeIndex = 0;
        [SerializeField] ParticleSystem sporeVfx;
        [SerializeField] float smoothing = 4f;
        [Tooltip("Seconds the healing glow takes to fade after a treatment step.")]
        [SerializeField] float pulseFadeSeconds = 1.6f;

        static readonly int LesionId    = Shader.PropertyToID("_LesionDensity");
        static readonly int YellowingId = Shader.PropertyToID("_Yellowing");
        static readonly int WiltId      = Shader.PropertyToID("_Wilt");
        static readonly int DamageId    = Shader.PropertyToID("_LeafDamage");
        static readonly int GrowthId    = Shader.PropertyToID("_HealthyGrowth");
        static readonly int GlowId      = Shader.PropertyToID("_RecoveryGlow");

        const float SettledEpsilon = 0.0005f;

        MaterialPropertyBlock _block;
        readonly PlantHealthState _displayed = new PlantHealthState();
        PlantHealthState _target = new PlantHealthState();
        float _glow;
        bool _settled;

        /// <summary>What the plant currently shows (smoothed), for UI that mirrors the twin.</summary>
        public PlantHealthState Displayed => _displayed;

        /// <summary>How many renderers the health state is pushed to each frame it changes.</summary>
        public int RendererCount => targetRenderers != null ? targetRenderers.Length : 0;

        void Awake()
        {
            _block ??= new MaterialPropertyBlock();
            if (_groups == null && (targetRenderers == null || targetRenderers.Length == 0))
                targetRenderers = GetComponentsInChildren<Renderer>();
        }

        public void SetTargetState(PlantHealthState state, bool immediate = false)
        {
            if (state == null) return;
            _target = state.Clone();
            if (immediate) CopyInto(_target, _displayed);
            _settled = false;
            Push();
        }

        static readonly int LesionMapId = Shader.PropertyToID("_LesionMap");
        static readonly int YellowMapId = Shader.PropertyToID("_YellowMap");
        static readonly int LesionColorId = Shader.PropertyToID("_LesionColor");
        static readonly int HaloColorId = Shader.PropertyToID("_HaloColor");
        static readonly int YellowColorId = Shader.PropertyToID("_YellowColor");

        Diseases.SimulationProfile _look;
        bool _defaultsRead;
        Texture _defaultLesionMap, _defaultYellowMap;
        Color _defaultLesion, _defaultHalo, _defaultYellow;

        /// <summary>
        /// Adopts a new set of renderers after the twin was rebuilt for another species,
        /// and re-applies the current disease look and health state to them.
        /// </summary>
        public void SetRenderers(Renderer[] renderers) => SetRenderers(this, renderers);

        /// <summary>
        /// Several owners can feed one binder (the AR twin, the viewer twin, the lifted leaf):
        /// each registers its renderers under its own key and all of them show the same health
        /// state. Registering again replaces that owner's renderers; null removes them.
        /// </summary>
        public void SetRenderers(Object owner, Renderer[] renderers)
        {
            _groups ??= new System.Collections.Generic.Dictionary<Object, Renderer[]>();
            if (renderers == null) _groups.Remove(owner); else _groups[owner] = renderers;

            var all = new System.Collections.Generic.List<Renderer>();
            foreach (var group in _groups.Values) all.AddRange(group);
            targetRenderers = all.ToArray();
            _heroRenderers.Clear();
            foreach (var kv in _groups)
                if (kv.Key is LeafSpecimen && kv.Value != null) foreach (var r in kv.Value) _heroRenderers.Add(r);

            _block ??= new MaterialPropertyBlock();
            _settled = false;
            ApplyLook();
            Push();
        }

        System.Collections.Generic.Dictionary<Object, Renderer[]> _groups;

        /// <summary>
        /// Shows a specific disease's look on the twin: its masks (early blight spots vs late
        /// blight blotches) and its colours (apple rust is orange, powdery mildew is white).
        /// Null restores the material's default look.
        /// </summary>
        public void SetDiseaseLook(Diseases.SimulationProfile profile)
        {
            _look = profile;
            ApplyLook();
        }

        TraceResult _trace;
        readonly System.Collections.Generic.HashSet<Renderer> _heroRenderers = new System.Collections.Generic.HashSet<Renderer>();

        /// <summary>
        /// Plants the spots traced on the scanned leaf onto the twin: their shapes and places become the lesion and
        /// yellowing masks (the magnified leaf gets them one to one, the plant's small leaflets a repeated version),
        /// instead of the authored masks. Null goes back to the authored look.
        /// </summary>
        public void SetScanTrace(TraceResult trace)
        {
            _trace = trace != null && trace.IsUsable ? trace : null;
            ApplyLook();
        }

        /// <summary>Kept for callers that only swap masks.</summary>
        public void SetDiseaseMasks(Texture lesionMask, Texture yellowingMask)
        {
            if (targetRenderers == null) return;
            _block ??= new MaterialPropertyBlock();
            foreach (var r in targetRenderers)
            {
                if (r == null) continue;
                r.GetPropertyBlock(_block);
                if (lesionMask != null) _block.SetTexture(LesionMapId, lesionMask);
                if (yellowingMask != null) _block.SetTexture(YellowMapId, yellowingMask);
                r.SetPropertyBlock(_block);
            }
        }

        void ApplyLook()
        {
            if (targetRenderers == null || targetRenderers.Length == 0) return;
            _block ??= new MaterialPropertyBlock();
            ReadDefaults();

            var lesionMap = _look != null && _look.lesionMask != null ? _look.lesionMask : _defaultLesionMap;
            var yellowMap = _look != null && _look.yellowingMask != null ? _look.yellowingMask : _defaultYellowMap;
            bool custom = _look != null && _look.overrideColors;

            foreach (var r in targetRenderers)
            {
                if (r == null) continue;
                r.GetPropertyBlock(_block);
                var lesionTex = lesionMap;
                var yellowTex = yellowMap;
                if (_trace != null)   // the scan's own spots win over the authored masks
                {
                    bool hero = _heroRenderers.Contains(r);
                    lesionTex = hero ? _trace.HeroLesionMask : _trace.TwinLesionMask;
                    yellowTex = hero ? _trace.HeroYellowMask : _trace.TwinYellowMask;
                }
                if (lesionTex != null) _block.SetTexture(LesionMapId, lesionTex);
                if (yellowTex != null) _block.SetTexture(YellowMapId, yellowTex);
                _block.SetColor(LesionColorId, custom ? _look.lesionColor : _defaultLesion);
                _block.SetColor(HaloColorId, custom ? _look.haloColor : _defaultHalo);
                _block.SetColor(YellowColorId, custom ? _look.yellowColor : _defaultYellow);
                r.SetPropertyBlock(_block);
            }
        }

        void ReadDefaults()
        {
            if (_defaultsRead) return;
            foreach (var r in targetRenderers)
            {
                var m = r != null ? r.sharedMaterial : null;
                if (m == null || !m.HasProperty(LesionColorId)) continue;
                _defaultLesion = m.GetColor(LesionColorId);
                _defaultHalo = m.HasProperty(HaloColorId) ? m.GetColor(HaloColorId) : _defaultLesion;
                _defaultYellow = m.HasProperty(YellowColorId) ? m.GetColor(YellowColorId) : _defaultLesion;
                _defaultLesionMap = m.HasProperty(LesionMapId) ? m.GetTexture(LesionMapId) : null;
                _defaultYellowMap = m.HasProperty(YellowMapId) ? m.GetTexture(YellowMapId) : null;
                // The leaf material is the one with real disease colours; stems share the shader.
                if (m.HasProperty("_DiseaseMask") && m.GetFloat("_DiseaseMask") > 0.9f) break;
            }
            _defaultsRead = true;
        }

        /// <summary>Brief emissive "healing" pulse, played when a treatment step lands.</summary>
        public void PulseRecovery(float strength = 1f)
        {
            _glow = Mathf.Max(_glow, Mathf.Clamp01(strength));
            _settled = false;
        }

        void Update()
        {
            if (_target == null || _settled) return;

            float t = smoothing <= 0f ? 1f : Mathf.Clamp01(Time.deltaTime * smoothing);
            var lerped = PlantHealthState.Lerp(_displayed, _target, t);
            CopyInto(lerped, _displayed);

            if (_glow > 0f)
                _glow = Mathf.Max(0f, _glow - Time.deltaTime / Mathf.Max(0.05f, pulseFadeSeconds));

            Push();

            // Stop re-uploading property blocks every frame once nothing is moving.
            _settled = _glow <= 0f && Distance(_displayed, _target) < SettledEpsilon;
        }

        void Push()
        {
            if (targetRenderers == null || _block == null) return;
            foreach (var r in targetRenderers)
            {
                if (r == null) continue;
                r.GetPropertyBlock(_block);
                _block.SetFloat(LesionId, _displayed.lesionDensity);
                _block.SetFloat(YellowingId, _displayed.yellowing);
                _block.SetFloat(WiltId, _displayed.wilting);
                _block.SetFloat(DamageId, _displayed.leafDamage);
                _block.SetFloat(GrowthId, _displayed.healthyGrowth);
                _block.SetFloat(GlowId, _glow);
                r.SetPropertyBlock(_block);
            }

            if (wiltBlendshapeTarget != null && wiltBlendshapeTarget.sharedMesh != null &&
                wiltBlendshapeIndex < wiltBlendshapeTarget.sharedMesh.blendShapeCount)
                wiltBlendshapeTarget.SetBlendShapeWeight(wiltBlendshapeIndex, _displayed.wilting * 100f);

            if (sporeVfx != null)
            {
                var emission = sporeVfx.emission;
                emission.rateOverTime = _displayed.lesionDensity * 12f;
            }
        }

        static float Distance(PlantHealthState a, PlantHealthState b) =>
            Mathf.Abs(a.lesionDensity - b.lesionDensity) + Mathf.Abs(a.yellowing - b.yellowing) +
            Mathf.Abs(a.wilting - b.wilting) + Mathf.Abs(a.leafDamage - b.leafDamage) +
            Mathf.Abs(a.healthyGrowth - b.healthyGrowth) + Mathf.Abs(a.diseaseSeverity - b.diseaseSeverity);

        static void CopyInto(PlantHealthState src, PlantHealthState dst)
        {
            dst.diseaseSeverity = src.diseaseSeverity;
            dst.lesionDensity = src.lesionDensity;
            dst.yellowing = src.yellowing;
            dst.wilting = src.wilting;
            dst.leafDamage = src.leafDamage;
            dst.healthyGrowth = src.healthyGrowth;
        }
    }
}
