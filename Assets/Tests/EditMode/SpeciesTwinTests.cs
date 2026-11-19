using NUnit.Framework;
using PlantDoctor.Plants;
using UnityEditor;
using UnityEngine;

namespace PlantDoctor.Tests
{
    /// <summary>
    /// One generator, many species: every supported plant must build a twin of the right
    /// size with disease-carrying renderers, and its data must be complete enough to run
    /// the diagnose -> treat -> prevent loop.
    /// </summary>
    public class SpeciesTwinTests
    {
        const string DatabasePath = "Assets/Data/Plants/PlantDatabase.asset";
        static readonly string[] Supported = { "tomato", "potato", "pepper", "apple", "blueberry", "cherry", "corn", "grape", "orange",
                                                "peach", "raspberry", "soybean", "squash", "strawberry",
                                                // PlantSeg
                                                "banana", "basil", "bean", "broccoli", "cabbage", "carrot", "cauliflower", "celery", "coffee",
                                                "cucumber", "eggplant", "garlic", "ginger", "lettuce", "maple", "plum", "rice", "tobacco", "wheat",
                                                // mango, sugarcane, chilli (other datasets, 11 Nov 2026)
                                                "mango", "sugarcane", "chilli",
                                                // cotton (CropScan XR classes, 19 Nov 2026)
                                                "cotton" };

        GameObject _root;

        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.DestroyImmediate(_root);
        }

        PlantTwinBuilder NewBuilder()
        {
            _root = new GameObject("PlantRoot");
            var builder = _root.AddComponent<PlantTwinBuilder>();
            // Fruit is skipped without a material; any material will do for geometry checks.
            var so = new SerializedObject(builder);
            so.FindProperty("fruitMaterial").objectReferenceValue = new Material(Shader.Find("Hidden/InternalErrorShader"));
            so.ApplyModifiedPropertiesWithoutUndo();
            return builder;
        }

        static Bounds RendererBounds(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>();
            var b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            return b;
        }

        static SpeciesProfile Herb()
        {
            var p = ScriptableObject.CreateInstance<SpeciesProfile>();
            p.speciesId = "test_herb";
            p.archetype = TwinArchetype.Herb;
            p.height = 0.8f;
            p.fruit = FruitForm.None;
            return p;
        }

        static SpeciesProfile Tree()
        {
            var p = ScriptableObject.CreateInstance<SpeciesProfile>();
            p.speciesId = "test_tree";
            p.archetype = TwinArchetype.Tree;
            p.leafForm = LeafForm.Simple;
            p.leafSize = new Vector2(0.1f, 0.075f);
            p.fruit = FruitForm.Apple;
            return p;
        }

        [Test]
        public void HerbTwinHasDiseaseRenderersAndRoughlyTheProfileHeight()
        {
            var builder = NewBuilder();
            var profile = Herb();
            builder.Build(profile);

            Assert.AreSame(profile, builder.Current);
            Assert.Greater(builder.DiseaseRenderers.Count, profile.leafCount / 2, "Every leaf and stem should carry the disease look.");
            float top = RendererBounds(_root).max.y;
            Assert.That(top, Is.InRange(profile.height * 0.8f, profile.height * 1.3f), $"Herb twin top at {top:0.00} m.");
        }

        [Test]
        public void TreeTwinHasATrunkCanopyAndFruit()
        {
            var builder = NewBuilder();
            var profile = Tree();
            builder.Build(profile);

            Assert.AreEqual(2, builder.DiseaseRenderers.Count, "Tree = one wood mesh + one canopy mesh.");
            var canopy = _root.transform.Find("Generated/Canopy").GetComponent<Renderer>().bounds;
            Assert.Greater(canopy.max.y, profile.trunkHeight + 0.2f, "Canopy must sit above the trunk.");
            Assert.Greater(canopy.extents.x, profile.canopyRadius * 0.5f, "Canopy must spread out sideways.");
            Assert.IsNotNull(_root.transform.Find("Generated/Fruit_0"), "Apple twin should carry fruit.");
        }

