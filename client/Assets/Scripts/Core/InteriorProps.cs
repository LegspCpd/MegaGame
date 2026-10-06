using System.Collections.Generic;
using UnityEngine;

namespace Megame.Client
{
    /// <summary>
    /// Furniture and loose pickups for building interiors.
    ///
    /// Without this an interior is four walls and a light: playable but empty.
    /// Furniture is static scenery with colliders so it blocks movement; pickups
    /// spin, hover and can be collected for money or health.
    /// </summary>
    public class InteriorProps
    {
        private static MeshBuilder _wood;
        private static MeshBuilder _metal;
        private static MeshBuilder _fabric;

        private static Material _woodMat;
        private static Material _metalMat;
        private static Material _fabricMat;

        /// <summary>Fills one interior, in the building's local space.</summary>
        public static void Populate(Transform root, float w, float d, System.Random rnd)
        {
            _wood = new MeshBuilder();
            _metal = new MeshBuilder();
            _fabric = new MeshBuilder();

            _woodMat = PlayableWorld.NewSharedMaterial(new Color(0.42f, 0.30f, 0.19f));
            _metalMat = PlayableWorld.NewSharedMaterial(new Color(0.58f, 0.60f, 0.62f));
            _fabricMat = PlayableWorld.NewSharedMaterial(new Color(0.30f, 0.34f, 0.42f));

            float halfW = w * 0.5f - 1.1f;
            float halfD = d * 0.5f - 1.1f;
            if (halfW < 1.2f || halfD < 1.2f) return;

            // Counters against a wall.
            Vector3 counterAt = new Vector3(
                (float)(rnd.NextDouble() - 0.5) * halfW * 1.2f, 0f,
                (float)(rnd.NextDouble() - 0.5) * halfD * 1.2f);

            _metal.AddBox(counterAt + Vector3.up * 0.45f, new Vector3(2.6f, 0.9f, 0.7f));
            _metal.AddBox(counterAt + Vector3.up * 0.94f, new Vector3(2.7f, 0.08f, 0.8f));

            // Shelving on another wall.
            Vector3 shelfAt = new Vector3(
                counterAt.x + (float)(rnd.NextDouble() - 0.5) * halfW,
                0f,
                -counterAt.z * 0.7f);

            for (int s = 0; s < 3; s++)
            {
                _wood.AddBox(shelfAt + Vector3.up * (0.6f + s * 0.65f),
                             new Vector3(1.8f, 0.07f, 0.45f));
            }
            _wood.AddBox(shelfAt + new Vector3(-0.85f, 1.3f, 0f), new Vector3(0.08f, 2.6f, 0.45f));
            _wood.AddBox(shelfAt + new Vector3(0.85f, 1.3f, 0f), new Vector3(0.08f, 2.6f, 0.45f));

            // A table with chairs in the middle of the room.
            Vector3 tableAt = new Vector3(
                (float)(rnd.NextDouble() - 0.5) * halfW * 0.8f, 0f,
                (float)(rnd.NextDouble() - 0.5) * halfD * 0.8f);

            _wood.AddBox(tableAt + Vector3.up * 0.38f, new Vector3(1.7f, 0.08f, 1.1f));
            _metal.AddBox(tableAt + new Vector3(-0.7f, 0.19f, 0f), new Vector3(0.09f, 0.38f, 0.09f));
            _metal.AddBox(tableAt + new Vector3(0.7f, 0.19f, 0f), new Vector3(0.09f, 0.38f, 0.09f));

            _fabric.AddBox(tableAt + new Vector3(0f, 0.22f, 0.95f), new Vector3(0.5f, 0.06f, 0.5f));
            _fabric.AddBox(tableAt + new Vector3(0f, 0.46f, 1.18f), new Vector3(0.5f, 0.45f, 0.06f));
            _metal.AddBox(tableAt + new Vector3(0f, 0.11f, 0.95f), new Vector3(0.08f, 0.22f, 0.08f));

            // Crates in a corner.
            Vector3 crateAt = new Vector3(-halfW * 0.8f, 0f, halfD * 0.8f);
            _wood.AddBox(crateAt + Vector3.up * 0.35f, new Vector3(0.7f, 0.7f, 0.7f));
            _wood.AddBox(crateAt + new Vector3(0.55f, 0.28f, 0.15f),
                         new Vector3(0.55f, 0.55f, 0.55f));

            Flush(root);

            // Loose pickups the player can walk over to collect.
            int pickups = 1 + rnd.Next(3);
            for (int i = 0; i < pickups; i++)
            {
                var pos = new Vector3(
                    (float)(rnd.NextDouble() - 0.5) * (w - 2.5f), 0.45f,
                    (float)(rnd.NextDouble() - 0.5) * (d - 2.5f));

                bool health = rnd.NextDouble() < 0.45f;
                Pickup.Create(root, pos, health
                    ? Pickup.Kind.Health
                    : Pickup.Kind.Cash);
            }
        }

