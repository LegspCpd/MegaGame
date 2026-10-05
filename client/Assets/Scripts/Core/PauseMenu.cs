using UnityEngine;

namespace Megame.Client
{
    /// <summary>
    /// Pause / save menu for the single-player build. ESC opens it; the game is
    /// frozen (and the cursor released) while it is up. Saves are written under
    /// %APPDATA%/MegaGame Studios/MegaGame/saves by <see cref="SaveSystem"/>.
    /// </summary>
    public class PauseMenu : MonoBehaviour
    {
        public static PauseMenu Instance { get; private set; }

        private bool _open;
        private float _cursorLockTimeout;
        private string _status = "";
        private GUIStyle _header;
        private GUIStyle _button;
        private GUIStyle _hint;

        public static PauseMenu Ensure()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("PauseMenu");
            return go.AddComponent<PauseMenu>();
        }

        private void Awake()
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void EnsureStyles()
        {
            if (_header != null) return;
            _header = new GUIStyle(GUI.skin.label) { fontSize = 28, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _header.normal.textColor = new Color(1f, 0.85f, 0.35f);
            _button = new GUIStyle(GUI.skin.button) { fontSize = 18 };
            _hint = new GUIStyle(GUI.skin.label) { fontSize = 13, alignment = TextAnchor.MiddleCenter };
            _hint.normal.textColor = new Color(0.75f, 0.78f, 0.82f);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Toggle();
            }

            // Re-hide the cursor shortly after closing so it does not sit
            // visible while playing.
            if (!_open && _cursorLockTimeout > 0f)
            {
                _cursorLockTimeout -= Time.unscaledDeltaTime;
                if (_cursorLockTimeout <= 0f) CursorLock();
            }
        }

        public void Toggle()
        {
            _open = !_open;
            if (_open)
            {
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;
                Time.timeScale = 0f;
            }
            else
            {
                CursorLock();
                Time.timeScale = 1f;
            }
        }

        private void CursorLock()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void OnGUI()
        {
            if (!_open) return;
            EnsureStyles();

            var area = new Rect(0f, 0f, Screen.width, Screen.height);
            GUI.Box(area, GUIContent.none);

            float w = 320f;
            float x = (Screen.width - w) * 0.5f;
            float y = Screen.height * 0.22f;

            GUI.Label(new Rect(x, y, w, 44f), "PAUSED", _header);
            y += 60f;

            if (GUI.Button(new Rect(x, y, w, 44f), "Resume", _button))
            {
                Toggle();
                return;
            }
            y += 54f;

            if (GUI.Button(new Rect(x, y, w, 44f), "Quick Save", _button))
            {
                DoQuickSave();
                return;
            }
            y += 54f;

            if (GUI.Button(new Rect(x, y, w, 44f), "Load Game", _button))
            {
                DoQuickLoad();
                return;
            }
            y += 54f;

            if (GUI.Button(new Rect(x, y, w, 44f), "Quit to Desktop", _button))
            {
                SaveAndQuit();
                return;
            }
            y += 62f;

            if (!string.IsNullOrEmpty(_status))
            {
                GUI.Label(new Rect(x, y, w, 24f), _status, _hint);
            }

            GUI.Label(new Rect(x, Screen.height - 60f, w, 24f),
                "Saves: " + SaveSystem.SaveDirectory, _hint);
        }

        private void DoQuickSave()
        {
            var player = Object.FindObjectOfType<PlayablePlayer>();
            var data = BuildSaveData(player);
            string error;
            if (SaveSystem.Save(data, out error))
            {
                _status = "Saved.";
            }
            else
            {
                _status = "Save failed: " + error;
            }
        }

        private void DoQuickLoad()
        {
            var saves = SaveSystem.ListSaves();
            if (saves.Count == 0)
            {
                _status = "No saves yet.";
                return;
            }

            string error;
            var data = SaveSystem.Load(saves[saves.Count - 1], out error);
            if (data == null)
            {
                _status = "Load failed: " + error;
                return;
            }

            var player = Object.FindObjectOfType<PlayablePlayer>();
            if (player != null)
            {
                player.transform.position = new Vector3(data.PlayerX, data.PlayerY, data.PlayerZ);
                player.transform.rotation = Quaternion.Euler(0f, data.PlayerYaw, 0f);
            }

            _status = "Loaded '" + saves[saves.Count - 1] + "'.";
        }

        private void SaveAndQuit()
        {
            var player = Object.FindObjectOfType<PlayablePlayer>();
            string error;
            SaveSystem.Save(BuildSaveData(player), out error);

            Time.timeScale = 1f;
            Application.Quit();
        }

        private static SaveData BuildSaveData(PlayablePlayer player)
        {
            var data = new SaveData { SlotName = "quicksave", Health = 100f };
            if (player != null)
            {
                var p = player.transform.position;
                data.PlayerX = p.x;
                data.PlayerY = p.y;
                data.PlayerZ = p.z;
                data.PlayerYaw = player.transform.eulerAngles.y;
                data.Stamina = player.Stamina;
            }
            return data;
        }
    }
}