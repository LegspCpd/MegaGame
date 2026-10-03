using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

namespace Megame.Client
{
    public class Minimap : MonoBehaviour
    {
        [Header("References")]
        public RawImage mapImage;
        public Transform playerIcon;
        public RectTransform maskRect;
        public float mapScale = 1000f; // World units per texture pixel

        [Header("Blips")]
        public GameObject blipPrefab;
        public Sprite[] blipSprites;
        public Color[] blipColors;

        private Texture2D _mapTexture;
        private Dictionary<ulong, RectTransform> _blips = new Dictionary<ulong, RectTransform>();
        private Transform _playerTransform;

        public void Initialize(Transform playerTransform, Texture2D mapTexture)
        {
            _playerTransform = playerTransform;
            _mapTexture = mapTexture;
            mapImage.texture = _mapTexture;
        }

        private void LateUpdate()
        {
            if (_playerTransform == null) return;

            UpdatePlayerIcon();
            UpdateBlips();
        }

        private void UpdatePlayerIcon()
        {
            // Rotate map to match player heading (or keep north-up)
            float playerYaw = _playerTransform.eulerAngles.y;
            mapImage.rectTransform.localRotation = Quaternion.Euler(0, 0, -playerYaw);

            // Player icon stays centered
            playerIcon.localPosition = Vector3.zero;
            playerIcon.localRotation = Quaternion.Euler(0, 0, playerYaw);
        }

        private void UpdateBlips()
        {
            // Would update blip positions based on entity positions
            // For now, placeholder
        }

        public void AddBlip(ulong entityId, Vector3 worldPos, int spriteIndex, int colorIndex, string label)
        {
            var blip = Instantiate(blipPrefab, maskRect);
            blip.GetComponent<Image>().sprite = blipSprites[spriteIndex];
            blip.GetComponent<Image>().color = blipColors[colorIndex];
            blip.GetComponentInChildren<Text>().text = label;

            _blips[entityId] = blip.GetComponent<RectTransform>();
            UpdateBlipPosition(entityId, worldPos);
        }

        public void RemoveBlip(ulong entityId)
        {
            if (_blips.TryGetValue(entityId, out var blip))
            {
                Destroy(blip.gameObject);
                _blips.Remove(entityId);
            }
        }

        public void UpdateBlipPosition(ulong entityId, Vector3 worldPos)
        {
            if (!_blips.TryGetValue(entityId, out var blip)) return;

            if (_playerTransform == null) return;

            Vector3 relative = worldPos - _playerTransform.position;
            Vector2 mapPos = new Vector2(relative.x, relative.z) / mapScale;

            // Rotate by player yaw
            float yaw = _playerTransform.eulerAngles.y * Mathf.Deg2Rad;
            float cos = Mathf.Cos(yaw);
            float sin = Mathf.Sin(yaw);
            float rotatedX = mapPos.x * cos - mapPos.y * sin;
            float rotatedY = mapPos.x * sin + mapPos.y * cos;

            blip.anchoredPosition = new Vector2(rotatedX, rotatedY) * 100f; // UI scale

            // Hide if outside mask
            bool visible = Mathf.Abs(rotatedX) < maskRect.rect.width / 200f &&
                          Mathf.Abs(rotatedY) < maskRect.rect.height / 200f;
            blip.gameObject.SetActive(visible);
        }

        public void SetMapTexture(Texture2D texture)
        {
            _mapTexture = texture;
            mapImage.texture = texture;
        }
    }
}