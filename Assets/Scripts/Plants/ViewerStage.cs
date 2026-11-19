using PlantDoctor.UI;
using UnityEngine;

namespace PlantDoctor.Plants
{
    /// <summary>
    /// A small off-screen set, far from the AR scene, holding the viewer twin and the hero leaf
    /// with a camera for each. Screens ask it for a picture (a RenderTexture) of the plant or
    /// the leaf. Because it lives on its own layer and position, nothing in it can drift into
    /// the AR view, and it costs nothing while no screen is showing it.
    /// </summary>
    public class ViewerStage : MonoBehaviour
    {
        [SerializeField] PlantTwinBuilder plantBuilder;
        [SerializeField] Transform plantRoot;
        [SerializeField] Viewport3D plantView;
        [SerializeField] Viewport3D leafView;
        [SerializeField] LeafSpecimen leaf;

        public Viewport3D PlantView => plantView;
        public Viewport3D LeafView => leafView;
        public LeafSpecimen Leaf => leaf;
        public PlantTwinBuilder PlantBuilder => plantBuilder;

        void OnEnable()
        {
            if (plantBuilder != null) plantBuilder.Rebuilt += HandleRebuilt;
        }

        void OnDisable()
        {
            if (plantBuilder != null) plantBuilder.Rebuilt -= HandleRebuilt;
        }

        void Start()
        {
            if (leaf != null)
            {
                leaf.EnsureBuilt();
                leaf.transform.localRotation = Quaternion.LookRotation(Vector3.up, Vector3.back);   // upright, face to the camera
            }
            if (leafView != null && leaf != null) leafView.SetSubject(leaf.transform, Vector3.zero, leaf.FrameRadius);
            if (plantBuilder != null && plantBuilder.Current != null) HandleRebuilt(plantBuilder.Current);
        }

        void HandleRebuilt(SpeciesProfile profile)
        {
            if (profile == null) return;
            // Frame the plant itself: small rosettes (strawberry, squash) by their spread of leaves, not a fixed half metre.
            float h = Mathf.Max(0.3f, Mathf.Max(profile.height, profile.archetype == TwinArchetype.Rosette ? profile.leafSize.x * 1.8f : 0f));
            if (plantView != null && plantRoot != null)
                plantView.SetSubject(plantRoot, new Vector3(0f, h * 0.5f, 0f), Mathf.Max(0.22f, h * 0.62f));
            leaf?.SetSpecies(profile);
            leaf?.SetTint(profile.leafTint);
            // Each species' leaf has its own size and shape (a compound leaf is wider, a grass blade longer): frame it again.
            if (leafView != null && leaf != null) leafView.SetSubject(leaf.transform, Vector3.zero, leaf.FrameRadius);
        }

        /// <summary>Turns the plant camera on only while a screen is showing its picture.</summary>
        public void ShowPlant(bool visible)
        {
            if (plantView != null) plantView.Active = visible;
        }

        public void ShowLeaf(bool visible)
        {
            if (leafView != null) leafView.Active = visible;
        }
    }
}
