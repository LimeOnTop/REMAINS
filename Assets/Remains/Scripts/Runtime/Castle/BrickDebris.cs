using System.Collections.Generic;
using UnityEngine;

namespace Remains
{
    /// <summary>
    /// Local-only flying brick chunks (cosmetic, not networked). Capped so a big collapse can't flood physics.
    /// </summary>
    public static class BrickDebris
    {
        public const int DebrisLayer = 9;
        private const int MaxPieces = 260;

        private static readonly Queue<GameObject> s_Live = new Queue<GameObject>();
        private static Mesh s_Cube;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void ConfigureCollisions()
        {
            // Debris never blocks players (so rubble can't seal a breach) and doesn't collide with itself.
            Physics.IgnoreLayerCollision(DebrisLayer, 3, true);
            Physics.IgnoreLayerCollision(DebrisLayer, 6, true);
            Physics.IgnoreLayerCollision(DebrisLayer, DebrisLayer, true);
            s_Live.Clear();
        }

        /// <summary>Bursts <paramref name="count"/> bricks out of a box (world centre, rotation, size).</summary>
        public static void Spawn(Vector3 center, Quaternion rotation, Vector3 size, Material material, int count, Vector3 impulseFrom, float lifetime = 7f)
        {
            if (s_Cube == null) s_Cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");

            for (int i = 0; i < count; i++)
            {
                while (s_Live.Count >= MaxPieces)
                {
                    var oldest = s_Live.Dequeue();
                    if (oldest != null) Object.Destroy(oldest);
                }

                var brick = new GameObject("Brick") { layer = DebrisLayer };
                Vector3 local = new Vector3(Random.Range(-0.5f, 0.5f) * size.x, Random.Range(-0.5f, 0.5f) * size.y, Random.Range(-0.5f, 0.5f) * size.z);
                brick.transform.SetPositionAndRotation(center + rotation * local, rotation * Quaternion.Euler(Random.Range(-20f, 20f), Random.Range(0f, 360f), Random.Range(-20f, 20f)));
                brick.transform.localScale = new Vector3(Random.Range(0.35f, 0.5f), Random.Range(0.18f, 0.26f), Random.Range(0.22f, 0.32f)) * Mathf.Clamp(size.y / 0.75f, 0.6f, 1.4f);

                brick.AddComponent<MeshFilter>().sharedMesh = s_Cube;
                var renderer = brick.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                brick.AddComponent<BoxCollider>();

                var body = brick.AddComponent<Rigidbody>();
                body.mass = 4f;
                Vector3 away = (brick.transform.position - impulseFrom);
                away = away.sqrMagnitude > 0.001f ? away.normalized : Random.onUnitSphere;
                body.linearVelocity = (away * Random.Range(2f, 6f)) + Vector3.up * Random.Range(1f, 3.5f);
                body.angularVelocity = Random.insideUnitSphere * 8f;

                Object.Destroy(brick, lifetime + Random.Range(0f, 2f));
                s_Live.Enqueue(brick);
            }
        }

        /// <summary>Short-lived grey dust cubes that expand and fade (low-poly "smoke").</summary>
        public static void Dust(Vector3 position, float radius, Material material, int count = 6)
        {
            if (s_Cube == null) s_Cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            for (int i = 0; i < count; i++)
            {
                var puff = new GameObject("Dust") { layer = 2 };
                puff.transform.position = position + Random.insideUnitSphere * radius * 0.5f;
                puff.transform.rotation = Random.rotation;
                puff.AddComponent<MeshFilter>().sharedMesh = s_Cube;
                var renderer = puff.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                var fade = puff.AddComponent<DustPuff>();
                fade.Begin(radius * Random.Range(0.4f, 0.8f), Random.Range(0.8f, 1.6f));
            }
        }
    }

    /// <summary>Grows, rises and shrinks away a dust cube.</summary>
    public class DustPuff : MonoBehaviour
    {
        private float m_Size;
        private float m_Duration;
        private float m_Time;
        private Vector3 m_Drift;

        public void Begin(float size, float duration)
        {
            m_Size = size;
            m_Duration = duration;
            m_Drift = new Vector3(Random.Range(-0.5f, 0.5f), Random.Range(0.6f, 1.4f), Random.Range(-0.5f, 0.5f));
            transform.localScale = Vector3.one * size * 0.3f;
        }

        private void Update()
        {
            m_Time += Time.deltaTime;
            float t = m_Time / m_Duration;
            if (t >= 1f)
            {
                Destroy(gameObject);
                return;
            }

            float scale = m_Size * (t < 0.25f ? Mathf.Lerp(0.3f, 1f, t / 0.25f) : Mathf.Lerp(1f, 0.05f, (t - 0.25f) / 0.75f));
            transform.localScale = Vector3.one * scale;
            transform.position += m_Drift * Time.deltaTime;
            transform.Rotate(20f * Time.deltaTime, 35f * Time.deltaTime, 0f);
        }
    }
}
