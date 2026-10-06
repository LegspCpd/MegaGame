using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Megame.Client
{
    /// <summary>
    /// Letterboxed cinematic sequence that plays camera moves and subtitles
    /// without any assets or the network layer.
    ///
    /// The original CutsceneManager (Systems/CutsceneManager.cs) loaded an
    /// empty track list and pushed subtitles through
    /// GameClient.Instance.uiManager, which is null in the single-player build.
    /// </summary>
    public class CinematicPlayer : MonoBehaviour
    {
        public class Shot
        {
            public string Subtitle;
            public string Speaker;
            public Vector3 CameraOffset;   // relative to the subject
            public float LookAtHeight = 1.4f;
            public float Duration = 3.5f;
            public float Fov = 45f;
        }

        public static CinematicPlayer Instance { get; private set; }

        private readonly List<Shot> _shots = new List<Shot>();
        private Camera _camera;
        private Transform _subject;
        private PlayableCameraRig _rig;
        private Vector3 _subjectHome;

        private int _index;
        private float _shotStart;
        private float _shotLength = 3.5f;
        private bool _playing;

        private float _fromFov, _toFov;
        private Quaternion _shotFromRot;
        private Quaternion _shotToRot;
        private Vector3 _shotFromPos;
        private Vector3 _shotToPos;

        private GUIStyle _subtitleStyle;
        private GUIStyle _speakerStyle;
        private GUIStyle _skipStyle;
        private Texture2D _barTex;

        public bool IsPlaying => _playing;

        public static CinematicPlayer Create(Camera cam, PlayableCameraRig rig, Transform subject)
        {
            var go = new GameObject("CinematicPlayer");
            var c = go.AddComponent<CinematicPlayer>();
            c._camera = cam;
            c._rig = rig;
            c._subject = subject;
            c._subjectHome = subject != null ? subject.position : Vector3.zero;
            c.RegisterIntroSequence();
            Instance = c;
            return c;
        }

        private void Awake()
        {
            if (Instance == null) Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// Opening fly-over: a few angled shots over the city skyline with
        /// narration, then hands control back to gameplay.
        /// </summary>
        private void RegisterIntroSequence()
        {
            _shots.Clear();
            _shots.Add(new Shot
            {
                Speaker = "MEGAME",
                Subtitle = "Welcome to the city. Somewhere down there is your next job.",
                CameraOffset = new Vector3(-26f, 14f, -26f),
                LookAtHeight = 6f,
                Duration = 4.5f,
                Fov = 55f
            });
            _shots.Add(new Shot
            {
                Speaker = "DISPATCH",
                Subtitle = "You've got three vehicles on the street. Take any of them.",
                CameraOffset = new Vector3(18f, 8f, -14f),
                LookAtHeight = 3f,
                Duration = 4.5f,
                Fov = 48f
            });
            _shots.Add(new Shot
            {
                Speaker = "DISPATCH",
                Subtitle = "Press 1, 2 or 3 to switch weapons. F to get in a car.",
                CameraOffset = new Vector3(10f, 4.5f, 14f),
                LookAtHeight = 1.6f,
                Duration = 4.5f,
                Fov = 40f
            });
        }

        public void PlayAll()
        {
            if (_shots.Count == 0) return;
            _index = 0;
            _playing = true;
            BeginShot(0);
        }

        public void Skip()
        {
            if (!_playing) return;
            _index = _shots.Count;
            End();
        }

        private void BeginShot(int index)
        {
            var shot = _shots[index];
            _shotStart = Time.time;
            _shotLength = Mathf.Max(0.1f, shot.Duration);

            // Look from the previous shot's framing into this one.
            _shotFromRot = Quaternion.LookRotation(
                (_subjectHome + Vector3.up * shot.LookAtHeight) - (CameraPosition(shot, 0f)));
            _shotToRot = Quaternion.LookRotation(
                (_subjectHome + Vector3.up * shot.LookAtHeight) - (CameraPosition(shot, 1f)));
            _shotFromPos = CameraPosition(shot, 0f);
            _shotToPos = CameraPosition(shot, 1f);

            _fromFov = _camera != null ? _camera.fieldOfView : 60f;
            _toFov = shot.Fov;
        }

        private Vector3 CameraPosition(Shot shot, float t)
        {
            // Slight push-in across the shot so it is not a static frame.
            var offset = Vector3.Lerp(shot.CameraOffset, shot.CameraOffset * 0.86f, t);
            return _subjectHome + offset;
        }

        private void Update()
        {
            if (!_playing || _camera == null) return;

            var kb = Keyboard.current;
            if (kb != null && (kb[Key.Escape].wasPressedThisFrame || kb[Key.Space].wasPressedThisFrame))
            {
                Skip();
                return;
            }

            float t = Mathf.Clamp01((Time.time - _shotStart) / _shotLength);

            // Ease the camera move so cuts feel deliberate.
            float e = t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) * 0.5f;

            _camera.transform.position = Vector3.Lerp(_shotFromPos, _shotToPos, e);
            _camera.transform.rotation = Quaternion.Slerp(_shotFromRot, _shotToRot, e);
            _camera.fieldOfView = Mathf.Lerp(_fromFov, _toFov, e);

            if (t >= 1f)
            {
                _index++;
                if (_index >= _shots.Count) End();
                else BeginShot(_index);
            }
        }

        private void End()
        {
            _playing = false;
            if (_rig != null && _subject != null) _rig.Follow(_subject);
            if (_camera != null) _camera.fieldOfView = 70f;
        }

        private void EnsureStyles()
        {
            if (_subtitleStyle != null) return;

            _barTex = Solid(new Color(0f, 0f, 0f, 0.85f));

            _speakerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _speakerStyle.normal.textColor = new Color(1f, 0.84f, 0.35f);

            _subtitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 21,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true
            };
            _subtitleStyle.normal.textColor = Color.white;

            _skipStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                alignment = TextAnchor.MiddleCenter
            };
            _skipStyle.normal.textColor = new Color(0.7f, 0.74f, 0.8f);
        }

        private void OnGUI()
        {
            if (!_playing || _index >= _shots.Count) return;
            EnsureStyles();

            var shot = _shots[_index];

            // Letterbox bars.
            const float barH = 78f;
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, barH), _barTex);
            GUI.DrawTexture(new Rect(0f, Screen.height - barH, Screen.width, barH), _barTex);

            // Subtitle inside the lower bar.
            float w = Mathf.Min(900f, Screen.width - 120f);
            float x = (Screen.width - w) * 0.5f;
            GUI.Label(new Rect(x, Screen.height - barH + 6f, w, 22f), shot.Speaker, _speakerStyle);
            GUI.Label(new Rect(x, Screen.height - barH + 28f, w, 44f), shot.Subtitle, _subtitleStyle);

            GUI.Label(new Rect(0f, barH + 6f, Screen.width, 18f), "SPACE / ESC to skip", _skipStyle);
        }

        private static Texture2D Solid(Color c)
        {
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, c);
            tex.Apply();
            tex.hideFlags = HideFlags.HideAndDontSave;
            return tex;
        }
    }
}