        [Test]
        public void RebuildingReplacesTheOldBody()
        {
            var builder = NewBuilder();
            builder.Build(Herb());
            builder.Build(Tree());

            int generated = 0;
            foreach (Transform child in _root.transform)
                if (child.name == "Generated") generated++;
            Assert.AreEqual(1, generated, "The previous species' parts must be removed.");
            Assert.IsNotNull(_root.transform.Find("Generated/Wood"));
            Assert.IsNull(_root.transform.Find("Generated/Pot"));
        }

        [Test]
        public void PlantsStandOnSoilAndRootsNotInAPot()
        {
            var builder = NewBuilder();
            builder.Build(Herb());
            Assert.IsNull(_root.transform.Find("Generated/Pot"), "There are no pots.");
            Assert.IsNotNull(_root.transform.Find("Generated/SoilMound"));
            var roots = _root.transform.Find("Generated/Roots");
            Assert.IsNotNull(roots, "The root system is part of every twin.");

            var mesh = roots.GetComponent<MeshFilter>().sharedMesh;
            Assert.Greater(mesh.vertexCount, 500, "A root system has many branching roots.");
            Assert.GreaterOrEqual(mesh.bounds.min.y, -0.002f, "Roots lie on the surface the plant stands on, never below it.");
            Assert.Greater(mesh.bounds.extents.x, 0.08f, "Roots spread out around the crown.");
        }

        [Test]
        public void LeavesUseTheSpeciesPhotographsWhenTheyExist()
        {
            var database = AssetDatabase.LoadAssetAtPath<PlantDatabase>(DatabasePath);
            if (database == null) Assert.Ignore("Demo data not generated yet (Tools > AI Plant Doctor > Build App Scene).");
            database.Rebuild();
            foreach (var id in Supported)
            {
                database.TryResolve(id, out var plant);
                var profile = plant.species;
                Assert.IsTrue(FoliageMeshes.HasPhotos(profile), $"{id} should have leaf photographs (run tools/assets/make_plant_textures.py).");
                foreach (var r in profile.leafRects)
                {
                    Assert.That(r.xMin, Is.InRange(0f, 1f)); Assert.That(r.xMax, Is.InRange(0f, 1f));
                    Assert.That(r.yMin, Is.InRange(0f, 1f)); Assert.That(r.yMax, Is.InRange(0f, 1f));
                }

                var builder = NewBuilder();
                builder.Build(profile);
                Assert.IsNotNull(_root.transform.Find("Generated/Roots"), $"{id} stands on its roots.");
                Assert.IsNull(_root.transform.Find("Generated/Pot"));
                Object.DestroyImmediate(_root);
            }
        }

