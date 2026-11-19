using UnityEngine;

namespace PlantDoctor.AR
{
    /// <summary>
    /// Affine map from screen-viewport UV (0..1, origin bottom-left) to camera-texture UV.
    ///
    /// Cameras deliver frames in sensor orientation, often mirrored, and at an aspect
    /// ratio that differs from the screen. Expressing all of that as one 2x3 affine
    /// transform lets the preview shader and the CPU capture use the *same* mapping,
    /// so the frame the user lines up in the reticle is exactly the frame analysed.
    /// </summary>
    public struct ViewportMapping
    {
        /// <summary>texU = dot(RowU, (u, v, 1)).</summary>
        public Vector3 RowU;
        /// <summary>texV = dot(RowV, (u, v, 1)).</summary>
        public Vector3 RowV;

        public static ViewportMapping Identity => new ViewportMapping
        {
            RowU = new Vector3(1f, 0f, 0f),
            RowV = new Vector3(0f, 1f, 0f)
        };

        public Vector2 Map(float u, float v) => new Vector2(
            RowU.x * u + RowU.y * v + RowU.z,
            RowV.x * u + RowV.y * v + RowV.z);

        /// <summary>
        /// Webcam-style source: the texture must be rotated <paramref name="clockwiseDegrees"/>
        /// clockwise (and optionally flipped vertically first) to be upright, and is shown
        /// aspect-filled on a screen of <paramref name="screenAspect"/> (width / height).
        /// </summary>
        public static ViewportMapping ForRotatedTexture(int textureWidth, int textureHeight,
            int clockwiseDegrees, bool verticallyMirrored, float screenAspect)
        {
            int rot = ((clockwiseDegrees % 360) + 360) % 360;
            rot = Mathf.RoundToInt(rot / 90f) * 90 % 360;
            bool quarterTurn = rot == 90 || rot == 270;

            float w = Mathf.Max(1, textureWidth), h = Mathf.Max(1, textureHeight);
            float uprightAspect = quarterTurn ? h / w : w / h;
            screenAspect = Mathf.Max(0.01f, screenAspect);

            // Step 1: viewport -> upright-image UV (aspect-fill crop around the centre).
            float su = 1f, sv = 1f;
            if (uprightAspect > screenAspect) su = screenAspect / uprightAspect;   // crop sides
            else sv = uprightAspect / screenAspect;                                // crop top/bottom
            // ru = 0.5 + (u - 0.5) * su ; rv = 0.5 + (v - 0.5) * sv
            var ru = new Vector3(su, 0f, 0.5f - 0.5f * su);
            var rv = new Vector3(0f, sv, 0.5f - 0.5f * sv);

            // Step 2: upright-image UV -> true-image UV (undo the clockwise rotation).
            Vector3 tu, tv;
            switch (rot)
            {
                case 90:  tu = One - rv; tv = ru;      break;   // tu = 1 - rv, tv = ru
                case 180: tu = One - ru; tv = One - rv; break;
                case 270: tu = rv;       tv = One - ru; break;
                default:  tu = ru;       tv = rv;      break;
            }

            // Step 3: true image -> stored texture (vertical mirror).
            if (verticallyMirrored) tv = One - tv;

            return new ViewportMapping { RowU = tu, RowV = tv };
        }

        static readonly Vector3 One = new Vector3(0f, 0f, 1f);   // the constant "1" in affine form

        // ------------------------------------------------------------------
        // CPU resampling into a viewport-aligned image
        // ------------------------------------------------------------------

        /// <summary>Output dimensions for a viewport image of the given aspect and long edge.</summary>
        public static Vector2Int OutputSize(float viewportAspect, int longEdge)
        {
            longEdge = Mathf.Max(16, longEdge);
            viewportAspect = Mathf.Max(0.01f, viewportAspect);
            return viewportAspect >= 1f
                ? new Vector2Int(longEdge, Mathf.Max(16, Mathf.RoundToInt(longEdge / viewportAspect)))
                : new Vector2Int(Mathf.Max(16, Mathf.RoundToInt(longEdge * viewportAspect)), longEdge);
        }

        public void Resample(Color32[] src, int sw, int sh, Color32[] dst, int dw, int dh)
        {
            for (int y = 0; y < dh; y++)
            {
                float v = (y + 0.5f) / dh;
                for (int x = 0; x < dw; x++)
                {
                    var uv = Map((x + 0.5f) / dw, v);
                    dst[y * dw + x] = Bilinear(src, sw, sh, uv.x, uv.y);
                }
            }
        }

        static Color32 Bilinear(Color32[] p, int w, int h, float u, float v)
        {
            Prepare(w, h, u, v, out int x0, out int y0, out int x1, out int y1, out float fx, out float fy);
            return Mix(p[y0 * w + x0], p[y0 * w + x1], p[y1 * w + x0], p[y1 * w + x1], fx, fy);
        }

        static void Prepare(int w, int h, float u, float v,
            out int x0, out int y0, out int x1, out int y1, out float fx, out float fy)
        {
            float px = Mathf.Clamp(u * w - 0.5f, 0f, w - 1);
            float py = Mathf.Clamp(v * h - 0.5f, 0f, h - 1);
            x0 = (int)px; y0 = (int)py;
            x1 = Mathf.Min(x0 + 1, w - 1); y1 = Mathf.Min(y0 + 1, h - 1);
            fx = px - x0; fy = py - y0;
        }

        static Color32 Mix(Color32 a, Color32 b, Color32 c, Color32 d, float fx, float fy)
        {
            float r = Lerp2(a.r, b.r, c.r, d.r, fx, fy);
            float g = Lerp2(a.g, b.g, c.g, d.g, fx, fy);
            float bl = Lerp2(a.b, b.b, c.b, d.b, fx, fy);
            return new Color32((byte)r, (byte)g, (byte)bl, 255);
        }

        static float Lerp2(byte a, byte b, byte c, byte d, float fx, float fy)
        {
            float top = a + (b - a) * fx;
            float bottom = c + (d - c) * fx;
            return top + (bottom - top) * fy + 0.5f;
        }
    }
}
