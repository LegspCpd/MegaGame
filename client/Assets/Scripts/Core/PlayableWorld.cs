using UnityEngine;
using UnityEngine.Rendering;

namespace Megame.Client
{
    /// <summary>
    /// Builds a playable sandbox at runtime.
    ///
    /// The project ships no art assets (no models, textures or prefabs), and
    /// Boot.unity only contains a camera, so everything the player sees is
    /// assembled from Unity primitives here. That keeps the game playable
    /// without depending on GUID-coupled scene references.
    /// </summary>
    public static class PlayableWorld
    {
        public const int BlockSize = 60;      // metres per city block
        public const int BlocksPerAxis = 5;   // 5x5 blocks => 300x300 m
        public const float RoadWidth = 12f;

        /// <summary>Where the player respawns after dying.</summary>
        public static Vector3 SpawnPoint { get; private set; }

        private static bool _built;

        /// <summary>
        /// Runs after the scene's own objects (notably Camera.main) exist.
        /// Bootstrap fires BeforeSceneLoad, which is too early to configure a
        /// camera, so this is the entry point that actually builds the world.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        public static void BootstrapWorld()
        {
            EnsureBuilt();
        }

        public static void EnsureBuilt()
        {
            if (_built) return;
            _built = true;
            Build();
        }

        private static void Build()
        {
            SetupEnvironment();
            BuildGround();
            BuildCity();
            BuildProps();
            StreetProps.Build(BlocksPerAxis, BlockSize, RoadWidth, new System.Random(5150));
            DialogueSystem.Create();
            QuestSystem.Create();
            BuildPedestrians();

            var spawn = new Vector3(0f, 1.2f, -RoadWidth * 0.5f - 2f);
            var player = PlayablePlayer.Create(spawn);
            var rig = PlayableCameraRig.AttachTo(player.transform);
            var inventory = WeaponInventory.Create(Camera.main, player.transform);
            var vitals = PlayerVitals.Create(player.gameObject);
            PlayerDamage.Create(player.gameObject, vitals);

            var hud = PlayableHUD.Ensure();
            PauseMenu.Ensure();
            var interaction = PlayablePlayerInteraction.Create(player, inventory, rig);
            hud.Bind(player, inventory, interaction, vitals);
            PlayableRadar.Create(player.transform);

            var cinematic = CinematicPlayer.Create(Camera.main, rig, player.transform);
            CinematicStarter.Ensure(cinematic);

            // Spawn point used when the player dies or respawns.
            SpawnPoint = spawn;
        }

        private static void SetupEnvironment()
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.42f, 0.52f, 0.68f);
            RenderSettings.ambientEquatorColor = new Color(0.32f, 0.34f, 0.36f);
            RenderSettings.ambientGroundColor = new Color(0.14f, 0.13f, 0.12f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.55f, 0.62f, 0.72f);
            RenderSettings.fogStartDistance = 80f;
            RenderSettings.fogEndDistance = 320f;

