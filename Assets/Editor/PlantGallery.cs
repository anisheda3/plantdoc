#if UNITY_EDITOR
using System.IO;
using PlantDoctor.Plants;
using UnityEditor;
using UnityEngine;

namespace PlantDoctor.EditorTools
{
    /// <summary>
    /// Renders every species twin on a plain table, whole and as a close-up of its roots, to
    /// docs/screenshots/plants. Used to judge the look of the generated plants without the full app.
    ///   Unity.exe -batchmode -projectPath E:\plant -executeMethod PlantDoctor.EditorTools.PlantGallery.Capture
    /// </summary>
    public static class PlantGallery
    {
        const string Out = "docs/screenshots/plants";
        static readonly string[] AllSpecies = { "Tomato", "Potato", "BellPepper", "Apple", "Blueberry", "Cherry", "Corn", "Grape", "Orange", "Peach", "Raspberry", "Soybean", "Squash", "Strawberry",
            "Banana", "Basil", "Bean", "Broccoli", "Cabbage", "Carrot", "Cauliflower", "Celery", "Coffee", "Cucumber", "Eggplant", "Garlic", "Ginger", "Lettuce", "Maple", "Plum", "Rice", "Tobacco", "Wheat", "Mango", "Sugarcane", "Chilli", "Cotton" };

        /// <summary>All species, or only those named after -species on the command line (comma-separated asset names).</summary>
        static string[] Species
        {
            get
            {
                var args = System.Environment.GetCommandLineArgs();
                for (int i = 0; i < args.Length - 1; i++)
                    if (args[i] == "-species") return args[i + 1].Split(',');
                return AllSpecies;
            }
        }

        [MenuItem("Tools/AI Plant Doctor/Render Plant Gallery")]
        public static void Capture()
        {
            bool ok = false;
            try { ok = Render(); }
            catch (System.Exception e) { Debug.LogException(e); }
            if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
        }

        static bool Render()
        {
            Directory.CreateDirectory(Out);
            var names = Species;
            var profiles = new SpeciesProfile[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                profiles[i] = AssetDatabase.LoadAssetAtPath<SpeciesProfile>($"Assets/Data/Species/Species_{names[i]}.asset");
                if (profiles[i] == null) { Debug.LogError($"Species_{names[i]} missing; run the project setup first."); return false; }
            }

            var rig = new GameObject("PlantGalleryRig");
            try
            {
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(0.50f, 0.53f, 0.58f);
                var light = new GameObject("Sun").AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.2f;
                light.shadows = LightShadows.Soft;
                light.transform.SetParent(rig.transform);
                light.transform.rotation = Quaternion.Euler(52f, -35f, 0f);

                var table = GameObject.CreatePrimitive(PrimitiveType.Plane);
                table.transform.SetParent(rig.transform);
                table.transform.localScale = Vector3.one * 0.6f;
                var tableMat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = new Color(0.62f, 0.55f, 0.46f) };
                tableMat.SetColor("_BaseColor", new Color(0.62f, 0.55f, 0.46f));
                table.GetComponent<MeshRenderer>().sharedMaterial = tableMat;

                var twin = PlantBuilder.BuildTwin("GalleryTwin", Vector3.zero, profiles[0], null, "");
                twin.Twin.transform.SetParent(rig.transform);

                var camGo = new GameObject("Camera");
                camGo.transform.SetParent(rig.transform);
                var cam = camGo.AddComponent<Camera>();
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.78f, 0.84f, 0.90f);
                cam.fieldOfView = 35f;
                cam.nearClipPlane = 0.02f;
                cam.farClipPlane = 80f;
                var rt = new RenderTexture(1024, 1024, 24, RenderTextureFormat.ARGB32);
                cam.targetTexture = rt;

