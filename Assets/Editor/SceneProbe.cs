#if UNITY_EDITOR
using System.Text;
using PlantDoctor.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PlantDoctor.EditorTools
{
    /// <summary>Command-line scene audit: reports every serialized object reference left empty on the UI screens.</summary>
    public static class SceneProbe
    {
        public static void Audit()
        {
            EditorSceneManager.OpenScene(SceneBootstrapper.ScenePath);
            var sb = new StringBuilder("[Probe] Empty references:\n");
            int count = 0;
            foreach (var mb in Object.FindObjectsOfType<MonoBehaviour>(true))
            {
                if (mb == null || !mb.GetType().Namespace.StartsWith("PlantDoctor")) continue;
                var so = new SerializedObject(mb);
                var it = so.GetIterator();
                while (it.NextVisible(true))
                {
                    if (it.propertyType != SerializedPropertyType.ObjectReference || it.objectReferenceValue != null) continue;
                    if (it.name == "m_Script") continue;
                    sb.AppendLine($"  {mb.GetType().Name}.{it.propertyPath} on {mb.gameObject.name}");
                    count++;
                }
            }
            sb.AppendLine($"[Probe] {count} empty references");
            Debug.Log(sb.ToString());
        }
    }
}
#endif
