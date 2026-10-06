using UnityEngine;

namespace Megame.Client
{
    /// <summary>
    /// Street furniture that makes the city read as a place rather than a
    /// blockout: pavements, kerbs, lamp posts, bins, benches, guard rails,
    /// traffic lights, crossings and shopfronts.
    ///
    /// Everything is primitive geometry and merged into a handful of combined
    /// meshes per material, so a few hundred objects cost a handful of draw
    /// calls instead of a few hundred.
    /// </summary>
    public static class StreetProps
    {
        private static MeshBuilder _curbs;
        private static MeshBuilder _posts;
        private static MeshBuilder _metal;
        private static MeshBuilder _wood;
        private static MeshBuilder _paint;

        private static Material _curbMat, _postMat, _metalMat, _woodMat, _paintMat;

        public static void Build(int blocksPerAxis, float blockSize, float roadWidth, System.Random rnd)
        {
            _curbs = new MeshBuilder();
            _posts = new MeshBuilder();
            _metal = new MeshBuilder();
            _wood = new MeshBuilder();
            _paint = new MeshBuilder();

            _curbMat = PlayableWorld.NewSharedMaterial(new Color(0.52f, 0.52f, 0.50f));
            _postMat = PlayableWorld.NewSharedMaterial(new Color(0.28f, 0.29f, 0.31f));
            _metalMat = PlayableWorld.NewSharedMaterial(new Color(0.62f, 0.64f, 0.66f));
            _woodMat = PlayableWorld.NewSharedMaterial(new Color(0.42f, 0.30f, 0.18f));
            _paintMat = PlayableWorld.NewSharedMaterial(new Color(0.85f, 0.82f, 0.35f));

            float half = blocksPerAxis / 2f;
            float extent = blocksPerAxis * blockSize;
            float kerbW = roadWidth * 0.5f + 1.6f;

            // Pavement strips and kerbs along every road line.
            for (int i = 0; i <= blocksPerAxis * 2; i++)
            {
                float c = -extent / 2f + i * (blockSize / 2f);
                Pavement(c, 0f, kerbW, roadWidth);
                Pavement(0f, c, kerbW, roadWidth);
            }

            // Furniture at intervals along the pavement centre line.
            for (int i = 0; i < blocksPerAxis * 2; i++)
            {
                float c = -extent / 2f + i * (blockSize / 2f);
                int steps = Mathf.Max(2, blocksPerAxis * 2);
                for (int s = 0; s < steps; s++)
                {
                    float t = -extent / 2f + (s + 0.5f) * (extent / steps);
                    bool swap = (i + s) % 2 == 0;
                    float x = swap ? c : t;
                    float z = swap ? t : c;
                    float sign = ((i + s) % 4 < 2) ? 1f : -1f;

                    float ox = swap ? x + sign * kerbW * 0.75f : x;
                    float oz = swap ? z : z + sign * kerbW * 0.75f;

                    float roll = (float)rnd.NextDouble();
                    if (roll < 0.30f) LampPost(ox, oz);
                    else if (roll < 0.48f) Bin(ox, oz);
                    else if (roll < 0.62f) Bench(ox, oz, sign, swap);
                    else if (roll < 0.72f) GuardRail(ox, oz, sign, swap, blockSize / steps);
                    else if (roll < 0.80f) Hydrant(ox, oz);
                }

                // Traffic light and crossing at the junction of this line.
                TrafficLight(c, -roadWidth * 0.5f - 3.2f, true);
                TrafficLight(-roadWidth * 0.5f - 3.2f, c, false);
                Crossing(c, 0f, roadWidth);
                Crossing(0f, c, roadWidth);
            }

            Flush();
        }

        // ---------------------------------------------------------------- pieces

        private static void Pavement(float x, float z, float kerbW, float roadWidth)
        {
            // Raised walkway either side of the carriageway.
            float w = 1.5f;
            float y = 0.12f;

            _curbs.AddBox(new Vector3(x, y, z + kerbW + w * 0.5f),
                new Vector3(roadWidth + 0.4f, y * 2f, w), uvScale: 1f, skipTopBottom: false);
            _curbs.AddBox(new Vector3(x, y, z - kerbW - w * 0.5f),
                new Vector3(roadWidth + 0.4f, y * 2f, w));

            if (Mathf.Abs(x) > 0.01f)
            {
                _curbs.AddBox(new Vector3(x + kerbW + w * 0.5f, y, z),
                    new Vector3(w, y * 2f, roadWidth + 0.4f));
                _curbs.AddBox(new Vector3(x - kerbW - w * 0.5f, y, z),
                    new Vector3(w, y * 2f, roadWidth + 0.4f));
            }
        }

        private static void LampPost(float x, float z)
        {
            _posts.AddFrustum(new Vector3(x, 0.24f, z), 0.11f, 0.07f, 4.4f, 6);
            // Lamp head, cantilevered over the road.
            _posts.AddBox(new Vector3(x, 4.62f, z), new Vector3(0.14f, 0.12f, 0.9f));
            _posts.AddBox(new Vector3(x, 4.52f, z + 0.45f), new Vector3(0.3f, 0.14f, 0.4f));
        }

