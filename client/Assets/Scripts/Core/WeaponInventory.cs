using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Megame.Client
{
    /// <summary>
    /// Inventory of weapons the player owns, with digit-key switching.
    ///
    /// Replaces UI/WeaponWheel.cs, which needed a Canvas, an Image grid and a
    /// slot prefab that the project does not have, and was wired to the
    /// networked WeaponController that is inert in single-player.
    /// </summary>
    public class WeaponInventory : MonoBehaviour
    {
        public class Entry
        {
            public PlayableWeapon Weapon;
            public string Name;
            public Color Tint;
            public int Slot;
        }

        public static WeaponInventory Instance { get; private set; }

        private readonly List<Entry> _entries = new List<Entry>();
        private int _current = -1;
        private GUIStyle _slotStyle;
        private GUIStyle _nameStyle;
        private Texture2D _slotBg;
        private Texture2D _slotBgActive;

        public PlayableWeapon Current =>
            _current >= 0 && _current < _entries.Count ? _entries[_current].Weapon : null;

        public int SlotCount => _entries.Count;

        public static WeaponInventory Create(Camera cam, Transform player)
        {
            var go = new GameObject("WeaponInventory");
            var inv = go.AddComponent<WeaponInventory>();
            inv.Populate(cam, player);
            Instance = inv;
            return inv;
        }

        private void Awake()
        {
            if (Instance == null) Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Populate(Camera cam, Transform player)
        {
            // Three distinct weapons so switching is visibly different: the
            // view model length, fire rate and damage all change.
            var rifle = PlayableWeapon.Create(cam, PlayableWeapon.WeaponKind.Rifle);
            rifle.Damage = 26f;
            rifle.FireRateRpm = 620f;
            rifle.MagazineSize = 30;
            rifle.MaxReserve = 240;
            rifle.ReloadSeconds = 1.9f;
            rifle.RecoilKick = 0.85f;
            rifle.gameObject.SetActive(false);

            var shotgun = PlayableWeapon.Create(cam, PlayableWeapon.WeaponKind.Shotgun);
            shotgun.Damage = 12f;
            shotgun.FireRateRpm = 85f;
            shotgun.MagazineSize = 8;
            shotgun.MaxReserve = 48;
            shotgun.ReloadSeconds = 3.1f;
            shotgun.RecoilKick = 2.6f;
            shotgun.gameObject.SetActive(false);

            var pistol = PlayableWeapon.Create(cam, PlayableWeapon.WeaponKind.Pistol);
            pistol.Damage = 34f;
            pistol.FireRateRpm = 380f;
            pistol.MagazineSize = 15;
            pistol.MaxReserve = 90;
            pistol.ReloadSeconds = 1.3f;
            pistol.RecoilKick = 1.4f;
            pistol.gameObject.SetActive(false);

            _entries.Add(new Entry { Weapon = rifle, Name = "RIFLE", Tint = new Color(0.85f, 0.35f, 0.25f), Slot = 1 });
            _entries.Add(new Entry { Weapon = shotgun, Name = "SHOTGUN", Tint = new Color(0.90f, 0.65f, 0.20f), Slot = 2 });
            _entries.Add(new Entry { Weapon = pistol, Name = "PISTOL", Tint = new Color(0.45f, 0.70f, 0.95f), Slot = 3 });

            if (_entries.Count > 0) Equip(0);
        }

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb == null || _entries.Count == 0) return;

            for (int i = 0; i < _entries.Count && i < 9; i++)
            {
                // Key.Digit1..Digit9 are contiguous, but the cast is required:
                // the enum does not support arithmetic directly.
                if (kb[(Key)((int)Key.Digit1 + i)].wasPressedThisFrame)
                {
                    Equip(i);
                    return;
                }
            }

            // Mouse wheel cycles.
            var mouse = Mouse.current;
            if (mouse == null) return;
            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) < 0.01f) return;
            int dir = scroll > 0f ? 1 : -1;
            Equip((_current + dir + _entries.Count) % _entries.Count);
        }

        public void Equip(int index)
        {
            if (index < 0 || index >= _entries.Count) return;

            if (_current >= 0 && _current < _entries.Count)
                _entries[_current].Weapon.gameObject.SetActive(false);

            _current = index;
            _entries[index].Weapon.gameObject.SetActive(true);
        }

        public void EquipNext() => Equip((_current + 1) % _entries.Count);

        private void EnsureStyles()
        {
            if (_slotStyle != null) return;

            _slotBg = SolidTexture(new Color(0f, 0f, 0f, 0.55f));
            _slotBgActive = SolidTexture(new Color(0.20f, 0.30f, 0.45f, 0.85f));

            _slotStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold
            };
            _slotStyle.normal.textColor = new Color(0.88f, 0.92f, 0.96f);

            _nameStyle = new GUIStyle(_slotStyle) { fontSize = 16 };
        }

        private void OnGUI()
        {
            if (_entries.Count <= 1) return;
            EnsureStyles();

            // Slot strip, bottom centre.
            const float slotSize = 54f;
            const float gap = 6f;
            float total = _entries.Count * slotSize + (_entries.Count - 1) * gap;
            float x = (Screen.width - total) * 0.5f;
            float y = Screen.height - slotSize - 18f;

            for (int i = 0; i < _entries.Count; i++)
            {
                var r = new Rect(x + i * (slotSize + gap), y, slotSize, slotSize);
                GUI.DrawTexture(r, i == _current ? _slotBgActive : _slotBg);

                var col = _entries[i].Tint;
                GUI.color = col;
                GUI.DrawTexture(new Rect(r.x + r.width * 0.5f - 6f, r.y + 8f, 12f, 12f),
                    SolidTextureCached());
                GUI.color = Color.white;

                GUI.Label(new Rect(r.x, r.y + 20f, r.width, 16f), _entries[i].Name, _slotStyle);
                GUI.Label(new Rect(r.x, r.y + 34f, r.width, 16f),
                    $"{_entries[i].Slot}", _slotStyle);

                var weapon = _entries[i].Weapon;
                GUI.Label(new Rect(r.x, r.y + r.height - 16f, r.width, 16f),
                    $"{weapon.AmmoInMag}", _slotStyle);
            }
        }

        private Texture2D _dot;
        private Texture2D SolidTextureCached()
        {
            if (_dot == null) _dot = SolidTexture(Color.white);
            return _dot;
        }

        private static Texture2D SolidTexture(Color c)
        {
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, c);
            tex.Apply();
            tex.hideFlags = HideFlags.HideAndDontSave;
            return tex;
        }
    }
}