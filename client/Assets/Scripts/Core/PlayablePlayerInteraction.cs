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
        private WeaponInventory _inventory;
        private PlayableCameraRig _rig;
        private PlayableVehicle _currentVehicle;
        private PlayableVehicle _nearbyVehicle;

        public string Prompt { get; private set; } = "";

        public static PlayablePlayerInteraction Create(
            PlayablePlayer player, WeaponInventory inventory, PlayableCameraRig rig)
        {
            var go = new GameObject("Interaction");
            var handler = go.AddComponent<PlayablePlayerInteraction>();
            handler._player = player;
            handler._inventory = inventory;
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

            var weapon = _inventory != null ? _inventory.Current : null;
            if (kb != null && kb[Key.R].wasPressedThisFrame && weapon != null)
            {
                weapon.StartReload();
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
            _player.ResetVelocity();

            if (_rig != null) _rig.Follow(vehicle.transform);
            if (_inventory != null && _inventory.Current != null)
                _inventory.Current.gameObject.SetActive(false);
        }

        private void ExitVehicle()
        {
            if (_currentVehicle == null) return;

            _currentVehicle.SetDriving(false);
            _currentVehicle = null;

            // Try both sides, then further out, then finally straight above.
            // Teleporting into a wall or into the car itself would leave the
            // player stuck inside geometry.
            Vector3 spot = FindExitSpot();
            _player.enabled = true;
            _player.transform.position = spot;
            _player.ResetVelocity();

            if (_rig != null) _rig.Follow(_player.transform);
            if (_inventory != null && _inventory.Current != null)
                _inventory.Current.gameObject.SetActive(true);
        }

        private Vector3 FindExitSpot()
        {
            Transform car = _currentVehicle != null ? _currentVehicle.transform : null;
            if (car == null) return _player.transform.position;

            Vector3 origin = car.position + Vector3.up * 0.8f;
            float[] offsets = { 2.2f, 3.2f, 4.4f };

            foreach (float dist in offsets)
            {
                for (int side = 0; side < 4; side++)
                {
                    Vector3 dir = side switch
                    {
                        0 => car.right,
                        1 => -car.right,
                        2 => car.forward,
                        _ => -car.forward,
                    };
                    Vector3 candidate = car.position + dir * dist + Vector3.up * 0.6f;

                    // Reject spots inside geometry.
                    if (Physics.CheckSphere(candidate, 0.45f, ~0,
                            QueryTriggerInteraction.Ignore)) continue;

                    // Prefer a spot with ground under it.
                    if (Physics.Raycast(candidate + Vector3.up * 3f, Vector3.down,
                            out RaycastHit hit, 8f))
                    {
                        return hit.point + Vector3.up * 0.05f;
                    }
                    return candidate;
                }
            }

            // Nothing clear: drop the player on top of the car.
            return car.position + Vector3.up * 2.2f;
        }
    }
}