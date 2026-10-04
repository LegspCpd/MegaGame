using UnityEngine;
using System.Collections.Generic;
using Megame.Gameplay;

namespace Megame.Client
{
    public class MissionManager
    {
        private GameClient _client;
        private Dictionary<string, MissionProgress> _activeMissions = new Dictionary<string, MissionProgress>();
        private List<string> _availableMissions = new List<string>();
        private List<string> _completedMissions = new List<string>();
        private string _currentMissionId;

        public MissionManager(GameClient client)
        {
            _client = client;
        }

        public void OnMissionTriggered(string missionId)
        {
            _currentMissionId = missionId;
            _activeMissions[missionId] = new MissionProgress { MissionId = missionId };
            _availableMissions.Remove(missionId);

            // Show mission start UI
            _client.uiManager.ShowMissionUpdate("Mission Started", "Check objectives");
        }

        public void OnMissionUpdate(string missionId, ObjectiveProgress[] objectives)
        {
            if (!_activeMissions.TryGetValue(missionId, out var progress)) return;

            progress.Objectives = new Dictionary<string, ObjectiveProgress>();
            foreach (var obj in objectives)
            {
                progress.Objectives[obj.ObjectiveId] = obj;

                // Check for completion
                if (obj.Status == MissionObjectiveStatus.Completed)
                {
                    OnObjectiveCompleted(missionId, obj);
                }
            }

            // Update UI
            UpdateMissionUI(missionId);
        }

        private void OnObjectiveCompleted(string missionId, ObjectiveProgress obj)
        {
            // Show notification
            Debug.Log($"Objective completed: {obj.ObjectiveId}");

            // Check if all objectives done
            var progress = _activeMissions[missionId];
            bool allDone = true;
            foreach (var o in progress.Objectives.Values)
            {
                if (o.Status != MissionObjectiveStatus.Completed && !o.IsOptional)
                {
                    allDone = false;
                    break;
                }
            }

            if (allDone)
            {
                OnMissionCompleted(missionId);
            }
        }

        private void OnMissionCompleted(string missionId)
        {
            _activeMissions.Remove(missionId);
            _completedMissions.Add(missionId);

            _client.uiManager.HideMission();
            // Show completion UI, give rewards
        }

        public void OnMissionFailed(string missionId)
        {
            _activeMissions.Remove(missionId);
            _client.uiManager.HideMission();
        }

        public void CompleteMission(string missionId)
        {
            if (_activeMissions.ContainsKey(missionId))
            {
                OnMissionCompleted(missionId);
            }
        }

        public void OnMissionsAvailable(List<string> missionIds)
        {
            _availableMissions = missionIds;
        }

        private void UpdateMissionUI(string missionId)
        {
            if (!_activeMissions.TryGetValue(missionId, out var progress)) return;

            // Find current active objective
            foreach (var obj in progress.Objectives.Values)
            {
                if (obj.Status == MissionObjectiveStatus.Active)
                {
                    _client.uiManager.ShowMissionUpdate(missionId, GetObjectiveDescription(obj));
                    break;
                }
            }
        }

        private string GetObjectiveDescription(ObjectiveProgress obj)
        {
            string desc = obj.ObjectiveId; // Would lookup from definition
            if (obj.Target > 0)
            {
                desc += $" ({obj.Current}/{obj.Target})";
            }
            return desc;
        }

        public IReadOnlyList<string> AvailableMissions => _availableMissions;
        public IReadOnlyList<string> CompletedMissions => _completedMissions;
        public string CurrentMission => _currentMissionId;
    }

    public class MissionProgress
    {
        public string MissionId;
        public Dictionary<string, ObjectiveProgress> Objectives = new Dictionary<string, ObjectiveProgress>();
        public int CurrentPhase;
    }
}