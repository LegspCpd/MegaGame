using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Megame.Data;
using Megame.Vehicles;
using Megame.Systems;
using Megame.World;
using Megame.Core;
using Megame.Controllers;
using Megame.UI;

namespace Megame.Editor
{
    /// <summary>
    /// Creates a comprehensive test scene with all vehicle types
    /// </summary>
    public class TestSceneGenerator : EditorWindow
    {
        private VehicleDatabase vehicleDatabase;
        private bool createTraffic = true;
        public bool createModShop = true;
        public bool createGarage = true;
        public bool createPoliceScene = true;
        public bool createRaceTrack = true;
        public bool createOffroadCourse = true;
        public bool createEmergencyServices = true;
        
        [MenuItem("MegaGame/Tools/Test Scene Generator")]
        public static void ShowWindow()
        {
            GetWindow<TestSceneGenerator>("Test Scene Generator");
        }
        
        private void OnGUI()
        {
            GUILayout.Label("MegaGame Test Scene Generator", EditorStyles.boldLabel);
            EditorGUILayout.Space();
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Vehicle Database:", GUILayout.Width(120));
            vehicleDatabase = (VehicleDatabase)EditorGUILayout.ObjectField(vehicleDatabase, typeof(VehicleDatabase), false);
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space();
            
            createTraffic = EditorGUILayout.Toggle("Create Traffic System", createTraffic);
            createModShop = EditorGUILayout.Toggle("Create Mod Shop", createModShop);
            createGarage = EditorGUILayout.Toggle("Create Garage", createGarage);
            createPoliceScene = EditorGUILayout.Toggle("Create Police Scene", createPoliceScene);
            createRaceTrack = EditorGUILayout.Toggle("Create Race Track", createRaceTrack);
            createOffroadCourse = EditorGUILayout.Toggle("Create Offroad Course", createOffroadCourse);
            createEmergencyServices = EditorGUILayout.Toggle("Create Emergency Services", createEmergencyServices);
            
            EditorGUILayout.Space();
            
            if (GUILayout.Button("Generate Test Scene", GUILayout.Height(40)))
            {
                GenerateTestScene();
            }
            
            if (GUILayout.Button("Generate Vehicle Showroom", GUILayout.Height(30)))
            {
                GenerateShowroom();
            }
            
            if (GUILayout.Button("Generate Police Fleet", GUILayout.Height(30)))
            {
                GeneratePoliceFleet();
            }
        }
        
        private void GenerateTestScene()
        {
            if (vehicleDatabase == null)
            {
                EditorUtility.DisplayDialog("Error", "Please assign a VehicleDatabase!", "OK");
                return;
            }
            
            // Create new scene
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            
            // Setup basic scene
            SetupBasicScene();
            
            // Create traffic system
            if (createTraffic)
            {
                CreateTrafficSystem();
            }
            
            // Create mod shop
            if (createModShop)
            {
                CreateModShop();
            }
            
            // Create garage
            if (createGarage)
            {
                CreateGarage();
            }
            
            // Create police scene
            if (createPoliceScene)
            {
                CreatePoliceScene();
            }
            
            // Create race track
            if (createRaceTrack)
            {
                CreateRaceTrack();
            }
            
            // Create offroad course
            if (createOffroadCourse)
            {
                CreateOffroadCourse();
            }
            
            // Create emergency services
            if (createEmergencyServices)
            {
                CreateEmergencyServicesScene();
            }
            
            // Save scene
            string scenePath = "Assets/Scenes/TestScene.unity";
            Directory.CreateDirectory(Path.GetDirectoryName(scenePath));
            EditorSceneManager.SaveScene(scene, scenePath);
            
            EditorUtility.DisplayDialog("Success", $"Test scene generated at {scenePath}", "OK");
        }
        
