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

            var spawn = new Vector3(0f, 1.2f, -RoadWidth * 0.5f - 2f);
            var player = PlayablePlayer.Create(spawn);
            var rig = PlayableCameraRig.AttachTo(player.transform);
            var weapon = PlayableWeapon.Create(Camera.main, PlayableWeapon.WeaponKind.Rifle);

            PlayableHUD.Ensure();
            PauseMenu.Ensure();
            PlayablePlayerInteraction.Create(player, weapon, rig);
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

            var mat = NewMaterial(new Color(0.24f, 0.30f, 0.22f));
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
            return NewMaterial(new Color(0.14f, 0.14f, 0.16f));
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

            var mb = new MeshBuilder();
            mb.AddSetbackTower(
                Vector3.zero,
                w, d,
                lowerH,
                hasUpper ? upperH : 0.01f,
                inset,
                twist,
                uvScale: Mathf.Max(1f, Mathf.Max(w, d) * 0.35f));

            // Root sits on the ground; the mesh is built in local space so the
            // transform stays a clean parent for roof cap and window bands.
            var root = new GameObject("Building");
            root.transform.position = new Vector3(x, 0f, z);

            var body = mb.ToObject("BuildingBody", bodyMat, Vector3.zero);
            body.transform.SetParent(root.transform, false);

            var col = root.AddComponent<BoxCollider>();
            col.size = new Vector3(w, h, d);
            col.center = new Vector3(0f, h * 0.5f, 0f);

            // Ground-floor band gives the street level some read.
            var baseTrim = GameObject.CreatePrimitive(PrimitiveType.Cube);
            baseTrim.name = "BuildingBase";
            baseTrim.transform.SetParent(root.transform, false);
            baseTrim.transform.localPosition = new Vector3(0f, 1.1f, 0f);
            baseTrim.transform.localScale = new Vector3(w + 0.5f, 2.2f, d + 0.5f);
            baseTrim.GetComponent<MeshRenderer>().sharedMaterial = trimMat;

            if (hasUpper)
            {
                var roof = GameObject.CreatePrimitive(PrimitiveType.Cube);
                roof.name = "RoofCap";
                roof.transform.SetParent(root.transform, false);
                roof.transform.localPosition = new Vector3(0f, h + 0.25f, 0f);
                roof.transform.localScale = new Vector3(
                    Mathf.Max(0.5f, w - inset * 2f), 0.5f, Mathf.Max(0.5f, d - inset * 2f));
                roof.transform.localRotation = Quaternion.Euler(0f, twist, 0f);
                StripCollider(roof);
                roof.GetComponent<MeshRenderer>().sharedMaterial = trimMat;
            }

            // Emissive window bands on the lower shaft.
            if (h > 20f)
            {
                var windowMat = NewMaterial(new Color(0.9f, 0.85f, 0.6f), true);
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
        }

        private static void CreatePark(float x, float z, System.Random rnd)
        {
            var lawn = GameObject.CreatePrimitive(PrimitiveType.Cube);
            lawn.name = "Park";
            lawn.transform.position = new Vector3(x, 0.03f, z);
            lawn.transform.localScale = new Vector3(BlockSize - RoadWidth - 6f, 0.05f, BlockSize - RoadWidth - 6f);
            StripCollider(lawn);
            lawn.GetComponent<MeshRenderer>().sharedMaterial =
                NewMaterial(new Color(0.18f, 0.42f, 0.20f));

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
                NewMaterial(new Color(0.30f, 0.20f, 0.12f));

            var crown = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            crown.name = "TreeCrown";
            crown.transform.position = new Vector3(x, scale * 1.35f, z);
            crown.transform.localScale = Vector3.one * scale * 1.15f;
            StripCollider(crown);
            crown.GetComponent<MeshRenderer>().sharedMaterial =
                NewMaterial(new Color(0.16f, 0.42f, 0.18f));
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

        internal static Material NewMaterial(Color color, bool emissive = false)
        {
            var mat = new Material(FindUsableShader()) { color = color };
            if (emissive && mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", color * 1.6f);
            }
            return mat;
        }
    }
}