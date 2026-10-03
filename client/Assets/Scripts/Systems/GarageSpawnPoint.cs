using UnityEngine;

namespace Megame.Systems
{
    /// <summary>
    /// Garage spawn point for player vehicle spawning
    /// </summary>
    public class GarageSpawnPoint : MonoBehaviour
    {
        [Header("Spawn Settings")]
        public string spawnName = "Garage Bay";
        public bool isDefault = false;
        public int bayIndex = 0;
        
        [Header("Vehicle Orientation")]
        public bool alignToTransform = true;
        public Vector3 positionOffset = Vector3.zero;
        public Vector3 rotationOffset = Vector3.zero;
        
        [Header("Restrictions")]
        public VehicleClass[] allowedClasses = new VehicleClass[0]; // Empty = all
        public VehicleType[] allowedTypes = new VehicleType[0]; // Empty = all
        public int maxVehicleLength = 0; // 0 = unlimited
        
        [Header("Visual")]
        public GameObject bayMarker;
        public Color occupiedColor = Color.red;
        public Color freeColor = Color.green;
        public Color reservedColor = Color.yellow;
        
        [Header("Effects")]
        public ParticleSystem spawnEffect;
        public AudioClip spawnSound;
        
        private bool isOccupied = false;
        private GameObject currentVehicle = null;
        
        private void Awake()
        {
            UpdateMarker();
        }
        
        private void OnValidate()
        {
            UpdateMarker();
        }
        
        private void UpdateMarker()
        {
            if (bayMarker != null)
            {
                var rend = bayMarker.GetComponent<Renderer>();
                if (rend != null)
                {
                    rend.material.color = isOccupied ? occupiedColor : freeColor;
                }
            }
        }
        
        public bool CanSpawn(VehicleDefinition def)
        {
            if (isOccupied) return false;
            
            if (allowedClasses.Length > 0 && !System.Array.Exists(allowedClasses, c => c == def.vehicleClass))
                return false;
            
            if (allowedTypes.Length > 0 && !System.Array.Exists(allowedTypes, t => t == def.vehicleType))
                return false;
            
            if (maxVehicleLength > 0)
            {
                // Check vehicle length - would need definition bounds
            }
            
            return true;
        }
        
        public GameObject SpawnVehicle(VehicleDefinition def, VehicleModifications mods, VehicleCondition condition, string plateText, int plateStyle)
        {
            if (!CanSpawn(def) || def.modelPrefab == null) return null;
            
            Vector3 spawnPos = transform.position + transform.TransformDirection(positionOffset);
            Quaternion spawnRot = transform.rotation * Quaternion.Euler(rotationOffset);
            
            // Check for obstructions
            if (Physics.CheckBox(spawnPos + Vector3.up, new Vector3(1.5f, 1f, 3f), spawnRot, LayerMask.GetMask("Vehicle", "Default")))
            {
                Debug.LogWarning($"Garage bay {spawnName} is obstructed!");
                return null;
            }
            
            var go = Instantiate(def.modelPrefab, spawnPos, spawnRot);
            go.name = $"Garage_{def.modelName}_{System.Guid.NewGuid().ToString().Substring(0, 8)}";
            
            var controller = go.GetComponent<OptimizedVehicleController>();
            if (controller == null) controller = go.AddComponent<OptimizedVehicleController>();
            
            controller.Initialize(def);
            controller.ApplyMods(mods);
            controller.currentCondition = condition.Clone();
            controller.currentPlateText = plateText;
            controller.currentPlateStyle = plateStyle;
            
            isOccupied = true;
            currentVehicle = go;
            UpdateMarker();
            
            // Effects
            if (spawnEffect != null)
            {
                var effect = Instantiate(spawnEffect, spawnPos, spawnRot);
                Destroy(effect.gameObject, 2f);
            }
            
            if (spawnSound != null)
            {
                AudioSource.PlayClipAtPoint(spawnSound, spawnPos);
            }
            
            return go;
        }
        
        public void ReleaseVehicle()
        {
            isOccupied = false;
            currentVehicle = null;
            UpdateMarker();
        }
        
        public void ForceRelease()
        {
            if (currentVehicle != null)
            {
                Destroy(currentVehicle);
            }
            ReleaseVehicle();
        }
        
        private void OnDrawGizmos()
        {
            Gizmos.color = isOccupied ? Color.red : Color.green;
            Gizmos.DrawWireCube(transform.position + Vector3.up, new Vector3(3.5f, 2f, 7f));
            Gizmos.DrawRay(transform.position, transform.forward * 5f);
            
            // Label
            #if UNITY_EDITOR
            UnityEditor.Handles.Label(transform.position + Vector3.up * 3f, spawnName);
            #endif
        }
        
        private void OnDrawGizmosSelected()
        {
            // Show obstruction check area
            Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
            Gizmos.DrawCube(transform.position + transform.TransformDirection(positionOffset) + Vector3.up, new Vector3(3f, 2f, 6f));
        }
    }
}