        /// <summary>
        /// Leaf size against plant height stays inside what the floras report (docs/PLANT_MORPHOLOGY_SOURCES.md). The leaf measured is
        /// the blade (stalk excluded) for simple and trifoliate leaves (a trifoliate's terminal leaflet), the whole leaf for compound
        /// leaves, and the blade for corn and the tree canopies. Ranges marked "assumed" use a plant height the sources do not give.
        /// </summary>
        [TestCase("tomato", 0.10f, 0.25f)]      // leaves to 30 cm on ~1.5 m
        [TestCase("pepper", 0.05f, 0.65f)]      // blades 4-13 cm on 20-80 cm
        [TestCase("apple", 0.02f, 0.05f)]       // 7-10.5 cm on 2-4 m (cultivars)
        [TestCase("potato", 0.08f, 0.47f)]      // leaf 8-28 cm (blade 8-22 + petiole 2-6, Solanaceae Source) on 0.6-1 m
        [TestCase("blueberry", 0.003f, 0.07f)]  // blades 1.5-7 cm on 1-5 m
        [TestCase("cherry", 0.005f, 0.045f)]    // blades 3-13 cm on an assumed 3-6 m orchard tree
        [TestCase("corn", 0.125f, 0.9f)]        // blades 50-90 cm on 1-4 m
        [TestCase("grape", 0.028f, 0.12f)]      // blades 7-18 cm on an assumed 1.5-2.5 m trained vine
        [TestCase("orange", 0.009f, 0.045f)]    // leaves 7.5-11 cm on 2.7 m to an assumed 8 m
        [TestCase("peach", 0.009f, 0.05f)]      // blades 7-15 cm on 3-8 m
        [TestCase("raspberry", 0.03f, 0.3f)]    // terminal leaflet 3-15 cm (whole leaf assumed 2x) on 1-2 m
        [TestCase("soybean", 0.025f, 0.4f)]     // terminal leaflet 5-12 cm on 0.3-2 m
        [TestCase("squash", 0.085f, 0.49f)]     // blades 110-340 mm (UCLA) on 0.7-1.3 m (KPU)
        [TestCase("strawberry", 0.075f, 0.7f)]  // leaflets 3-7 cm on 10-40 cm
        // PlantSeg plants (docs/PLANT_MORPHOLOGY_SOURCES.md, "The PlantSeg plants")
        [TestCase("banana", 0.3f, 0.8f)]        // blades 1.5-2.5 m on 3-5 m plants
        [TestCase("basil", 0.03f, 0.4f)]        // blades 2.5-5 (8) cm on 20-80 cm
        [TestCase("bean", 0.1f, 0.5f)]          // leaflets 6-15 cm on bush plants 30-60 cm (height assumed)
        [TestCase("broccoli", 0.2f, 0.75f)]     // blades to 40-45 cm on 0.6-1 m
        [TestCase("cabbage", 0.5f, 1.5f)]       // outer blades 30-45 cm on 0.3-0.5 m plants (assumed)
        [TestCase("cauliflower", 0.3f, 1.0f)]   // blades to 40-45 cm on 0.5-0.8 m (assumed)
        [TestCase("carrot", 0.3f, 1.0f)]        // fronds 20-30 cm on 30-60 cm foliage (assumed)
        [TestCase("celery", 0.4f, 1.2f)]        // whole leaves 40-60 cm on 0.5-1 m
        [TestCase("coffee", 0.05f, 0.1f)]       // leaves to 10-12 cm on bushes pruned to 1.2-1.8 m
        [TestCase("cucumber", 0.03f, 0.14f)]    // blades 7-20 cm on vines trained to 1.5-2 m (height assumed)
        [TestCase("eggplant", 0.02f, 0.5f)]     // blades 3-25 cm on 0.5-1.5 m
        [TestCase("garlic", 0.5f, 0.95f)]       // blades to 50 cm on plants 0.5-0.8 m to the leaf tips (assumed)
        [TestCase("ginger", 0.05f, 1.0f)]       // blades 5-30 cm on pseudostems 30-100 cm
        [TestCase("lettuce", 0.6f, 1.2f)]       // a rosette about as tall as its leaves are long (assumed)
        [TestCase("maple", 0.02f, 0.125f)]      // leaves 10-25 cm on a young tree of 2-5 m (assumed)
        [TestCase("plum", 0.004f, 0.02f)]       // leaves 4-10 cm on 5-10 m trees
        [TestCase("rice", 0.17f, 1.2f)]         // blades 25-60 cm on 0.5-1.5 m
        [TestCase("tobacco", 0.1f, 1.0f)]       // blades 20-50 cm on 0.5-2 m
        [TestCase("wheat", 0.09f, 1.0f)]        // blades 15-40 cm on 40-170 cm
        [TestCase("mango", 0.0025f, 0.075f)]    // leaves 10-30 cm on trees of 4 m (young, assumed) to 40 m
        [TestCase("sugarcane", 0.12f, 0.5f)]    // blades 70-150 cm on 3-6 m canes
        [TestCase("chilli", 0.03f, 0.4f)]       // leaves to 7.5-12 cm on 0.3-1.2 m
        [TestCase("cotton", 0.05f, 0.35f)]      // blades 7.5-15 cm, on vigorous plants to ~18 cm (+ petiole as long) on 1-1.5 m
        public void LeafSizeRelativeToThePlantMatchesPublishedProportions(string id, float min, float max)
        {
            var p = Profile(id);
            bool stalked = p.archetype != TwinArchetype.Tree && (p.leafForm == LeafForm.Simple || p.leafForm == LeafForm.Trifoliate);
            float blade = stalked ? p.leafSize.x / (1f + p.petioleLength) : p.leafSize.x;
            float ratio = blade / p.height;
            Assert.That(ratio, Is.InRange(min, max), $"{id}: largest leaf is {ratio:0.000} of the plant's height; the sources give {min:0.000}-{max:0.000}.");
        }