        private void SetupBasicScene()
        {
            // Lighting
            var lightGO = new GameObject("Sun");
            var light = lightGO.AddComponent<Light>();
            light.type = UnityEngine.LightType.Directional;
            light.intensity = 1.2f;
            light.color = new Color(1f, 0.95f, 0.85f);
            light.transform.rotation = Quaternion.Euler(50, -30, 0);
            light.shadows = LightShadows.Soft;
            
            // Skybox
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;
            RenderSettings.ambientIntensity = 1f;
            
            // Ground plane
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(100, 1, 100);
            var groundMat = new Material(Shader.Find("Standard"));
            groundMat.color = new Color(0.3f, 0.35f, 0.3f);
            ground.GetComponent<Renderer>().material = groundMat;
            
            // Player start
            var playerStart = new GameObject("PlayerStart");
            playerStart.transform.position = new Vector3(0, 1, 0);
            playerStart.tag = "Respawn";
            
            // Camera
            var camGO = new GameObject("Main Camera");
            camGO.tag = "MainCamera";
            var cam = camGO.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.Skybox;
            var camController = camGO.AddComponent<CameraController>();
            
            // Audio listener
            camGO.AddComponent<AudioListener>();
        }
        
        private void CreateTrafficSystem()
        {
            var trafficGO = new GameObject("TrafficSystem");
            var traffic = trafficGO.AddComponent<TrafficFlowSystem>();
            
            // Create road network
            var roadNetworkGO = new GameObject("RoadNetwork");
            roadNetworkGO.transform.SetParent(trafficGO.transform);
            
            // Create a simple circular road for testing
            CreateTestRoadNetwork(roadNetworkGO);
            
            // Create traffic config
            var config = ScriptableObject.CreateInstance<TrafficFlowConfig>();
            config.baseMaxVehicles = 30;
            config.initialSpawnCount = 15;
            AssetDatabase.CreateAsset(config, "Assets/Resources/TrafficFlowConfig.asset");
            traffic.config = config;
        }
        
        private void CreateTestRoadNetwork(GameObject parent)
        {
            // Create a figure-8 track for traffic testing
            int segments = 20;
            float radius = 100f;
            float roadWidth = 12f;
            
            for (int i = 0; i < segments; i++)
            {
                float angle = (i / (float)segments) * Mathf.PI * 2;
                float nextAngle = ((i + 1) / (float)segments) * Mathf.PI * 2;
                
                Vector3 center = new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
                Vector3 nextCenter = new Vector3(Mathf.Cos(nextAngle) * radius, 0, Mathf.Sin(nextAngle) * radius);
                
                var segmentGO = new GameObject($"RoadSegment_{i}");
                segmentGO.transform.SetParent(parent.transform);
                
                // Create road mesh
                var meshFilter = segmentGO.AddComponent<MeshFilter>();
                var meshRenderer = segmentGO.AddComponent<MeshRenderer>();
                
                // Simple road material
                var mat = new Material(Shader.Find("Standard"));
                mat.color = new Color(0.2f, 0.2f, 0.2f);
                meshRenderer.material = mat;
                
                // Generate road mesh
                var mesh = GenerateRoadMesh(center, nextCenter, roadWidth);
                meshFilter.mesh = mesh;
                
                // Add road segment component
                var roadSeg = segmentGO.AddComponent<RoadSegment>();
                roadSeg.segmentId = $"segment_{i}";
                roadSeg.speedLimit = 80f;
                roadSeg.laneTransforms = new Transform[2];
                
                // Create lane markers
                for (int lane = 0; lane < 2; lane++)
                {
                    float offset = (lane - 0.5f) * (roadWidth / 2f);
                    var laneGO = new GameObject($"Lane_{lane}");
                    laneGO.transform.SetParent(segmentGO.transform);
                    laneGO.transform.position = center;
                    laneGO.transform.rotation = Quaternion.LookRotation(nextCenter - center);
                    laneGO.transform.Translate(offset, 0.05f, 0);
                    roadSeg.laneTransforms[lane] = laneGO.transform;
                }
            }
        }
        
        private Mesh GenerateRoadMesh(Vector3 start, Vector3 end, float width)
        {
            var mesh = new Mesh();
            
            Vector3 dir = (end - start).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, dir).normalized;
            
            Vector3[] vertices = new Vector3[4];
            vertices[0] = start - right * (width / 2f);
            vertices[1] = start + right * (width / 2f);
            vertices[2] = end - right * (width / 2f);
            vertices[3] = end + right * (width / 2f);
            
