#if UNITY_EDITOR
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using PlantDoctor.AI;
using UnityEditor;
using UnityEngine;

namespace PlantDoctor.EditorTools
{
    /// <summary>
    /// Runs the real LeafFeatureTracer over a folder of test photos and writes what it found, so the result can be scored against
    /// human-drawn masks (tools/eval/score_tracer.py). Headless:
    ///   Unity.exe -batchmode -projectPath E:\plant -executeMethod PlantDoctor.EditorTools.TracerEval.Run -evalDir E:\path\to\folder
    /// The folder holds img_*.png; it gets pred_leaf_*.png, pred_lesion_*.png, pred_yellow_*.png (white = traced) and results.csv.
    /// </summary>
    public static class TracerEval
    {
        public static void Run()
        {
            int code = 1;
            try { code = Execute() ? 0 : 1; }
            catch (Exception e) { UnityEngine.Debug.LogException(e); }
            EditorApplication.Exit(code);
        }

        [Serializable] class Config { public string name; public string[] keys; public float[] values; }
        [Serializable] class Sweep { public Config[] configs; }

        static bool Execute()
        {
            string dir = null, sweepPath = null;
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "-evalDir") dir = args[i + 1];
                if (args[i] == "-sweep") sweepPath = args[i + 1];
            }
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) { UnityEngine.Debug.LogError("-evalDir <folder> missing"); return false; }

            // One run per configuration; with no sweep file the tracer runs once with its defaults.
            var configs = new[] { new Config { name = "default", keys = new string[0], values = new float[0] } };
            if (!string.IsNullOrEmpty(sweepPath)) configs = JsonUtility.FromJson<Sweep>(File.ReadAllText(sweepPath)).configs;

            var images = Directory.GetFiles(dir, "img_*.png");
            var textures = new Texture2D[images.Length];
            for (int i = 0; i < images.Length; i++)
            {
                textures[i] = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!textures[i].LoadImage(File.ReadAllBytes(images[i]))) textures[i] = null;
            }

            var defaults = Snapshot();
            bool ok = true;
            foreach (var config in configs)
            {
                Restore(defaults);
                for (int k = 0; k < config.keys.Length; k++) SetSetting(config.keys[k], config.values[k]);
                string outDir = config.name == "default" ? dir : Path.Combine(dir, "out_" + config.name);
                Directory.CreateDirectory(outDir);
                ok &= RunOne(images, textures, outDir, config.name);
            }
            Restore(defaults);
            return ok;
        }

        static System.Collections.Generic.Dictionary<string, object> Snapshot()
        {
            var d = new System.Collections.Generic.Dictionary<string, object>();
            foreach (var f in typeof(TracerSettings).GetFields()) d[f.Name] = f.GetValue(null);
            return d;
        }

        static void Restore(System.Collections.Generic.Dictionary<string, object> d)
        {
            foreach (var f in typeof(TracerSettings).GetFields()) f.SetValue(null, d[f.Name]);
        }

        static void SetSetting(string key, float value)
        {
            var f = typeof(TracerSettings).GetField(key);
            if (f == null) throw new ArgumentException("no tracer setting " + key);
            if (f.FieldType == typeof(bool)) f.SetValue(null, value > 0.5f);
            else if (f.FieldType == typeof(int)) f.SetValue(null, Mathf.RoundToInt(value));
            else f.SetValue(null, value);
        }

        static bool RunOne(string[] images, Texture2D[] textures, string outDir, string label)
        {
            var csv = new StringBuilder("name,width,height,leaf_fraction,lesion_count,yellow_count,milliseconds\n");
            int done = 0;
            for (int i = 0; i < images.Length; i++)
            {
                if (textures[i] == null) continue;
                string id = Path.GetFileNameWithoutExtension(images[i]).Substring(4);
                var clock = Stopwatch.StartNew();
                var trace = LeafFeatureTracer.Trace(textures[i]);
                clock.Stop();
                int w = textures[i].width, h = textures[i].height;
                Write(outDir, "leaf_" + id, trace.Leaf, w, h);
                Write(outDir, "lesion_" + id, trace.LesionMask, w, h);
                Write(outDir, "yellow_" + id, trace.YellowMask, w, h);
                csv.Append($"{id},{w},{h},{trace.LeafCoverage:0.0000},{trace.LesionCount},{trace.YellowCount},{clock.ElapsedMilliseconds}\n");
                trace.Dispose();
                done++;
            }
            File.WriteAllText(Path.Combine(outDir, "results.csv"), csv.ToString());
            UnityEngine.Debug.Log($"[TracerEval] {label}: traced {done} photos -> {outDir}");
            return done > 0;
        }

        /// <summary>White where traced. Rows are written from the top of the picture, like the input.</summary>
        static void Write(string dir, string name, bool[] mask, int w, int h)
        {
            var px = new Color32[w * h];
            if (mask != null)
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                        if (mask[y * w + x]) px[(h - 1 - y) * w + x] = new Color32(255, 255, 255, 255);
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.SetPixels32(px);
            File.WriteAllBytes(Path.Combine(dir, "pred_" + name + ".png"), tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
        }
    }
}
#endif
