using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;
using TMPro;
using Megame.Controllers;
using Megame.Data;
using Megame.Client;
using Megame.Vehicles;

namespace Megame.Systems
{
    /// <summary>
    /// Road Network - defines the road system for traffic AI
    /// </summary>
    public class RoadNetwork : MonoBehaviour
    {
        public static RoadNetwork Instance { get; private set; }
        
        [Header("Road Segments")]
        public List<RoadSegment> segments = new List<RoadSegment>();
        public List<RoadIntersection> intersections = new List<RoadIntersection>();
        
        [Header("Spawn Points")]
        public List<RoadSpawnPoint> spawnPoints = new List<RoadSpawnPoint>();
        
        [Header("Debug")]
        public bool showGizmos = true;
        
        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
            
            // Auto-find segments if empty
            if (segments.Count == 0)
            {
                segments.AddRange(GetComponentsInChildren<RoadSegment>());
            }
            
            if (intersections.Count == 0)
            {
                intersections.AddRange(GetComponentsInChildren<RoadIntersection>());
            }
            
            if (spawnPoints.Count == 0)
            {
                spawnPoints.AddRange(GetComponentsInChildren<RoadSpawnPoint>());
            }
        }
        
        public RoadSpawnPoint GetRandomSpawnPoint()
        {
            var validPoints = spawnPoints.FindAll(p => p.IsValid());
            if (validPoints.Count == 0) return null;
            
            return validPoints[Random.Range(0, validPoints.Count)];
        }
        
        public RoadSpawnPoint GetNearestSpawnPoint(Vector3 position, float maxDistance = 100f)
        {
            RoadSpawnPoint nearest = null;
            float nearestDist = maxDistance;
            
            foreach (var point in spawnPoints)
            {
                if (!point.IsValid()) continue;
                
                float dist = Vector3.Distance(point.transform.position, position);
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearest = point;
                }
            }
            
