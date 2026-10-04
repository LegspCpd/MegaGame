using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using Megame.Gameplay;
using Megame.Client;

namespace Megame.Client
{
    public class UIManager : MonoBehaviour
    {
        [Header("HUD")]
        public GameObject hudRoot;
        public Image healthBar;
        public Image armorBar;
        public Image staminaBar;
        public TextMeshProUGUI ammoText;
        public TextMeshProUGUI moneyText;
        public TextMeshProUGUI wantedText;
        public Image wantedStars;
        public Minimap minimap;
        public WeaponWheel weaponWheel;

        [Header("Subtitles")]
        public GameObject subtitleRoot;
        public TextMeshProUGUI subtitleText;
        public TextMeshProUGUI speakerNameText;
        public SubtitleStyleKind subtitleStyle;

        [Header("Dialogue")]
        public GameObject dialogueRoot;
        public TextMeshProUGUI dialogueText;
        public TextMeshProUGUI dialogueSpeaker;
        public Transform choiceContainer;
        public GameObject choiceButtonPrefab;

        [Header("Mission")]
        public GameObject missionRoot;
        public TextMeshProUGUI missionTitle;
        public TextMeshProUGUI missionObjective;
        public Transform objectiveList;

        [Header("Phone")]
        public GameObject phoneRoot;
        public PhoneUI phoneUI;
        public PhoneManager phoneManager;

        [Header("Vehicle")]
        public GameObject vehicleHUD;
        public Image speedometer;
        public TextMeshProUGUI speedText;
        public Image fuelGauge;
        public Image gearIndicator;

        [Header("Interaction")]
        public GameObject interactionPrompt;
        public TextMeshProUGUI interactionText;

        private GameClient _client;
        private Queue<SubtitleEntry> _subtitleQueue = new Queue<SubtitleEntry>();
        private SubtitleEntry _currentSubtitle;
        private float _subtitleTimer;

        public void Initialize(GameClient client)
        {
            _client = client;
            hudRoot.SetActive(true);
            subtitleRoot.SetActive(false);
            dialogueRoot.SetActive(false);
            missionRoot.SetActive(false);
            phoneRoot.SetActive(false);
            vehicleHUD.SetActive(false);
            interactionPrompt.SetActive(false);
        }

        private void Update()
        {
            UpdateHUD();
            UpdateSubtitles();
        }

        private void UpdateHUD()
        {
            if (_client.LocalPlayerId == 0) return;

            var player = _client.GetEntity<Transform>(_client.LocalPlayerId);
            if (player == null) return;

            // Health/Armor/Stamina from server state
            // Would update from PlayerStateUpdate
        }

        public void SetHealth(float current, float max)
        {
            healthBar.fillAmount = current / max;
        }

        public void SetArmor(float current, float max)
        {
            armorBar.fillAmount = current / max;
            armorBar.gameObject.SetActive(current > 0);
        }

        public void SetStamina(float current, float max)
        {
            staminaBar.fillAmount = current / max;
        }

        public void SetAmmo(int inClip, int reserve)
        {
            ammoText.text = $"{inClip} / {reserve}";
        }

        public void SetMoney(uint money)
        {
            moneyText.text = $"${money:N0}";
        }

        public void SetWantedLevel(int level)
        {
            wantedText.text = $"★ {level}";
            // Update stars visual
        }

        public void SetVehicleHUD(bool active, float speed, float maxSpeed, float fuel, int gear)
        {
            vehicleHUD.SetActive(active);
            if (active)
            {
                speedText.text = $"{Mathf.RoundToInt(speed)} km/h";
                speedometer.fillAmount = speed / maxSpeed;
                fuelGauge.fillAmount = fuel / 100f;
                gearIndicator.fillAmount = gear / 6f;
            }
        }

        public void ShowInteractionPrompt(string text)
        {
            interactionPrompt.SetActive(true);
            interactionText.text = text;
        }

        public void HideInteractionPrompt()
        {
            interactionPrompt.SetActive(false);
        }

        // Subtitles
        public void ShowSubtitle(SubtitleData data)
        {
            _subtitleQueue.Enqueue(new SubtitleEntry
            {
                Text = data.Text,
                Speaker = data.SpeakerName,
                Duration = data.DisplayTime > 0 ? data.DisplayTime : 5f,
                Position = data.Position,
                Style = ConvertSubtitleStyle(data.Style)
            });
        }

