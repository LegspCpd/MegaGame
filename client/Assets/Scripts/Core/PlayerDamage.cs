using UnityEngine;

namespace Megame.Client
{
    /// <summary>
    /// Applies the consequences that nothing else owns: damage from vehicle
    /// impacts and falls, and what happens when the player runs out of health.
    ///
    /// PlayerVitals.TakeDamage had no callers, so the player could not be hurt
    /// and OnDied was never observed. This wires both up.
    /// </summary>
    public class PlayerDamage : MonoBehaviour
    {
        public static PlayerDamage Instance { get; private set; }

        [Header("Falls")]
        public float SafeFallSpeed = 18f;     // above this, start taking damage
        public float FallDamageScale = 9f;

        [Header("Vehicles")]
        public float VehicleDamageSpeed = 12f;  // impact speed that hurts
        public float VehicleDamageScale = 6f;

        [Header("Hostiles")]
        public float ContactDamage = 18f;

        private PlayerVitals _vitals;
        private PlayablePlayer _player;

        private float _fallStartY;
        private bool _falling;

        private float _hitFlashEndsAt;
        private GUIStyle _deathStyle;
        private Texture2D _flashTex;

        public static PlayerDamage Create(GameObject playerObject, PlayerVitals vitals)
        {
            var existing = playerObject.GetComponent<PlayerDamage>();
            if (existing != null) { Instance = existing; return existing; }

            var d = playerObject.AddComponent<PlayerDamage>();
            d._vitals = vitals;
            Instance = d;
            return d;
        }

        private void Awake()
        {
            if (Instance == null) Instance = this;
            _player = GetComponent<PlayablePlayer>();

            if (_vitals == null) _vitals = GetComponent<PlayerVitals>();

            if (_vitals != null)
            {
                _vitals.OnDamaged += HandleDamaged;
                _vitals.OnDied += HandleDeath;
            }
        }

        private void OnDestroy()
        {
            if (_vitals != null)
            {
                _vitals.OnDamaged -= HandleDamaged;
                _vitals.OnDied -= HandleDeath;
            }
            if (Instance == this) Instance = null;
        }

        private void HandleDamaged(float amount)
        {
            // Drives the red vignette: Time.time minus this is the fade.
            _hitFlashEndsAt = Time.time + 0.45f;
        }

        private void Update()
        {
            if (_player == null || _vitals == null) return;

            if (_vitals.IsDead)
            {
                // Wait for a moment, then put the player back on their feet.
                if (Time.time > _respawnAt) Respawn();
                return;
            }

            TrackFalls();
        }

        // ------------------------------------------------------------- falls

        private void TrackFalls()
        {
            if (_player.IsGrounded)
            {
                if (_falling)
                {
                    _falling = false;
                    ResolveLanding();
                }
                _fallStartY = _player.transform.position.y;
                return;
            }

            if (!_falling)
            {
                _falling = true;
                _fallStartY = _player.transform.position.y;
            }
        }

        private void ResolveLanding()
        {
            float drop = _fallStartY - _player.transform.position.y;
            if (drop <= 0f) return;

            // Terminal velocity of a free fall from that height.
            float impact = Mathf.Sqrt(2f * Mathf.Abs(9.81f) * drop);
            if (impact <= SafeFallSpeed) return;

            float damage = (impact - SafeFallSpeed) * FallDamageScale;
            _vitals.TakeDamage(damage, _player.transform.position);
        }

        // ------------------------------------------------------------ vehicles

        /// <summary>Call from a vehicle when it strikes something.</summary>
        public void OnVehicleImpact(float impactSpeed, Vector3 point)
        {
            if (_vitals == null || _vitals.IsDead) return;
            if (impactSpeed < VehicleDamageSpeed) return;

            float damage = (impactSpeed - VehicleDamageSpeed) * VehicleDamageScale;
            _vitals.TakeDamage(damage, point);
        }

        /// <summary>Call when a hostile pedestrian reaches the player.</summary>
        public void OnHostileContact(Vector3 point)
        {
            if (_vitals == null || _vitals.IsDead) return;
            _vitals.TakeDamage(ContactDamage, point);
        }

        /// <summary>External damage entry point (punches, scripted events).</summary>
        public void Hurt(float amount, Vector3 point)
        {
            if (_vitals == null) return;
            _vitals.TakeDamage(amount, point);
        }

        // -------------------------------------------------------------- death

        private float _respawnAt;

        private void HandleDeath()
        {
            _respawnAt = Time.time + 2.5f;
        }

        private void Respawn()
        {
            _vitals.Respawn(PlayableWorld.SpawnPoint);
            _falling = false;
            _fallStartY = PlayableWorld.SpawnPoint.y;
        }

        // --------------------------------------------------------------- draw

        private void OnGUI()
        {
            if (_vitals == null) return;

            if (_flashTex == null)
            {
                _flashTex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                _flashTex.SetPixel(0, 0, Color.white);
                _flashTex.Apply();
                _flashTex.hideFlags = HideFlags.HideAndDontSave;
            }

            // Red vignette right after taking damage.
            float since = Time.time - _hitFlashEndsAt;
            if (since < 0.45f && !_vitals.IsDead)
            {
                float a = Mathf.Lerp(0.28f, 0f, since / 0.45f);
                var prev = GUI.color;
                GUI.color = new Color(0.85f, 0.1f, 0.1f, a);
                GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), _flashTex);
                GUI.color = prev;
            }

            if (!_vitals.IsDead) return;

            if (_deathStyle == null)
            {
                _deathStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 46,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
                _deathStyle.normal.textColor = new Color(0.85f, 0.15f, 0.15f);
            }

            float remaining = Mathf.Max(0f, _respawnAt - Time.time);
            GUI.Label(new Rect(0f, Screen.height * 0.34f, Screen.width, 60f), "YOU DIED", _deathStyle);
            GUI.Label(new Rect(0f, Screen.height * 0.34f + 62f, Screen.width, 28f),
                $"Respawning in {remaining:0.0}s", _deathStyle);
        }
    }
}