            return nearest;
        }
        
        public List<RoadSpawnPoint> GetSpawnPointsInRegion(string regionId)
        {
            return spawnPoints.FindAll(p => p.region == regionId && p.IsValid());
        }
        
        public RoadSegment GetSegmentAt(Vector3 position, float maxDistance = 10f)
        {
            RoadSegment nearest = null;
            float nearestDist = maxDistance;
            
            foreach (var segment in segments)
            {
                float dist = segment.GetDistanceTo(position);
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearest = segment;
                }
            }
            
            return nearest;
        }
        
        public List<RoadSegment> GetConnectedSegments(RoadSegment segment)
        {
            var connected = new List<RoadSegment>();
            
            foreach (var intersection in intersections)
            {
                if (intersection.HasSegment(segment))
                {
                    connected.AddRange(intersection.GetConnectedSegments(segment));
                }
            }
            
            return connected;
        }
        
        private void OnDrawGizmos()
        {
            if (!showGizmos) return;
            
            Gizmos.color = Color.gray;
            foreach (var segment in segments)
            {
                if (segment != null)
                {
                    segment.DrawGizmos();
                }
            }
            
            Gizmos.color = Color.yellow;
            foreach (var intersection in intersections)
            {
                if (intersection != null)
                {
                    Gizmos.DrawWireSphere(intersection.transform.position, 5f);
                }
            }
            
            Gizmos.color = Color.green;
            foreach (var point in spawnPoints)
            {
                if (point != null && point.IsValid())
                {
                    Gizmos.DrawWireSphere(point.transform.position, 1f);
                    Gizmos.DrawRay(point.transform.position, point.transform.forward * 3f);
                }
            }
        }
    }
    
    // ============================================================================
    // ROAD SEGMENT
    // ============================================================================
    
    public class RoadSegment : MonoBehaviour
    {
        [Header("Identity")]
        public string segmentId = "";
        public string region = "";
        public RoadKind roadType = RoadKind.Major;
        
        [Header("Geometry")]
        public Transform[] laneTransforms = new Transform[0]; // Lane centerlines
        public float speedLimit = 50f; // km/h
        public int laneCount = 2;
        public float laneWidth = 3.5f;
        public bool oneWay = true;
        
        [Header("Spline")]
        public bool useSpline = false;
        public Transform[] splinePoints = new Transform[0];
        
        [Header("Connections")]
        public RoadSegment[] connectedSegments = new RoadSegment[0];
        
        private void OnValidate()
        {
            if (string.IsNullOrEmpty(segmentId))
            {
                segmentId = name;
            }
        }
        
        public RoadSpawnPoint GetRandomSpawnPoint()
        {
            if (laneTransforms.Length == 0) return null;
            
            var lane = laneTransforms[Random.Range(0, laneTransforms.Length)];
            float t = Random.value;
            
            Vector3 pos, rot;
            if (useSpline && splinePoints.Length >= 2)
            {
                pos = GetSplinePosition(t);
                rot = GetSplineRotation(t);
            }
            else
            {
                // Linear interpolation between first and last lane transform
                Transform start = laneTransforms[0];
                Transform end = laneTransforms[laneTransforms.Length - 1];
                pos = Vector3.Lerp(start.position, end.position, t);
                rot = Quaternion.Slerp(start.rotation, end.rotation, t);
            }
            
            return new RoadSpawnPoint
            {
                position = pos,
                rotation = Quaternion.Euler(0, rot.eulerAngles.y, 0),
                roadSegment = this,
                lane = System.Array.IndexOf(laneTransforms, lane),
                region = region,
            };
        }
        
        public float GetDistanceTo(Vector3 position)
        {
            if (laneTransforms.Length == 0) return float.MaxValue;
            
            float minDist = float.MaxValue;
            foreach (var lane in laneTransforms)
            {
                if (lane != null)
                {
                    float dist = Vector3.Distance(lane.position, position);
                    minDist = Mathf.Min(minDist, dist);
                }
            }
            return minDist;
        }
        
        public Vector3 GetClosestPointOnRoad(Vector3 position)
        {
            if (laneTransforms.Length == 0) return transform.position;
            
            Vector3 closest = laneTransforms[0].position;
            float minDist = float.MaxValue;
            
            foreach (var lane in laneTransforms)
            {
                if (lane != null)
                {
                    float dist = Vector3.Distance(lane.position, position);
                    if (dist < minDist)
                    {
                        minDist = dist;
                        closest = lane.position;
                    }
                }
            }
            
            return closest;
        }
        
        public Vector3 GetDirectionAt(Vector3 position)
        {
            if (laneTransforms.Length == 0) return transform.forward;
            
            // Find nearest lane point and return its forward
            Transform nearest = null;
            float minDist = float.MaxValue;
            
            foreach (var lane in laneTransforms)
            {
                if (lane != null)
                {
                    float dist = Vector3.Distance(lane.position, position);
                    if (dist < minDist)
                    {
                        minDist = dist;
                        nearest = lane;
                    }
                }
            }
            
            return nearest != null ? nearest.forward : transform.forward;
        }
        
        private Vector3 GetSplinePosition(float t)
        {
            if (splinePoints.Length < 2) return transform.position;
            
            // Simple Catmull-Rom spline
            int numPoints = splinePoints.Length;
            float scaledT = t * (numPoints - 1);
            int index = Mathf.FloorToInt(scaledT);
            t = scaledT - index;
            
            index = Mathf.Clamp(index, 0, numPoints - 2);
            
            Vector3 p0 = index > 0 ? splinePoints[index - 1].position : splinePoints[index].position;
            Vector3 p1 = splinePoints[index].position;
            Vector3 p2 = splinePoints[index + 1].position;
            Vector3 p3 = index + 2 < numPoints ? splinePoints[index + 2].position : splinePoints[numPoints - 1].position;
            
            return CatmullRom(p0, p1, p2, p3, t);
        }
        
        private Quaternion GetSplineRotation(float t)
        {
            if (splinePoints.Length < 2) return transform.rotation;
            
            float scaledT = t * (splinePoints.Length - 1);
            int index = Mathf.FloorToInt(scaledT);
            t = scaledT - index;
            
            index = Mathf.Clamp(index, 0, splinePoints.Length - 2);
            
            Vector3 dir = (splinePoints[index + 1].position - splinePoints[index].position).normalized;
            return Quaternion.LookRotation(dir);
        }
        
        private Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;
            
            return 0.5f * (
                (2f * p1) +
                (-p0 + p2) * t +
                (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
                (-p0 + 3f * p1 - 3f * p2 + p3) * t3
            );
        }
        
        private void OnDrawGizmos()
        {
            // Draw lane centerlines
            Gizmos.color = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            
            if (laneTransforms.Length >= 2)
            {
                for (int i = 0; i < laneTransforms.Length - 1; i++)
                {
                    if (laneTransforms[i] != null && laneTransforms[i + 1] != null)
                    {
                        Gizmos.DrawLine(laneTransforms[i].position, laneTransforms[i + 1].position);
                    }
                }
            }
            
            // Draw spline
            if (useSpline && splinePoints.Length >= 2)
            {
                Gizmos.color = Color.cyan;
                Vector3 prev = GetSplinePosition(0);
                for (int i = 1; i <= 20; i++)
                {
                    float t = i / 20f;
                    Vector3 curr = GetSplinePosition(t);
                    Gizmos.DrawLine(prev, curr);
                    prev = curr;
                }
            }
        }
    }
    
    // ============================================================================
    // ROAD INTERSECTION
    // ============================================================================
    
    public class RoadIntersection : MonoBehaviour
    {
        [Header("Identity")]
        public string intersectionId = "";
        
        [Header("Connected Segments")]
        public RoadSegment[] incomingSegments = new RoadSegment[0];
        public RoadSegment[] outgoingSegments = new RoadSegment[0];
        
        [Header("Traffic Light")]
        public bool hasTrafficLight = false;
        public TrafficLightController trafficLight;
        
        [Header("Navigation")]
        public float turnRadius = 10f;
        public float intersectionSize = 20f;
        
        private void OnValidate()
        {
            if (string.IsNullOrEmpty(intersectionId))
            {
                intersectionId = name;
            }
        }
        
        public bool HasSegment(RoadSegment segment)
        {
            return System.Array.Exists(incomingSegments, s => s == segment) ||
                   System.Array.Exists(outgoingSegments, s => s == segment);
        }
        
        public List<RoadSegment> GetConnectedSegments(RoadSegment fromSegment)
        {
            var connected = new List<RoadSegment>();
            
            // Can go to any outgoing segment except the one we came from
            foreach (var outgoing in outgoingSegments)
            {
                if (outgoing != fromSegment)
                {
                    connected.Add(outgoing);
                }
            }
            
            // Can also go back on incoming (U-turn)
            foreach (var incoming in incomingSegments)
            {
                if (incoming != fromSegment)
                {
                    connected.Add(incoming);
                }
            }
            
            return connected;
        }
        
        public RoadSegment GetRandomOutgoing(RoadSegment fromSegment)
        {
            var connected = GetConnectedSegments(fromSegment);
            if (connected.Count == 0) return null;
            
            return connected[Random.Range(0, connected.Count)];
        }
        
        private void OnDrawGizmos()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, intersectionSize * 0.5f);
            
            Gizmos.color = Color.green;
            foreach (var seg in incomingSegments)
            {
                if (seg != null)
                    Gizmos.DrawLine(transform.position, seg.transform.position);
            }
            
            Gizmos.color = Color.red;
            foreach (var seg in outgoingSegments)
            {
                if (seg != null)
                    Gizmos.DrawLine(transform.position, seg.transform.position);
            }
        }
    }
    
    // ============================================================================
    // TRAFFIC LIGHT CONTROLLER
    // ============================================================================
    
    public class TrafficLightController : MonoBehaviour
    {
        [Header("Lights")]
        public Light[] redLights;
        public Light[] yellowLights;
        public Light[] greenLights;
        
        [Header("Timing")]
        public float cycleTime = 60f;
        public float greenTime = 25f;
        public float yellowTime = 5f;
        public float redTime = 30f;
        
        [Header("Offset")]
        public float phaseOffset = 0f; // For coordination
        
        private float timer;
        private LightPhase currentPhase = LightPhase.Red;
        
        public enum LightPhase
        {
            Green,
            Yellow,
            Red,
        }
        
        private void Start()
        {
            timer = phaseOffset;
            SetPhase(LightPhase.Red);
        }
        
        private void Update()
        {
            timer += Time.deltaTime;
            
            float phaseTime = GetCurrentPhaseTime();
            if (timer >= phaseTime)
            {
                timer = 0f;
                AdvancePhase();
            }
        }
        
        private float GetCurrentPhaseTime()
        {
            return currentPhase switch
            {
                LightPhase.Green => greenTime,
                LightPhase.Yellow => yellowTime,
                LightPhase.Red => redTime,
                _ => redTime,
            };
        }
        
        private void AdvancePhase()
        {
            currentPhase = currentPhase switch
            {
                LightPhase.Green => LightPhase.Yellow,
                LightPhase.Yellow => LightPhase.Red,
                LightPhase.Red => LightPhase.Green,
                _ => LightPhase.Red,
            };
            
            SetPhase(currentPhase);
        }
        
        private void SetPhase(LightPhase phase)
        {
            currentPhase = phase;
            
            foreach (var light in redLights) light.enabled = (phase == LightPhase.Red);
            foreach (var light in yellowLights) light.enabled = (phase == LightPhase.Yellow);
            foreach (var light in greenLights) light.enabled = (phase == LightPhase.Green);
        }
        
        public bool CanProceed(Vector3 approachDirection)
        {
            // Check if light is green for this approach
            // Simplified - would check which lanes this approach uses
            return currentPhase == LightPhase.Green;
        }
    }
    
    // ============================================================================
    // ROAD SPAWN POINT
    // ============================================================================
    
    public class RoadSpawnPoint : MonoBehaviour
    {
        [Header("Identity")]
        public string spawnId = "";
        public string region = "";
        public RoadSegment roadSegment;
        public int lane = 0;
        
        [Header("Properties")]
        public VehicleClass[] allowedClasses = new VehicleClass[0];
        public VehicleType[] allowedTypes = new VehicleType[0];
        public bool isEmergencyOnly = false;
        public float spawnWeight = 1f;
        
        [Header("Validation")]
        public float minClearanceRadius = 3f;
        public LayerMask obstructionMask = -1;
        
        private void OnValidate()
        {
            if (string.IsNullOrEmpty(spawnId))
            {
                spawnId = name;
            }
        }
        
        public bool IsValid()
        {
            // Check for obstructions
            if (Physics.CheckSphere(transform.position + Vector3.up, minClearanceRadius, obstructionMask))
            {
                return false;
            }
            return true;
        }
        
        private void OnDrawGizmos()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, 1f);
            Gizmos.DrawRay(transform.position, transform.forward * 5f);
            
            #if UNITY_EDITOR
            UnityEditor.Handles.Label(transform.position + Vector3.up * 2f, spawnId);
            #endif
        }
    }
    
    // ============================================================================
    // ROAD TYPES
    // ============================================================================
    
    public enum RoadKind
    {
        Highway,
        Major,
        Minor,
        Service,
        Alley,
        Offroad,
        Ramp,
        Bridge,
        Tunnel,
    }
}
