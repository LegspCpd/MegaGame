using UnityEngine;

namespace Megame.Client
{
    /// <summary>
    /// Immediate-mode HUD. IMGUI is used deliberately: it needs no font asset,
    /// no Canvas prefab and no scene wiring, which matters because the project
    /// ships no UI assets.
    /// </summary>
    public class PlayableHUD : MonoBehaviour
    {
        private GUIStyle _label;
        private GUIStyle _panel;
        private GUIStyle _title;
        private Texture2D _barBg;
        private Texture2D _barFill;
        private Texture2D _staminaBg;
        private Texture2D _staminaFill;
        private float _fps;

        public static PlayableHUD Ensure()
        {
            var existing = Object.FindObjectOfType<PlayableHUD>();
            if (existing != null) return existing;

            var go = new GameObject("PlayableHUD");
            return go.AddComponent<PlayableHUD>();
        }

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            _barBg = SolidTexture(new Color(0f, 0f, 0f, 0.55f));
            _barFill = SolidTexture(new Color(0.85f, 0.25f, 0.25f, 0.95f));
            _staminaBg = SolidTexture(new Color(0f, 0f, 0f, 0.55f));
            _staminaFill = SolidTexture(new Color(0.25f, 0.75f, 0.95f, 0.95f));
        }

        private static Texture2D SolidTexture(Color c)
        {
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, c);
            tex.Apply();
            tex.hideFlags = HideFlags.HideAndDontSave;
            return tex;
        }

        private void EnsureStyles()
        {
            if (_label != null) return;

            _label = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                normal = { textColor = new Color(0.92f, 0.94f, 0.96f) }
            };
            _label.normal.textColor = new Color(0.92f, 0.94f, 0.96f);

            _title = new GUIStyle(_label) { fontSize = 20, fontStyle = FontStyle.Bold };
            _title.normal.textColor = new Color(1f, 0.85f, 0.35f);

            _panel = new GUIStyle(GUI.skin.box);
        }

        private void Update()
        {
            _fps = Mathf.Lerp(_fps, 1f / Mathf.Max(Time.deltaTime, 0.0001f), 0.1f);
        }

        private void OnGUI()
        {
            EnsureStyles();

            var player = Object.FindObjectOfType<PlayablePlayer>();

            DrawTopLeft(player);
            DrawBars(player);
            DrawHelp();
        }

        private void DrawTopLeft(PlayablePlayer player)
        {
            var r = new Rect(12f, 12f, 320f, 100f);
            GUI.Box(r, GUIContent.none, _panel);
            GUI.Label(new Rect(r.x + 10f, r.y + 6f, 300f, 24f), "MEGAME", _title);

            string pos = player != null ? player.transform.position.ToString("F1") : "-";
            GUI.Label(new Rect(r.x + 10f, r.y + 32f, 300f, 20f), $"FPS  {_fps:F0}", _label);
            GUI.Label(new Rect(r.x + 10f, r.y + 52f, 300f, 20f), $"POS  {pos}", _label);

            // Surface the real network state instead of hiding failures.
            var client = GameClient.Instance;
            if (client == null || !client.autoConnect)
            {
                GUI.Label(new Rect(r.x + 10f, r.y + 72f, 300f, 20f), "MODE  single-player", _label);
            }
            else
            {
                string state = client.IsConnected
                    ? $"connected  id={client.LocalPlayerId}  tick={client.ServerTick}"
                    : "connecting to localhost:50051 ...";
                GUI.Label(new Rect(r.x + 10f, r.y + 72f, 300f, 20f), $"NET  {state}", _label);
            }
        }

        private void DrawBars(PlayablePlayer player)
        {
            float health = 100f;
            float stamina = player != null ? player.Stamina : 100f;
            float maxStamina = player != null ? player.MaxStamina : 100f;

            float w = 220f, h = 18f;
            float x = Screen.width - w - 16f;
            float y = Screen.height - 60f;

            GUI.DrawTexture(new Rect(x, y, w, h), _barBg);
            GUI.DrawTexture(new Rect(x, y, w * (health / 100f), h), _barFill);
            GUI.Label(new Rect(x, y - 20f, w, 18f), "HEALTH", _label);

            GUI.DrawTexture(new Rect(x, y + h + 4f, w, h), _staminaBg);
            GUI.DrawTexture(new Rect(x, y + h + 4f, w * Mathf.Clamp01(stamina / maxStamina), h), _staminaFill);
            GUI.Label(new Rect(x, y + h - 16f, w, 18f), "STAMINA", _label);
        }

        private void DrawHelp()
        {
            var r = new Rect(12f, Screen.height - 150f, 300f, 138f);
            GUI.Box(r, GUIContent.none, _panel);
            GUI.Label(new Rect(r.x + 10f, r.y + 6f, 280f, 20f), "CONTROLS", _title);
            GUI.Label(new Rect(r.x + 10f, r.y + 30f, 280f, 110f),
                "WASD / Arrows .... Move\n" +
                "Shift .............. Sprint\n" +
                "Ctrl ............... Crouch\n" +
                "Space .............. Jump\n" +
                "Right mouse ....... Look\n" +
                "Q / E .............. Rotate camera\n" +
                "Scroll ............. Zoom\n" +
                "ESC ................ Menu / Save",
                _label);
        }
    }
}