            int[] triangles = { 0, 2, 1, 1, 2, 3 };
            Vector2[] uvs = { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
            Vector3[] normals = { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.uv = uvs;
            mesh.normals = normals;
            mesh.RecalculateBounds();
            
            return mesh;
        }
        
        private void CreateModShop()
        {
            var shopGO = new GameObject("ModShop");
            shopGO.transform.position = new Vector3(200, 0, 0);
            
            var shop = shopGO.AddComponent<ModShopUI>();
            
            // Create shop building placeholder
            var building = GameObject.CreatePrimitive(PrimitiveType.Cube);
            building.name = "ModShopBuilding";
            building.transform.SetParent(shopGO.transform);
            building.transform.localPosition = Vector3.up * 5;
            building.transform.localScale = new Vector3(30, 10, 40);
            
            var mat = new Material(Shader.Find("Standard"));
            mat.color = new Color(0.1f, 0.1f, 0.15f);
            building.GetComponent<Renderer>().material = mat;
            
            // Sign
            var sign = new GameObject("ModShopSign");
            sign.transform.SetParent(shopGO.transform);
            sign.transform.position = new Vector3(0, 15, 20);
            var signText = sign.AddComponent<TextMesh>();
            signText.text = "LOS SANTOS CUSTOMS";
            signText.fontSize = 64;
            signText.color = Color.green;
            signText.anchor = TextAnchor.MiddleCenter;
        }
        
        private void CreateGarage()
        {
            var garageGO = new GameObject("PlayerGarage");
            garageGO.transform.position = new Vector3(-200, 0, 0);
            
            var garage = garageGO.AddComponent<VehiclePersistenceSystem>();
            
            // Garage building
            var building = GameObject.CreatePrimitive(PrimitiveType.Cube);
            building.name = "GarageBuilding";
            building.transform.SetParent(garageGO.transform);
            building.transform.localPosition = Vector3.up * 4;
            building.transform.localScale = new Vector3(25, 8, 40);
            
            var mat = new Material(Shader.Find("Standard"));
            mat.color = new Color(0.2f, 0.2f, 0.2f);
            building.GetComponent<Renderer>().material = mat;
            
            // Garage doors
            for (int i = 0; i < 4; i++)
            {
                var door = GameObject.CreatePrimitive(PrimitiveType.Cube);
                door.name = $"GarageDoor_{i}";
                door.transform.SetParent(garageGO.transform);
                door.transform.localPosition = new Vector3((i - 1.5f) * 6, 2, 20.5f);
                door.transform.localScale = new Vector3(4, 4, 0.5f);
                door.GetComponent<Renderer>().material = new Material(Shader.Find("Standard")) { color = Color.gray };
                
                var spawnPoint = door.AddComponent<GarageSpawnPoint>();
                spawnPoint.spawnName = $"Bay {i + 1}";
                spawnPoint.isDefault = (i == 0);
            }
        }
        
        private void CreatePoliceScene()
        {
            var policeGO = new GameObject("PoliceStation");
            policeGO.transform.position = new Vector3(0, 0, 300);
            
            // Station building
            var building = GameObject.CreatePrimitive(PrimitiveType.Cube);
            building.name = "PoliceStationBuilding";
            building.transform.SetParent(policeGO.transform);
            building.transform.localPosition = Vector3.up * 6;
            building.transform.localScale = new Vector3(40, 12, 30);
            building.GetComponent<Renderer>().material = new Material(Shader.Find("Standard")) { color = new Color(0.1f, 0.1f, 0.3f) };
            
            // Police vehicles spawn
            var spawnArea = new GameObject("PoliceVehicleSpawns");
            spawnArea.transform.SetParent(policeGO.transform);
            
            for (int i = 0; i < 8; i++)
            {
                var spawn = new GameObject($"PoliceSpawn_{i}");
                spawn.transform.SetParent(spawnArea.transform);
                spawn.transform.localPosition = new Vector3((i % 4) * 10 - 15, 0, (i / 4) * 10 - 5);
                
                var spawnPoint = spawn.AddComponent<GarageSpawnPoint>();
                spawnPoint.spawnName = $"Police Bay {i + 1}";
            }
            
            // Impound lot
            var impound = new GameObject("ImpoundLot");
            impound.transform.SetParent(policeGO.transform);
            impound.transform.localPosition = new Vector3(0, 0, -50);
            var impoundGround = GameObject.CreatePrimitive(PrimitiveType.Plane);
            impoundGround.transform.SetParent(impound.transform);
            impoundGround.transform.localScale = new Vector3(5, 1, 5);
        }
        
        private void CreateRaceTrack()
        {
            var trackGO = new GameObject("RaceTrack");
            trackGO.transform.position = new Vector3(400, 0, 0);
            
            // Create oval track
            int segments = 30;
            float majorRadius = 150f;
            float minorRadius = 80f;
            float trackWidth = 20f;
            
            for (int i = 0; i < segments; i++)
            {
                float t = i / (float)segments;
                float angle = t * Mathf.PI * 2;
                
                // Ellipse parametric
                float x = Mathf.Cos(angle) * majorRadius;
                float z = Mathf.Sin(angle) * minorRadius;
                Vector3 pos = new Vector3(x, 0, z);
                
                float nextAngle = ((i + 1) / (float)segments) * Mathf.PI * 2;
                float nextX = Mathf.Cos(nextAngle) * majorRadius;
                float nextZ = Mathf.Sin(nextAngle) * minorRadius;
                Vector3 nextPos = new Vector3(nextX, 0, nextZ);
                
                Vector3 dir = (nextPos - pos).normalized;
                Vector3 right = Vector3.Cross(Vector3.up, dir);
                
                var segmentGO = new GameObject($"TrackSegment_{i}");
                segmentGO.transform.SetParent(trackGO.transform);
                
                var meshFilter = segmentGO.AddComponent<MeshFilter>();
                var meshRenderer = segmentGO.AddComponent<MeshRenderer>();
                
                var mat = new Material(Shader.Find("Standard"));
                mat.color = new Color(0.15f, 0.15f, 0.15f);
                meshRenderer.material = mat;
                
                var mesh = GenerateRoadMesh(pos, nextPos, trackWidth);
                meshFilter.mesh = mesh;
                
                // Add racing line markers
                var line = new GameObject("RacingLine");
                line.transform.SetParent(segmentGO.transform);
                var lr = line.AddComponent<LineRenderer>();
                lr.startWidth = 0.3f;
                lr.endWidth = 0.3f;
                lr.material = new Material(Shader.Find("Sprites/Default")) { color = Color.white };
                lr.positionCount = 2;
                lr.SetPosition(0, pos + right * 2);
                lr.SetPosition(1, nextPos + right * 2);
            }
            
            // Start/Finish line
            var startFinish = new GameObject("StartFinish");
            startFinish.transform.SetParent(trackGO.transform);
            startFinish.transform.position = new Vector3(majorRadius, 0.1f, 0);
            var sfLine = startFinish.AddComponent<LineRenderer>();
            sfLine.startWidth = 1f;
            sfLine.endWidth = 1f;
            sfLine.material = new Material(Shader.Find("Sprites/Default")) { color = Color.white };
            sfLine.positionCount = 2;
            sfLine.SetPosition(0, new Vector3(majorRadius, 0.1f, -trackWidth/2));
            sfLine.SetPosition(1, new Vector3(majorRadius, 0.1f, trackWidth/2));
            
            // Grandstands
            CreateGrandstands(trackGO, majorRadius, minorRadius);
        }
        
        private void CreateGrandstands(GameObject parent, float majorR, float minorR)
        {
            for (int i = 0; i < 8; i++)
            {
                float angle = i * Mathf.PI / 4;
                float x = Mathf.Cos(angle) * (majorR + 50);
                float z = Mathf.Sin(angle) * (minorR + 50);
                
                var stand = GameObject.CreatePrimitive(PrimitiveType.Cube);
                stand.name = $"Grandstand_{i}";
                stand.transform.SetParent(parent.transform);
                stand.transform.position = new Vector3(x, 5, z);
                stand.transform.rotation = Quaternion.Euler(0, angle * Mathf.Rad2Deg + 90, 0);
                stand.transform.localScale = new Vector3(30, 10, 15);
                stand.GetComponent<Renderer>().material = new Material(Shader.Find("Standard")) { color = new Color(0.4f, 0.35f, 0.3f) };
            }
        }
        
        private void CreateOffroadCourse()
        {
            var offroadGO = new GameObject("OffroadCourse");
            offroadGO.transform.position = new Vector3(-400, 0, -400);
            
            // Create terrain variations
            int size = 20;
            for (int x = -size; x <= size; x++)
            {
                for (int z = -size; z <= size; z++)
                {
                    if (Random.value < 0.3f) continue; // Sparse obstacles
                    
                    var obstacleType = Random.Range(0, 4);
                    GameObject obstacle = null;
                    
                    switch (obstacleType)
                    {
                        case 0: // Rock
                            obstacle = CreateRock();
                            break;
                        case 1: // Log
                            obstacle = CreateLog();
                            break;
                        case 2: // Hill
                            obstacle = CreateHill();
                            break;
                        case 3: // Water pit
                            obstacle = CreateWaterPit();
                            break;
                    }
                    
                    if (obstacle != null)
                    {
                        obstacle.transform.SetParent(offroadGO.transform);
                        obstacle.transform.position = new Vector3(
                            x * 20 + Random.Range(-5f, 5f),
                            0,
                            z * 20 + Random.Range(-5f, 5f)
                        );
                        obstacle.transform.rotation = Quaternion.Euler(0, Random.Range(0, 360), 0);
                    }
                }
            }
            
            // Create mud patches
            for (int i = 0; i < 20; i++)
            {
                var mud = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                mud.name = $"MudPatch_{i}";
                mud.transform.SetParent(offroadGO.transform);
                mud.transform.position = new Vector3(
                    Random.Range(-200f, 200f),
                    0.01f,
                    Random.Range(-200f, 200f)
                );
                mud.transform.localScale = new Vector3(Random.Range(5f, 15f), 0.1f, Random.Range(5f, 15f));
                var mat = new Material(Shader.Find("Standard"));
                mat.color = new Color(0.3f, 0.2f, 0.1f);
                mud.GetComponent<Renderer>().material = mat;
            }
        }
        
        private GameObject CreateRock()
        {
            var rock = new GameObject("Rock");
            var meshFilter = rock.AddComponent<MeshFilter>();
            var meshRenderer = rock.AddComponent<MeshRenderer>();
            
            // Create low-poly rock mesh
            var mesh = new Mesh();
            int verts = 12;
            Vector3[] vertices = new Vector3[verts];
            for (int i = 0; i < verts; i++)
            {
                float angle = (i / (float)verts) * Mathf.PI * 2;
                float r = Random.Range(1f, 3f);
                float y = Random.Range(-1f, 2f);
                vertices[i] = new Vector3(Mathf.Cos(angle) * r, y, Mathf.Sin(angle) * r);
            }
            
            int[] triangles = new int[(verts - 2) * 3];
            for (int i = 0; i < verts - 2; i++)
            {
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = i + 1;
                triangles[i * 3 + 2] = i + 2;
            }
            
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            meshFilter.mesh = mesh;
            
            var mat = new Material(Shader.Find("Standard"));
            mat.color = new Color(0.4f, 0.35f, 0.3f);
            meshRenderer.material = mat;
            
            rock.AddComponent<MeshCollider>();
            return rock;
        }
        
        private GameObject CreateLog()
        {
            var log = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            log.name = "FallenLog";
            log.transform.localScale = new Vector3(1f, 5f, 1f);
            log.transform.rotation = Quaternion.Euler(0, 0, 90);
            var mat = new Material(Shader.Find("Standard"));
            mat.color = new Color(0.3f, 0.2f, 0.1f);
            log.GetComponent<Renderer>().material = mat;
            return log;
        }
        
        private GameObject CreateHill()
        {
            var hill = new GameObject("Hill");
            var terrain = hill.AddComponent<Terrain>();
            var terrainData = new TerrainData();
            terrainData.heightmapResolution = 33;
            terrainData.size = new Vector3(30, 10, 30);
            
            // Generate hill heightmap
            float[,] heights = new float[33, 33];
            for (int x = 0; x < 33; x++)
            {
                for (int z = 0; z < 33; z++)
                {
                    float dx = (x - 16) / 16f;
                    float dz = (z - 16) / 16f;
                    float dist = Mathf.Sqrt(dx * dx + dz * dz);
                    heights[x, z] = Mathf.Max(0, 1 - dist) * 0.5f;
                }
            }
            terrainData.SetHeights(0, 0, heights);
            terrain.terrainData = terrainData;
            
            return hill;
        }
        
        private GameObject CreateWaterPit()
        {
            var pit = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pit.name = "WaterPit";
            pit.transform.localScale = new Vector3(8f, 0.5f, 8f);
            pit.transform.position = new Vector3(0, -0.25f, 0);
            
            var waterMat = new Material(Shader.Find("Standard"));
            waterMat.color = new Color(0.1f, 0.2f, 0.4f, 0.8f);
            waterMat.SetFloat("_Mode", 3); // Transparent
            waterMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            waterMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            waterMat.SetInt("_ZWrite", 0);
            waterMat.DisableKeyword("_ALPHATEST_ON");
            waterMat.EnableKeyword("_ALPHABLEND_ON");
            waterMat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            waterMat.renderQueue = 3000;
            
            pit.GetComponent<Renderer>().material = waterMat;
            
            // Add water trigger
            var trigger = pit.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0, 0.5f, 0);
            trigger.size = new Vector3(16, 2, 16);
            
            return pit;
        }
        