        /// <summary>Leaf stalk against blade (petiole / blade length) from the same floras.</summary>
        [TestCase("grape", 0.22f, 1.29f)]       // petiole 4-9 cm, blade 7-18 cm
        [TestCase("strawberry", 0.29f, 3.3f)]   // petiole 2-10 cm, leaflets 3-7 cm
        [TestCase("soybean", 0.17f, 4f)]        // petiole 2-20 cm, terminal leaflet 5-12 cm
        [TestCase("squash", 0.5f, 3.4f)]        // petiole 200-370 mm on 110-340 mm blades (UCLA); 15-25 cm on 20-30 cm (KPU)
        [TestCase("cucumber", 0.5f, 2.9f)]      // petiole 10-16 (20) cm on 7-20 cm blades
        [TestCase("eggplant", 0.24f, 3.3f)]     // petiole 6-10 cm on 3-25 cm blades
        [TestCase("banana", 0.3f, 0.45f)]       // petiole ~80 cm on 1.9-2.3 m blades
        [TestCase("broccoli", 0.2f, 0.8f)]      // petioles to 30 cm on blades to 40-45 cm
        public void LeafStalkRelativeToTheBladeMatchesPublishedProportions(string id, float min, float max)
        {
            Assert.That(Profile(id).petioleLength, Is.InRange(min, max));
        }

        /// <summary>The fuller crowns are a declared display exception, limited to the four fruit trees and to at most twice the real size.</summary>
        [Test]
        public void OnlyTheFruitTreesGetTheLushCanopyException()
        {
            foreach (var id in Supported)
            {
                float scale = Profile(id).canopyLeafScale;
                bool fruitTree = id == "apple" || id == "cherry" || id == "orange" || id == "peach" || id == "plum" || id == "mango";
                if (fruitTree) Assert.That(scale, Is.InRange(1f, 2f), $"{id}: canopy leaves at most twice their real size.");
                else Assert.AreEqual(1f, scale, $"{id} keeps real leaf proportions.");
            }
        }

        /// <summary>Fruit drawn larger for visibility is a declared exception, limited to the fruiting shrubs and trees, at most 2x.</summary>
        [Test]
        public void OnlyTheFruitCropsGetTheFruitVisibilityException()
        {
            var allowed = new[] { "apple", "blueberry", "cherry", "orange", "peach", "raspberry", "plum", "mango" };
            foreach (var id in Supported)
            {
                var p = Profile(id);
                if (System.Array.IndexOf(allowed, id) >= 0) Assert.That(p.fruitDisplayScale, Is.InRange(1f, 2f), $"{id}: fruit at most twice its real size.");
                else Assert.AreEqual(1f, p.fruitDisplayScale, $"{id} keeps its real fruit size.");
                if (p.canopyLeafScale > 1f) Assert.AreEqual(p.canopyLeafScale, p.fruitDisplayScale, 1e-4f, $"{id}: fruit scaled with the leaves keeps their real proportion.");
            }
        }

        [Test]
        public void FruitAndRootDetailsFollowTheSources()
        {
            var orange = Profile("orange");
            float fruitOverLeaf = orange.fruitRadius * 2f / orange.leafSize.x;
            Assert.That(fruitOverLeaf, Is.InRange(0.55f, 1.2f), "Oranges are 6-9 cm across on 7.5-11 cm leaves (Flora of Pakistan).");
            Assert.IsFalse(Profile("blueberry").rootHairs, "Blueberry roots have no root hairs.");
            Assert.Greater(Profile("soybean").rootNodules, 0, "Soybean roots carry nitrogen-fixing nodules.");
            Assert.Greater(Profile("corn").braceRoots, 0, "Corn has brace roots from its lowest nodes.");
        }

        SpeciesProfile Profile(string id)
        {
            var database = AssetDatabase.LoadAssetAtPath<PlantDatabase>(DatabasePath);
            if (database == null) Assert.Ignore("Demo data not generated yet (Tools > AI Plant Doctor > Build App Scene).");
            database.Rebuild();
            Assert.IsTrue(database.TryResolve(id, out var plant), $"'{id}' missing from the database.");
            return plant.species;
        }

