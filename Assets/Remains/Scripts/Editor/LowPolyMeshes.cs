using System.Collections.Generic;
using UnityEngine;

namespace Remains.Editor
{
    /// <summary>
    /// Procedural flat-shaded low-poly meshes (every triangle has its own vertices, so normals are faceted).
    /// </summary>
    public static class LowPolyMeshes
    {
        private class Builder
        {
            public readonly List<Vector3> Vertices = new List<Vector3>();
            public readonly List<int> Triangles = new List<int>();

            /// <summary>Adds a triangle whose front face points away from <paramref name="inside"/>.</summary>
            public void Triangle(Vector3 a, Vector3 b, Vector3 c, Vector3 inside)
            {
                Vector3 normal = Vector3.Cross(b - a, c - a);
                Vector3 centroid = (a + b + c) / 3f;
                if (Vector3.Dot(normal, centroid - inside) < 0f)
                {
                    (b, c) = (c, b);
                }
                int index = Vertices.Count;
                Vertices.Add(a);
                Vertices.Add(b);
                Vertices.Add(c);
                Triangles.Add(index);
                Triangles.Add(index + 1);
                Triangles.Add(index + 2);
            }

            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 inside)
            {
                Triangle(a, b, c, inside);
                Triangle(a, c, d, inside);
            }

            public Mesh Build(string name)
            {
                var mesh = new Mesh { name = name };
                if (Vertices.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                mesh.SetVertices(Vertices);
                mesh.SetTriangles(Triangles, 0);
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                return mesh;
            }
        }

        /// <summary>Upright N-sided prism centred on the origin (height along Y).</summary>
        public static Mesh Prism(string name, int sides, float radius, float height)
        {
            var b = new Builder();
            float half = height * 0.5f;
            var top = new Vector3(0, half, 0);
            var bottom = new Vector3(0, -half, 0);

            for (int i = 0; i < sides; i++)
            {
                float a0 = (i / (float)sides) * Mathf.PI * 2f;
                float a1 = ((i + 1) / (float)sides) * Mathf.PI * 2f;
                var p0 = new Vector3(Mathf.Cos(a0) * radius, 0, Mathf.Sin(a0) * radius);
                var p1 = new Vector3(Mathf.Cos(a1) * radius, 0, Mathf.Sin(a1) * radius);

                b.Quad(p0 + Vector3.up * half, p1 + Vector3.up * half, p1 - Vector3.up * half, p0 - Vector3.up * half, Vector3.zero);
                b.Triangle(top, p1 + Vector3.up * half, p0 + Vector3.up * half, Vector3.zero);
                b.Triangle(bottom, p0 - Vector3.up * half, p1 - Vector3.up * half, Vector3.zero);
            }
            return b.Build(name);
        }

        /// <summary>N-sided cone with its base at y = 0.</summary>
        public static Mesh Cone(string name, int sides, float radius, float height)
        {
            var b = new Builder();
            var apex = new Vector3(0, height, 0);
            var center = Vector3.zero;
            for (int i = 0; i < sides; i++)
            {
                float a0 = (i / (float)sides) * Mathf.PI * 2f;
                float a1 = ((i + 1) / (float)sides) * Mathf.PI * 2f;
                var p0 = new Vector3(Mathf.Cos(a0) * radius, 0, Mathf.Sin(a0) * radius);
                var p1 = new Vector3(Mathf.Cos(a1) * radius, 0, Mathf.Sin(a1) * radius);
                b.Triangle(apex, p1, p0, new Vector3(0, height * 0.25f, 0));
                b.Triangle(center, p0, p1, new Vector3(0, height * 0.25f, 0));
            }
            return b.Build(name);
        }

        /// <summary>N-sided cone (base at y = 0) whose faces alternate between two submeshes, for striped tent roofs.</summary>
        public static Mesh StripedCone(string name, int sides, float radius, float height)
        {
            var vertices = new List<Vector3>();
            var even = new List<int>();
            var odd = new List<int>();
            var apex = new Vector3(0, height, 0);
            for (int i = 0; i < sides; i++)
            {
                float a0 = (i / (float)sides) * Mathf.PI * 2f;
                float a1 = ((i + 1) / (float)sides) * Mathf.PI * 2f;
                var p0 = new Vector3(Mathf.Cos(a0) * radius, 0, Mathf.Sin(a0) * radius);
                var p1 = new Vector3(Mathf.Cos(a1) * radius, 0, Mathf.Sin(a1) * radius);

                Vector3 normal = Vector3.Cross(p1 - apex, p0 - apex);
                bool flip = Vector3.Dot(normal, (apex + p0 + p1) / 3f - new Vector3(0, height * 0.25f, 0)) < 0f;
                int index = vertices.Count;
                vertices.Add(apex);
                vertices.Add(flip ? p0 : p1);
                vertices.Add(flip ? p1 : p0);
                var target = i % 2 == 0 ? even : odd;
                target.Add(index);
                target.Add(index + 1);
                target.Add(index + 2);
            }

            var mesh = new Mesh { name = name, subMeshCount = 2 };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(even, 0);
            mesh.SetTriangles(odd, 1);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Rough rock: an icosahedron with jittered vertices.</summary>
        public static Mesh Rock(string name, int seed)
        {
            var random = new System.Random(seed);
            float t = (1f + Mathf.Sqrt(5f)) / 2f;
            var v = new List<Vector3>
            {
                new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1)
            };
            for (int i = 0; i < v.Count; i++)
            {
                float jitter = 0.75f + (float)random.NextDouble() * 0.5f;
                v[i] = v[i].normalized * 0.5f * jitter;
                v[i] = new Vector3(v[i].x, v[i].y * 0.7f, v[i].z);
            }

            int[] faces =
            {
                0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11,
                1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9,
                4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1
            };

            var b = new Builder();
            for (int i = 0; i < faces.Length; i += 3)
            {
                b.Triangle(v[faces[i]], v[faces[i + 1]], v[faces[i + 2]], Vector3.zero);
            }
            return b.Build(name);
        }

        /// <summary>Flat-shaded heightfield terrain centred on the origin.</summary>
        public static Mesh Terrain(string name, float size, int cells, System.Func<float, float, float> height)
        {
            var b = new Builder();
            float step = size / cells;
            float start = -size * 0.5f;

            for (int z = 0; z < cells; z++)
            {
                for (int x = 0; x < cells; x++)
                {
                    float x0 = start + x * step, x1 = x0 + step;
                    float z0 = start + z * step, z1 = z0 + step;
                    var p00 = new Vector3(x0, height(x0, z0), z0);
                    var p10 = new Vector3(x1, height(x1, z0), z0);
                    var p01 = new Vector3(x0, height(x0, z1), z1);
                    var p11 = new Vector3(x1, height(x1, z1), z1);

                    // Alternate the diagonal for a less regular low-poly look.
                    if ((x + z) % 2 == 0)
                    {
                        b.Triangle(p00, p01, p11, p00 + Vector3.down * 100f);
                        b.Triangle(p00, p11, p10, p00 + Vector3.down * 100f);
                    }
                    else
                    {
                        b.Triangle(p00, p01, p10, p00 + Vector3.down * 100f);
                        b.Triangle(p10, p01, p11, p00 + Vector3.down * 100f);
                    }
                }
            }
            return b.Build(name);
        }
    }
}