        private static void Bin(float x, float z)
        {
            _metal.AddFrustum(new Vector3(x, 0.24f, z), 0.26f, 0.30f, 0.85f, 8);
            _metal.AddBox(new Vector3(x, 1.13f, z), new Vector3(0.36f, 0.08f, 0.36f));
        }

        private static void Bench(float x, float z, float sign, bool alongZ)
        {
            float w = alongZ ? 0.55f : 1.8f;
            float d = alongZ ? 1.8f : 0.55f;

            _wood.AddBox(new Vector3(x, 0.72f, z), new Vector3(w, 0.08f, d));
            _wood.AddBox(new Vector3(x + (alongZ ? sign * 0.22f : 0f), 1.02f, z + (alongZ ? 0f : sign * 0.22f)),
                new Vector3(alongZ ? 0.08f : w, 0.5f, alongZ ? d : 0.08f));

            _metal.AddBox(new Vector3(x - (alongZ ? sign * 0.2f : w * 0.4f), 0.48f, z - (alongZ ? d * 0.4f : sign * 0.2f)),
                new Vector3(0.1f, 0.5f, 0.1f));
            _metal.AddBox(new Vector3(x + (alongZ ? sign * 0.2f : w * 0.4f), 0.48f, z + (alongZ ? d * 0.4f : sign * 0.2f)),
                new Vector3(0.1f, 0.5f, 0.1f));
        }

        private static void GuardRail(float x, float z, float sign, bool alongZ, float length)
        {
            float span = Mathf.Min(length * 0.45f, 3.2f);
            for (int p = 0; p < 3; p++)
            {
                float t = (p - 1) * span * 0.42f;
                float px = alongZ ? x + sign * 1.0f : x + t;
                float pz = alongZ ? z + t : z + sign * 1.0f;

                _posts.AddBox(new Vector3(px, 0.62f, pz), new Vector3(0.1f, 0.78f, 0.1f));
                if (p < 2)
                {
                    float mx = alongZ ? x + sign * 1.0f : x + t + span * 0.21f;
                    float mz = alongZ ? z + t + span * 0.21f : z + sign * 1.0f;
                    _metal.AddBox(new Vector3(mx, 1.02f, mz),
                        new Vector3(alongZ ? 0.06f : span * 0.45f, 0.22f, alongZ ? span * 0.45f : 0.06f));
                }
            }
        }

        private static void Hydrant(float x, float z)
        {
            _paint.AddFrustum(new Vector3(x, 0.24f, z), 0.14f, 0.11f, 0.62f, 6);
            _paint.AddBox(new Vector3(x, 0.68f, z), new Vector3(0.34f, 0.12f, 0.14f));
        }

        private static void TrafficLight(float x, float z, bool alongRoad)
        {
            _posts.AddFrustum(new Vector3(x, 0.24f, z), 0.1f, 0.08f, 3.2f, 6);
            _metal.AddBox(new Vector3(x, 3.55f, z), new Vector3(0.28f, 0.9f, 0.24f));

            // Three lenses, top to bottom.
            float[] cols = { 0.9f, 0.8f, 0.25f };
            for (int i = 0; i < 3; i++)
            {
                float y = 3.86f - i * 0.27f;
                _paint.AddBox(new Vector3(x, y, z + 0.13f), new Vector3(0.14f, 0.14f, 0.03f));
            }
        }

        private static void Crossing(float x, float z, float roadWidth)
        {
            // Zebra stripes painted across the carriageway.
            int stripes = 6;
            float sw = roadWidth / (stripes * 2);
            for (int i = 0; i < stripes; i++)
            {
                float o = -roadWidth * 0.5f + sw + i * sw * 2f;
                _paint.AddBox(new Vector3(x + o, 0.045f, z),
                    new Vector3(sw, 0.03f, roadWidth * 0.7f));
                _paint.AddBox(new Vector3(x, 0.045f, z + o),
                    new Vector3(roadWidth * 0.7f, 0.03f, sw));
            }
        }

        // ----------------------------------------------------------------- flush

        private static void Flush()
        {
            Emit("Pavements", _curbs, _curbMat);
            Emit("Posts", _posts, _postMat);
            Emit("Metal", _metal, _metalMat);
            Emit("Timber", _wood, _woodMat);
            Emit("Paint", _paint, _paintMat);

            _curbs = _posts = _metal = _wood = _paint = null;
        }

        private static void Emit(string name, MeshBuilder mb, Material mat)
        {
            if (mb == null || mb.VertexCount == 0) return;

            // MeshBuilder produces geometry only, so there is nothing to strip:
            // these are pure scenery with no colliders at all.
            var go = mb.ToObject("Street_" + name, mat, Vector3.zero);
            go.isStatic = true;
        }
    }
}