        private static void Flush(Transform root)
        {
            // Furniture keeps colliders: it should stop the player.
            Emit(root, "Furniture_Wood", _wood, _woodMat, keepColliders: true);
            Emit(root, "Furniture_Metal", _metal, _metalMat, keepColliders: true);
            Emit(root, "Furniture_Fabric", _fabric, _fabricMat, keepColliders: true);

            _wood = _metal = _fabric = null;
        }

        private static void Emit(Transform root, string name, MeshBuilder mb,
                                 Material mat, bool keepColliders)
        {
            if (mb == null || mb.VertexCount == 0) return;

            var go = mb.ToObject(name, mat, Vector3.zero);
            go.transform.SetParent(root, true);

            if (keepColliders) return;

            foreach (var c in go.GetComponentsInChildren<Collider>())
            {
                Object.Destroy(c);
            }
        }
    }

    /// <summary>
    /// A collectable that hovers, spins and pays out when walked over.
    /// </summary>
    public class Pickup : MonoBehaviour
    {
        public enum Kind { Cash, Health }

        public Kind Type = Kind.Cash;
        public int Value = 75;
        public float SpinSpeed = 90f;
        public float BobHeight = 0.12f;
        public float BobSpeed = 2.2f;
        public float PickupRadius = 1.1f;

        private Vector3 _origin;
        private float _phase;
        private bool _taken;

        public static Pickup Create(Transform parent, Vector3 localPos, Kind kind)
        {
            var go = new GameObject("Pickup_" + kind);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;

            var p = go.AddComponent<Pickup>();
            p.Type = kind;
            p.Value = kind == Kind.Cash ? 100 : 30;
            p._origin = localPos;
            p._phase = localPos.x * 0.7f + localPos.z * 1.3f;

            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Body";
            body.transform.SetParent(go.transform, false);
            body.transform.localScale = kind == Kind.Cash
                ? new Vector3(0.34f, 0.12f, 0.22f)
                : new Vector3(0.2f, 0.2f, 0.2f);
            Object.Destroy(body.GetComponent<Collider>());
            body.GetComponent<MeshRenderer>().sharedMaterial = PlayableWorld.NewSharedMaterial(
                kind == Kind.Cash
                    ? new Color(0.30f, 0.85f, 0.40f)
                    : new Color(0.92f, 0.30f, 0.32f),
                emissive: true);

            return p;
        }

        private void Update()
        {
            if (_taken) return;

            float t = Time.time * BobSpeed + _phase;
            transform.localPosition = _origin + Vector3.up * (Mathf.Sin(t) * BobHeight);
            transform.Rotate(Vector3.up, SpinSpeed * Time.deltaTime, Space.World);

            var vitals = PlayerVitals.Instance;
            var player = PlayablePlayer.Instance;
            if (vitals == null || player == null) return;

            float dist = Vector3.Distance(player.transform.position, transform.position);
            if (dist > PickupRadius) return;

            if (Type == Kind.Cash)
            {
                vitals.AddCash(Value);
            }
            else
            {
                vitals.AddHealth(Value);
            }

            _taken = true;
            Destroy(gameObject);
        }
    }
}