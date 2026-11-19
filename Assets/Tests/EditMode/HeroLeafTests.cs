using NUnit.Framework;
using PlantDoctor.Plants;
using UnityEditor;
using UnityEngine;

namespace PlantDoctor.Tests
{
    /// <summary>
    /// The zoomed leaf is each species' own leaf as it grows (user request, 10 Nov 2026): whole compound and trifoliate leaves with
    /// gaps between the leaflets, grass blades across the frame, and for fruit diseases the fruit itself.
    /// </summary>
    public class HeroLeafTests
    {
        static SpeciesProfile Species(string id)
        {
            var database = AssetDatabase.LoadAssetAtPath<PlantDatabase>("Assets/Data/Plants/PlantDatabase.asset");
            if (database == null) Assert.Ignore("Demo data not generated yet.");
            database.Rebuild();
            Assert.IsTrue(database.TryResolve(id, out var plant), id);
            return plant.species;
        }

        static (int covered, int gaps, int interior) Count(HeroLeafModel m)
        {
            int covered = 0, gaps = 0, interior = 0;
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                {
                    var uv = new Vector2((x + 0.5f) / 64f, (y + 0.5f) / 64f);
                    bool inBox = m.Span.Contains(uv);
                    float c = m.Coverage(uv);
                    if (c >= 0.5f) covered++;
                    else if (inBox) gaps++;
                    if (m.Interior(uv, 2)) interior++;
                }
            return (covered, gaps, interior);
        }

        [TestCase("tomato")]
        [TestCase("potato")]
        [TestCase("raspberry")]
        [TestCase("strawberry")]
        [TestCase("soybean")]
        public void CompoundAndTrifoliateLeavesAreWholeLeavesWithGapsBetweenLeaflets(string id)
        {
            var model = HeroLeafBuilder.Build(Species(id), null, 0.45f);
            try
            {
                var (covered, gaps, interior) = Count(model);
                Assert.Greater(covered, 600, $"{id}: the leaflets cover a good part of the leaf's box.");
                Assert.Greater(gaps, 150, $"{id}: there are gaps between the leaflets (a whole leaf, not one leaflet).");
                Assert.Greater(interior, 100, $"{id}: there is room inside the leaflets for the labels.");
                Assert.IsNotNull(model.Stalks, $"{id}: the rachis or petiole is drawn.");
            }
            finally { Object.DestroyImmediate(model.Blades); if (model.Stalks != null) Object.DestroyImmediate(model.Stalks); }
        }

        [Test]
        public void GrassBladesLieAcrossTheFrame()
        {
            var model = HeroLeafBuilder.Build(Species("corn"), null, 0.45f);
            try { Assert.AreNotEqual(0f, model.Turn, "A long blade is turned to lie across the frame."); }
            finally { Object.DestroyImmediate(model.Blades); }
        }

        /// <summary>
        /// The zoom step's labels need places to point at: for every disease of every plant, the specimen the app shows (the leaf, or
        /// the fruit, head or root) yields at least two marker points, all on leaf or fruit tissue (user, 11 Nov 2026).
        /// </summary>
        [Test]
        public void EveryDiseaseGetsMarkersOnTheZoomedLeaf()
        {
            var database = AssetDatabase.LoadAssetAtPath<PlantDatabase>("Assets/Data/Plants/PlantDatabase.asset");
            if (database == null) Assert.Ignore("Demo data not generated yet.");
            database.Rebuild();
            var go = new GameObject("Specimen");
            try
            {
                var leaf = go.AddComponent<LeafSpecimen>();
                var failures = new System.Collections.Generic.List<string>();
                int checkedCount = 0;
                foreach (var plant in database.plants)
                {
                    if (plant == null || plant.diseases == null) continue;
                    leaf.SetSpecies(plant.species);
                    foreach (var d in plant.diseases)
                    {
                        if (d == null || d.simulationProfile == null) continue;
                        leaf.SetPart(d.affectedPart);
                        var spots = leaf.FindSpots(d.simulationProfile.lesionMask, d.simulationProfile.yellowingMask);
                        int n = spots.LesionMaskUv.Count + (spots.HaloMaskUv.HasValue ? 1 : 0) + (spots.AreaMaskUv.HasValue ? 1 : 0);
                        bool onTissue = true;
                        foreach (var uv in spots.LesionMaskUv) onTissue &= leaf.Model.Coverage(uv) >= 0.5f;
                        if (spots.AreaMaskUv.HasValue) onTissue &= leaf.Model.Coverage(spots.AreaMaskUv.Value) >= 0.5f;
                        if (n < 2 || !onTissue) failures.Add($"{plant.plantId}/{d.diseaseId}: {n} markers{(onTissue ? "" : ", one off the leaf")}");
                        checkedCount++;
                    }
                }
                Assert.Greater(checkedCount, 100);
                Assert.IsEmpty(failures, string.Join("; ", failures));
            }
            finally { Object.DestroyImmediate(go); }
        }

        [TestCase("pepper")]
        [TestCase("peach")]
        [TestCase("banana")]
        [TestCase("cauliflower")]
        [TestCase("carrot")]
        public void FruitDiseasesZoomOnTheFruitWithRoomForLabels(string id)
        {
            var model = HeroLeafBuilder.BuildOrgan(Species(id));
            try
            {
                Assert.IsTrue(model.IsOrgan);
                Assert.IsTrue(model.Interior(model.Middle, 1) || Count(model).interior > 20, $"{id}: the side facing the viewer is solid surface.");
                Assert.Greater(model.Radius, 0.1f);
            }
            finally { Object.DestroyImmediate(model.Blades); if (model.Stalks != null) Object.DestroyImmediate(model.Stalks); }
        }
    }
}
