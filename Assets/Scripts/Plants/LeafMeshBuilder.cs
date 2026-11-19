using UnityEngine;

namespace PlantDoctor.Plants
{
    /// <summary>
    /// Builds the one hero leaf: a finely subdivided sheet the size of the leaf's bounding box,
    /// folded along the midrib, drooping slightly at the tip. The leaf's actual outline comes
    /// from the alpha of its photographic texture (see the shader's alpha clip), so the
    /// silhouette, veins and serrated edge are all real.
    ///
    /// The leaf is 1 unit tall, standing on +Z with its stalk at the origin; the sheet faces +Y.
    /// UV0 maps the texture; UV1 maps the disease masks across the whole leaf.
    /// </summary>
    public static class LeafMeshBuilder
    {
        const float FoldDepth = 0.075f;    // how far each half tilts down from the midrib
        const float TipDroop = 0.10f;
        const float Ripple = 0.004f;

        /// <summary>Local position of a point of the leaf given its texture coordinates (u across, v along).</summary>
        public static Vector3 LocalPoint(float u, float v, float widthOverHeight)
        {
            float halfWidth = widthOverHeight * 0.5f;
            float x = (u - 0.5f) * widthOverHeight;
            float z = v;
            float side = Mathf.Abs(x) / Mathf.Max(halfWidth, 1e-4f);
            float envelope = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Mathf.PI * Mathf.Clamp01(z * 1.04f))), 0.6f);
            float y = -FoldDepth * Mathf.Pow(side, 1.15f) * envelope;
            y -= TipDroop * z * z * z;
            y += Ripple * Mathf.Sin(z * 24f + x * 38f) * side * side;
            return new Vector3(x, y, z);
        }

        public static Mesh Build(float widthOverHeight, int columns = 32, int rows = 80)
        {
            widthOverHeight = Mathf.Clamp(widthOverHeight, 0.1f, 2f);
            var vertices = new Vector3[(columns + 1) * (rows + 1)];
            var uv0 = new Vector2[vertices.Length];
            var uv1 = new Vector2[vertices.Length];
            for (int r = 0; r <= rows; r++)
            {
                float v = r / (float)rows;
                for (int c = 0; c <= columns; c++)
                {
                    float u = c / (float)columns;
                    int i = r * (columns + 1) + c;
                    vertices[i] = LocalPoint(u, v, widthOverHeight);
                    uv0[i] = new Vector2(u, v);
                    uv1[i] = new Vector2(0.05f + 0.9f * u, 0.05f + 0.9f * v);
                }
            }

            var triangles = new int[columns * rows * 6];
            int t = 0;
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < columns; c++)
                {
                    int a = r * (columns + 1) + c, b = a + 1, d = a + columns + 1, e = d + 1;
                    triangles[t++] = a; triangles[t++] = d; triangles[t++] = b;
                    triangles[t++] = b; triangles[t++] = d; triangles[t++] = e;
                }

            var mesh = new Mesh { name = "HeroLeaf" };
            mesh.vertices = vertices;
            mesh.uv = uv0;
            mesh.uv2 = uv1;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