        private void CreateEmergencyServicesScene()
        {
            var emergencyGO = new GameObject("EmergencyServices");
            emergencyGO.transform.position = new Vector3(300, 0, 300);
            
            // Fire Station
            var fireStation = new GameObject("FireStation");
            fireStation.transform.SetParent(emergencyGO.transform);
            
            var fsBuilding = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fsBuilding.name = "FireStationBuilding";
            fsBuilding.transform.SetParent(fireStation.transform);
            fsBuilding.transform.localPosition = Vector3.up * 6;
            fsBuilding.transform.localScale = new Vector3(30, 12, 40);
            fsBuilding.GetComponent<Renderer>().material = new Material(Shader.Find("Standard")) { color = new Color(0.6f, 0.1f, 0.1f) };
            
            // Fire truck bays
            for (int i = 0; i < 4; i++)
            {
                var bay = new GameObject($"FireBay_{i}");
                bay.transform.SetParent(fireStation.transform);
                bay.transform.localPosition = new Vector3((i - 1.5f) * 7, 0, 25);
                
                var spawn = bay.AddComponent<GarageSpawnPoint>();
                spawn.spawnName = $"Fire Bay {i + 1}";
            }
            
            // Hospital
            var hospital = new GameObject("Hospital");
            hospital.transform.SetParent(emergencyGO.transform);
            hospital.transform.localPosition = new Vector3(100, 0, 0);
            
            var hospBuilding = GameObject.CreatePrimitive(PrimitiveType.Cube);
            hospBuilding.name = "HospitalBuilding";
            hospBuilding.transform.SetParent(hospital.transform);
            hospBuilding.transform.localPosition = new Vector3(0, 8, 0);
            hospBuilding.transform.localScale = new Vector3(50, 16, 40);
            hospBuilding.GetComponent<Renderer>().material = new Material(Shader.Find("Standard")) { color = Color.white };
            
            // Helipad
            var helipad = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            helipad.name = "Helipad";
            helipad.transform.SetParent(hospital.transform);
            helipad.transform.localPosition = new Vector3(0, 8.5f, 30);
            helipad.transform.localScale = new Vector3(15, 0.2f, 15);
            var helipadMat = new Material(Shader.Find("Standard"));
            helipadMat.color = Color.red;
            helipad.GetComponent<Renderer>().material = helipadMat;
            
            // "H" marker
            var hMarker = new GameObject("HMarker");
            hMarker.transform.SetParent(helipad.transform);
            hMarker.transform.localPosition = Vector3.up * 0.15f;
            var hText = hMarker.AddComponent<TextMesh>();
            hText.text = "H";
            hText.fontSize = 200;
            hText.color = Color.white;
            hText.anchor = TextAnchor.MiddleCenter;
            hText.fontStyle = FontStyle.Bold;
        }
        
