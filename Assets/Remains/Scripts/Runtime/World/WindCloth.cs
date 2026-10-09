using System.Collections.Generic;
using UnityEngine;

namespace Remains
{
    /// <summary>
    /// Low-poly fabric waving in the wind (tent walls, flaps, pennants, banners). Generates its own faceted grid
    /// mesh and displaces it on the CPU; cheap enough for dozens of pieces and only runs while visible.
    /// Local space: X from 0 to width (left to right), Y from 0 down to -height; the fabric faces -Z.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class WindCloth : MonoBehaviour
    {
        public enum PinnedEdge
        {
            Top,
            Left
        }

        [SerializeField] private float width = 2f;
        [SerializeField] private float height = 1f;
        [SerializeField] private int segmentsX = 8;
        [SerializeField] private int segmentsY = 4;
        [SerializeField] private PinnedEdge pinned = PinnedEdge.Top;
        [SerializeField] private float amplitude = 0.12f;
        [SerializeField] private float frequency = 2.2f;
        [SerializeField] private float waveLength = 1.6f;
        [SerializeField] private float updateRate = 30f;
        [SerializeField] private float maxDistance = 90f;

        private Mesh m_Mesh;
        private Vector3[] m_Base;
        private Vector3[] m_Vertices;
        private float m_Phase;
        private float m_Timer;
        private MeshRenderer m_Renderer;

        public void Configure(float clothWidth, float clothHeight, int columns, int rows, PinnedEdge edge, float waveAmplitude, float waveFrequency)
        {
            width = clothWidth;
            height = clothHeight;
            segmentsX = columns;
            segmentsY = rows;
            pinned = edge;
            amplitude = waveAmplitude;
            frequency = waveFrequency;
            Build();
        }

        private void Awake()
        {
            m_Renderer = GetComponent<MeshRenderer>();
            m_Phase = Random.Range(0f, 10f);
            Build();
        }

        private void Build()
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            float dx = width / segmentsX, dy = height / segmentsY;

            for (int y = 0; y < segmentsY; y++)
            {
                for (int x = 0; x < segmentsX; x++)
                {
                    var a = new Vector3(x * dx, -y * dy, 0f);
                    var b = new Vector3((x + 1) * dx, -y * dy, 0f);
                    var c = new Vector3((x + 1) * dx, -(y + 1) * dy, 0f);
                    var d = new Vector3(x * dx, -(y + 1) * dy, 0f);
                    AddTriangle(vertices, triangles, a, b, c);
                    AddTriangle(vertices, triangles, a, c, d);
                }
            }

            m_Base = vertices.ToArray();
            m_Vertices = vertices.ToArray();

            if (m_Mesh == null)
            {
                m_Mesh = new Mesh { name = "WindCloth" };
                m_Mesh.MarkDynamic();
            }
            m_Mesh.Clear();
            m_Mesh.SetVertices(m_Vertices);
            m_Mesh.SetTriangles(triangles, 0);
            m_Mesh.RecalculateNormals();
            m_Mesh.RecalculateBounds();
            m_Mesh.bounds = new Bounds(new Vector3(width * 0.5f, -height * 0.5f, 0f), new Vector3(width, height, amplitude * 4f + 0.2f));
            GetComponent<MeshFilter>().sharedMesh = m_Mesh;
        }

        private static void AddTriangle(List<Vector3> vertices, List<int> triangles, Vector3 a, Vector3 b, Vector3 c)
        {
            int index = vertices.Count;
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            triangles.Add(index);
            triangles.Add(index + 1);
            triangles.Add(index + 2);
        }

        private void Update()
        {
            if (m_Mesh == null || m_Renderer == null || !m_Renderer.isVisible) return;

            m_Timer -= Time.deltaTime;
            if (m_Timer > 0f) return;
            m_Timer = 1f / updateRate;

            var camera = Camera.main;
            if (camera != null && (camera.transform.position - transform.position).sqrMagnitude > maxDistance * maxDistance) return;

            float time = Time.time * frequency + m_Phase;
            float gust = 0.75f + 0.25f * Mathf.PerlinNoise(Time.time * 0.3f, m_Phase);

            for (int i = 0; i < m_Base.Length; i++)
            {
                Vector3 p = m_Base[i];
                float weight = pinned == PinnedEdge.Top
                    ? Mathf.Pow(Mathf.Clamp01(-p.y / height), 1.3f)
                    : Mathf.Pow(Mathf.Clamp01(p.x / width), 1.15f);

                float wave = Mathf.Sin(time + (p.x - p.y) / waveLength * 2f) + 0.4f * Mathf.Sin(time * 1.7f + p.x * 3.1f);
                Vector3 offset = new Vector3(0f, 0f, wave * amplitude * weight * gust);
                if (pinned == PinnedEdge.Left)
                {
                    // Flags droop a little at the loose end.
                    offset.y = -weight * 0.08f * height + Mathf.Sin(time * 0.8f + p.x) * 0.03f * weight;
                }
                m_Vertices[i] = p + offset;
            }

            m_Mesh.SetVertices(m_Vertices);
            m_Mesh.RecalculateNormals();
        }
    }
}
