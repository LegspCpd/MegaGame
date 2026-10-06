using UnityEngine;

namespace Megame.Client
{
    /// <summary>
    /// A hinged door that swings when the player pushes it.
    ///
    /// Placed in the doorway left by PlayableWorld.BuildInterior. Swing is
    /// driven by proximity and the player's velocity, so walking through
    /// shoves it open rather than requiring an interaction.
    /// </summary>
    public class SwingingDoor : MonoBehaviour
    {
        public float OpenAngle = 82f;
        public float OpenSpeed = 320f;      // degrees per second
        public float CloseDelay = 1.1f;
        public float PushRadius = 2.1f;

        private Transform _pivot;
        private float _targetAngle;
        private float _closedAt;

        /// <summary>True once the leaf has swung clear of the jamb.</summary>
        public bool IsOpen => _pivot != null && Mathf.Abs(Mathf.DeltaAngle(0f, _pivot.localEulerAngles.y)) > 4f;

        /// <summary>
        /// Builds a door in the gap at <paramref name="centre"/>, hinged on
        /// <paramref name="hingeSide"/>.
        /// </summary>
        public static SwingingDoor Create(Transform parent, string name, Vector3 localCentre,
                                          float width, float height, Vector3 hingeSide,
                                          bool alongX)
        {
            // The pivot carries the rotation; the slab hangs off it so the
            // geometry never has to be re-parented mid-swing.
            var pivot = new GameObject(name + "Pivot");
            pivot.transform.SetParent(parent, false);
            pivot.transform.localPosition = localCentre + hingeSide * (width * 0.5f);

            var door = pivot.gameObject.AddComponent<SwingingDoor>();

            var slab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            slab.name = "Slab";
            slab.transform.SetParent(pivot.transform, false);
            // Swing about the hinge, so the leaf sits half a width back along
            // whichever axis the hinge is on. Using hingeSide.x only worked for
            // Z-facing walls; doors in X-facing walls need the Z component.
            slab.transform.localPosition = -hingeSide * (width * 0.5f);
            slab.transform.localScale = alongX
                ? new Vector3(width, height, 0.12f)
                : new Vector3(0.12f, height, width);
            door.BuildVisual(slab);

            door._pivot = pivot.transform;

            return door;
        }

        private void BuildVisual(GameObject slab)
        {
            var mat = PlayableWorld.NewSharedMaterial(new Color(0.34f, 0.24f, 0.15f));
            slab.GetComponent<MeshRenderer>().sharedMaterial = mat;

            // A handle, on the swinging side so it reads as a door.
            var handle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            handle.name = "Handle";
            handle.transform.SetParent(slab.transform, false);
            handle.transform.localPosition = new Vector3(slab.transform.localScale.x * 0.32f, 0f, 0.1f);
            handle.transform.localScale = new Vector3(0.08f, 0.08f, 0.12f);
            Destroy(handle.GetComponent<Collider>());
            handle.GetComponent<MeshRenderer>().sharedMaterial =
                PlayableWorld.NewSharedMaterial(new Color(0.70f, 0.68f, 0.55f));
        }

        private void Update()
        {
            if (_pivot == null) return;

            var player = PlayablePlayer.Instance;
            bool pushing = false;

            if (player != null)
            {
                Vector3 toPlayer = player.transform.position - transform.position;
                toPlayer.y = 0f;

                if (toPlayer.magnitude < PushRadius)
                {
                    // Only count the player as pushing when they are actually
                    // walking into the door rather than standing beside it.
                    float approach = Vector3.Dot(toPlayer.normalized, transform.forward);
                    float speed = player.Velocity.magnitude;
                    if (speed > 1.2f && Mathf.Abs(approach) > 0.15f) pushing = true;
                }
            }

            if (pushing)
            {
                float sign = Mathf.Sign(Vector3.Dot(
                    (player != null ? player.transform.position : transform.position) - transform.position,
                    transform.right));
                _targetAngle = Mathf.Clamp(-sign * OpenAngle, -OpenAngle, OpenAngle);
                _closedAt = Time.time + CloseDelay;
            }
            else if (Time.time > _closedAt)
            {
                _targetAngle = 0f;
            }

            Vector3 euler = _pivot.localEulerAngles;
            float y = Mathf.MoveTowardsAngle(euler.y, _targetAngle, OpenSpeed * Time.deltaTime);
            _pivot.localRotation = Quaternion.Euler(0f, y, 0f);
        }
    }
}