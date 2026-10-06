using System.Collections.Generic;
using UnityEngine;

namespace Megame.Client
{
    /// <summary>
    /// Self-contained radar drawn with IMGUI.
    ///
    /// The project's scene has no Canvas, no RawImage and no blip prefab, so the
    /// original Minimap class could never run. This version needs nothing but a
    /// Transform to follow: it renders a rotating radar disc, the player arrow
    /// and a blip for every vehicle and pickup in range.
    /// </summary>
    public class PlayableRadar : MonoBehaviour
    {
        public static PlayableRadar Instance { get; private set; }

        [Header("Layout")]
        public float Size = 190f;
        public float Range = 90f;          // world metres shown radius
        public float CornerRadius = 95f;    // distance from screen edges

        [Header("Style")]
        public Color BackgroundColor = new Color(0f, 0f, 0f, 0.55f);
        public Color RadarColor = new Color(0.20f, 0.85f, 0.45f, 0.85f);
        public Color NorthTickColor = new Color(0.95f, 0.35f, 0.30f, 0.95f);
        public Color VehicleBlipColor = new Color(0.35f, 0.75f, 1f, 0.95f);
        public Color ObjectiveBlipColor = new Color(1f, 0.82f, 0.25f, 0.95f);

        private Transform _player;
        private readonly List<Transform> _vehicles = new List<Transform>();
        private GUIStyle _label;
        private Texture2D _circleTex;
        private Texture2D _blipTex;
        private Texture2D _arrowTex;
        private float _refreshTimer;

        public static PlayableRadar Create(Transform player)
        {
            var go = new GameObject("PlayableRadar");
            var radar = go.AddComponent<PlayableRadar>();
            radar._player = player;
            Instance = radar;
            return radar;
        }

        private void Awake()
        {
            if (_player == null)
            {
                var p = FindObjectOfType<PlayablePlayer>();
                if (p != null) _player = p.transform;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void EnsureAssets()
        {
            if (_circleTex == null)
            {
                _circleTex = RadialTexture(160, 0.08f);
                _blipTex = SolidTexture(6);
                _arrowTex = SolidTexture(8);

                _label = new GUIStyle(GUI.skin.label) { fontSize = 11, alignment = TextAnchor.MiddleCenter };
                _label.normal.textColor = new Color(0.85f, 0.9f, 0.95f);
            }
        }

        /// <summary>Re-scan the scene for vehicles a few times a second.</summary>
        private void Update()
        {
            _refreshTimer -= Time.unscaledDeltaTime;
            if (_refreshTimer > 0f) return;
            _refreshTimer = 0.5f;

            _vehicles.Clear();
            var vehicles = FindObjectsOfType<PlayableVehicle>();
            for (int i = 0; i < vehicles.Length; i++)
            {
                _vehicles.Add(vehicles[i].transform);
            }
        }

        private void OnGUI()
        {
            if (_player == null) return;
            EnsureAssets();

            float x = Screen.width - Size - CornerRadius * 0.5f;
            float y = Screen.height - Size - CornerRadius * 0.5f;
            var area = new Rect(x, y, Size, Size);

            GUI.DrawTexture(area, _circleTex, ScaleMode.StretchToFill, true, 0f,
                BackgroundColor, 0f, 0f);

            DrawRadarContent(area);
            DrawFrame(area);
        }

        private void DrawRadarContent(Rect area)
        {
            float yaw = _player.eulerAngles.y;
            float half = area.width * 0.5f;
            float scale = half / Range;   // pixels per world metre

            var oldMatrix = GUI.matrix;
            // Rotate the whole radar so the player's heading points up.
            GUIUtility.RotateAroundPivot(-yaw, area.center);

            // Range rings.
            GUI.color = new Color(RadarColor.r, RadarColor.g, RadarColor.b, 0.22f);
            for (int i = 1; i <= 3; i++)
            {
                float r = half * (i / 3f);
                GUI.DrawTexture(new Rect(area.center.x - r, area.center.y - r, r * 2f, r * 2f),
                    _circleTex, ScaleMode.StretchToFill, true, 0f, RadarColor, 0f, 0f);
            }

            // North marker.
            GUI.color = NorthTickColor;
            GUI.DrawTexture(new Rect(area.center.x - 3f, area.y + 2f, 6f, 6f), _blipTex);

            // Vehicle blips. The radar matrix is already rotated above, so world
            // deltas only need projecting onto the local axes -- rotating them
            // again here would double the rotation.
            for (int i = 0; i < _vehicles.Count; i++)
            {
                Transform v = _vehicles[i];
                if (v == null) continue;

                Vector3 delta = v.position - _player.position;
                delta.y = 0f;
                if (delta.magnitude > Range) continue;

                Vector2 p = new Vector2(delta.x, delta.z) * scale;
                GUI.color = VehicleBlipColor;
                GUI.DrawTexture(new Rect(area.center.x + p.x - 3f, area.center.y - p.y - 3f, 6f, 6f),
                    _blipTex);
            }

            GUI.matrix = oldMatrix;
            GUI.color = Color.white;

            // Player arrow at the centre, rotated by heading.
            var arrow = new Vector2(area.center.x, area.center.y - 9f);
            float a = yaw * Mathf.Deg2Rad;
            var rotated = new Vector2(
                arrow.x - area.center.x + Mathf.Sin(a) * 11f,
                arrow.y - area.center.y - Mathf.Cos(a) * 11f);

            GUI.color = Color.white;
            GUI.DrawTexture(new Rect(rotated.x - 4f, rotated.y - 4f, 8f, 8f), _arrowTex);

            GUI.Label(new Rect(area.x, area.yMax - 20f, area.width, 18f),
                $"{Mathf.RoundToInt(_player.position.x)}, {Mathf.RoundToInt(_player.position.z)}", _label);
        }

        private void DrawFrame(Rect area)
        {
            GUI.color = new Color(RadarColor.r, RadarColor.g, RadarColor.b, 0.55f);
            // Corner brackets instead of a full border, which reads cleaner.
            float t = 3f, len = 16f;
            GUI.DrawTexture(new Rect(area.x, area.yMax - t, len, t), _blipTex);
            GUI.DrawTexture(new Rect(area.x, area.y, len, t), _blipTex);
            GUI.DrawTexture(new Rect(area.xMax - len, area.yMax - t, len, t), _blipTex);
            GUI.DrawTexture(new Rect(area.xMax - len, area.y, len, t), _blipTex);
            GUI.color = Color.white;
        }

        private static Texture2D SolidTexture(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var c = new Color(1f, 1f, 1f, 1f);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    tex.SetPixel(x, y, c);
                }
            }
            tex.Apply();
            tex.hideFlags = HideFlags.HideAndDontSave;
            return tex;
        }

        /// <summary>
        /// Circular sprite with a soft rim: alpha falls from 1 at the centre to
        /// 0 across a band of width <paramref name="rimWidth"/> at d = 1.
        /// </summary>
        private static Texture2D RadialTexture(int size, float rimWidth)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f - half) / half;
                    float dy = (y + 0.5f - half) / half;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01((1f - d) / rimWidth);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            tex.Apply();
            tex.hideFlags = HideFlags.HideAndDontSave;
            return tex;
        }
    }
}