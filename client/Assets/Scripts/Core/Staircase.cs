using UnityEngine;

namespace Megame.Client
{
    /// <summary>
    /// Stairs and an upper floor for enterable buildings.
    ///
    /// The CharacterController cannot climb a ramp built from a tilted box --
    /// stepOffset only steps over small lips -- so the flight is a real
    /// staircase: alternating treads and risers, which the controller's step
    /// offset handles one at a time.
    /// </summary>
    public class Staircase : MonoBehaviour
    {

        /// <summary>
        /// Builds a flight from <paramref name="from"/> up to
        /// <paramref name="to"/> (they must share the X or Z axis so the run is
        /// straight). Returns the landing point at the top.
        /// </summary>
        public static Vector3 Build(Transform parent, Vector3 from, Vector3 to, float width)
        {
            var horizontal = new Vector3(to.x - from.x, 0f, to.z - from.z);
            float run = horizontal.magnitude;
            float rise = to.y - from.y;

            // Too shallow to be a staircase, too steep to climb: skip it.
            if (run < 1.6f || rise < 1.6f || rise / Mathf.Max(run, 0.01f) > 1.1f)
                return to;

            bool alongX = Mathf.Abs(horizontal.x) >= Mathf.Abs(horizontal.z);
            Vector3 dir = horizontal.normalized;
            Vector3 side = alongX ? new Vector3(0f, 0f, 1f) : new Vector3(1f, 0f, 0f);

            int steps = Mathf.Clamp(Mathf.RoundToInt(rise / 0.32f), 4, 40);
            float stepRise = rise / steps;
            float stepRun = run / steps;

            var treads = new MeshBuilder();
            var risers = new MeshBuilder();
            var mat = PlayableWorld.NewSharedMaterial(new Color(0.48f, 0.44f, 0.40f));

            for (int i = 0; i < steps; i++)
            {
                Vector3 treadCentre = from
                    + dir * (stepRun * (i + 0.5f))
                    + Vector3.up * (stepRise * (i + 1f));

                // Tread slab.
                treads.AddBox(treadCentre - Vector3.up * (stepRise * 0.5f),
                    alongX ? new Vector3(stepRun, 0.12f, width)
                           : new Vector3(width, 0.12f, stepRun));

                // Riser filling the gap to the next tread, so there is no
                // see-through slot when looking along the flight.
                risers.AddBox(treadCentre - dir * (stepRun * 0.5f) - Vector3.up * (stepRise * 0.5f),
                    alongX ? new Vector3(0.08f, stepRise, width)
                           : new Vector3(width, stepRise, 0.08f));
            }

            Emit(parent, "StairTreads", treads, mat);
            Emit(parent, "StairRisers", risers, mat);

            // Handrail: posts plus a rail, on the open side.
            var rail = new MeshBuilder();
            int posts = Mathf.Max(2, steps / 4);
            for (int i = 0; i <= posts; i++)
            {
                float t = i / (float)posts;
                Vector3 p = from + horizontal * t + Vector3.up * (rise * t + 0.95f) + side * (width * 0.5f);
                rail.AddBox(p - Vector3.up * 0.48f, new Vector3(0.07f, 0.96f, 0.07f));
            }
            Emit(parent, "StairRail", rail, PlayableWorld.NewSharedMaterial(new Color(0.30f, 0.31f, 0.33f)));

            // Landing slab at the top.
            var landing = new MeshBuilder();
            landing.AddBox(to + Vector3.up * -0.06f,
                alongX ? new Vector3(1.6f, 0.12f, width) : new Vector3(width, 0.12f, 1.6f));
            Emit(parent, "StairLanding", landing, mat);

            return to;
        }

        private static void Emit(Transform parent, string name, MeshBuilder mb, Material mat)
        {
            if (mb == null || mb.VertexCount == 0) return;

            var go = mb.ToObject(name, mat, Vector3.zero);
            go.transform.SetParent(parent, false);

            // Stairs keep their colliders: the player walks on them.
        }
    }
}