        [Test]
        public void RootsAreHalfBuriedUnderSoilRidges()
        {
            var builder = NewBuilder();
            builder.Build(Herb());
            var crumbs = _root.transform.Find("Generated/RootSoil");
            Assert.IsNotNull(crumbs, "Soil ridges and crumbs cover the roots near the crown.");
            var mesh = crumbs.GetComponent<MeshFilter>().sharedMesh;
            Assert.Greater(mesh.vertexCount, 400, "Each primary root has a ridge over its first part.");
            Assert.GreaterOrEqual(mesh.bounds.min.y, -0.002f, "Soil never goes below the surface the plant stands on.");
        }

        [Test]
        public void EverySupportedPlantHasAProfileAndCompleteDiseaseData()
        {
            var database = AssetDatabase.LoadAssetAtPath<PlantDatabase>(DatabasePath);
            if (database == null) Assert.Ignore("Demo data not generated yet (Tools > AI Plant Doctor > Build App Scene).");
            database.Rebuild();

            foreach (var id in Supported)
            {
                Assert.IsTrue(database.TryResolve(id, out var plant), $"'{id}' missing from the database.");
                Assert.IsNotNull(plant.species, $"{id} has no species profile, so its twin can't be built.");
                Assert.AreEqual(id, plant.species.speciesId);
                Assert.AreEqual(ExpectedDiseases[id], plant.diseases.Length, $"{id}: one disease per PlantVillage and PlantSeg class (plus pepper powdery mildew).");
                Assert.Greater(plant.diseases.Length, 0, $"{id} must have at least one disease (user request, 10 Nov 2026).");
                foreach (var d in plant.diseases)
                {
                    Assert.IsNotNull(d.treatmentPlan, $"{id}/{d.diseaseId} has no treatment plan.");
                    Assert.GreaterOrEqual(d.treatmentPlan.steps.Length, 3, $"{id}/{d.diseaseId} needs treatment steps.");
                    Assert.Greater(d.treatmentPlan.residualSeverityFloor, 0f, "Treatment must never model a full cure.");
                    Assert.IsNotNull(d.preventionPlan, $"{id}/{d.diseaseId} has no prevention plan.");
                    Assert.IsNotNull(d.simulationProfile, $"{id}/{d.diseaseId} has no simulation profile.");
                    Assert.IsNotNull(d.simulationProfile.lesionMask, $"{id}/{d.diseaseId} has no lesion mask.");
                    Assert.IsNotNull(d.simulationProfile.yellowingMask, $"{id}/{d.diseaseId} has no yellowing mask.");
                    Assert.AreEqual(4, d.lifecycleStages != null ? d.lifecycleStages.Length : 0, $"{id}/{d.diseaseId} needs four lifecycle stages.");
                    Assert.AreEqual(4, d.conditionFactors != null ? d.conditionFactors.Length : 0, $"{id}/{d.diseaseId} needs four conditions.");
                    bool organ = d.affectedPart == Diseases.AffectedPart.Fruit || d.affectedPart == Diseases.AffectedPart.Head || d.affectedPart == Diseases.AffectedPart.Root;
                    if (organ) Assert.AreNotEqual(FruitForm.None, plant.species.fruit, $"{id}/{d.diseaseId} shows on the {d.affectedPart}, so the twin needs one.");
                }
            }
        }

        /// <summary>
        /// Diseases per plant: the PlantVillage classes (plus pepper powdery mildew), then the PlantSeg classes not already there
        /// (zucchini's go to squash, citrus canker to orange).
        /// </summary>
        static readonly System.Collections.Generic.Dictionary<string, int> ExpectedDiseases = new System.Collections.Generic.Dictionary<string, int>
        {
            { "tomato", 9 }, { "potato", 2 }, { "pepper", 4 }, { "apple", 4 }, { "blueberry", 5 }, { "cherry", 2 }, { "corn", 4 },
            { "grape", 5 }, { "orange", 2 }, { "peach", 6 }, { "raspberry", 4 }, { "soybean", 6 }, { "squash", 4 }, { "strawberry", 2 },
            { "banana", 6 }, { "basil", 1 }, { "bean", 3 }, { "broccoli", 3 }, { "cabbage", 3 }, { "carrot", 3 }, { "cauliflower", 2 },
            { "celery", 2 }, { "coffee", 4 }, { "cucumber", 3 }, { "eggplant", 3 }, { "garlic", 2 }, { "ginger", 2 }, { "lettuce", 2 },
            { "maple", 1 }, { "plum", 5 }, { "rice", 5 }, { "tobacco", 4 }, { "wheat", 10 },
            { "mango", 7 }, { "sugarcane", 4 }, { "chilli", 5 }, { "cotton", 3 }
        };

