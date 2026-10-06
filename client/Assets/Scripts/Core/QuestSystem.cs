using System;
using System.Collections.Generic;
using UnityEngine;

namespace Megame.Client
{
    /// <summary>
    /// Objectives the player can pick up, track and complete.
    ///
    /// The project has MissionManager, but it only reacts to server-pushed
    /// mission updates and renders through GameClient.uiManager, which is null
    /// in the single-player build. This is the offline equivalent: tasks are
    /// defined locally, progress is tracked, and completion pays out.
    /// </summary>
    public class QuestSystem : MonoBehaviour
    {
        public class Quest
        {
            public string Id;
            public string Title;
            public string Summary;
            public Func<bool> IsComplete;
            public int Reward;
            public bool Completed;
            public bool Tracked;
        }

        public static QuestSystem Instance { get; private set; }

        private readonly List<Quest> _quests = new List<Quest>();
        private readonly List<string> _log = new List<string>();
        private float _logExpiresAt;

        private GUIStyle _titleStyle;
        private GUIStyle _bodyStyle;
        private GUIStyle _logStyle;
        private Texture2D _panelTex;
        private Texture2D _logTex;

        public int CompletedCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _quests.Count; i++) if (_quests[i].Completed) n++;
                return n;
            }
        }

        public static QuestSystem Create()
        {
            var go = new GameObject("QuestSystem");
            var q = go.AddComponent<QuestSystem>();
            q.RegisterStarterQuests();
            Instance = q;
            return q;
        }

        private void Awake()
        {
            if (Instance == null) Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ---------------------------------------------------------------- setup

        private void RegisterStarterQuests()
        {
            var vitals = PlayerVitals.Instance;

            // "First contact": kill a pedestrian. Deliberately low-stakes: it
            // teaches that shooting has a consequence without being a tutorial
            // wall.
            _quests.Add(new Quest
            {
                Id = "first_contact",
                Title = "First Contact",
                Summary = "Take down one street thug.",
                Reward = 150,
                IsComplete = () => TotalKills >= 1
            });

            _quests.Add(new Quest
            {
                Id = "wheelman",
                Title = "Wheelman",
                Summary = "Get behind the wheel of a car.",
                Reward = 100,
                IsComplete = () => HasDriven
            });

            _quests.Add(new Quest
            {
                Id = "rich",
                Title = "Street Smart",
                Summary = "Hold $3000 in your wallet.",
                Reward = 250,
                IsComplete = () => vitals != null && vitals.Cash >= 3000f
            });
        }

        // -------------------------------------------------------------- tracking

        private int TotalKills { get; set; }
        private bool HasDriven { get; set; }

        /// <summary>Called by the weapon when a pedestrian is downed.</summary>
        public void ReportKill()
        {
            TotalKills++;
            PushLog("Pedestrian down");
        }

        /// <summary>Called when the player takes the wheel.</summary>
        public void ReportDriving()
        {
            if (HasDriven) return;
            HasDriven = true;
            PushLog("Now driving");
        }

        // -------------------------------------------------------------- update

        private void Update()
        {
            // Track the first incomplete quest automatically, otherwise the
            // player never sees the objective list at all.
            if (Tracked == null)
            {
                for (int i = 0; i < _quests.Count; i++)
                {
                    if (_quests[i].Completed) continue;
                    _quests[i].Tracked = true;
                    break;
                }
            }

            for (int i = 0; i < _quests.Count; i++)
            {
                var q = _quests[i];
                if (q.Completed) continue;

                // IsComplete reads live game state (cash, kill count) and can
                // legitimately throw if a dependency was destroyed. Log it
                // rather than failing the quest in silence.
                bool done;
                try
                {
                    done = q.IsComplete();
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[Quests] '{q.Id}' check failed: {e.Message}");
                    continue;
                }

                if (!done) continue;

                q.Completed = true;
                q.Tracked = false;

                var vitals = PlayerVitals.Instance;
                if (vitals != null) vitals.AddCash(q.Reward);

                PushLog($"COMPLETE: {q.Title}  +${q.Reward}");
            }
        }

        public void PushLog(string message)
        {
            _log.Add(message);
            if (_log.Count > 4) _log.RemoveAt(0);
            _logExpiresAt = Time.time + 6f;
        }

        public Quest Tracked
        {
            get
            {
                for (int i = 0; i < _quests.Count; i++)
                {
                    if (_quests[i].Tracked && !_quests[i].Completed) return _quests[i];
                }
                return null;
            }
        }

        public void ToggleTracked(Quest quest)
        {
            if (quest == null) return;
            quest.Tracked = !quest.Tracked;
        }

        public IReadOnlyList<Quest> Quests => _quests;

        // ----------------------------------------------------------------- draw

        private void EnsureStyles()
        {
            if (_titleStyle != null) return;

            _panelTex = Solid(new Color(0.05f, 0.06f, 0.08f, 0.88f));
            _logTex = Solid(new Color(0.10f, 0.12f, 0.16f, 0.92f));

            _titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 17, fontStyle = FontStyle.Bold };
            _titleStyle.normal.textColor = new Color(1f, 0.84f, 0.35f);

            _bodyStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, wordWrap = true };
            _bodyStyle.normal.textColor = new Color(0.85f, 0.88f, 0.92f);

            _logStyle = new GUIStyle(GUI.skin.label) { fontSize = 14 };
            _logStyle.normal.textColor = new Color(0.95f, 0.97f, 1f);
        }

        private void OnGUI()
        {
            EnsureStyles();

            DrawTracked();
            DrawLog();
        }

        private void DrawTracked()
        {
            var quest = Tracked;
            if (quest == null) return;

            const float w = 330f, h = 74f;
            float x = Screen.width - w - 16f;
            float y = 16f;

            GUI.DrawTexture(new Rect(x, y, w, h), _panelTex);
            GUI.Label(new Rect(x + 12f, y + 8f, w - 24f, 22f), quest.Title, _titleStyle);
            GUI.Label(new Rect(x + 12f, y + 30f, w - 24f, 34f), quest.Summary, _bodyStyle);
        }

        private void DrawLog()
        {
            if (_log.Count == 0 || Time.time > _logExpiresAt) return;

            const float w = 320f;
            float h = 22f + _log.Count * 20f;
            float x = Screen.width - w - 16f;
            float y = Screen.height * 0.34f;

            GUI.DrawTexture(new Rect(x, y, w, h), _logTex);
            for (int i = 0; i < _log.Count; i++)
            {
                GUI.Label(new Rect(x + 10f, y + 6f + i * 20f, w - 20f, 20f), _log[i], _logStyle);
            }
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