        private void GenerateShowroom()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            
            SetupBasicScene();
            
            if (vehicleDatabase == null) return;
            
            var showroomGO = new GameObject("VehicleShowroom");
            
            var vehicles = vehicleDatabase.vehicles.Where(v => v != null).ToArray();
            
            float spacing = 15f;
            int cols = 5;
            
            for (int i = 0; i < vehicles.Length; i++)
            {
                var v = vehicles[i];
                if (v == null || v.modelPrefab == null) continue;
                
                int row = i / cols;
                int col = i % cols;
                
                var instance = Instantiate(v.modelPrefab);
                instance.name = $"Showroom_{v.modelName}";
                instance.transform.SetParent(showroomGO.transform);
                instance.transform.position = new Vector3(col * spacing, 0, row * -spacing);
                instance.transform.rotation = Quaternion.Euler(0, 45, 0);
                
                // Disable physics
                var rb = instance.GetComponent<Rigidbody>();
                if (rb != null) rb.isKinematic = true;
                var colliders = instance.GetComponentsInChildren<Collider>();
                foreach (var c in colliders) c.enabled = false;
                
                // Add rotation script
                var rotator = instance.AddComponent<ShowroomRotator>();
                rotator.rotationSpeed = 10f;
                
                // Info plate
                var plate = new GameObject("InfoPlate");
                plate.transform.SetParent(instance.transform);
                plate.transform.localPosition = new Vector3(0, 3, -3);
                var text = plate.AddComponent<TextMesh>();
                text.text = v.displayName;
                text.fontSize = 32;
                text.color = Color.white;
                text.anchor = TextAnchor.MiddleCenter;
            }
            
