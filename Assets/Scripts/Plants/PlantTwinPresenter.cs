using PlantDoctor.Core;
using PlantDoctor.Treatment;
using UnityEngine;

namespace PlantDoctor.Plants
{
    /// <summary>Where a plant twin shows up.</summary>
    public enum TwinPresentation
    {
        /// <summary>Not shown (the AR twin on devices without AR).</summary>
        Hidden,
        /// <summary>Always visible: the viewer twin, which only the viewer camera can see.</summary>
        Studio,
        /// <summary>The AR twin: anchored to a real surface by PlantTwinPlacer, shown only on the plant (AR) view.</summary>
        PlacedInWorld
    }

    /// <summary>
    /// Presentation layer for one plant twin: shows and hides it, grows it in when it first
    /// lands, rebuilds it for the identified species and keeps its disease look in step with the
    /// diagnosis. Reads session state; never writes to the simulation. There are two twins (the
    /// AR one and the viewer one), each with its own presenter, sharing one health binder.
    /// </summary>
    public class PlantTwinPresenter : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField] AppFlowController flow;
        [SerializeField] PlantVisualBinder visualBinder;
        [Tooltip("Rebuilds the twin's body for the identified species.")]
        [SerializeField] PlantTwinBuilder twinBuilder;
        [Tooltip("Everything visible about the twin (plant, shadow). Toggled; this component stays active.")]
        [SerializeField] GameObject visualRoot;
        [Tooltip("Scaled for the grow-in animation. Usually the PlantRoot under the twin.")]
        [SerializeField] Transform growRoot;

        [Header("Motion")]
        [SerializeField] float growSeconds = 0.8f;
        [Tooltip("State shown before any scan: a healthy plant.")]
        [SerializeField] PlantHealthState idleState = new PlantHealthState { healthyGrowth = 0.85f, lesionDensity = 0f };

        TwinPresentation _mode = TwinPresentation.Studio;
        float _grow = 1f;        // 0..1 progress of the grow-in
        bool _visible = true;

        public bool IsVisible => _visible;
        public TwinPresentation Mode => _mode;

        void Awake()
        {
            if (growRoot == null) growRoot = transform;
        }

        void OnEnable()
        {
            GameEvents.OnStateChanged += HandleStateChanged;
            GameEvents.OnPlantIdentified += HandlePlantIdentified;
            GameEvents.OnTreatmentStepCompleted += HandleStepCompleted;
            GameEvents.OnRecoveryCompleted += HandleRecoveryCompleted;
        }

        void OnDisable()
        {
            GameEvents.OnStateChanged -= HandleStateChanged;
            GameEvents.OnPlantIdentified -= HandlePlantIdentified;
            GameEvents.OnTreatmentStepCompleted -= HandleStepCompleted;
            GameEvents.OnRecoveryCompleted -= HandleRecoveryCompleted;
        }

        void Start() => visualBinder?.SetTargetState(idleState, immediate: true);

        public void SetPresentationMode(TwinPresentation mode)
        {
            _mode = mode;
            // The AR twin waits for the placer to show it; the viewer twin is always there.
            SetVisible(mode == TwinPresentation.Studio, animate: false);
        }

        /// <summary>Shows the twin; with a grow-in the first time it lands on a surface.</summary>
        public void Reveal(bool animate)
        {
            if (_mode == TwinPresentation.Hidden) return;
            SetVisible(true, animate);
        }

        public void Conceal() => SetVisible(false, animate: false);

        void SetVisible(bool visible, bool animate)
        {
            _visible = visible;
            if (visualRoot != null) visualRoot.SetActive(visible);
            if (!visible) return;
            _grow = animate ? 0f : 1f;
            ApplyGrow();
        }

        void HandleStateChanged(SimState from, SimState to)
        {
            if (to == SimState.Welcome) visualBinder?.SetTargetState(idleState);
        }

        void HandlePlantIdentified(PlantData plant)
        {
            // One generator, many species: rebuild the body when the species changes.
            if (twinBuilder != null && plant != null && plant.species != null && twinBuilder.Current != plant.species)
                twinBuilder.Build(plant.species);

            // Then project what was actually observed on the scanned leaf onto it.
            var ctx = flow != null ? flow.Context : null;
            twinBuilder?.ShowDiseaseOn(ctx?.Disease != null ? ctx.Disease.affectedPart : Diseases.AffectedPart.Leaf);
            visualBinder?.SetScanTrace(ctx?.ScanTrace);   // before the look, so the look is applied once with the scan's spots
            visualBinder?.SetDiseaseLook(ctx?.Disease?.simulationProfile);
            if (ctx != null) visualBinder?.SetTargetState(ctx.InitialState, immediate: true);
        }

        void HandleStepCompleted(TreatmentStep step, int index, int total) => visualBinder?.PulseRecovery(0.9f);
        void HandleRecoveryCompleted() => visualBinder?.PulseRecovery(0.6f);

        void Update()
        {
            if (_grow < 1f)
            {
                _grow = Mathf.Min(1f, _grow + Time.deltaTime / Mathf.Max(0.05f, growSeconds));
                ApplyGrow();
            }
        }

        void ApplyGrow()
        {
            if (growRoot == null) return;
            growRoot.localScale = Vector3.one * EaseOutCubic(_grow);
        }

        static float EaseOutCubic(float t)
        {
            t = Mathf.Clamp01(t);
            return 1f - Mathf.Pow(1f - t, 3f);
        }
    }
}
