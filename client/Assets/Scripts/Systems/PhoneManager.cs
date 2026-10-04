using UnityEngine;
using System.Collections.Generic;
using Megame.Gameplay;
using UnityEngine.UI;
using TMPro;
using Megame.Controllers;
using Megame.Data;
using Megame.Client;
using Megame.Vehicles;

namespace Megame.Client
{
    public class PhoneManager
    {
        private GameClient _client;
        private PhoneState _state;

        public PhoneManager(GameClient client)
        {
            _client = client;
            _state = new PhoneState();
        }

        public void OpenPhone()
        {
            _client.uiManager.TogglePhone();
            RefreshUI();
        }

        public void ClosePhone()
        {
            _client.uiManager.TogglePhone();
        }

        public void OnMessageReceived(PhoneMessage message)
        {
            _state.Messages.Add(message);
            // Show notification
            if (_client.uiManager.phoneRoot.activeSelf)
            {
                RefreshUI();
            }
        }

        public void OnContactAdded(PhoneContact contact)
        {
            _state.Contacts.Add(contact);
        }

        public void SendMessage(string contactId, string text)
        {
            var message = new PhoneMessage
            {
                ContactId = contactId,
                Text = text,
                FromPlayer = true,
                Timestamp = (ulong)System.DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Type = MessageType.Text
            };

            _state.Messages.Add(message);
            _ = _client.SendRPCAsync("SendPhoneMessage", message);
        }

        public void RefreshUI()
        {
            _client.uiManager.phoneUI.SetContacts(_state.Contacts);
            _client.uiManager.phoneUI.SetMessages(_state.Messages);
        }

        private class PhoneState
        {
            public List<PhoneContact> Contacts = new List<PhoneContact>();
            public List<PhoneMessage> Messages = new List<PhoneMessage>();
        }
    }

    // Phone UI component
    public class PhoneUI : MonoBehaviour
    {
        public Transform contactList;
        public GameObject contactPrefab;
        public Transform messageList;
        public GameObject messagePrefab;
        public TMP_InputField messageInput;
        public Button sendButton;

        private string _selectedContactId;

        public void SetContacts(List<PhoneContact> contacts)
        {
            foreach (Transform child in contactList)
            {
                Destroy(child.gameObject);
            }

            foreach (var contact in contacts)
            {
                var go = Instantiate(contactPrefab, contactList);
                go.GetComponentInChildren<TextMeshProUGUI>().text = contact.Name;
                go.GetComponent<Button>().onClick.AddListener(() => SelectContact(contact.Id));
            }
        }

        public void SetMessages(List<PhoneMessage> messages)
        {
            foreach (Transform child in messageList)
            {
                Destroy(child.gameObject);
            }

            foreach (var msg in messages)
            {
                var go = Instantiate(messagePrefab, messageList);
                var texts = go.GetComponentsInChildren<TextMeshProUGUI>();
                texts[0].text = msg.FromPlayer ? "You" : msg.ContactId; // Would resolve contact name
                texts[1].text = msg.Text;
            }
        }

        public void SelectContact(string contactId)
        {
            _selectedContactId = contactId;
            // Filter messages
        }

        public void OnSendClicked()
        {
            if (string.IsNullOrEmpty(_selectedContactId) || string.IsNullOrEmpty(messageInput.text)) return;
            
            GameClient.Instance.phoneManager.SendMessage(_selectedContactId, messageInput.text);
            messageInput.text = "";
        }
    }
}