        /// <summary>Every PlantSeg class written in the data files resolves, by its dataset label, to a disease of its plant.</summary>
        [Test]
        public void EveryPlantSegLabelResolvesToADiseaseOfItsPlant()
        {
            var database = AssetDatabase.LoadAssetAtPath<PlantDatabase>(DatabasePath);
            if (database == null) Assert.Ignore("Demo data not generated yet.");
            database.Rebuild();
            int labels = 0;
            foreach (var path in System.IO.Directory.GetFiles("Assets/Editor/Data/PlantSeg", "*.json"))
            {
                var json = System.IO.File.ReadAllText(path);
                string plantId = System.Text.RegularExpressions.Regex.Match(json, "\"plantId\":\\s*\"([^\"]+)\"").Groups[1].Value;
                Assert.IsTrue(database.TryResolve(plantId, out var plant), $"{plantId} missing.");
                foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(json, "\"labels\":\\s*\\[([^\\]]*)\\]"))
                    foreach (System.Text.RegularExpressions.Match l in System.Text.RegularExpressions.Regex.Matches(m.Groups[1].Value, "\"([^\"]+)\""))
                    {
                        Assert.IsNotNull(plant.FindDisease(l.Groups[1].Value), $"{plantId}: label '{l.Groups[1].Value}' does not resolve.");
                        labels++;
                    }
            }
            Assert.GreaterOrEqual(labels, 115, "Every one of PlantSeg's 115 classes (plus PlantDoc's) is a label of some disease.");
        }

        [TestCase("Apple___Black_rot", "apple", "black_rot")]
        [TestCase("Cherry___Powdery_mildew", "cherry", "powdery_mildew")]
        [TestCase("Corn___Cercospora_leaf_spot Gray_leaf_spot", "corn", "gray_leaf_spot")]
        [TestCase("Corn___Common_rust", "corn", "common_rust")]
        [TestCase("Corn___Northern_Leaf_Blight", "corn", "northern_leaf_blight")]
        [TestCase("Grape___Black_rot", "grape", "black_rot")]
        [TestCase("Grape___Esca_(Black_Measles)", "grape", "esca")]
        [TestCase("Grape___Leaf_blight_(Isariopsis_Leaf_Spot)", "grape", "leaf_blight")]
        [TestCase("Orange___Haunglongbing_(Citrus_greening)", "orange", "citrus_greening")]
        [TestCase("Peach___Bacterial_spot", "peach", "bacterial_spot")]
        [TestCase("Potato___Late_blight", "potato", "late_blight")]
        [TestCase("Squash___Powdery_mildew", "squash", "powdery_mildew")]
        [TestCase("Strawberry___Leaf_scorch", "strawberry", "leaf_scorch")]
        [TestCase("Tomato___Bacterial_spot", "tomato", "bacterial_spot")]
        [TestCase("Tomato___Early_blight", "tomato", "early_blight")]
        [TestCase("Tomato___Late_blight", "tomato", "late_blight")]
        [TestCase("Tomato___Leaf_Mold", "tomato", "leaf_mold")]
        [TestCase("Tomato___Septoria_leaf_spot", "tomato", "septoria_leaf_spot")]
        [TestCase("Tomato___Spider_mites Two-spotted_spider_mite", "tomato", "spider_mites")]
        [TestCase("Tomato___Target_Spot", "tomato", "target_spot")]
        [TestCase("Tomato___Tomato_Yellow_Leaf_Curl_Virus", "tomato", "yellow_leaf_curl_virus")]
        [TestCase("Tomato___Tomato_mosaic_virus", "tomato", "mosaic_virus")]
        [TestCase("Potato___Early_blight", "potato", "early_blight")]
        [TestCase("Pepper,_bell___Bacterial_spot", "pepper", "bacterial_spot")]
        [TestCase("Apple___Cedar_apple_rust", "apple", "cedar_apple_rust")]
        [TestCase("Apple___Apple_scab", "apple", "apple_scab")]
        // PlantDoc's 17 disease classes (all of them PlantVillage diseases)
        [TestCase("Apple Scab Leaf", "apple", "apple_scab")]
        [TestCase("Apple rust leaf", "apple", "cedar_apple_rust")]
        [TestCase("Bell_pepper leaf spot", "pepper", "bacterial_spot")]
        [TestCase("Corn Gray leaf spot", "corn", "gray_leaf_spot")]
        [TestCase("Corn leaf blight", "corn", "northern_leaf_blight")]
        [TestCase("Corn rust leaf", "corn", "common_rust")]
        [TestCase("Potato leaf early blight", "potato", "early_blight")]
        [TestCase("Potato leaf late blight", "potato", "late_blight")]
        [TestCase("Squash Powdery mildew leaf", "squash", "powdery_mildew")]
        [TestCase("Tomato Early blight leaf", "tomato", "early_blight")]
        [TestCase("Tomato Septoria leaf spot", "tomato", "septoria_leaf_spot")]
        [TestCase("Tomato leaf bacterial spot", "tomato", "bacterial_spot")]
        [TestCase("Tomato leaf late blight", "tomato", "late_blight")]
        [TestCase("Tomato leaf mosaic virus", "tomato", "mosaic_virus")]
        [TestCase("Tomato leaf yellow virus", "tomato", "yellow_leaf_curl_virus")]
        [TestCase("Tomato mold leaf", "tomato", "leaf_mold")]
        [TestCase("Tomato two spotted spider mites leaf", "tomato", "spider_mites")]
        [TestCase("grape leaf black rot", "grape", "black_rot")]
        // A few PlantSeg classes, including the merged ones
        [TestCase("zucchini bacterial wilt", "squash", "bacterial_wilt")]
        [TestCase("zucchini powdery mildew", "squash", "powdery_mildew")]
        [TestCase("citrus canker", "orange", "citrus_canker")]
        [TestCase("bean rust", "soybean", "rust")]
        [TestCase("wheat stripe rust", "wheat", "stripe_rust")]
        [TestCase("maple tar spot", "maple", "tar_spot")]
        [TestCase("Bacterial Canker", "mango", "bacterial_canker")]
        [TestCase("Sooty Mould", "mango", "sooty_mould")]
        [TestCase("RedRot", "sugarcane", "red_rot")]
        [TestCase("Yellow", "sugarcane", "yellow_leaf")]
        [TestCase("leaf curl", "chilli", "leaf_curl")]
        [TestCase("Cercospora Leaf Spot", "chilli", "leaf_spot")]
        // CropScan XR's class names, including the PlantVillage spellings the app's own aliases lacked
        [TestCase("Corn_(maize)___Common_rust_", "corn", "common_rust")]
        [TestCase("Cherry_(including_sour)___Powdery_mildew", "cherry", "powdery_mildew")]
        [TestCase("Rice___Leaf_blast", "rice", "blast")]
        [TestCase("Rice___Tungro", "rice", "tungro")]
        [TestCase("Wheat___Rust", "wheat", "leaf_rust")]
        [TestCase("Wheat___Blast", "wheat", "blast")]
        [TestCase("Cotton___Fusarium_wilt", "cotton", "fusarium_wilt")]
        [TestCase("Chilli___Cercospora_leaf_spot", "chilli", "leaf_spot")]
        [TestCase("Mango___Die_back", "mango", "die_back")]
        public void DatasetLabelsResolveToAuthoredDiseases(string label, string plantId, string diseaseId)
        {
            var database = AssetDatabase.LoadAssetAtPath<PlantDatabase>(DatabasePath);
            if (database == null) Assert.Ignore("Demo data not generated yet.");
            database.Rebuild();

            Assert.IsTrue(database.TryResolve(plantId, out var plant));
            var disease = plant.FindDisease(label);
            Assert.IsNotNull(disease, $"Model label '{label}' should resolve via aliases.");
            Assert.AreEqual(diseaseId, disease.diseaseId);
        }
    }
}
