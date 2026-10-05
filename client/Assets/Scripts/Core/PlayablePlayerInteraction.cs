using UnityEngine;
using UnityEngine.InputSystem;

namespace Megame.Client
{
    /// <summary>
    /// Wires the player, their weapon and nearby vehicles together: enter/exit a
    /// car, and hide the view model while driving.
    /// </summary>
    public class PlayablePlayerInteraction : MonoBehaviour
    {
        public float InteractRange = 4.2f;

        private PlayablePlayer _player;
        private PlayableWeapon _weapon;
        private PlayableCameraRig _rig;
        private PlayableVehicle _currentVehicle;
        private PlayableVehicle _nearbyVehicle;

        public string Prompt { get; private set; } = "";

        public static PlayablePlayerInteraction Create(
            PlayablePlayer player, PlayableWeapon weapon, PlayableCameraRig rig)
        {
            var go = new GameObject("Interaction");
            var handler = go.AddComponent<PlayablePlayerInteraction>();
            handler._player = player;
            handler._weapon = weapon;
            handler._rig = rig;
            return handler;
        }

        private void Update()
        {
            if (_player == null) return;

            var kb = Keyboard.current;

            if (_currentVehicle != null)
            {
                Prompt = "[F] Exit vehicle";
                if (kb != null && kb[Key.F].wasPressedThisFrame) ExitVehicle();
                return;
            }

            ScanForVehicle();

            if (_nearbyVehicle != null)
            {
                Prompt = "[F] Enter vehicle";
                if (kb != null && kb[Key.F].wasPressedThisFrame) EnterVehicle(_nearbyVehicle);
            }
            else
            {
                Prompt = "";
            }

            if (kb != null && kb[Key.R].wasPressedThisFrame && _weapon != null)
            {
                _weapon.StartReload();
            }
        }

        private void ScanForVehicle()
        {
            _nearbyVehicle = null;
            if (_player == null) return;

            Vector3 origin = _player.transform.position + Vector3.up * 0.9f;
            float best = InteractRange * InteractRange;

            var colliders = Physics.OverlapSphere(origin, InteractRange);
            foreach (var c in colliders)
            {
                var vehicle = c.GetComponentInParent<PlayableVehicle>();
                if (vehicle == null || vehicle.IsOccupied) continue;

                Vector3 to = vehicle.transform.position - _player.transform.position;
                if (to.sqrMagnitude < best)
                {
                    best = to.sqrMagnitude;
                    _nearbyVehicle = vehicle;
                }
            }
        }

        private void EnterVehicle(PlayableVehicle vehicle)
        {
            _currentVehicle = vehicle;
            vehicle.SetDriving(true);

            _player.enabled = false;
            _player.transform.position = vehicle.transform.position + vehicle.transform.up * 0.4f;

            if (_rig != null) _rig.Follow(vehicle.transform);
            if (_weapon != null) _weapon.gameObject.SetActive(false);
        }

        private void ExitVehicle()
        {
            if (_currentVehicle == null) return;

            Vector3 door = _currentVehicle.transform.position +
                           _currentVehicle.transform.right * 2.0f + Vector3.up * 0.3f;

            _currentVehicle.SetDriving(false);
            _currentVehicle = null;

            _player.enabled = true;
            _player.transform.position = door;

            if (_rig != null) _rig.Follow(_player.transform);
            if (_weapon != null) _weapon.gameObject.SetActive(true);
        }
    }
}