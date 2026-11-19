#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace PlantDoctor.EditorTools
{
    /// <summary>Makes sure a named physics/render layer exists (the viewer stage has its own).</summary>
    public static class ProjectLayers
    {
        public const string Viewer = "Viewer";

        public static int Ensure(string layerName)
        {
            int existing = LayerMask.NameToLayer(layerName);
            if (existing >= 0) return existing;

            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (assets == null || assets.Length == 0)
            {
                Debug.LogError("[AI Plant Doctor] Could not open TagManager to add the layer " + layerName);
                return -1;
            }
            var so = new SerializedObject(assets[0]);
            var layers = so.FindProperty("layers");
            for (int i = 8; i < layers.arraySize; i++)          // 0-7 are built in
            {
                var slot = layers.GetArrayElementAtIndex(i);
                if (!string.IsNullOrEmpty(slot.stringValue)) continue;
                slot.stringValue = layerName;
                so.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssets();
                Debug.Log($"[AI Plant Doctor] Added layer {i}: {layerName}");
                return i;
            }
            Debug.LogError("[AI Plant Doctor] No free layer slot for " + layerName);
            return -1;
        }

        /// <summary>Culling mask that excludes the given layer.</summary>
        public static int Without(int layer) => layer < 0 ? ~0 : ~(1 << layer);
    }
}
#endif