            // Lighting
            var lightGO = new GameObject("ShowroomLights");
            for (int i = 0; i < 8; i++)
            {
                float angle = i * Mathf.PI / 4;
                var lightObj = new GameObject($"Spot_{i}");
                lightObj.transform.SetParent(lightGO.transform);
                lightObj.transform.position = new Vector3(Mathf.Cos(angle) * 50, 30, Mathf.Sin(angle) * 50);
                lightObj.transform.LookAt(Vector3.zero);
                
                var light = lightObj.AddComponent<Light>();
                light.type = UnityEngine.LightType.Spot;
                light.intensity = 50000;
                light.range = 100;
                light.spotAngle = 30;
                light.color = new Color(1, 0.95f, 0.9f);
            }
            
            string scenePath = "Assets/Scenes/VehicleShowroom.unity";
            Directory.CreateDirectory(Path.GetDirectoryName(scenePath));
            EditorSceneManager.SaveScene(scene, scenePath);
        }
        
        private void GeneratePoliceFleet()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            
            SetupBasicScene();
            
            if (vehicleDatabase == null) return;
            
            var fleetGO = new GameObject("PoliceFleet");
            
            var policeVehicles = vehicleDatabase.GetEmergencyVehicles()
                .Where(v => v.vehicleType == VehicleType.Police || v.vehicleType == VehicleType.Sheriff)
                .ToArray();
            