                var report = new System.Text.StringBuilder("species, height (m, profile), height (m, built), spread radius (m), spread/height, lowest point (m) (part), fruit count, fruit centre height range (m), fruit max distance from axis (m)\n");
                foreach (var profile in profiles)
                {
                    twin.Builder.Build(profile);
                    float h = profile.height;
                    Shoot(cam, rt, new Vector3(0f, h * 0.48f, 0f), h * 2.3f, 12f, -28f, $"{profile.speciesId}_whole");
                    Shoot(cam, rt, new Vector3(0f, 0.02f, 0f), profile.FootprintRadius * 2.0f, 38f, -20f, $"{profile.speciesId}_roots");
                    Shoot(cam, rt, new Vector3(0f, h * 0.45f, 0f), h * 1.1f, 6f, 40f, $"{profile.speciesId}_leaves");
                    if (profile.flowerCount > 0) FlowerShot(cam, rt, twin.Builder.transform, profile);
                    // For the habit check: straight from the side (height against spread) and from above (how the crown fills).
                    float size = Mathf.Max(h, Spread(twin.Builder.transform) * 2f);
                    Shoot(cam, rt, new Vector3(0f, h * 0.5f, 0f), size * 2.0f, 2f, 90f, $"{profile.speciesId}_side");
                    Shoot(cam, rt, new Vector3(0f, h * 0.3f, 0f), size * 1.9f, 75f, 30f, $"{profile.speciesId}_top");
                    report.AppendLine(Audit(profile, twin.Builder.transform));
                }
                twin.Twin.SetActive(false);
                table.SetActive(false);
                foreach (var profile in profiles) HeroShot(cam, rt, rig.transform, profile);
                foreach (var profile in profiles) if (profile.fruit != FruitForm.None) OrganShot(cam, rt, rig.transform, profile);
                File.WriteAllText(names.Length == AllSpecies.Length ? $"{Out}/audit.txt" : $"{Out}/audit_part.txt", report.ToString());
                Object.DestroyImmediate(rt);
                Debug.Log($"[AI Plant Doctor] Plant gallery written to {Out}");
                return true;
            }
            finally { Object.DestroyImmediate(rig); }
        }

        /// <summary>
        /// The species' zoomed leaf (HeroLeafBuilder), healthy and with spots, against a plain backdrop: checks the leaf's layout and
        /// that the disease masks run across all its leaflets. Written as &lt;id&gt;_hero.png.
        /// </summary>
        static void HeroShot(Camera cam, RenderTexture rt, Transform parent, SpeciesProfile p)
        {
            var leafMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/M_LeafSpecimen.mat");
            var stalkMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/M_LeafSpecimenStalk.mat");
            var model = HeroLeafBuilder.Build(p, null, 0.45f);
            var holder = new GameObject("Hero");
            holder.transform.SetParent(parent, false);
            holder.transform.position = new Vector3(0f, 5f, 0f);
            for (int k = 0; k < 2; k++)
            {
                var root = new GameObject(k == 0 ? "Healthy" : "Spotted");
                root.transform.SetParent(holder.transform, false);
                // Side by side, each upright and facing the camera, the way the zoom step shows it.
                root.transform.localPosition = new Vector3((k - 0.5f) * model.Radius * 2.15f, 0f, 0f);
                root.transform.localRotation = Quaternion.LookRotation(Vector3.up, Vector3.back) * Quaternion.Euler(0f, model.Turn, 0f);
                var leaf = new GameObject("Leaf", typeof(MeshFilter), typeof(MeshRenderer));
                leaf.transform.SetParent(root.transform, false);
                leaf.transform.localPosition = -model.Centre;
                leaf.GetComponent<MeshFilter>().sharedMesh = model.Blades;
                var mr = leaf.GetComponent<MeshRenderer>();
                mr.sharedMaterial = leafMat;
                var block = new MaterialPropertyBlock();
                block.SetTexture("_BaseMap", p.leafAtlas != null ? (Texture)p.leafAtlas : Texture2D.whiteTexture);
                block.SetVector("_BaseMap_ST", new Vector4(1f, 1f, 0f, 0f));
                block.SetColor("_BaseColor", Color.Lerp(Color.white, p.leafTint, 0.6f) * 1.08f);
                block.SetFloat("_LesionDensity", k == 0 ? 0f : 0.55f);
                block.SetFloat("_Yellowing", k == 0 ? 0f : 0.4f);
                mr.SetPropertyBlock(block);
                if (model.Stalks != null)
                {
                    var st = new GameObject("Stalks", typeof(MeshFilter), typeof(MeshRenderer));
                    st.transform.SetParent(leaf.transform, false);
                    st.GetComponent<MeshFilter>().sharedMesh = model.Stalks;
                    var sr = st.GetComponent<MeshRenderer>();
                    sr.sharedMaterial = stalkMat;
                    var sb = new MaterialPropertyBlock();
                    sb.SetColor("_BaseColor", p.stemColor);
                    sr.SetPropertyBlock(sb);
                }
            }
            var prevBg = cam.backgroundColor;
            cam.backgroundColor = new Color(0.93f, 0.92f, 0.88f);
            float dist = model.Radius * 2.3f / Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            Shoot(cam, rt, holder.transform.position, dist, 0f, 0f, $"{p.speciesId}_hero");
            cam.backgroundColor = prevBg;
            Object.DestroyImmediate(holder);
            Object.DestroyImmediate(model.Blades);
            if (model.Stalks != null) Object.DestroyImmediate(model.Stalks);
        }

        /// <summary>Close-up of the plant's flowers (centred on the first flower cluster), to check their shape and colour.</summary>
        static void FlowerShot(Camera cam, RenderTexture rt, Transform root, SpeciesProfile p)
        {
            foreach (var mr in root.GetComponentsInChildren<MeshRenderer>())
            {
                if (mr.name != "Flowers") continue;
                var verts = mr.GetComponent<MeshFilter>().sharedMesh.vertices;
                if (verts.Length == 0) return;
                var at = mr.transform.TransformPoint(verts[verts.Length / 7]);
                Shoot(cam, rt, at, p.flowerRadius * 14f, 25f, 20f, $"{p.speciesId}_flowers");
                return;
            }
        }

        /// <summary>The species' fruit, head or root as the zoom step shows it for an organ disease: healthy, and with a rot (end rot for
        /// the pepper and banana, the ringed fruit rot for the rest). Written as &lt;id&gt;_organ.png.</summary>
        static void OrganShot(Camera cam, RenderTexture rt, Transform parent, SpeciesProfile p)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/M_FruitDisease.mat");
            var stalkMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/M_LeafSpecimenStalk.mat");
            var mask = AssetDatabase.LoadAssetAtPath<Texture2D>(p.fruit == FruitForm.Bell || p.fruit == FruitForm.BananaBunch
                ? "Assets/Textures/Generated/T_LesionMask_EndRot.png" : "Assets/Textures/Generated/T_LesionMask_FruitRot.png");
            var model = HeroLeafBuilder.BuildOrgan(p);
            Debug.Log($"[Gallery] organ {p.speciesId}: radius {model.Radius:0.000}, centre {model.Centre}, {model.Blades.vertexCount} vertices");
            var holder = new GameObject("Organ");
            holder.transform.SetParent(parent, false);
            holder.transform.position = new Vector3(0f, 5f, 0f);
            for (int k = 0; k < 2; k++)
            {
                var root = new GameObject(k == 0 ? "Healthy" : "Rotting");
                root.transform.SetParent(holder.transform, false);
                root.transform.localPosition = new Vector3((k - 0.5f) * model.Radius * 2.15f, 0f, 0f);
                root.transform.localRotation = Quaternion.LookRotation(Vector3.up, Vector3.back);
                var go = new GameObject("Fruit", typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(root.transform, false);
                go.transform.localPosition = -model.Centre;
                go.GetComponent<MeshFilter>().sharedMesh = model.Blades;
                var mr = go.GetComponent<MeshRenderer>();
                mr.sharedMaterial = mat;
                var block = new MaterialPropertyBlock();
                var surface = CropMeshes.Texture(p.fruit);
                block.SetTexture("_BaseMap", surface != null ? (Texture)surface : Texture2D.whiteTexture);
                block.SetColor("_BaseColor", p.fruitColors != null && p.fruitColors.Length > 0 ? p.fruitColors[0] : Color.white);
                if (mask != null) block.SetTexture("_LesionMap", mask);
                block.SetColor("_LesionColor", new Color(0.32f, 0.22f, 0.14f));
                block.SetColor("_HaloColor", new Color(0.55f, 0.45f, 0.3f));
                block.SetFloat("_LesionDensity", k == 0 ? 0f : 0.7f);
                mr.SetPropertyBlock(block);
                if (model.Stalks != null)
                {
                    var st = new GameObject("Calyx", typeof(MeshFilter), typeof(MeshRenderer));
                    st.transform.SetParent(go.transform, false);
                    st.GetComponent<MeshFilter>().sharedMesh = model.Stalks;
                    var sr = st.GetComponent<MeshRenderer>();
                    sr.sharedMaterial = stalkMat;
                    var sb = new MaterialPropertyBlock();
                    sb.SetColor("_BaseColor", new Color(0.36f, 0.48f, 0.24f));
                    sr.SetPropertyBlock(sb);
                }
            }
            var prevBg = cam.backgroundColor;
            cam.backgroundColor = new Color(0.93f, 0.92f, 0.88f);
            float dist = model.Radius * 2.3f / Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            Shoot(cam, rt, holder.transform.position, dist, 0f, 0f, $"{p.speciesId}_organ");
            cam.backgroundColor = prevBg;
            Object.DestroyImmediate(holder);
            Object.DestroyImmediate(model.Blades);
            if (model.Stalks != null) Object.DestroyImmediate(model.Stalks);
        }

        /// <summary>Largest horizontal distance of the plant's above-ground parts from its axis (roots and soil left out).</summary>
        static float Spread(Transform root)
        {
            float r = 0f;
            foreach (var mr in root.GetComponentsInChildren<MeshRenderer>())
            {
                string n = mr.name;
                if (n.StartsWith("Roots") || n.StartsWith("Soil") || n.StartsWith("RootSoil")) continue;
                foreach (var w in WorldVerts(mr)) r = Mathf.Max(r, new Vector2(w.x, w.z).magnitude);
            }
            return r;
        }

        /// <summary>
        /// The renderer's vertices in world space. Measured on the vertices rather than renderer.bounds: the bounds of a rotated
        /// part are the box round its rotated local box, which pokes out far beyond the mesh (a leaning cane "went" 6 cm underground).
        /// </summary>
        static System.Collections.Generic.IEnumerable<Vector3> WorldVerts(MeshRenderer mr)
        {
            var mesh = mr.GetComponent<MeshFilter>()?.sharedMesh;
            if (mesh == null) yield break;
            var m = mr.transform.localToWorldMatrix;
            foreach (var v in mesh.vertices) yield return m.MultiplyPoint3x4(v);
        }

        /// <summary>One line of numbers per species for the habit check (written to docs/screenshots/plants/audit.txt).</summary>
        static string Audit(SpeciesProfile p, Transform root)
        {
            float top = 0f, low = 0f, fMin = float.MaxValue, fMax = float.MinValue, fFar = 0f;
            int fruit = 0;
            string lowName = "-";
            foreach (var mr in root.GetComponentsInChildren<MeshRenderer>())
            {
                foreach (var w in WorldVerts(mr))
                {
                    top = Mathf.Max(top, w.y);
                    if (w.y < low) { low = w.y; lowName = mr.name; }
                }
                if (!mr.name.StartsWith("Fruit_")) continue;
                var mesh = mr.GetComponent<MeshFilter>().sharedMesh;
                var verts = mesh.vertices;
                // Fruit may be batched into one mesh; sample its vertices for the height range and the reach from the axis.
                for (int i = 0; i < verts.Length; i += 37)
                {
                    var w = mr.transform.TransformPoint(verts[i]);
                    fMin = Mathf.Min(fMin, w.y); fMax = Mathf.Max(fMax, w.y);
                    fFar = Mathf.Max(fFar, new Vector2(w.x, w.z).magnitude);
                }
                fruit++;
            }
            float spread = Spread(root);
            return $"{p.speciesId}, {p.height:0.00}, {top:0.00}, {spread:0.00}, {spread * 2f / Mathf.Max(0.01f, top):0.00}, {low:0.000} ({lowName}), {fruit} renderer(s), " +
                   (fruit > 0 ? $"{fMin:0.00}-{fMax:0.00}, {fFar:0.00}" : "-, -");
        }

        static void Shoot(Camera cam, RenderTexture rt, Vector3 target, float distance, float pitch, float yaw, string name)
        {
            var rot = Quaternion.Euler(pitch, yaw, 0f);
            cam.transform.SetPositionAndRotation(target - rot * Vector3.forward * distance, rot);
            cam.Render();
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            File.WriteAllBytes($"{Out}/{name}.png", tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }
    }
}
#endif
