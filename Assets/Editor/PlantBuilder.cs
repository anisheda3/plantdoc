#if UNITY_EDITOR
using PlantDoctor.Plants;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace PlantDoctor.EditorTools
{
    /// <summary>
    /// Builds the scaffolding of a plant twin and saves its materials as project assets. The plant
    /// body itself is generated at runtime by PlantTwinBuilder for whichever species the AI
    /// identifies. There are two twins in the scene: the AR twin (placed on a real surface) and
    /// the viewer twin (on its own layer, seen only by the viewer camera).
    ///
    ///   Twin                     (PlantTwinPresenter, always active)
    ///     Visual                 (toggled on/off)
    ///       PlantRoot            (PlantTwinBuilder; scaled by the grow-in animation)
    ///         Generated          (soil heap, roots, stems, leaves, fruit or trunk and canopy, built at runtime)
    ///       ContactShadow        (soft blob)
    ///       ShadowCatcher        (receives the real-time shadow; invisible otherwise)
    /// </summary>
    public static class PlantBuilder
    {
        const string MeshFolder = "Assets/Meshes/Generated";
        const string MaterialFolder = "Assets/Materials";
        const string MaterialPath = "Assets/Materials/M_PlantDisease.mat";
        const string TextureFolder = "Assets/Textures/Generated";
        const string PlantArt = "Assets/Art/Plants";
        public const string LeafTexturePath = "Assets/Art/Leaf/leaf_single.png";

        public class Result
        {
            public GameObject Twin;
            public GameObject Visual;
            public Transform PlantRoot;
            public PlantTwinPresenter Presenter;
            public PlantTwinBuilder Builder;
        }

        public static Mesh GroundQuad()
        {
            EnsureFolder("Assets/Meshes");
            EnsureFolder(MeshFolder);
            return SaveMesh(ProceduralMeshes.GroundQuad(), "SM_GroundQuad");
        }

        /// <param name="layerName">Layer for everything generated; empty keeps the default layer.</param>
        public static Result BuildTwin(string name, Vector3 worldPosition, SpeciesProfile defaultSpecies,
            PlantVisualBinder binder, string layerName)
        {
            EnsureFolder(MaterialFolder);
            var quadMesh = GroundQuad();

            var leafMat = CreateDiseaseMaterial();
            var stemMat = CreateStemMaterial();
            var soilMat = LitTexturedMaterial("M_Soil", $"{PlantArt}/shared/soil.png", Color.white, 0.04f);
            var rootMat = LitTexturedMaterial("M_Root", $"{PlantArt}/shared/root.png", Color.white, 0.12f);
            var barkTex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{PlantArt}/shared/bark.png");
            var fruitMat = LitMaterial("M_Fruit", Color.white, 0.72f);   // tinted per species
            var fruitDiseaseMat = CreateFruitDiseaseMaterial();             // fruit, heads and roots showing a disease
            var blobMat = ShaderMaterial("M_ContactShadow", "PlantDoctor/BlobShadow");
            var catcherMat = ShaderMaterial("M_ShadowCatcher", "PlantDoctor/ARShadowReceiver");

            var result = new Result();
            result.Twin = new GameObject(name);
            result.Twin.transform.position = worldPosition;

            result.Visual = new GameObject("Visual");
            result.Visual.transform.SetParent(result.Twin.transform, false);

            var root = new GameObject("PlantRoot");
            root.transform.SetParent(result.Visual.transform, false);
            result.PlantRoot = root.transform;

            // ---- ground presentation: a soft contact shadow and a surface that only shows the real shadow ----
            var contact = Part("ContactShadow", quadMesh, blobMat, result.Visual.transform,
                new Vector3(0f, 0.002f, 0f), Quaternion.identity, Vector3.one * 0.5f);
            NoShadows(contact);
            var catcher = Part("ShadowCatcher", quadMesh, catcherMat, result.Visual.transform,
                new Vector3(0f, 0.001f, 0f), Quaternion.identity, Vector3.one * 2.4f);
            catcher.shadowCastingMode = ShadowCastingMode.Off;
            catcher.receiveShadows = true;

            result.Builder = root.AddComponent<PlantTwinBuilder>();
            Wire(result.Builder,
                ("leafMaterial", leafMat), ("stemMaterial", stemMat), ("rootMaterial", rootMat),
                ("soilMaterial", soilMat), ("barkTexture", barkTex), ("fruitMaterial", fruitMat), ("diseasedFruitMaterial", fruitDiseaseMat), ("visualBinder", binder),
                ("defaultProfile", defaultSpecies));
            WireArray(result.Builder, "footprintPieces", new Object[] { contact.transform });
            var so = new SerializedObject(result.Builder);
            so.FindProperty("layerName").stringValue = layerName ?? "";
            so.ApplyModifiedPropertiesWithoutUndo();

            if (!string.IsNullOrEmpty(layerName))
            {
                int layer = LayerMask.NameToLayer(layerName);
                if (layer >= 0)
                    foreach (var t in result.Twin.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
            }

            result.Presenter = result.Twin.AddComponent<PlantTwinPresenter>();
            AssetDatabase.SaveAssets();
            return result;
        }

        static MeshRenderer Part(string name, Mesh mesh, Material material, Transform parent,
            Vector3 localPos, Quaternion localRot, Vector3 localScale)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot;
            go.transform.localScale = localScale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            return renderer;
        }

        static void Wire(Object target, params (string field, Object value)[] assignments) => UIKit.Wire(target, assignments);

        static void WireArray(Object target, string field, Object[] values)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            prop.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void NoShadows(Renderer r)
        {
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        static Mesh SaveMesh(Mesh mesh, string name)
        {
            string path = $"{MeshFolder}/{name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                // Overwrite in place so existing scene references stay valid.
                EditorUtility.CopySerialized(mesh, existing);
                existing.name = name;
                EditorUtility.SetDirty(existing);
                Object.DestroyImmediate(mesh);
                return existing;
            }
            mesh.name = name;
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        /// <summary>
        /// Creates (or refreshes) the shared leaf material and connects the generated
        /// mask textures to the shader's property contract.
        /// </summary>
        public static Material CreateDiseaseMaterial()
        {
            var mat = LoadOrCreate(MaterialPath, "PlantDoctor/PlantDisease");
            AssignTexture(mat, "_BaseMap", $"{TextureFolder}/T_LeafAlbedo.png");
            AssignTexture(mat, "_LesionMap", $"{TextureFolder}/T_LesionMask_EarlyBlight.png");
            AssignTexture(mat, "_YellowMap", $"{TextureFolder}/T_YellowingMask.png");
            SetColor(mat, "_BaseColor", Color.white);
            SetColor(mat, "_LesionColor", new Color(0.22f, 0.13f, 0.06f));
            SetColor(mat, "_HaloColor", new Color(0.76f, 0.70f, 0.34f));
            SetColor(mat, "_YellowColor", new Color(0.78f, 0.70f, 0.33f));
            SetFloat(mat, "_DiseaseMask", 1f);
            SetFloat(mat, "_AlphaClip", 1f);                 // the leaf photographs' alpha is the outline
            mat.EnableKeyword("_ALPHATEST_ON");
            SetFloat(mat, "_Translucency", 0.6f);
            SetFloat(mat, "_WindStrength", 0.006f);
            SetFloat(mat, "_Smoothness", 0.4f);
            SetFloat(mat, "_RimStrength", 0.08f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>The hero leaf: the same disease shader, with the photograph's alpha as the outline and no wind.</summary>
        public static Material CreateLeafSpecimenMaterial()
        {
            EnsureFolder(MaterialFolder);
            var mat = LoadOrCreate($"{MaterialFolder}/M_LeafSpecimen.mat", "PlantDoctor/PlantDisease");
            AssignTexture(mat, "_BaseMap", LeafTexturePath);
            AssignTexture(mat, "_LesionMap", $"{TextureFolder}/T_LesionMask_EarlyBlight.png");
            AssignTexture(mat, "_YellowMap", $"{TextureFolder}/T_YellowingMask.png");
            SetColor(mat, "_BaseColor", Color.white);
            SetColor(mat, "_LesionColor", new Color(0.22f, 0.13f, 0.06f));
            SetColor(mat, "_HaloColor", new Color(0.76f, 0.70f, 0.34f));
            SetColor(mat, "_YellowColor", new Color(0.78f, 0.70f, 0.33f));
            SetFloat(mat, "_DiseaseMask", 1f);
            SetFloat(mat, "_AlphaClip", 1f);
            mat.EnableKeyword("_ALPHATEST_ON");
            SetFloat(mat, "_Translucency", 0.55f);
            SetFloat(mat, "_WindStrength", 0f);
            SetFloat(mat, "_Smoothness", 0.5f);
            SetFloat(mat, "_RimStrength", 0.08f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>The zoomed leaf's stalks: the stem texture, tinted per species, perfectly still (no wind) and never diseased.</summary>
        public static Material CreateLeafStalkMaterial()
        {
            EnsureFolder(MaterialFolder);
            var mat = LoadOrCreate($"{MaterialFolder}/M_LeafSpecimenStalk.mat", "PlantDoctor/PlantDisease");
            AssignTexture(mat, "_BaseMap", $"{PlantArt}/shared/stem.png");
            SetColor(mat, "_BaseColor", Color.white);
            SetFloat(mat, "_DiseaseMask", 0f);
            SetFloat(mat, "_Translucency", 0.15f);
            SetFloat(mat, "_WindStrength", 0f);
            SetFloat(mat, "_RimStrength", 0.04f);
            SetFloat(mat, "_Smoothness", 0.25f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>
        /// Fruit, heads and roots that carry the disease (fruit rots, blossom end rot, smut, curd rot, cavity spot): the disease shader
        /// on the fruit's own colour and texture, glossier and less translucent than a leaf, still (no wind).
        /// </summary>
        public static Material CreateFruitDiseaseMaterial()
        {
            EnsureFolder(MaterialFolder);
            var mat = LoadOrCreate($"{MaterialFolder}/M_FruitDisease.mat", "PlantDoctor/PlantDisease");
            AssignTexture(mat, "_LesionMap", $"{TextureFolder}/T_LesionMask_FruitRot.png");
            AssignTexture(mat, "_YellowMap", $"{TextureFolder}/T_YellowingMask.png");
            SetColor(mat, "_BaseColor", Color.white);
            SetFloat(mat, "_DiseaseMask", 1f);
            SetFloat(mat, "_Translucency", 0.05f);
            SetFloat(mat, "_WindStrength", 0f);
            SetFloat(mat, "_Smoothness", 0.6f);
            SetFloat(mat, "_RimStrength", 0.05f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static Material CreateStemMaterial()
        {
            var mat = LoadOrCreate($"{MaterialFolder}/M_PlantStem.mat", "PlantDoctor/PlantDisease");
            AssignTexture(mat, "_BaseMap", $"{PlantArt}/shared/stem.png");
            SetColor(mat, "_BaseColor", Color.white);
            SetFloat(mat, "_DiseaseMask", 0.35f);
            SetFloat(mat, "_Translucency", 0.15f);
            SetFloat(mat, "_WindStrength", 0.002f);
            SetFloat(mat, "_RimStrength", 0.04f);
            SetFloat(mat, "_Smoothness", 0.2f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static Material LitMaterial(string name, Color color, float smoothness)
        {
            var mat = LoadOrCreate($"{MaterialFolder}/{name}.mat", "Universal Render Pipeline/Lit");
            SetColor(mat, "_BaseColor", color);
            SetFloat(mat, "_Smoothness", smoothness);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static Material LitTexturedMaterial(string name, string texturePath, Color color, float smoothness)
        {
            var mat = LitMaterial(name, color, smoothness);
            AssignTexture(mat, "_BaseMap", texturePath);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static Material ShaderMaterial(string name, string shader)
        {
            var mat = LoadOrCreate($"{MaterialFolder}/{name}.mat", shader);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static Material LoadOrCreate(string path, string shaderName)
        {
            var shader = Shader.Find(shaderName) ?? Shader.Find("Universal Render Pipeline/Lit");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader) { name = System.IO.Path.GetFileNameWithoutExtension(path) };
                AssetDatabase.CreateAsset(mat, path);
            }
            else if (mat.shader != shader)
            {
                mat.shader = shader;
            }
            return mat;
        }

        static void SetColor(Material m, string p, Color c) { if (m.HasProperty(p)) m.SetColor(p, c); }
        static void SetFloat(Material m, string p, float v) { if (m.HasProperty(p)) m.SetFloat(p, v); }

        static void AssignTexture(Material mat, string property, string path)
        {
            if (!mat.HasProperty(property)) return;
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex != null) mat.SetTexture(property, tex);
            else Debug.LogWarning($"[AI Plant Doctor] Missing texture {path}. " +
                                  "Run Tools > AI Plant Doctor > Build Plant Assets first.");
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
            var leaf = System.IO.Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
#endif
