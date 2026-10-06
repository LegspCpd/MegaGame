using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Megame.Client
{
    /// <summary>
    /// Self-contained branching dialogue for the single-player build.
    ///
    /// The original DialogueManager (Systems/DialogueManager.cs) returned a hard
    /// coded placeholder node, sent every choice to the Go server over gRPC, and
    /// drove its UI through GameClient.uiManager -- which is null in the
    /// single-player build, so showing a node would have thrown a
    /// NullReferenceException. This version carries its own scripts, renders
    /// with IMGUI and advances locally.
    /// </summary>
    public class DialogueSystem : MonoBehaviour
    {
        public class Choice
        {
            public string Text;
            public string NextNodeId;
        }

        public class Node
        {
            public string Id;
            public string Speaker;
            public string Text;
            public Choice[] Choices;
            public string NextNodeId;      // used when Choices is empty
            public float AutoAdvance;     // seconds; 0 = wait for E
        }

        public static DialogueSystem Instance { get; private set; }

        private readonly Dictionary<string, Node> _scripts = new Dictionary<string, Node>();
        private Node _current;
        private int _highlighted;

        private GUIStyle _speakerStyle;
        private GUIStyle _bodyStyle;
        private GUIStyle _choiceStyle;
        private GUIStyle _choiceActiveStyle;
        private GUIStyle _hintStyle;
        private Texture2D _panelTex;
        private Texture2D _choiceTex;
        private Texture2D _choiceActiveTex;

        public bool IsActive => _current != null;

        public static DialogueSystem Create()
        {
            var go = new GameObject("DialogueSystem");
            var d = go.AddComponent<DialogueSystem>();
            d.RegisterScripts();
            Instance = d;
            return d;
        }

        private void Awake()
        {
            if (Instance == null) Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ------------------------------------------------------------ scripts

        private void RegisterScripts()
        {
            // "Stranger on the corner": a branching conversation that shows
            // choice handling without needing any content files.
            var stranger = new Dictionary<string, Node>
            {
                ["start"] = new Node
                {
                    Id = "start",
                    Speaker = "Stranger",
                    Text = "You look like you've got wheels. Fancy a job?",
                    Choices = new[]
                    {
                        new Choice { Text = "What kind of job?", NextNodeId = "job" },
                        new Choice { Text = "Not right now.", NextNodeId = "bye" },
                    }
                },
                ["job"] = new Node
                {
                    Id = "job",
                    Speaker = "Stranger",
                    Text = "Courier runs. Pick something up across town, bring it here. Pays $250 a run.",
                    Choices = new[]
                    {
                        new Choice { Text = "Where do I start?", NextNodeId = "start" },
                        new Choice { Text = "I'll pass.", NextNodeId = "bye" },
                    }
                },
                ["bye"] = new Node
                {
                    Id = "bye",
                    Speaker = "Stranger",
                    Text = "Your loss. I'll be around.",
                    NextNodeId = null,
                },
            };

            foreach (var kv in stranger) _scripts[kv.Key] = kv.Value;
        }

        public void RegisterScript(string dialogueId, Dictionary<string, Node> nodes)
        {
            foreach (var kv in nodes) _scripts[kv.Key] = kv.Value;
        }

        // -------------------------------------------------------------- flow

        public void StartDialogue(string dialogueId, string startNodeId = "start")
        {
            if (_scripts.TryGetValue(startNodeId, out var node)) ShowNode(node);
        }

        public void ShowNode(Node node)
        {
            _current = node;
            _highlighted = 0;
        }

        public void EndDialogue()
        {
            _current = null;
        }

        private void Advance()
        {
            if (_current == null) return;

            if (_current.Choices != null && _current.Choices.Length == 0)
            {
                if (string.IsNullOrEmpty(_current.NextNodeId)) { EndDialogue(); return; }
                if (_scripts.TryGetValue(_current.NextNodeId, out var next)) ShowNode(next);
                else EndDialogue();
                return;
            }

            // Wait for E (or click) before continuing a no-choice node.
            var kb = Keyboard.current;
            bool confirm = (kb != null && kb[Key.E].wasPressedThisFrame) ||
                           (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame);
            if (!confirm) return;

            if (!string.IsNullOrEmpty(_current.NextNodeId) &&
                _scripts.TryGetValue(_current.NextNodeId, out var nx)) ShowNode(nx);
            else EndDialogue();
        }

        private void Choose(int index)
        {
            if (_current?.Choices == null || index < 0 || index >= _current.Choices.Length) return;

            var choice = _current.Choices[index];
            if (!string.IsNullOrEmpty(choice.NextNodeId) &&
                _scripts.TryGetValue(choice.NextNodeId, out var next)) ShowNode(next);
            else EndDialogue();
        }

        private void Update()
        {
            if (_current == null) return;

            var kb = Keyboard.current;
            if (kb == null) return;

            if (_current.Choices == null || _current.Choices.Length == 0)
            {
                if (kb[Key.Escape].wasPressedThisFrame) EndDialogue();
                else Advance();
                return;
            }

            // Choice navigation: up/down highlights, E or Enter confirms.
            int n = _current.Choices.Length;
            if (kb[Key.W].wasPressedThisFrame || kb[Key.UpArrow].wasPressedThisFrame)
                _highlighted = (_highlighted - 1 + n) % n;
            if (kb[Key.S].wasPressedThisFrame || kb[Key.DownArrow].wasPressedThisFrame)
                _highlighted = (_highlighted + 1) % n;

            if (kb[Key.E].wasPressedThisFrame || kb[Key.Enter].wasPressedThisFrame ||
                kb[Key.Space].wasPressedThisFrame)
            {
                Choose(_highlighted);
            }

            // Number keys pick directly.
            for (int i = 0; i < n && i < 9; i++)
            {
                if (kb[(Key)((int)Key.Digit1 + i)].wasPressedThisFrame) { Choose(i); break; }
            }
        }

        // --------------------------------------------------------------- draw

        private void EnsureStyles()
        {
            if (_speakerStyle != null) return;

            _panelTex = Solid(new Color(0.05f, 0.06f, 0.08f, 0.92f));
            _choiceTex = Solid(new Color(0.14f, 0.15f, 0.18f, 0.85f));
            _choiceActiveTex = Solid(new Color(0.26f, 0.34f, 0.48f, 0.95f));

            _speakerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold
            };
            _speakerStyle.normal.textColor = new Color(1f, 0.84f, 0.35f);

            _bodyStyle = new GUIStyle(GUI.skin.label) { fontSize = 17, wordWrap = true };
            _bodyStyle.normal.textColor = new Color(0.92f, 0.94f, 0.97f);

            _choiceStyle = new GUIStyle(GUI.skin.label) { fontSize = 16 };
            _choiceStyle.normal.textColor = new Color(0.82f, 0.86f, 0.92f);

            _choiceActiveStyle = new GUIStyle(_choiceStyle) { fontStyle = FontStyle.Bold };
            _choiceActiveStyle.normal.textColor = Color.white;

            _hintStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, alignment = TextAnchor.MiddleCenter };
            _hintStyle.normal.textColor = new Color(0.65f, 0.70f, 0.76f);
        }

        private void OnGUI()
        {
            if (_current == null) return;
            EnsureStyles();

            float w = Mathf.Min(760f, Screen.width - 80f);
            float x = (Screen.width - w) * 0.5f;
            float y = Screen.height * 0.58f;

            bool hasChoices = _current.Choices != null && _current.Choices.Length > 0;
            float bodyH = 58f;
            float choiceH = hasChoices ? _current.Choices.Length * 34f + 12f : 0f;
            float panelH = bodyH + choiceH + 18f;

            GUI.DrawTexture(new Rect(x, y, w, panelH), _panelTex);

            GUI.Label(new Rect(x + 18f, y + 8f, w - 36f, 24f), _current.Speaker, _speakerStyle);
            GUI.Label(new Rect(x + 18f, y + 32f, w - 36f, bodyH), _current.Text, _bodyStyle);

            if (hasChoices)
            {
                float cy = y + bodyH + 8f;
                for (int i = 0; i < _current.Choices.Length; i++)
                {
                    var r = new Rect(x + 14f, cy + i * 34f, w - 28f, 30f);
                    bool active = i == _highlighted;
                    GUI.DrawTexture(r, active ? _choiceActiveTex : _choiceTex);
                    GUI.Label(new Rect(r.x + 10f, r.y, r.width - 20f, r.height),
                        $"{i + 1}.  {_current.Choices[i].Text}",
                        active ? _choiceActiveStyle : _choiceStyle);
                }
            }
            else
            {
                GUI.Label(new Rect(x, y + panelH + 6f, w, 18f), "E / Click to continue      ESC to close", _hintStyle);
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