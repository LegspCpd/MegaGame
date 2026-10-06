using System;
using UnityEngine;

namespace Megame.Client
{
    /// <summary>
    /// Player vitals for the single-player build: health, armour, damage
    /// sources, regeneration and death.
    ///
    /// The HUD previously drew a hard-coded 100 for health because nothing in
    /// the playable layer owned the concept. This is that missing piece, and it
    /// is what lets the weapon, vehicles and NPCs actually mean something.
    /// </summary>
    public class PlayerVitals : MonoBehaviour
    {
        public static PlayerVitals Instance { get; private set; }

        [Header("Vitals")]
        public float MaxHealth = 100f;
        public float MaxArmor = 100f;
        public float HealthRegenDelay = 6f;
        public float HealthRegenRate = 6f;

        private float _health;
        private float _armor;
        private float _lastDamageTime = -99f;
        private float _cash = 2500f;
        private bool _dead;

        public event Action<float> OnDamaged;      // amount dealt
        public event Action OnDied;           // no payload: death has no argument
        public event Action OnRespawned;

        public float Health => _health;
        public float Armor => _armor;
        public float Health01 => MaxHealth > 0f ? Mathf.Clamp01(_health / MaxHealth) : 0f;
        public float Armor01 => MaxArmor > 0f ? Mathf.Clamp01(_armor / MaxArmor) : 0f;
        public float HealthFraction => MaxHealth > 0f ? _health / MaxHealth : 0f;
        public bool IsDead => _dead;
        public float Cash => _cash;

        public static PlayerVitals Create(GameObject playerObject)
        {
            var existing = playerObject.GetComponent<PlayerVitals>();
            if (existing != null) { Instance = existing; return existing; }

            var vitals = playerObject.AddComponent<PlayerVitals>();
            Instance = vitals;
            return vitals;
        }

        private void Awake()
        {
            if (Instance == null) Instance = this;
            _health = MaxHealth;
            _armor = 0f;
            _dead = false;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (_dead) return;

            // Regenerate only after staying out of trouble for a while.
            if (Time.time - _lastDamageTime > HealthRegenDelay && _health < MaxHealth)
            {
                _health = Mathf.Min(MaxHealth, _health + HealthRegenRate * Time.deltaTime);
            }
        }

        /// <summary>Apply damage. Armour soaks it first, and only a full
        /// health hit kills.</summary>
        public void TakeDamage(float amount, Vector3 source)
        {
            if (_dead || amount <= 0f) return;

            _lastDamageTime = Time.time;

            float remaining = amount;
            if (_armor > 0f)
            {
                float absorbed = Mathf.Min(_armor, remaining);
                _armor -= absorbed;
                remaining -= absorbed;
            }

            if (remaining > 0f)
            {
                _health -= remaining;
            }

            OnDamaged?.Invoke(amount);

            if (_health <= 0f)
            {
                _health = 0f;
                _dead = true;
                OnDied?.Invoke();
            }
        }

        public void AddArmor(float amount)
        {
            _armor = Mathf.Clamp(_armor + Mathf.Max(0f, amount), 0f, MaxArmor);
        }

        public void AddHealth(float amount)
        {
            _health = Mathf.Clamp(_health + Mathf.Max(0f, amount), 0f, MaxHealth);
        }

        public void AddCash(float amount)
        {
            _cash = Mathf.Max(0f, _cash + amount);
        }

        public bool SpendCash(float amount)
        {
            if (_cash < amount) return false;
            _cash -= amount;
            return true;
        }

        /// <summary>Respawn at the given point, restoring health.</summary>
        public void Respawn(Vector3 position)
        {
            _dead = false;
            _health = MaxHealth;
            _armor = 0f;
            _lastDamageTime = -99f;

            var cc = GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            transform.position = position;
            if (cc != null) cc.enabled = true;

            OnRespawned?.Invoke();
        }

        // ------------------------------------------------------------ save data

        public void Capture(out float health, out float armor, out float cash)
        {
            health = _health;
            armor = _armor;
            cash = _cash;
        }

        public void Restore(float health, float armor, float cash)
        {
            _health = Mathf.Clamp(health, 0f, MaxHealth);
            _armor = Mathf.Clamp(armor, 0f, MaxArmor);
            _cash = Mathf.Max(0f, cash);
            _dead = _health <= 0f;
            if (!_dead) _lastDamageTime = -99f;
        }
    }
}