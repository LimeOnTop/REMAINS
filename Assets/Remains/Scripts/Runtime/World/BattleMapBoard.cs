using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Remains
{
    /// <summary>One stroke segment on the battle map, in normalized map coordinates (0..65535).</summary>
    public struct BoardSegment : INetworkSerializeByMemcpy, IEquatable<BoardSegment>
    {
        public ushort AX, AY, BX, BY;
        public byte Color;

        public bool Equals(BoardSegment other) => AX == other.AX && AY == other.AY && BX == other.BX && BY == other.BY && Color == other.Color;
        public override bool Equals(object obj) => obj is BoardSegment other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(AX, AY, BX, BY, Color);
    }

    /// <summary>
    /// The attackers' planning map shown on the table in every tent (Docs/GDD.md, section 6).
    /// All tables show the same shared drawing: LMB draws (each player has their own colour), RMB erases,
    /// X wipes the map. Strokes are replicated as a list of segments owned by the session owner.
    /// </summary>
    public class BattleMapBoard : NetworkBehaviour
    {
        public static BattleMapBoard Instance { get; private set; }

        private const int MaxSegments = 5000;
        private static readonly Color[] s_Palette =
        {
            new Color(0.78f, 0.1f, 0.08f),
            new Color(0.1f, 0.22f, 0.7f),
            new Color(0.08f, 0.08f, 0.08f),
            new Color(0.1f, 0.5f, 0.12f),
            new Color(0.55f, 0.12f, 0.6f),
            new Color(0.85f, 0.45f, 0.02f)
        };

        [Tooltip("Top-down picture of the battlefield (baked by the content builder, must be readable).")]
        [SerializeField] private Texture2D baseMap;
        [Tooltip("Table tops showing the map. Each is a scaled cube: local X/Z in [-0.5, 0.5] map to the texture.")]
        [SerializeField] private List<Renderer> surfaces = new List<Renderer>();
        [SerializeField] private Material surfaceMaterial;
        [SerializeField] private float reach = 3.2f;
        [SerializeField] private int brushRadius = 3;

        private NetworkList<BoardSegment> m_Segments;
        private Texture2D m_Texture;
        private Color32[] m_BasePixels;
        private Color32[] m_Pixels;
        private Material m_RuntimeMaterial;
        private bool m_Dirty;
        private bool m_NeedsFullRedraw;
        private float m_UploadTimer;

        private readonly List<BoardSegment> m_Outgoing = new List<BoardSegment>();
        private bool m_HasLastPoint;
        private Vector2 m_LastPoint;
        private float m_SendTimer;
        private bool m_Hovering;

        private void Awake()
        {
            m_Segments = new NetworkList<BoardSegment>();
        }

        public override void OnNetworkSpawn()
        {
            Instance = this;
            CreateTexture();
            m_Segments.OnListChanged += HandleListChanged;
            m_NeedsFullRedraw = true;
        }

        public override void OnNetworkDespawn()
        {
            m_Segments.OnListChanged -= HandleListChanged;
            SetHover(false);
            if (Instance == this) Instance = null;
        }

        private void CreateTexture()
        {
            if (baseMap == null || m_Texture != null) return;

            m_Texture = new Texture2D(baseMap.width, baseMap.height, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            m_BasePixels = baseMap.GetPixels32();
            m_Pixels = (Color32[])m_BasePixels.Clone();
            m_Texture.SetPixels32(m_Pixels);
            m_Texture.Apply();

            m_RuntimeMaterial = surfaceMaterial != null ? new Material(surfaceMaterial) : new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            m_RuntimeMaterial.SetTexture("_BaseMap", m_Texture);
            m_RuntimeMaterial.mainTexture = m_Texture;
            foreach (var surface in surfaces)
            {
                if (surface != null) surface.sharedMaterial = m_RuntimeMaterial;
            }
        }

        #region Rendering

        private void HandleListChanged(NetworkListEvent<BoardSegment> change)
        {
            if (change.Type == NetworkListEvent<BoardSegment>.EventType.Add)
            {
                DrawSegment(change.Value);
            }
            else
            {
                m_NeedsFullRedraw = true;
            }
        }

        private void LateUpdate()
        {
            if (m_Texture == null) return;

            if (m_NeedsFullRedraw)
            {
                m_NeedsFullRedraw = false;
                Array.Copy(m_BasePixels, m_Pixels, m_Pixels.Length);
                foreach (var segment in m_Segments) DrawSegment(segment);
                m_Dirty = true;
            }

            m_UploadTimer -= Time.deltaTime;
            if (m_Dirty && m_UploadTimer <= 0f)
            {
                m_UploadTimer = 0.08f;
                m_Dirty = false;
                m_Texture.SetPixels32(m_Pixels);
                m_Texture.Apply(false);
            }
        }

        private void DrawSegment(BoardSegment segment)
        {
            if (m_Pixels == null) return;
            int w = m_Texture.width, h = m_Texture.height;
            Vector2 a = new Vector2(segment.AX / 65535f * (w - 1), segment.AY / 65535f * (h - 1));
            Vector2 b = new Vector2(segment.BX / 65535f * (w - 1), segment.BY / 65535f * (h - 1));
            Color32 color = s_Palette[segment.Color % s_Palette.Length];

            int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b)));
            for (int i = 0; i <= steps; i++)
            {
                Vector2 p = Vector2.Lerp(a, b, i / (float)steps);
                Stamp(Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.y), color, w, h);
            }
            m_Dirty = true;
        }

        private void Stamp(int cx, int cy, Color32 color, int w, int h)
        {
            int r = brushRadius;
            for (int y = -r; y <= r; y++)
            {
                int py = cy + y;
                if (py < 0 || py >= h) continue;
                for (int x = -r; x <= r; x++)
                {
                    int px = cx + x;
                    if (px < 0 || px >= w || x * x + y * y > r * r) continue;
                    m_Pixels[py * w + px] = color;
                }
            }
        }

        #endregion

        #region Local drawing

        private void Update()
        {
            if (!IsSpawned) return;

            bool hovering = TryGetLocalPointer(out Vector2 uv);
            SetHover(hovering);
            if (!hovering)
            {
                m_HasLastPoint = false;
                FlushOutgoing(true);
                return;
            }

            HudHint.Set("КАРТА: ЛКМ — рисовать план · ПКМ — стереть · X — очистить всё", 0f, 4);

            var mouse = Mouse.current;
            var keyboard = Keyboard.current;

            if (keyboard != null && keyboard.xKey.wasPressedThisFrame)
            {
                ClearRpc();
            }

            if (mouse != null && mouse.rightButton.isPressed)
            {
                m_HasLastPoint = false;
                m_SendTimer -= Time.deltaTime;
                if (m_SendTimer <= 0f)
                {
                    m_SendTimer = 0.1f;
                    EraseRpc(uv, 0.03f);
                }
                return;
            }

            if (mouse != null && mouse.leftButton.isPressed)
            {
                if (m_HasLastPoint && Vector2.Distance(uv, m_LastPoint) > 0.003f)
                {
                    var segment = new BoardSegment
                    {
                        AX = ToShort(m_LastPoint.x), AY = ToShort(m_LastPoint.y),
                        BX = ToShort(uv.x), BY = ToShort(uv.y),
                        Color = (byte)(NetworkManager.LocalClientId % (ulong)s_Palette.Length)
                    };
                    m_Outgoing.Add(segment);
                    DrawSegment(segment);
                    m_LastPoint = uv;
                }
                else if (!m_HasLastPoint)
                {
                    m_LastPoint = uv;
                    m_HasLastPoint = true;
                }
            }
            else
            {
                m_HasLastPoint = false;
            }

            FlushOutgoing(false);
        }

        private static ushort ToShort(float value) => (ushort)Mathf.RoundToInt(Mathf.Clamp01(value) * 65535f);

        private void FlushOutgoing(bool force)
        {
            if (m_Outgoing.Count == 0) return;
            m_SendTimer -= Time.deltaTime;
            if (!force && m_SendTimer > 0f && m_Outgoing.Count < 32) return;
            m_SendTimer = 0.1f;
            AddSegmentsRpc(m_Outgoing.ToArray());
            m_Outgoing.Clear();
        }

        private bool TryGetLocalPointer(out Vector2 uv)
        {
            uv = default;
            if (LoadoutMenu.IsOpen) return false;

            var member = TeamMember.Local;
            var camera = Camera.main;
            if (member == null || camera == null || member.Player == null || member.Player.PlayerState == null || !member.Player.PlayerState.IsActive) return false;
            if (GrabController.Local != null && GrabController.Local.Held != null) return false;

            var ray = new Ray(camera.transform.position, camera.transform.forward);
            if (!Physics.Raycast(ray, out var hit, reach + 1.5f, ~(1 << BrickDebris.DebrisLayer), QueryTriggerInteraction.Ignore)) return false;

            foreach (var surface in surfaces)
            {
                if (surface == null || hit.collider.gameObject != surface.gameObject) continue;
                if (Vector3.Distance(member.transform.position, hit.point) > reach) return false;

                Vector3 local = surface.transform.InverseTransformPoint(hit.point);
                uv = new Vector2(local.x + 0.5f, local.z + 0.5f);
                return true;
            }
            return false;
        }

        private void SetHover(bool hovering)
        {
            if (m_Hovering == hovering) return;
            m_Hovering = hovering;
            LocalPlayerLocks.SetWeapon(this, hovering);
        }

        #endregion

        #region Replication

        [Rpc(SendTo.Owner)]
        private void AddSegmentsRpc(BoardSegment[] segments)
        {
            foreach (var segment in segments)
            {
                if (m_Segments.Count >= MaxSegments) return;
                m_Segments.Add(segment);
            }
        }

        [Rpc(SendTo.Owner)]
        private void EraseRpc(Vector2 center, float radius)
        {
            for (int i = m_Segments.Count - 1; i >= 0; i--)
            {
                var s = m_Segments[i];
                var a = new Vector2(s.AX / 65535f, s.AY / 65535f);
                var b = new Vector2(s.BX / 65535f, s.BY / 65535f);
                if (Vector2.Distance(a, center) < radius || Vector2.Distance(b, center) < radius)
                {
                    m_Segments.RemoveAt(i);
                }
            }
        }

        [Rpc(SendTo.Owner)]
        private void ClearRpc() => ClearBoard();

        /// <summary>Authority: wipes the plan (new round).</summary>
        public void ClearBoard()
        {
            if (HasAuthority && m_Segments.Count > 0) m_Segments.Clear();
        }

        #endregion

#if UNITY_EDITOR
        public void EditorSetup(Texture2D map, List<Renderer> tableSurfaces, Material material)
        {
            baseMap = map;
            surfaces = tableSurfaces;
            surfaceMaterial = material;
        }
#endif
    }
}
