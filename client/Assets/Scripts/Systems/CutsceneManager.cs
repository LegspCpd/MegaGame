using UnityEngine;
using System.Collections.Generic;
using Megame.Gameplay;

namespace Megame.Client
{
    public class CutsceneManager
    {
        private GameClient _client;
        private CutscenePlayer _currentCutscene;
        private bool _isPlaying;

        public CutsceneManager(GameClient client)
        {
            _client = client;
        }

        public async System.Threading.Tasks.Task PlayCutscene(string cutsceneId)
        {
            // Request cutscene data from server
            var response = await _client.SendRPCAsync("GetCutscene", new { cutsceneId });

            if (response.Success)
            {
                // Parse cutscene data and play
                // For now, create a simple cutscene player
                _currentCutscene = new GameObject("CutscenePlayer").AddComponent<CutscenePlayer>();
                _currentCutscene.Initialize(cutsceneId);
                _isPlaying = true;

                // Disable player control
                _client.inputManager.enabled = false;
                _client.cameraController.enabled = false;

                await _currentCutscene.PlayAsync();

                // Re-enable player control
                _client.inputManager.enabled = true;
                _client.cameraController.enabled = true;

                _isPlaying = false;
                Destroy(_currentCutscene.gameObject);
                _currentCutscene = null;

                // Notify server
                await _client.SendRPCAsync("CutsceneFinished", new { cutsceneId });
            }
        }

        public void SkipCutscene()
        {
            if (_currentCutscene != null && _isPlaying)
            {
                _currentCutscene.Skip();
            }
        }

        public bool IsPlaying => _isPlaying;
    }

    public class CutscenePlayer : MonoBehaviour
    {
        private CutsceneData _data;
        private float _currentTime;
        private Camera _camera;
        private Dictionary<string, Transform> _actors = new Dictionary<string, Transform>();

        public void Initialize(string cutsceneId)
        {
            _data = LoadCutsceneData(cutsceneId);
            _camera = Camera.main;
        }

        public async System.Threading.Tasks.Task PlayAsync()
        {
            _currentTime = 0f;

            while (_currentTime < _data.Duration)
            {
                _currentTime += Time.deltaTime;
                UpdateTracks(_currentTime);
                await System.Threading.Tasks.Task.Yield();
            }
        }

        private void UpdateTracks(float time)
        {
            foreach (var track in _data.Tracks)
            {
                var keyframe = GetKeyframeAtTime(track, time);
                if (keyframe == null) continue;

                ApplyKeyframe(track, keyframe);
            }
        }

        private CutsceneKeyframeData GetKeyframeAtTime(CutsceneTrackData track, float time)
        {
            CutsceneKeyframeData prev = null;
            foreach (var kf in track.Keyframes)
            {
                if (kf.Time > time) break;
                prev = kf;
            }
            return prev;
        }

        private void ApplyKeyframe(CutsceneTrackData track, CutsceneKeyframeData keyframe)
        {
            switch (track.Type)
            {
                case CutsceneTrackType.Camera:
                    ApplyCameraKeyframe(keyframe);
                    break;
                case CutsceneTrackType.EntityTransform:
                    ApplyEntityTransform(keyframe);
                    break;
                case CutsceneTrackType.EntityAnimation:
                    ApplyEntityAnimation(keyframe);
                    break;
                case CutsceneTrackType.Audio:
                    ApplyAudio(keyframe);
                    break;
                case CutsceneTrackType.Subtitle:
                    ApplySubtitle(keyframe);
                    break;
            }
        }

        private void ApplyCameraKeyframe(CutsceneKeyframeData kf)
        {
            // Apply camera position/rotation/FOV
        }

        private void ApplyEntityTransform(CutsceneKeyframeData kf)
        {
            if (_actors.TryGetValue(kf.TargetId, out var actor))
            {
                // Apply transform
            }
        }

        private void ApplyEntityAnimation(CutsceneKeyframeData kf)
        {
            if (_actors.TryGetValue(kf.TargetId, out var actor))
            {
                var animator = actor.GetComponent<Animator>();
                if (animator != null)
                {
                    animator.Play(kf.AnimationName);
                }
            }
        }

        private void ApplyAudio(CutsceneKeyframeData kf)
        {
            // Play audio clip
        }

        private void ApplySubtitle(CutsceneKeyframeData kf)
        {
            GameClient.Instance.uiManager.ShowSubtitle(new SubtitleData
            {
                Text = kf.SubtitleText,
                SpeakerName = kf.SpeakerName,
                DisplayTime = kf.Duration
            });
        }

        public void Skip()
        {
            _currentTime = _data.Duration;
        }

        private CutsceneData LoadCutsceneData(string id)
        {
            // Would load from Resources or Addressables
            return new CutsceneData
            {
                Id = id,
                Duration = 10f,
                Tracks = new List<CutsceneTrackData>()
            };
        }
    }

    public class CutsceneData
    {
        public string Id;
        public float Duration;
        public List<CutsceneTrackData> Tracks;
    }

    public class CutsceneTrackData
    {
        public CutsceneTrackType Type;
        public string TargetId;
        public List<CutsceneKeyframeData> Keyframes;
    }

    public enum CutsceneTrackType
    {
        Camera,
        EntityTransform,
        EntityAnimation,
        Audio,
        VisualEffect,
        TimeOfDay,
        Weather,
        PostProcess,
        Subtitle,
        Script
    }

    public class CutsceneKeyframeData
    {
        public float Time;
        public EasingKind Easing;
        public Vector3 Position;
        public Quaternion Rotation;
        public float FOV;
        public string AnimationName;
        public string AudioClip;
        public string SubtitleText;
        public string SpeakerName;
        public float Duration;
    }

    public enum EasingKind
    {
        Linear,
        InQuad,
        OutQuad,
        InOutQuad,
        InCubic,
        OutCubic,
        InOutCubic
    }
}