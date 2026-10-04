using UnityEngine;
using System.Collections.Generic;
using Megame.Gameplay;

namespace Megame.Client
{
    public class DialogueManager
    {
        private GameClient _client;
        private DialogueState _currentDialogue;

        public DialogueManager(GameClient client)
        {
            _client = client;
        }

        public void StartDialogue(string dialogueId, ulong npcEntityId)
        {
            _currentDialogue = new DialogueState
            {
                DialogueId = dialogueId,
                NpcEntityId = npcEntityId,
                CurrentNodeId = "start"
            };

            // Request dialogue data from server
            _ = RequestDialogueNode(dialogueId, "start");
        }

        private async System.Threading.Tasks.Task RequestDialogueNode(string dialogueId, string nodeId)
        {
            var response = await _client.SendRPCAsync("GetDialogueNode", new
            {
                dialogueId,
                nodeId
            });

            if (response.Success)
            {
                // Parse node data
                // For now, use placeholder
                ShowDialogueNode(new DialogueNodeData
                {
                    Id = nodeId,
                    SpeakerName = "NPC",
                    Text = "Hello there! What brings you to our city?",
                    Choices = new[]
                    {
                        new DialogueChoiceData { Id = "1", Text = "I'm looking for work.", NextNodeId = "work" },
                        new DialogueChoiceData { Id = "2", Text = "Just passing through.", NextNodeId = "leave" }
                    }
                });
            }
        }

        public void ShowDialogueNode(DialogueNodeData node)
        {
            _currentDialogue.CurrentNodeId = node.Id;

            // Show subtitle if has audio
            if (!string.IsNullOrEmpty(node.AudioClip))
            {
                _client.uiManager.ShowSubtitle(new SubtitleData
                {
                    Text = node.Text,
                    SpeakerName = node.SpeakerName,
                    DisplayTime = node.Duration > 0 ? node.Duration : 5f
                });
            }

            // Show dialogue UI with choices
            if (node.Choices != null && node.Choices.Length > 0)
            {
                _client.uiManager.ShowDialogue(node.SpeakerName, node.Text, node.Choices);
            }
            else if (!string.IsNullOrEmpty(node.NextNodeId))
            {
                // Auto-advance after duration
                _ = AutoAdvance(node.NextNodeId, node.Duration);
            }
        }

        private async System.Threading.Tasks.Task AutoAdvance(string nextNodeId, float delay)
        {
            await System.Threading.Tasks.Task.Delay((int)(delay * 1000));
            await RequestDialogueNode(_currentDialogue.DialogueId, nextNodeId);
        }

        public void SelectChoice(int index)
        {
            if (_currentDialogue == null) return;

            // Would send choice to server
            _ = _client.SendRPCAsync("SelectDialogueChoice", new
            {
                dialogueId = _currentDialogue.DialogueId,
                choiceIndex = index
            });
        }

        public void EndDialogue()
        {
            _currentDialogue = null;
            _client.uiManager.HideDialogue();
        }

        private class DialogueState
        {
            public string DialogueId;
            public ulong NpcEntityId;
            public string CurrentNodeId;
        }

        // Public because ShowDialogueNode takes it as a parameter.
        public class DialogueNodeData
        {
            public string Id;
            public string SpeakerName;
            public string Text;
            public string AudioClip;
            public float Duration;
            public DialogueChoiceData[] Choices;
            public string NextNodeId;
        }

        // Public because DialogueNodeData exposes it publicly.
        public class DialogueChoiceData
        {
            public string Id;
            public string Text;
            public string NextNodeId;
        }
    }
}