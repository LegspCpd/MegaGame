using UnityEngine;
using UnityEngine.InputSystem;

namespace Megame.Client
{
    /// <summary>
    /// A pedestrian that stands on the pavement and can be talked to.
    ///
    /// Built from primitives so no model is needed. Press F within range to open
    /// the dialogue system on its script.
    /// </summary>
    public class PlayableNpc : MonoBehaviour
    {
        public string DisplayName = "Stranger";
        public string ScriptId = "start";
        public float InteractRange = 3.2f;
        public float WanderRadius = 6f;
        public float WanderSpeed = 1.1f;

        [Header("Vitals")]
        public float MaxHealth = 60f;

        private static readonly System.Random Rng = new System.Random(4242);

        private Vector3 _home;
        private Vector3 _wanderTarget;
        private float _nextDecision;
        private Transform _body;
        private bool _talking;
        private GUIStyle _promptStyle;

        private float _health;
        private bool _dead;

        public bool IsDead => _dead;
        public float Health01 => MaxHealth > 0f ? Mathf.Clamp01(_health / MaxHealth) : 0f;
        public string NpcName => DisplayName;

        /// <summary>Applies damage. Returns true when the shot was fatal.</summary>
        public bool TakeDamage(float amount)
        {
            if (_dead || amount <= 0f) return false;

            _health -= amount;
            if (_health > 0f) return false;

            _health = 0f;
            _dead = true;

            // Collapse rather than vanish, so the body stays as scenery.
            transform.localScale = new Vector3(transform.localScale.x * 1.1f,
                                               transform.localScale.y * 0.35f,
                                               transform.localScale.z * 1.1f);
            return true;
        }

        public static PlayableNpc Create(Vector3 position, string displayName, Color tint,
                                      float maxHealth = 0f)
        {
            var go = new GameObject("NPC_" + displayName);
            go.transform.position = position;

            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(go.transform, false);
            body.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            body.transform.localScale = new Vector3(0.55f, 0.9f, 0.55f);
            // The body keeps a collider: bullets need something to raycast
            // against, and the player should not walk through a pedestrian.
            var bodyCol = body.GetComponent<CapsuleCollider>();
            if (bodyCol != null)
            {
                bodyCol.radius = 0.5f;
                bodyCol.height = 2f;
                bodyCol.center = Vector3.zero;
            }
            body.GetComponent<MeshRenderer>().sharedMaterial =
                PlayableWorld.NewMaterial(tint);

            var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Head";
            head.transform.SetParent(go.transform, false);
            head.transform.localPosition = new Vector3(0f, 1.72f, 0f);
            head.transform.localScale = Vector3.one * 0.42f;
            // Head collider removed to avoid a second hitbox on the same target.
            Object.Destroy(head.GetComponent<Collider>());
            head.GetComponent<MeshRenderer>().sharedMaterial =
                PlayableWorld.NewMaterial(new Color(0.86f, 0.68f, 0.55f));

            // Facing nub so the NPC's heading is readable.
            var nose = GameObject.CreatePrimitive(PrimitiveType.Cube);
            nose.transform.SetParent(go.transform, false);
            nose.transform.localPosition = new Vector3(0f, 1.7f, 0.2f);
            nose.transform.localScale = new Vector3(0.12f, 0.12f, 0.16f);
            Object.Destroy(nose.GetComponent<Collider>());
            nose.GetComponent<MeshRenderer>().sharedMaterial =
                PlayableWorld.NewMaterial(new Color(0.25f, 0.25f, 0.28f));

            var npc = go.AddComponent<PlayableNpc>();
            npc.DisplayName = displayName;
            npc._home = position;
            npc._body = body.transform;
            npc.MaxHealth = maxHealth > 0f ? maxHealth : npc.MaxHealth;
            npc._health = npc.MaxHealth;
            npc.PickWanderTarget();
            return npc;
        }

        private void PickWanderTarget()
        {
            float a = (float)Rng.NextDouble() * Mathf.PI * 2f;
            float r = (float)Rng.NextDouble() * WanderRadius;
            _wanderTarget = _home + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
            _nextDecision = Time.time + 3f + (float)Rng.NextDouble() * 4f;
        }

        private void Update()
        {
            // A downed NPC stops wandering, stops talking and stops being a
            // conversation target; it just lies there.
            if (_dead) return;

            var dialogue = DialogueSystem.Instance;

            if (_talking)
            {
                if (dialogue == null || !dialogue.IsActive) _talking = false;
                var cam = Camera.main;
                if (cam != null) FaceTowards(cam.transform.position, 10f);
                return;
            }

            Wander();

            if (dialogue == null || dialogue.IsActive) return;

            var player = PlayablePlayer.Instance;
            if (player == null) return;

            float dist = Vector3.Distance(player.transform.position, transform.position);
            if (dist > InteractRange) return;

            FaceTowards(player.transform.position, 6f);

            var kb = Keyboard.current;
            if (kb != null && kb[Key.F].wasPressedThisFrame)
            {
                _talking = true;
                dialogue.StartDialogue(ScriptId);
            }
        }

        private void Wander()
        {
            Vector3 to = _wanderTarget - transform.position;
            to.y = 0f;

            if (to.magnitude < 0.6f || Time.time > _nextDecision)
            {
                PickWanderTarget();
                return;
            }

            transform.position += to.normalized * WanderSpeed * Time.deltaTime;
            FaceTowards(transform.position + to.normalized, 4f);
        }

        private void FaceTowards(Vector3 target, float speed)
        {
            if (target == null) return;
            Vector3 dir = target - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.001f) return;
            var look = Quaternion.LookRotation(dir, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, look, speed * Time.deltaTime);
        }

        private void OnGUI()
        {
            if (_talking) return;
            if (DialogueSystem.Instance != null && DialogueSystem.Instance.IsActive) return;

            var player = PlayablePlayer.Instance;
            if (player == null) return;
            if (Vector3.Distance(player.transform.position, transform.position) > InteractRange) return;

            if (_promptStyle == null)
            {
                _promptStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 15,
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold
                };
                _promptStyle.normal.textColor = new Color(1f, 0.9f, 0.45f);
            }

            var vp = Camera.main != null
                ? (Vector2)Camera.main.WorldToScreenPoint(
                    transform.position + Vector3.up * 2.15f)
                : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

            GUI.Label(new Rect(vp.x - 110f, vp.y - 12f, 220f, 22f),
                $"[F]  Talk to {DisplayName}", _promptStyle);
        }
    }
}