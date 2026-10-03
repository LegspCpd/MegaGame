using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace Megame.Vehicles
{
    /// <summary>
    /// Police Vehicle Equipment System
    /// Handles push bars, cages, gun racks, laptop mounts, radar, etc.
    /// </summary>
    public class PoliceEquipmentSystem : MonoBehaviour
    {
        [Header("Equipment Slots")]
        public Transform pushBarMount;
        public Transform grilleGuardMount;
        public Transform roofRackMount;
        public Transform partitionMount;
        public Transform gunRackMount;
        public Transform laptopMount;
        public Transform radarMount;
        public Transform spotlightMountLeft;
        public Transform spotlightMountRight;
        public Transform sirenSpeakerMount;
        public Transform[] antennaMounts;
        
        [Header("Available Equipment")]
        public PoliceEquipmentDatabase equipmentDatabase;
        
        [Header("Current Loadout")]
        public PoliceLoadout currentLoadout = new PoliceLoadout();
        
        private Dictionary<EquipmentSlot, GameObject> equippedItems = new Dictionary<EquipmentSlot, GameObject>();
        
        public enum EquipmentSlot
        {
            PushBar,
            GrilleGuard,
            RoofRack,
            Partition,
            GunRack,
            LaptopMount,
            RadarUnit,
            SpotlightLeft,
            SpotlightRight,
            SirenSpeaker,
            Antenna,
            // Interior
            PrisonerSeat,
            K9Unit,
            FireExtinguisher,
            FirstAidKit,
            // Trunk
            SpikeStrip,
            TrafficCones,
            BarrierTape,
            // External
            SideSpotlights,
            AlleyLights,
            TakedownLights,
        }
        
        private void Awake()
        {
            if (equipmentDatabase == null)
            {
                equipmentDatabase = Resources.Load<PoliceEquipmentDatabase>("PoliceEquipmentDatabase");
            }
        }
        
        public void ApplyLoadout(PoliceLoadout loadout)
        {
            currentLoadout = loadout;
            
            // Clear existing
            ClearAllEquipment();
            
            // Apply each equipment piece
            if (loadout.pushBar != null) Equip(EquipmentSlot.PushBar, loadout.pushBar);
            if (loadout.grilleGuard != null) Equip(EquipmentSlot.GrilleGuard, loadout.grilleGuard);
            if (loadout.roofRack != null) Equip(EquipmentSlot.RoofRack, loadout.roofRack);
            if (loadout.partition != null) Equip(EquipmentSlot.Partition, loadout.partition);
            if (loadout.gunRack != null) Equip(EquipmentSlot.GunRack, loadout.gunRack);
            if (loadout.laptopMount != null) Equip(EquipmentSlot.LaptopMount, loadout.laptopMount);
            if (loadout.radarUnit != null) Equip(EquipmentSlot.RadarUnit, loadout.radarUnit);
            if (loadout.spotlightLeft != null) Equip(EquipmentSlot.SpotlightLeft, loadout.spotlightLeft);
            if (loadout.spotlightRight != null) Equip(EquipmentSlot.SpotlightRight, loadout.spotlightRight);
            if (loadout.sirenSpeaker != null) Equip(EquipmentSlot.SirenSpeaker, loadout.sirenSpeaker);
            
            foreach (var antenna in loadout.antennas)
            {
                Equip(EquipmentSlot.Antenna, antenna);
            }
        }
        
        public void Equip(EquipmentSlot slot, PoliceEquipment equipment)
        {
            if (equipment == null) return;
            
            Transform mount = GetMountPoint(slot);
            if (mount == null)
            {
                Debug.LogWarning($"No mount point for slot {slot}");
                return;
            }
            
            // Remove existing
            if (equippedItems.ContainsKey(slot))
            {
                DestroyImmediate(equippedItems[slot]);
            }
            
            // Instantiate equipment
            var instance = Instantiate(equipment.prefab, mount);
            instance.transform.localPosition = equipment.localPosition;
            instance.transform.localRotation = Quaternion.Euler(equipment.localRotation);
            instance.transform.localScale = equipment.localScale;
            
            // Configure equipment
            var equipmentComponent = instance.GetComponent<PoliceEquipmentBehaviour>();
            if (equipmentComponent != null)
            {
                equipmentComponent.Initialize(equipment);
            }
            
            // Special handling for specific equipment
            ConfigureEquipment(slot, instance, equipment);
            
            equippedItems[slot] = instance;
        }
        
        private void ConfigureEquipment(EquipmentSlot slot, GameObject instance, PoliceEquipment equipment)
        {
            switch (slot)
            {
                case EquipmentSlot.PushBar:
                    // Add collision modifier to vehicle
                    var pushBar = instance.GetComponent<PushBar>();
                    if (pushBar == null) pushBar = instance.AddComponent<PushBar>();
                    pushBar.impactMultiplier = equipment.pushBarImpactMultiplier;
                    pushBar.damageReduction = equipment.pushBarDamageReduction;
                    break;
                    
                case EquipmentSlot.SpotlightLeft:
                case EquipmentSlot.SpotlightRight:
                    var spotlight = instance.GetComponent<Light>();
                    if (spotlight == null) spotlight = instance.AddComponent<Light>();
                    spotlight.type = LightType.Spot;
                    spotlight.color = equipment.spotlightColor;
                    spotlight.intensity = equipment.spotlightIntensity;
                    spotlight.range = equipment.spotlightRange;
                    spotlight.spotAngle = equipment.spotlightAngle;
                    break;
                    
                case EquipmentSlot.LaptopMount:
                    var laptop = instance.GetComponent<PoliceLaptop>();
                    if (laptop == null) laptop = instance.AddComponent<PoliceLaptop>();
                    laptop.database = equipmentDatabase;
                    break;
                    
                case EquipmentSlot.RadarUnit:
                    var radar = instance.GetComponent<PoliceRadar>();
                    if (radar == null) radar = instance.AddComponent<PoliceRadar>();
                    radar.mode = equipment.radarMode;
                    break;
                    
                case EquipmentSlot.SirenSpeaker:
                    var speaker = instance.GetComponent<AudioSource>();
                    if (speaker == null) speaker = instance.AddComponent<AudioSource>();
                    speaker.spatialBlend = 1f;
                    speaker.volume = equipment.sirenVolume;
                    // speaker.clip = equipment.sirenTone;
                    break;
            }
        }
        
        private Transform GetMountPoint(EquipmentSlot slot)
        {
            switch (slot)
            {
                case EquipmentSlot.PushBar: return pushBarMount;
                case EquipmentSlot.GrilleGuard: return grilleGuardMount;
                case EquipmentSlot.RoofRack: return roofRackMount;
                case EquipmentSlot.Partition: return partitionMount;
                case EquipmentSlot.GunRack: return gunRackMount;
                case EquipmentSlot.LaptopMount: return laptopMount;
                case EquipmentSlot.RadarUnit: return radarMount;
                case EquipmentSlot.SpotlightLeft: return spotlightMountLeft;
                case EquipmentSlot.SpotlightRight: return spotlightMountRight;
                case EquipmentSlot.SirenSpeaker: return sirenSpeakerMount;
                case EquipmentSlot.Antenna: return antennaMounts?.Length > 0 ? antennaMounts[0] : transform;
                default: return transform;
            }
        }
        
        public void ClearAllEquipment()
        {
            foreach (var item in equippedItems.Values)
            {
                if (item != null) DestroyImmediate(item);
            }
            equippedItems.Clear();
        }
        
        public GameObject GetEquipped(EquipmentSlot slot)
        {
            equippedItems.TryGetValue(slot, out var item);
            return item;
        }
        
        public bool HasEquipment(EquipmentSlot slot)
        {
            return equippedItems.ContainsKey(slot);
        }
    }
    
    /// <summary>
    /// Police equipment definition
    /// </summary>
    [System.Serializable]
    public class PoliceEquipment
    {
        public string id;
        public string displayName;
        public EquipmentCategory category;
        public GameObject prefab;
        
        // Transform
        public Vector3 localPosition = Vector3.zero;
        public Vector3 localRotation = Vector3.zero;
        public Vector3 localScale = Vector3.one;
        
        // Push bar specific
        public float pushBarImpactMultiplier = 1.5f;
        public float pushBarDamageReduction = 0.3f;
        
        // Spotlight specific
        public Color spotlightColor = Color.white;
        public float spotlightIntensity = 50000f;
        public float spotlightRange = 200f;
        public float spotlightAngle = 30f;
        
        // Radar specific
        public RadarMode radarMode = RadarMode.Front;
        
        // Siren specific
        public float sirenVolume = 1f;
        // public AudioClip sirenTone;
        
        // General
        public float weight = 0f;
        public int cost = 0;
        public string requiredAgency = "";
        public int requiredRank = 0;
    }
    
    public enum EquipmentCategory
    {
        Exterior,
        Interior,
        Lighting,
        Electronics,
        Weapons,
        Storage,
        Tactical,
    }
    
    public enum RadarMode
    {
        Front,
        Rear,
        Dual,
        Fastest,
        Strongest,
    }
    
    /// <summary>
    /// Complete police vehicle loadout
    /// </summary>
    [System.Serializable]
    public class PoliceLoadout
    {
        public string loadoutName = "Standard Patrol";
        public string agency = "LSPD";
        
        // Exterior
        public PoliceEquipment pushBar;
        public PoliceEquipment grilleGuard;
        public PoliceEquipment roofRack;
        public PoliceEquipment[] antennas = new PoliceEquipment[0];
        
        // Lighting
        public PoliceEquipment spotlightLeft;
        public PoliceEquipment spotlightRight;
        public PoliceEquipment sirenSpeaker;
        
        // Interior
        public PoliceEquipment partition;
        public PoliceEquipment gunRack;
        public PoliceEquipment laptopMount;
        public PoliceEquipment radarUnit;
        
        // Weapons
        public PoliceEquipment[] gunRackWeapons = new PoliceEquipment[0];
        
        // Trunk
        public PoliceEquipment[] trunkEquipment = new PoliceEquipment[0];
        
        // Agency presets
        public static PoliceLoadout GetPreset(string agency)
        {
            return agency switch
            {
                "LSPD" => new PoliceLoadout
                {
                    loadoutName = "LSPD Standard Patrol",
                    agency = "LSPD",
                },
                "LAPD" => new PoliceLoadout
                {
                    loadoutName = "LAPD Standard Patrol",
                    agency = "LAPD",
                },
                "BCSO" => new PoliceLoadout
                {
                    loadoutName = "BCSO Desert Patrol",
                    agency = "BCSO",
                },
                "Sheriff" => new PoliceLoadout
                {
                    loadoutName = "Sheriff Standard",
                    agency = "Sheriff",
                },
                "FBI" => new PoliceLoadout
                {
                    loadoutName = "FBI Unmarked",
                    agency = "FBI",
                },
                "NOOSE" => new PoliceLoadout
                {
                    loadoutName = "NOOSE Tactical",
                    agency = "NOOSE",
                },
                _ => new PoliceLoadout { loadoutName = "Generic", agency = "Police" }
            };
        }
    }
    
    /// <summary>
    /// Equipment database for police vehicles
    /// </summary>
    [CreateAssetMenu(fileName = "PoliceEquipmentDatabase", menuName = "MegaGame/Police Equipment Database")]
    public class PoliceEquipmentDatabase : ScriptableObject
    {
        public PoliceEquipment[] pushBars;
        public PoliceEquipment[] grilleGuards;
        public PoliceEquipment[] roofRacks;
        public PoliceEquipment[] partitions;
        public PoliceEquipment[] gunRacks;
        public PoliceEquipment[] laptopMounts;
        public PoliceEquipment[] radarUnits;
        public PoliceEquipment[] spotlights;
        public PoliceEquipment[] sirenSpeakers;
        public PoliceEquipment[] antennas;
        public PoliceEquipment[] prisonerSeats;
        public PoliceEquipment[] k9Units;
        public PoliceEquipment[] trunkEquipment;
        
        public PoliceEquipment GetEquipment(string id)
        {
            var all = pushBars.Concat(grilleGuards).Concat(roofRacks)
                .Concat(partitions).Concat(gunRacks).Concat(laptopMounts)
                .Concat(radarUnits).Concat(spotlights).Concat(sirenSpeakers)
                .Concat(antennas).Concat(prisonerSeats).Concat(k9Units)
                .Concat(trunkEquipment);
            return all.FirstOrDefault(e => e != null && e.id == id);
        }
        
        public PoliceEquipment[] GetByCategory(EquipmentCategory category)
        {
            return category switch
            {
                EquipmentCategory.Exterior => pushBars.Concat(grilleGuards).Concat(roofRacks).ToArray(),
                EquipmentCategory.Interior => partitions.Concat(gunRacks).Concat(laptopMounts).ToArray(),
                EquipmentCategory.Lighting => spotlights.Concat(sirenSpeakers).ToArray(),
                EquipmentCategory.Electronics => radarUnits.ToArray(),
                EquipmentCategory.Weapons => gunRacks.ToArray(),
                EquipmentCategory.Storage => trunkEquipment.ToArray(),
                _ => new PoliceEquipment[0]
            };
        }
    }
    
    /// <summary>
    /// Push bar behavior - modifies collision physics
    /// </summary>
    public class PushBar : MonoBehaviour
    {
        public float impactMultiplier = 1.5f;
        public float damageReduction = 0.3f;
        
        private void OnCollisionEnter(Collision collision)
        {
            var otherVehicle = collision.gameObject.GetComponentInParent<OptimizedVehicleController>();
            if (otherVehicle != null)
            {
                // Increase impact force
                var contact = collision.contacts[0];
                var force = contact.normal * impactMultiplier * 1000f;
                otherVehicle.GetComponent<Rigidbody>()?.AddForceAtPosition(force, contact.point, ForceMode.Impulse);
            }
        }
    }
    
    /// <summary>
    /// Police laptop/MDT (Mobile Data Terminal)
    /// </summary>
    public class PoliceLaptop : MonoBehaviour
    {
        public PoliceEquipmentDatabase database;
        public bool isActive = false;
        
        [Header("UI")]
        public GameObject laptopUI;
        public Canvas laptopCanvas;
        
        public void Toggle()
        {
            isActive = !isActive;
            if (laptopCanvas != null) laptopCanvas.enabled = isActive;
        }
        
        public void RunPlate(string plate)
        {
            // Query database for vehicle registration
            Debug.Log($"Running plate: {plate}");
        }
        
        public void RunLicense(string license)
        {
            Debug.Log($"Running license: {license}");
        }
        
        public void ViewWarrants(string name)
        {
            Debug.Log($"Viewing warrants for: {name}");
        }
        
        public void WriteReport(string type)
        {
            Debug.Log($"Writing report: {type}");
        }
    }
    
    /// <summary>
    /// Police radar unit
    /// </summary>
    public class PoliceRadar : MonoBehaviour
    {
        public RadarMode mode = RadarMode.Dual;
        public float maxRange = 2000f;
        public float updateRate = 0.1f;
        public bool isActive = false;
        
        [Header("Display")]
        public TextMesh targetSpeedText;
        public TextMesh patrolSpeedText;
        public TextMesh modeText;
        
        private float patrolSpeed;
        private float targetSpeed;
        private VehicleController targetVehicle;
        private float updateTimer;
        
        public void SetMode(RadarMode newMode)
        {
            mode = newMode;
            if (modeText != null) modeText.text = $"MODE: {mode}";
        }
        
        public void Toggle()
        {
            isActive = !isActive;
        }
        
        private void Update()
        {
            if (!isActive) return;
            
            updateTimer += Time.deltaTime;
            if (updateTimer < 0.1f) return;
            updateTimer = 0f;
            
            // Update patrol speed
            var ownVehicle = GetComponentInParent<OptimizedVehicleController>();
            if (ownVehicle != null)
            {
                patrolSpeed = ownVehicle.SpeedKPH;
                if (patrolSpeedText != null) patrolSpeedText.text = $"PATROL: {patrolSpeed:F0} km/h";
            }
            
            // Scan for targets based on mode
            ScanForTargets();
        }
        
        private void ScanForTargets()
        {
            float maxDist = 0;
            VehicleController bestTarget = null;
            float bestSpeed = 0;
            
            var allVehicles = FindObjectsOfType<OptimizedVehicleController>();
            foreach (var v in allVehicles)
            {
                if (v == GetComponentInParent<OptimizedVehicleController>()) continue;
                
                float dist = Vector3.Distance(transform.position, v.transform.position);
                if (dist > 200f) continue; // Max radar range
                
                bool inFront = Vector3.Dot(transform.forward, (v.transform.position - transform.position).normalized) > 0.5f;
                bool inRear = Vector3.Dot(-transform.forward, (v.transform.position - transform.position).normalized) > 0.5f;
                
                bool validTarget = false;
                switch (mode)
                {
                    case RadarMode.Front: validTarget = inFront; break;
                    case RadarMode.Rear: validTarget = inRear; break;
                    case RadarMode.Dual: validTarget = inFront || inRear; break;
                    case RadarMode.Fastest: validTarget = true; break;
                    case RadarMode.Strongest: validTarget = dist < maxDist; break;
                }
                
                if (validTarget)
                {
                    float relativeSpeed = Mathf.Abs(v.SpeedKPH - patrolSpeed);
                    if (mode == RadarMode.Fastest)
                    {
                        if (relativeSpeed > bestSpeed)
                        {
                            bestSpeed = relativeSpeed;
                            bestTarget = v;
                        }
                    }
                    else if (mode == RadarMode.Strongest)
                    {
                        if (dist < maxDist)
                        {
                            maxDist = dist;
                            bestTarget = v;
                        }
                    }
                    else
                    {
                        if (relativeSpeed > bestSpeed)
                        {
                            bestSpeed = relativeSpeed;
                            bestTarget = v;
                        }
                    }
                }
            }
            
            if (bestTarget != null)
            {
                targetSpeed = bestTarget.SpeedKPH;
                targetVehicle = bestTarget;
            }
            
            if (targetSpeedText != null)
            {
                targetSpeedText.text = $"TARGET: {targetSpeed:F0} km/h";
            }
        }
    }
    
    /// <summary>
    /// Police partition/cage
    /// </summary>
    public class PrisonerPartition : MonoBehaviour
    {
        public bool isLocked = true;
        public GameObject doorLeft;
        public GameObject doorRight;
        
        public void Unlock()
        {
            isLocked = false;
            if (doorLeft != null) doorLeft.GetComponent<Animator>()?.SetTrigger("Open");
            if (doorRight != null) doorRight.GetComponent<Animator>()?.SetTrigger("Open");
        }
        
        public void Lock()
        {
            isLocked = true;
            if (doorLeft != null) doorLeft.GetComponent<Animator>()?.SetTrigger("Close");
            if (doorRight != null) doorRight.GetComponent<Animator>()?.SetTrigger("Close");
        }
    }
    
    /// <summary>
    /// Gun rack with weapon storage
    /// </summary>
    public class GunRack : MonoBehaviour
    {
        public Transform[] weaponSlots;
        public GameObject[] storedWeapons;
        public bool isLocked = true;
        
        public bool StoreWeapon(GameObject weaponPrefab, int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= weaponSlots.Length) return false;
            if (storedWeapons[slotIndex] != null) return false;
            
            var weapon = Instantiate(weaponPrefab, weaponSlots[slotIndex]);
            weapon.transform.localPosition = Vector3.zero;
            weapon.transform.localRotation = Quaternion.identity;
            storedWeapons[slotIndex] = weapon;
            
            // Disable physics on stored weapon
            var rb = weapon.GetComponent<Rigidbody>();
            if (rb != null) rb.isKinematic = true;
            var col = weapon.GetComponent<Collider>();
            if (col != null) col.enabled = false;
            
            return true;
        }
        
        public GameObject RetrieveWeapon(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= weaponSlots.Length) return null;
            if (storedWeapons[slotIndex] == null) return null;
            
            var weapon = storedWeapons[slotIndex];
            storedWeapons[slotIndex] = null;
            
            // Re-enable physics
            var rb = weapon.GetComponent<Rigidbody>();
            if (rb != null) rb.isKinematic = false;
            var col = weapon.GetComponent<Collider>();
            if (col != null) col.enabled = true;
            
            return weapon;
        }
    }
}