            var sunGo = new GameObject("Sun");
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.96f, 0.88f);
            sun.intensity = 1.35f;
            sun.shadows = LightShadows.Soft;
            sunGo.transform.rotation = Quaternion.Euler(48f, -32f, 0f);

            RenderSettings.skybox = null;
            Camera.main.clearFlags = CameraClearFlags.SolidColor;
            Camera.main.backgroundColor = new Color(0.55f, 0.66f, 0.82f);
        }

        private static void BuildGround()
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Ground";
            ground.transform.position = new Vector3(0f, -1f, 0f);
            ground.transform.localScale = new Vector3(600f, 2f, 600f);
            // The ground MUST keep a collider: the player's CharacterController
            // only reports isGrounded when it is standing on one.
            var groundCol = ground.GetComponent<BoxCollider>();
            if (groundCol != null)
            {
                groundCol.size = new Vector3(600f, 2f, 600f);
                groundCol.center = Vector3.zero;
            }
            ground.isStatic = true;

            var mat = NewSharedMaterial(new Color(0.24f, 0.30f, 0.22f));
            ground.GetComponent<MeshRenderer>().sharedMaterial = mat;

            // Road grid, drawn as thin slabs sitting just above the ground.
            int lines = BlocksPerAxis * 2 + 1;
            float extent = BlocksPerAxis * BlockSize;
            for (int i = 0; i < lines; i++)
            {
                float x = -extent / 2f + i * (BlockSize / 2f);
                var roadX = MakeSlab("RoadX", new Vector3(x, 0.01f, 0f), new Vector3(RoadWidth, 0.05f, extent));
                var roadZ = MakeSlab("RoadZ", new Vector3(0f, 0.012f, x), new Vector3(extent, 0.05f, RoadWidth));
                roadX.GetComponent<MeshRenderer>().sharedMaterial = RoadMaterial();
                roadZ.GetComponent<MeshRenderer>().sharedMaterial = RoadMaterial();
            }
        }

        private static Material RoadMaterial()
        {
            return NewSharedMaterial(new Color(0.14f, 0.14f, 0.16f));
        }

        private static GameObject MakeSlab(string name, Vector3 pos, Vector3 scale)
        {
            var slab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            slab.name = name;
            slab.transform.position = pos;
            slab.transform.localScale = scale;
            // Roads keep their collider too, but they sit flush on the ground so
            // the top face stays walkable without stepping on a curb.
            var col = slab.GetComponent<BoxCollider>();
            if (col != null)
            {
                col.size = Vector3.one;
                col.center = Vector3.zero;
            }
            slab.isStatic = true;
            return slab;
        }

        private static void BuildCity()
        {
            var rnd = new System.Random(20240607); // deterministic layout
            int half = BlocksPerAxis / 2;

            for (int bx = -half; bx < BlocksPerAxis - half; bx++)
            {
                for (int bz = -half; bz < BlocksPerAxis - half; bz++)
                {
                    float originX = bx * BlockSize + BlockSize / 2f;
                    float originZ = bz * BlockSize + BlockSize / 2f;

                    int count = rnd.Next(3, 6);
                    for (int i = 0; i < count; i++)
                    {
                        // Keep buildings off the roadway.
                        float lot = BlockSize - RoadWidth - 8f;
                        float x = originX + (float)((rnd.NextDouble() - 0.5) * lot);
                        float z = originZ + (float)((rnd.NextDouble() - 0.5) * lot);

                        float w = (float)(8 + rnd.NextDouble() * 10);
                        float d = (float)(8 + rnd.NextDouble() * 10);
                        float h = (float)(12 + rnd.NextDouble() * 46);

                        CreateBuilding(x, z, w, d, h, rnd);
                    }

                    if (rnd.NextDouble() < 0.35)
                    {
                        CreatePark(originX, originZ, rnd);
                    }
                }
            }
        }

        private static void CreateBuilding(float x, float z, float w, float d, float h, System.Random rnd)
        {
            float shade = 0.34f + (float)rnd.NextDouble() * 0.26f;
            var bodyMat = NewMaterial(new Color(shade, shade * 0.97f, shade * 0.92f));
            var trimMat = NewMaterial(new Color(shade * 0.55f, shade * 0.55f, shade * 0.58f));

            // Two stacked masses with a setback, so the silhouette steps back
            // instead of reading as one extruded rectangle.
            float lowerH = h * (0.55f + 0.25f * (float)rnd.NextDouble());
            float upperH = h - lowerH;
            bool hasUpper = upperH > 4f;
            float inset = hasUpper ? 0.8f + 1.4f * (float)rnd.NextDouble() : 0f;
            float twist = hasUpper ? (float)(rnd.NextDouble() * 12f - 6f) : 0f;

                        // A building is either a solid shell you cannot enter, or a hollow
            // shell you can walk into. Generating both for the same footprint
            // would leave the player trapped between two layers of wall, so
            // the solid mesh is only produced for non-enterable buildings.
            bool enterable = w >= 7f && d >= 7f && lowerH >= 5f;

            var root = new GameObject("Building");
            root.transform.position = new Vector3(x, 0f, z);

            if (enterable)
            {
                BuildInterior(root.transform, w, d, lowerH, rnd);
                InteriorProps.Populate(root.transform, w, d, rnd);
                BuildUpperFloor(root.transform, w, d, lowerH, rnd);
            }
            else
            {
                var mb = new MeshBuilder();
                mb.AddSetbackTower(
                    Vector3.zero,
                    w, d,
                    lowerH,
                    hasUpper ? upperH : 0.01f,
                    inset,
                    twist,
                    uvScale: Mathf.Max(1f, Mathf.Max(w, d) * 0.35f));

                var body = mb.ToObject("BuildingBody", bodyMat, Vector3.zero);
                body.transform.SetParent(root.transform, false);

                var col = root.AddComponent<BoxCollider>();
                col.size = new Vector3(w, h, d);
                col.center = new Vector3(0f, h * 0.5f, 0f);
            }

            // Ground-floor band gives the street level some read.
            var baseTrim = GameObject.CreatePrimitive(PrimitiveType.Cube);
            baseTrim.name = "BuildingBase";
            baseTrim.transform.SetParent(root.transform, false);
            baseTrim.transform.localPosition = new Vector3(0f, 1.1f, 0f);
            baseTrim.transform.localScale = new Vector3(w + 0.5f, 2.2f, d + 0.5f);
            baseTrim.GetComponent<MeshRenderer>().sharedMaterial = trimMat;

            BuildShopfront(root.transform, w, d, rnd);

            if (hasUpper)
            {
                var roof = GameObject.CreatePrimitive(PrimitiveType.Cube);
                roof.name = "RoofCap";
                roof.transform.SetParent(root.transform, false);
                roof.transform.localPosition = new Vector3(0f, h + 0.25f, 0f);
                roof.transform.localScale = new Vector3(
                    Mathf.Max(0.5f, w - inset * 2f), 0.5f, Mathf.Max(0.5f, d - inset * 2f));
                roof.transform.localRotation = Quaternion.Euler(0f, twist, 0f);
                StripCollider(roof);                roof.GetComponent<MeshRenderer>().sharedMaterial = trimMat;
            }

            // Emissive window bands on the lower shaft.
            if (h > 20f)
            {
                var windowMat = NewSharedMaterial(new Color(0.9f, 0.85f, 0.6f), true);
                int floors = Mathf.Max(1, (int)(lowerH / 6f));
                for (int f = 1; f < floors; f++)
                {
                    var band = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    band.name = "WindowBand";
                    band.transform.SetParent(root.transform, false);
                    band.transform.localPosition = new Vector3(0f, f * 6f, 0f);
                    band.transform.localScale = new Vector3(w * 1.004f, 1.1f, d * 1.004f);
                    StripCollider(band);
                    band.GetComponent<MeshRenderer>().sharedMaterial = windowMat;
                }
            }

            // Solid buildings get real window openings cut into their shell.
            // Enterable ones already have walls from BuildInterior, and cutting
            // windows into those as well would double the geometry.
            if (!enterable && lowerH > 8f)
            {
                BuildFacadeWindows(root.transform, w, d, lowerH, rnd);
            }
        }

        /// <summary>
        /// Adds a reachable upper storey: a slab, walls, a flight of stairs and
        /// a light, so tall buildings are not single-storey shells with a flat
        /// lid. Skipped when the ground floor is too low to stand up under.
        /// </summary>
        private static void BuildUpperFloor(Transform root, float w, float d,
                                            float groundH, System.Random rnd)
        {
            if (groundH < 5.0f) return;

            const float upperH = 3.0f;
            const float thickness = 0.3f;

            // Sit the loft at a normal storey height rather than against the
            // ceiling. Pushing the slab up to groundH would need a run longer
            // than the building is deep, and Staircase rejects anything
            // steeper than about 45 degrees, so the flight would be skipped and
            // the loft unreachable.
            float floorY = Mathf.Min(3.2f, groundH - 2.4f);
            if (floorY < 2.2f) return;

            float halfW = w * 0.5f;
            float halfD = d * 0.5f;

            var slabMat = NewSharedMaterial(new Color(0.32f, 0.31f, 0.33f));
            var wallMat = NewSharedMaterial(new Color(0.44f, 0.43f, 0.41f));

            bool alongX = w >= d;
            float run = Mathf.Min(alongX ? w : d, 5.5f);
            float stairStart = -(alongX ? halfW : halfD) + 1.2f;
            float stairEnd = stairStart + run;

            // Floor slab, split so the stairwell stays open.
            if (alongX)
            {
                Box(root, "UpperFloorA", slabMat,
                    new Vector3((stairStart - halfW) * 0.5f, floorY, 0f),
                    new Vector3(stairStart + halfW, 0.24f, d));
                Box(root, "UpperFloorB", slabMat,
                    new Vector3((stairEnd + halfW) * 0.5f, floorY, 0f),
                    new Vector3(halfW - stairEnd, 0.24f, d));
            }
            else
            {
                Box(root, "UpperFloorA", slabMat,
                    new Vector3(0f, floorY, (stairStart - halfD) * 0.5f),
                    new Vector3(w, 0.24f, stairStart + halfD));
                Box(root, "UpperFloorB", slabMat,
                    new Vector3(0f, floorY, (stairEnd + halfD) * 0.5f),
                    new Vector3(w, 0.24f, halfD - stairEnd));
            }

            // Upper walls.
            Box(root, "UpperWallZPos", wallMat,
                new Vector3(0f, floorY + upperH * 0.5f, halfD), new Vector3(w, upperH, thickness));
            Box(root, "UpperWallZNeg", wallMat,
                new Vector3(0f, floorY + upperH * 0.5f, -halfD), new Vector3(w, upperH, thickness));
            Box(root, "UpperWallXPos", wallMat,
                new Vector3(halfW, floorY + upperH * 0.5f, 0f), new Vector3(thickness, upperH, d));
            Box(root, "UpperWallXNeg", wallMat,
                new Vector3(-halfW, floorY + upperH * 0.5f, 0f), new Vector3(thickness, upperH, d));

            // Stairs from the ground floor up through the gap.
            Vector3 from = alongX
                ? new Vector3(stairStart, 0.2f, 0f)
                : new Vector3(0f, 0.2f, stairStart);
            Vector3 to = alongX
                ? new Vector3(stairEnd, floorY + 0.12f, 0f)
                : new Vector3(0f, floorY + 0.12f, stairEnd);

            Staircase.Build(root, from, to, 1.5f);

            // Roof over the upper storey.
            Box(root, "UpperRoof", wallMat,
                new Vector3(0f, floorY + upperH + 0.2f, 0f),
                new Vector3(w + 0.4f, 0.4f, d + 0.4f));

            // Light for the upper storey.
            var lampGo = new GameObject("UpperLight");
            lampGo.transform.SetParent(root, false);
            lampGo.transform.localPosition = new Vector3(0f, floorY + upperH - 0.5f, 0f);
            var lamp = lampGo.AddComponent<Light>();
            lamp.type = LightType.Point;
            lamp.color = new Color(1f, 0.93f, 0.80f);
            lamp.range = Mathf.Max(w, d) * 1.2f;
            lamp.intensity = Mathf.Max(1.4f, Mathf.Max(w, d) * 0.5f);
        }

        /// <summary>
        /// Cuts window bands into the outer shell of a solid building so the
        /// facade reads as storeys rather than a blank extrusion.
        /// </summary>
        private static void BuildFacadeWindows(Transform root, float w, float d,
                                                float height, System.Random rnd)
        {
            const float thickness = 0.32f;
            float storey = 3.4f;
            int storeys = Mathf.FloorToInt((height - 2.2f) / storey);
            if (storeys < 1) return;

            int columns = Mathf.Max(2, Mathf.FloorToInt(Mathf.Max(w, d) / 3.2f));

            // All facades merge into two meshes for the whole building.
            WindowCut.Begin();

            for (int s = 0; s < storeys; s++)
            {
                float y = 2.2f + s * storey + storey * 0.5f;

                WindowCut.Build(new Vector3(0f, y, d * 0.5f),
                    new Vector3(w, storey, thickness), 1.9f, 1.0f, 1.5f, columns);
                WindowCut.Build(new Vector3(0f, y, -d * 0.5f),
                    new Vector3(w, storey, thickness), 1.9f, 1.0f, 1.5f, columns);

                // Side faces, only on wider buildings so it does not overdraw.
                if (w > 9f)
                {
                    int sideCols = Mathf.Max(2, Mathf.FloorToInt(d / 3.2f));
                    WindowCut.Build(new Vector3(w * 0.5f, y, 0f),
                        new Vector3(thickness, storey, d), 1.9f, 1.0f, 1.5f, sideCols);
                    WindowCut.Build(new Vector3(-w * 0.5f, y, 0f),
                        new Vector3(thickness, storey, d), 1.9f, 1.0f, 1.5f, sideCols);
                }
            }

            WindowCut.Flush(root);
        }

        private static void CreatePark(float x, float z, System.Random rnd)
        {
            var lawn = GameObject.CreatePrimitive(PrimitiveType.Cube);
            lawn.name = "Park";
            lawn.transform.position = new Vector3(x, 0.03f, z);
            lawn.transform.localScale = new Vector3(BlockSize - RoadWidth - 6f, 0.05f, BlockSize - RoadWidth - 6f);
            StripCollider(lawn);
            lawn.GetComponent<MeshRenderer>().sharedMaterial =
                NewSharedMaterial(new Color(0.18f, 0.42f, 0.20f));

            int trees = rnd.Next(5, 12);
            for (int i = 0; i < trees; i++)
            {
                float tx = x + (float)((rnd.NextDouble() - 0.5) * (BlockSize - RoadWidth - 14f));
                float tz = z + (float)((rnd.NextDouble() - 0.5) * (BlockSize - RoadWidth - 14f));
                CreateTree(tx, tz, (float)(2.5 + rnd.NextDouble() * 2.5));
            }
        }

        private static void CreateTree(float x, float z, float scale)
        {
            var trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            trunk.name = "TreeTrunk";
            trunk.transform.position = new Vector3(x, scale * 0.5f, z);
            trunk.transform.localScale = new Vector3(0.3f * scale, scale * 0.5f, 0.3f * scale);
            trunk.GetComponent<MeshRenderer>().sharedMaterial =
                NewSharedMaterial(new Color(0.30f, 0.20f, 0.12f));

            var crown = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            crown.name = "TreeCrown";
            crown.transform.position = new Vector3(x, scale * 1.35f, z);
            crown.transform.localScale = Vector3.one * scale * 1.15f;
            StripCollider(crown);
            crown.GetComponent<MeshRenderer>().sharedMaterial =
                NewSharedMaterial(new Color(0.16f, 0.42f, 0.18f));
        }

        /// <summary>
        /// Ground-floor shopfront: a glazed front, a door, an awning and a lit
        /// sign. Without it every building meets the street with a blank wall,
        /// which is what makes a blockout look like a blockout.
        /// </summary>
        private static void BuildShopfront(Transform root, float w, float d, System.Random rnd)
        {
            var glassMat = NewSharedMaterial(new Color(0.16f, 0.22f, 0.26f));
            var awningMat = NewSharedMaterial(new Color(0.42f, 0.16f, 0.16f));

            // Pick the face that points at the nearest road.
            int face = rnd.Next(4);
            Vector3 outward;
            Vector3 right;
            switch (face)
            {
                case 0: outward = Vector3.forward; right = Vector3.right; break;
                case 1: outward = Vector3.right; right = Vector3.back; break;
                case 2: outward = Vector3.back; right = Vector3.left; break;
                default: outward = Vector3.left; right = Vector3.forward; break;
            }

            float halfW = (face % 2 == 0 ? d : w) * 0.5f + 0.06f;
            Vector3 front = outward * halfW;

            // Glazed shop window split by a door.
            float windowW = Mathf.Min(2.6f, halfW * 1.1f);
            float doorW = 1.1f;

            var window = GameObject.CreatePrimitive(PrimitiveType.Cube);
            window.name = "ShopWindow";
            window.transform.SetParent(root, false);
            window.transform.localPosition = front - right * (doorW * 0.5f + windowW * 0.5f)
                                           + Vector3.up * 2.4f;
            window.transform.localRotation = Quaternion.LookRotation(outward, Vector3.up);
            window.transform.localScale = new Vector3(windowW, 2.6f, 0.08f);
            StripCollider(window);
            window.GetComponent<MeshRenderer>().sharedMaterial = glassMat;

            var door = GameObject.CreatePrimitive(PrimitiveType.Cube);
            door.name = "ShopDoor";
            door.transform.SetParent(root, false);
            door.transform.localPosition = front + right * (doorW * 0.5f - halfW * 0.9f)
                                         + Vector3.up * 1.9f;
            door.transform.localRotation = Quaternion.LookRotation(outward, Vector3.up);
            door.transform.localScale = new Vector3(doorW, 2.4f, 0.08f);
            StripCollider(door);
            door.GetComponent<MeshRenderer>().sharedMaterial = trimMatOrDefault();

            // Awning over the front.
            var awning = GameObject.CreatePrimitive(PrimitiveType.Cube);
            awning.name = "Awning";
            awning.transform.SetParent(root, false);
            awning.transform.localPosition = front * 1.03f + Vector3.up * 4.1f;
            awning.transform.localRotation = Quaternion.LookRotation(outward, Vector3.up);
            awning.transform.localScale = new Vector3(halfW * 1.7f, 0.12f, 1.5f);
            StripCollider(awning);
            awning.GetComponent<MeshRenderer>().sharedMaterial = awningMat;

            // Lit sign board above the awning.
            var hue = (float)rnd.NextDouble();
            var signMat = NewMaterial(ColorFromHue(hue), emissive: true);

            var sign = GameObject.CreatePrimitive(PrimitiveType.Cube);
            sign.name = "ShopSign";
            sign.transform.SetParent(root, false);
            sign.transform.localPosition = front * 1.04f + Vector3.up * 5.2f;
            sign.transform.localRotation = Quaternion.LookRotation(outward, Vector3.up);
            sign.transform.localScale = new Vector3(halfW * 1.5f, 1.1f, 0.16f);
            StripCollider(sign);
            sign.GetComponent<MeshRenderer>().sharedMaterial = signMat;
        }

        private static Material _defaultTrim;

        private static Material trimMatOrDefault()
        {
            if (_defaultTrim != null) return _defaultTrim;
            _defaultTrim = NewSharedMaterial(new Color(0.30f, 0.31f, 0.34f));
            return _defaultTrim;
        }

        /// <summary>
        /// Turns the solid mass into a walk-in shell: four walls, a floor and a
        /// ceiling, with a doorway punched through one wall. Walls are built as
        /// segments around the opening rather than with a boolean cut, which
        /// keeps the geometry plain quads.
        /// </summary>
        private static void BuildInterior(Transform root, float w, float d, float height,
                                          System.Random rnd)
        {
            var wallMat = NewSharedMaterial(new Color(0.46f, 0.45f, 0.43f));
            var floorMat = NewSharedMaterial(new Color(0.30f, 0.29f, 0.31f));

            const float thickness = 0.35f;
            float halfW = w * 0.5f;
            float halfD = d * 0.5f;

            int doorFace = rnd.Next(4);
            const float doorW = 2.2f;
            const float doorH = 2.8f;

            // Built explicitly rather than through a closure: the segment maths
            // stays readable next to the four call sites.
            BuildWall(root, "WallZPos", wallMat, new Vector3(0f, height * 0.5f, halfD),
                      new Vector3(w, height, thickness),
                      doorFace == 0, doorW, doorH);
            BuildWall(root, "WallZNeg", wallMat, new Vector3(0f, height * 0.5f, -halfD),
                      new Vector3(w, height, thickness),
                      doorFace == 2, doorW, doorH);
            BuildWall(root, "WallXPos", wallMat, new Vector3(halfW, height * 0.5f, 0f),
                      new Vector3(thickness, height, d),
                      doorFace == 1, doorW, doorH);
            BuildWall(root, "WallXNeg", wallMat, new Vector3(-halfW, height * 0.5f, 0f),
                      new Vector3(thickness, height, d),
                      doorFace == 3, doorW, doorH);

            // Floor and ceiling.
            Box(root, "InteriorFloor", floorMat,
                new Vector3(0f, 0.06f, 0f), new Vector3(w - thickness, 0.12f, d - thickness));
            Box(root, "InteriorCeiling", wallMat,
                new Vector3(0f, height - 0.08f, 0f), new Vector3(w - thickness, 0.16f, d - thickness));

            // Outer roof slab, so the building is closed from outside.
            Box(root, "InteriorRoof", wallMat,
                new Vector3(0f, height + 0.2f, 0f), new Vector3(w + 0.4f, 0.4f, d + 0.4f));

            // Interior lighting. Without this the inside is pitch black: the
            // sun only reaches through the doorway and the ceiling blocks the
            // sky contribution.
            var lampGo = new GameObject("InteriorLight");
            lampGo.transform.SetParent(root, false);
            lampGo.transform.localPosition = new Vector3(0f, height - 0.6f, 0f);

            var lamp = lampGo.AddComponent<Light>();
            lamp.type = LightType.Point;
            lamp.color = new Color(1f, 0.94f, 0.82f);
            lamp.range = Mathf.Max(w, d) * 1.3f;
            lamp.intensity = Mathf.Max(1.6f, Mathf.Max(w, d) * 0.55f);

            // A visible fitting so the light source is not invisible.
            var fitting = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fitting.name = "LampFitting";
            fitting.transform.SetParent(lampGo.transform, false);
            fitting.transform.localPosition = Vector3.zero;
            fitting.transform.localScale = new Vector3(1.4f, 0.08f, 0.4f);
            StripCollider(fitting);
            fitting.GetComponent<MeshRenderer>().sharedMaterial =
                NewMaterial(new Color(1f, 0.95f, 0.85f), emissive: true);
        }

        /// <summary>
        /// One wall, built as two side segments plus a lintel when a doorway is
        /// wanted. Avoids boolean geometry on the mesh.
        /// </summary>
        private static void BuildWall(Transform root, string name, Material mat,
                                      Vector3 centre, Vector3 size,
                                      bool hasDoor, float doorW, float doorH)
        {
            if (!hasDoor)
            {
                Box(root, name, mat, centre, size);
                return;
            }

            bool alongX = size.x > size.z;
            float span = alongX ? size.x : size.z;
            float side = (span - doorW) * 0.5f;
            float offset = doorW * 0.5f + side * 0.5f;

            Vector3 left = alongX
                ? new Vector3(centre.x - offset, centre.y, centre.z)
                : new Vector3(centre.x, centre.y, centre.z - offset);
            Vector3 right = alongX
                ? new Vector3(centre.x + offset, centre.y, centre.z)
                : new Vector3(centre.x, centre.y, centre.z + offset);

            Vector3 segSize = alongX
                ? new Vector3(side, size.y, size.z)
                : new Vector3(size.x, size.y, side);

            Box(root, name + "A", mat, left, segSize);
            Box(root, name + "B", mat, right, segSize);

            // Lintel above the opening.
            float lintelH = size.y - doorH;
            Vector3 lintel = new Vector3(alongX ? doorW : size.x, lintelH,
                                         alongX ? size.z : doorW);
            Box(root, name + "Lintel", mat,
                new Vector3(centre.x, doorH + lintelH * 0.5f, centre.z), lintel);

            // Hang a door in the opening, hinged on one side.
            Vector3 hinge = alongX ? Vector3.left : Vector3.back;
            SwingingDoor.Create(root, name + "Door",
                new Vector3(centre.x, doorH * 0.5f, centre.z),
                doorW, doorH - 0.1f, hinge, alongX);
        }

        private static void Box(Transform parent, string name, Material mat,
                                Vector3 localPos, Vector3 localScale)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }
        /// <summary>
        /// Scatters talkable pedestrians along the pavements. They wander a
        /// little and can be spoken to with F, which exercises the branching
        /// dialogue system.
        /// </summary>
        private static void BuildPedestrians()
        {
            string[] names = { "Stranger", "Office Worker", "Taxi Driver", "Tourist", "Vendor" };
            var rnd = new System.Random(1717);

            for (int i = 0; i < 14; i++)
            {
                float x = (float)((rnd.NextDouble() - 0.5) * BlocksPerAxis * BlockSize);
                float z = (float)((rnd.NextDouble() - 0.5) * BlocksPerAxis * BlockSize);

                // Snap onto the nearest pavement so NPCs are not inside a block.
                float offset = RoadWidth * 0.5f + 1.8f;
                if (i % 2 == 0) x = Mathf.Round(x / (BlockSize * 0.5f)) * (BlockSize * 0.5f) + offset;
                else z = Mathf.Round(z / (BlockSize * 0.5f)) * (BlockSize * 0.5f) + offset;

                PlayableNpc.Create(
                    new Vector3(x, 0f, z),
                    names[i % names.Length],
                    ColorFromHue((float)rnd.NextDouble()),
                    // Variance so a rifle (26 dmg) does not one-shot everyone.
                    45f + (float)rnd.NextDouble() * 35f);
            }
        }

        private static void BuildProps()
        {
            // Parked cars along the kerb give the streets some life.
            var rnd = new System.Random(99);
            int half = BlocksPerAxis / 2;
            for (int bx = -half; bx < BlocksPerAxis - half; bx++)
            {
                for (int bz = -half; bz < BlocksPerAxis - half; bz++)
                {
                    if (rnd.NextDouble() > 0.42) continue;

                    // Real drivable cars, not boxes: the player can press F next
                    // to any of these and drive it.
                    float angle = rnd.Next(4) * 90f;
                    bool alongZ = angle == 0 || angle == 180;
                    float x = bx * BlockSize + BlockSize / 2f + (alongZ ? RoadWidth * 0.30f : 0f);
                    float z = bz * BlockSize + BlockSize / 2f + (alongZ ? 0f : RoadWidth * 0.30f);

                    PlayableVehicle.Create(
                        new Vector3(x, 0.05f, z),
                        Quaternion.Euler(0f, angle, 0f),
                        ColorFromHue((float)rnd.NextDouble()));
                }
            }
        }

        private static Color ColorFromHue(float t)
        {
            // Cheap HSV -> RGB, no dependency on a gradient asset.
            float h = t * 6f;
            int sector = (int)h;
            float f = h - sector;
            float q = 1f - f;
            switch (sector % 6)
            {
                case 0: return new Color(1f, f, 0f);
                case 1: return new Color(q, 1f, 0f);
                case 2: return new Color(0f, 1f, f);
                case 3: return new Color(0f, q, 1f);
                case 4: return new Color(f, 0f, 1f);
                default: return new Color(1f, 0f, q);
            }
        }

        private static void StripCollider(GameObject go)
        {
            // Only ever call this on decoration. The ground, roads, buildings and
            // props MUST keep their colliders or the CharacterController never
            // reports isGrounded and the player falls through the world.
            var col = go.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);
        }

        /// <summary>
        /// stripEngineCode can remove the Standard shader from the player build,
        /// which turns every surface magenta. Fall back through the other
        /// built-in shaders, and finally to an unlit one that is always present.
        /// </summary>
        private static Shader FindUsableShader()
        {
            var shader = Shader.Find("Standard");
            if (shader != null) return shader;
            shader = Shader.Find("Legacy Shaders/Diffuse");
            if (shader != null) return shader;
            shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader != null) return shader;
            return Shader.Find("Unlit/Color");
        }

        // Buildings tint themselves per instance with a random shade, so caching
        // them would fill the cache with one-off entries and defeat the point.
        // Cache only exact-colour requests, which is what the road, pavement
        // and window materials are.
        internal static Material NewSharedMaterial(Color color, bool emissive = false)
        {
            int key = ((int)(color.r * 255) << 24) | ((int)(color.g * 255) << 16)
                    | ((int)(color.b * 255) << 8) | (emissive ? 1 : 0);

            Material cached;
            if (_materialCache.TryGetValue(key, out cached) && cached != null) return cached;

            var mat = new Material(FindUsableShader()) { color = color };
            if (emissive && mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", color * 1.6f);
            }
            _materialCache[key] = mat;
            return mat;
        }

        internal static Material NewMaterial(Color color, bool emissive = false)
        {
            return new Material(FindUsableShader()) { color = color };
        }

        private static readonly System.Collections.Generic.Dictionary<int, Material> _materialCache =
            new System.Collections.Generic.Dictionary<int, Material>();
    }
}