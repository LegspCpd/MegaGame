using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Megame.Client
{
    public class WeaponWheel : MonoBehaviour
    {
        [Header("UI")]
        public GameObject wheelRoot;
        public Transform slotContainer;
        public GameObject slotPrefab;
        public TextMeshProUGUI weaponNameText;
        public TextMeshProUGUI ammoText;
        public Image weaponIcon;

        private WeaponSlotUI[] _slots = new WeaponSlotUI[8];
        private int _selectedSlot = -1;
        private bool _isOpen = false;

        private void Awake()
        {
            wheelRoot.SetActive(false);
            InitializeSlots();
        }

        private void InitializeSlots()
        {
            for (int i = 0; i < 8; i++)
            {
                var go = Instantiate(slotPrefab, slotContainer);
                var slot = go.GetComponent<WeaponSlotUI>();
                if (slot == null) slot = go.AddComponent<WeaponSlotUI>();
                slot.Index = i;
                _slots[i] = slot;
            }
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                ToggleWheel();
            }

            if (_isOpen)
            {
                HandleWheelInput();
            }
        }

        public void ToggleWheel()
        {
            _isOpen = !_isOpen;
            wheelRoot.SetActive(_isOpen);

            if (_isOpen)
            {
                RefreshWheel();
                Time.timeScale = 0.1f; // Slow motion
            }
            else
            {
                Time.timeScale = 1f;
                if (_selectedSlot >= 0)
                {
                    GameClient.Instance.weaponController.SwitchWeapon(_selectedSlot);
                }
            }
        }

        private void HandleWheelInput()
        {
            Vector2 mousePos = Input.mousePosition;
            Vector2 center = new Vector2(Screen.width / 2f, Screen.height / 2f);
            Vector2 dir = (mousePos - center).normalized;
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            if (angle < 0) angle += 360f;

            int slot = Mathf.FloorToInt((angle + 22.5f) / 45f) % 8;
            HighlightSlot(slot);
        }

        private void HighlightSlot(int slot)
        {
            for (int i = 0; i < 8; i++)
            {
                _slots[i].SetHighlighted(i == slot);
            }
            _selectedSlot = slot;

            // Update preview
            // Would show weapon info for selected slot
        }

        public void RefreshWheel()
        {
            // Would populate from player inventory
            // For now, placeholder
            for (int i = 0; i < 8; i++)
            {
                _slots[i].SetEmpty();
            }
        }
    }

    public class WeaponSlotUI : MonoBehaviour
    {
        public int Index;
        public Image icon;
        public Image highlight;
        public TextMeshProUGUI ammoText;

        public void SetWeapon(Sprite weaponIcon, int ammo, int reserve)
        {
            icon.sprite = weaponIcon;
            icon.enabled = true;
            ammoText.text = $"{ammo}/{reserve}";
        }

        public void SetEmpty()
        {
            icon.enabled = false;
            ammoText.text = "";
        }

        public void SetHighlighted(bool highlighted)
        {
            highlight.enabled = highlighted;
        }
    }
}