        private static SubtitleStyleKind ConvertSubtitleStyle(Megame.Gameplay.SubtitleStyle style)
        {
            if (style == null) return new SubtitleStyleKind();
            return new SubtitleStyleKind
            {
                FontSize = style.FontSize,
                Color = ColorToHex(style.Color),
                OutlineColor = ColorToHex(style.OutlineColor),
                OutlineThickness = style.OutlineThickness,
                Background = style.Background,
                BackgroundColor = ColorToHex(style.BackgroundColor),
                ShowSpeaker = style.SpeakerNameVisible
            };
        }

        private static string ColorToHex(Megame.Common.Color color)
        {
            if (color == null) return "#FFFFFF";
            int r = Mathf.RoundToInt(color.R * 255f);
            int g = Mathf.RoundToInt(color.G * 255f);
            int b = Mathf.RoundToInt(color.B * 255f);
            int a = Mathf.RoundToInt(color.A * 255f);
            return $"#{r:X2}{g:X2}{b:X2}{a:X2}";
        }

        private void UpdateSubtitles()
        {
            if (_currentSubtitle == null && _subtitleQueue.Count > 0)
            {
                _currentSubtitle = _subtitleQueue.Dequeue();
                _subtitleTimer = _currentSubtitle.Duration;
                subtitleRoot.SetActive(true);
                ApplySubtitleStyle(_currentSubtitle);
            }

            if (_currentSubtitle != null)
            {
                _subtitleTimer -= Time.deltaTime;
                if (_subtitleTimer <= 0)
                {
                    _currentSubtitle = null;
                    if (_subtitleQueue.Count == 0)
                    {
                        subtitleRoot.SetActive(false);
                    }
                }
            }
        }

        private void ApplySubtitleStyle(SubtitleEntry entry)
        {
            subtitleText.text = entry.Text;
            speakerNameText.text = entry.Speaker;
            speakerNameText.gameObject.SetActive(entry.Style.ShowSpeaker && !string.IsNullOrEmpty(entry.Speaker));

            subtitleText.fontSize = entry.Style.FontSize;
            subtitleText.color = ColorUtility.TryParseHtmlString(entry.Style.Color, out Color c) ? c : Color.white;

            // Outline
            // Would use TextMeshPro outline settings

            // Background
            // Would enable background image
        }

        // Dialogue
        public void ShowDialogue(string speaker, string text, DialogueManager.DialogueChoiceData[] choices)
        {
            dialogueRoot.SetActive(true);
            dialogueSpeaker.text = speaker;
            dialogueText.text = text;

            // Clear old choices
            foreach (Transform child in choiceContainer)
            {
                Destroy(child.gameObject);
            }

            // Create choice buttons
            foreach (var choice in choices)
            {
                var btn = Instantiate(choiceButtonPrefab, choiceContainer);
                btn.GetComponentInChildren<TextMeshProUGUI>().text = choice.Text;
                int index = System.Array.IndexOf(choices, choice);
                btn.GetComponent<Button>().onClick.AddListener(() => OnDialogueChoiceSelected(index));
            }
        }

        public void HideDialogue()
        {
            dialogueRoot.SetActive(false);
        }

        private void OnDialogueChoiceSelected(int index)
        {
            _client.dialogueManager.SelectChoice(index);
        }

        // Mission
        public void ShowMissionUpdate(string title, string objective)
        {
            missionRoot.SetActive(true);
            missionTitle.text = title;
            missionObjective.text = objective;
        }

        public void HideMission()
        {
            missionRoot.SetActive(false);
        }

        // Phone
        public void TogglePhone()
        {
            bool active = !phoneRoot.activeSelf;
            phoneRoot.SetActive(active);
            if (active)
            {
                phoneUI.RefreshUI();
            }
        }
    }

    public class SubtitleEntry
    {
        public string Text;
        public string Speaker;
        public float Duration;
        public SubtitlePosition Position;
        public SubtitleStyleKind Style;
    }

    [System.Serializable]
    public class SubtitleStyleKind
    {
        public int FontSize = 24;
        public string Color = "#FFFFFF";
        public string OutlineColor = "#000000";
        public float OutlineThickness = 2f;
        public bool Background = true;
        public string BackgroundColor = "#00000080";
        public bool ShowSpeaker = true;
    }
}