            float radius = 50f;
            for (int i = 0; i < policeVehicles.Length; i++)
            {
                var v = policeVehicles[i];
                if (v == null || v.modelPrefab == null) continue;
                
                float angle = (i / (float)policeVehicles.Length) * Mathf.PI * 2;
                Vector3 pos = new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
                
                var instance = Instantiate(v.modelPrefab);
                instance.name = $"Fleet_{v.modelName}";
                instance.transform.SetParent(fleetGO.transform);
                instance.transform.position = pos;
                instance.transform.LookAt(Vector3.zero);
                
                // Add police equipment
                var equip = instance.AddComponent<PoliceEquipmentSystem>();
                equip.ApplyLoadout(PoliceEquipmentSystem.GetPreset("LSPD"));
                
                // Enable emergency lights
                var dls = instance.GetComponent<DLSController>();
                if (dls != null) dls.SetActive(true);
                
                // Disable physics
                var rb = instance.GetComponent<Rigidbody>();
                if (rb != null) rb.isKinematic = true;
            }
            
            // Center display
            var centerDisplay = new GameObject("CenterDisplay");
            centerDisplay.transform.SetParent(fleetGO.transform);
            var plate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            plate.transform.SetParent(centerDisplay.transform);
            plate.transform.localScale = new Vector3(20, 0.5f, 10);
            plate.transform.position = Vector3.up * 0.25f;
            var plateMat = new Material(Shader.Find("Standard"));
            plateMat.color = Color.black;
            plate.GetComponent<Renderer>().material = plateMat;
            
            var textObj = new GameObject("FleetTitle");
            textObj.transform.SetParent(centerDisplay.transform);
            textObj.transform.position = Vector3.up * 2;
            var text = textObj.AddComponent<TextMesh>();
            text.text = "POLICE FLEET DISPLAY";
            text.fontSize = 100;
            text.color = Color.blue;
            text.anchor = TextAnchor.MiddleCenter;
            
            string scenePath = "Assets/Scenes/PoliceFleet.unity";
            Directory.CreateDirectory(Path.GetDirectoryName(scenePath));
            EditorSceneManager.SaveScene(scene, scenePath);
        }
        
        // Helper component for showroom
        public class ShowroomRotator : MonoBehaviour
        {
            public float rotationSpeed = 10f;
            
            private void Update()
            {
                transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime);
            }
        }
    }
}