using System;
using PlantDoctor.AR;
using UnityEngine;

namespace PlantDoctor.Tests
{
    /// <summary>
    /// Test camera: a procedurally drawn diseased tomato leaf filling most of the frame,
    /// with veins, fine texture and brown target spots — sharp, well lit, foliage-coloured.
    /// </summary>
    public class FakeLeafCamera : MonoBehaviour, ICameraFrameSource, ICameraPreviewSource
    {
        Texture2D _frame;

        public string DisplayName => "Synthetic leaf";
        public bool IsReady => _frame != null;
        public CameraFailure LastFailure => CameraFailure.None;
        public float CurrentMotion => 0f;
        public event Action<bool> ReadyStateChanged;

        public Texture PreviewTexture => _frame;
        public ViewportMapping PreviewMapping => ViewportMapping.Identity;

        public bool TryStart()
        {
            if (_frame == null) _frame = Draw(540, 1170);
            ReadyStateChanged?.Invoke(true);
            return true;
        }

        public void StopSource() { }
        public Texture2D AcquireFrame(int longEdge = 0) => _frame;

        void OnDestroy()
        {
            if (_frame != null) Destroy(_frame);
        }

        static Texture2D Draw(int w, int h)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var px = new Color32[w * h];
            var rng = new System.Random(3);
            var spots = new Vector3[14];
            for (int i = 0; i < spots.Length; i++)
                spots[i] = new Vector3((float)rng.NextDouble() * 0.6f + 0.2f, (float)rng.NextDouble() * 0.5f + 0.25f,
                    0.02f + (float)rng.NextDouble() * 0.03f);

            for (int y = 0; y < h; y++)
            {
                float v = y / (float)h;
                for (int x = 0; x < w; x++)
                {
                    float u = x / (float)w;
                    // Background: dark soil with grain.
                    float grain = Mathf.PerlinNoise(x * 0.15f, y * 0.15f);
                    Color c = new Color(0.18f + grain * 0.08f, 0.13f + grain * 0.05f, 0.09f);

                    // Leaf: a big ellipse centred slightly above the middle.
                    float du = (u - 0.5f) / 0.46f, dv = (v - 0.5f) / 0.36f;
                    if (du * du + dv * dv < 1f)
                    {
                        float vein = Mathf.Abs(u - 0.5f) < 0.006f ? 0.25f
                                   : Mathf.Abs(Mathf.Sin((v * 14f - Mathf.Abs(u - 0.5f) * 10f) * Mathf.PI)) > 0.97f ? 0.14f : 0f;
                        float tex2 = Mathf.PerlinNoise(x * 0.35f, y * 0.35f) * 0.12f;
                        c = new Color(0.20f + vein + tex2 * 0.5f, 0.50f + vein + tex2, 0.16f + vein * 0.5f);

                        foreach (var s in spots)
                        {
                            float d = Vector2.Distance(new Vector2(u, v * h / w * 0.46f), new Vector2(s.x, s.y * h / w * 0.46f));
                            if (d < s.z) c = Color.Lerp(new Color(0.32f, 0.2f, 0.08f), new Color(0.2f, 0.12f, 0.05f), Mathf.Repeat(d * 160f, 1f));
                            else if (d < s.z * 1.5f) c = Color.Lerp(c, new Color(0.8f, 0.75f, 0.3f), 0.6f);
                        }
                    }
                    px[y * w + x] = c;
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }
    }
}
