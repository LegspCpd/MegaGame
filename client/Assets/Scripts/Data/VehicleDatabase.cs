using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace Megame.Data
{
    /// <summary>
    /// Vehicle definition ScriptableObject - matches pipeline JSON output
    /// </summary>
    [CreateAssetMenu(fileName = "VehicleDefinition", menuName = "MegaGame/Vehicle Definition")]
    public class VehicleDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string modelName;
        public string displayName;
        public string manufacturer;
        public VehicleClass vehicleClass;
        public VehicleType vehicleType;
        
        [Header("Dimensions")]
        public float length = 4.5f;
        public float width = 1.8f;
        public float height = 1.4f;
        public float wheelbase = 2.7f;
        public float trackWidthFront = 1.5f;
        public float trackWidthRear = 1.5f;
        public float groundClearance = 0.15f;
        
        [Header("Physics")]
        public float mass = 1500f;
        public float dragCoefficient = 0.32f;
        public Vector3 centerOfMass = new Vector3(0, -0.3f, 0);
        public Vector3 inertiaMultiplier = Vector3.one;
        
        [Header("Engine")]
        public float maxSpeed = 200f; // km/h
        public float maxRPM = 7000f;
        public float idleRPM = 800f;
        public float maxTorque = 400f;
        public AnimationCurve torqueCurve = AnimationCurve.Linear(0, 0.3f, 1, 0.5f);
        public float[] gearRatios = { 3.5f, 2.2f, 1.5f, 1.1f, 0.9f, 0.7f };
        public float finalDriveRatio = 3.7f;
        public float driveBiasFront = 0f; // 0=RWD, 0.5=AWD, 1=FWD
        
        [Header("Steering")]
        public float maxSteerAngle = 35f;
        public float steerSpeed = 180f; // deg/s
        public AnimationCurve steerAngleBySpeed = AnimationCurve.Linear(0, 1, 50, 0.5f);
        
        [Header("Braking")]
        public float brakeForce = 3000f;
        public float handbrakeForce = 5000f;
        
        [Header("Suspension")]
        public float suspensionForce = 30000f;
        public float suspensionDamper = 5000f;
        public float suspensionTravel = 0.2f;
        public float springLength = 0.3f;
        
        [Header("Aero")]
        public float downforceCoefficient = 0.1f;
        
        [Header("Wheels")]
        public float wheelRadiusFront = 0.35f;
        public float wheelRadiusRear = 0.35f;
        public float wheelWidthFront = 0.25f;
        public float wheelWidthRear = 0.25f;
        
        [Header("Audio")]
        public string engineAudioHash = "generic";
        public string hornAudioHash = "generic";
        
        [Header("Mods")]
        public int modKitId = 0;
        public bool hasLivery = false;
        
        [Header("Emergency")]
        public bool hasSiren = false;
        public string sirenAudioHash = "";
        public string lightPattern = "";
        public string elsConfig = "";
        
        [Header("References")]
        public GameObject modelPrefab;
        public Material[] materials;
        public AudioClip engineSound;
        public AudioClip hornSound;
        public AudioClip sirenSound;
    }

    public enum VehicleClass
    {
        Compact,
        Sedan,
        SUV,
        Coupe,
        Muscle,
        SportsClassic,
        Sports,
        Super,
        Motorcycle,
        Offroad,
        Industrial,
        Utility,
        Vans,
        Cycles,
        Boats,
        Helicopters,
        Planes,
        Service,
        Emergency,
        Military,
        Commercial,
        Trains,
        Truck,
        Van
    }

    public enum VehicleType
    {
        Civilian,
        Police,
        Fire,
        EMS,
        Sheriff,
        StateTrooper,
        Federal,
        ParkRanger,
        Military,
        Taxi,
        Commercial
    }

    /// <summary>
    /// Vehicle database - loads all definitions from Resources
    /// </summary>
    public class VehicleDatabase : ScriptableObject
    {
        public static VehicleDatabase Instance { get; private set; }
        
        public VehicleDefinition[] vehicles;
        
        private Dictionary<string, VehicleDefinition> _lookup;
        
        private void OnEnable()
        {
            Instance = this;
            Initialize();
        }
        
        public void Initialize()
        {
            _lookup = vehicles.ToDictionary(v => v.modelName, v => v);
        }
        
        public VehicleDefinition Get(string modelName)
        {
            if (_lookup == null) Initialize();
            _lookup.TryGetValue(modelName, out var def);
            return def;
        }
        
        public VehicleDefinition[] GetByClass(VehicleClass cls)
        {
            if (_lookup == null) Initialize();
            return vehicles.Where(v => v.vehicleClass == cls).ToArray();
        }
        
        public VehicleDefinition[] GetByType(VehicleType type)
        {
            if (_lookup == null) Initialize();
            return vehicles.Where(v => v.vehicleType == type).ToArray();
        }
        
        public VehicleDefinition[] GetEmergencyVehicles()
        {
            if (_lookup == null) Initialize();
            return vehicles.Where(v => v.vehicleType != VehicleType.Civilian).ToArray();
        }
        
        public VehicleDefinition[] GetCivilianVehicles()
        {
            if (_lookup == null) Initialize();
            return vehicles.Where(v => v.vehicleType == VehicleType.Civilian).ToArray();
        }
        
        public VehicleDefinition[] GetModdableVehicles()
        {
            if (_lookup == null) Initialize();
            return vehicles.Where(v => v.modKitId > 0).ToArray();
        }
    }
    
    /// <summary>
    /// Vehicle mod kit definition
    /// </summary>
    [CreateAssetMenu(fileName = "VehicleModKitDefinition", menuName = "MegaGame/Vehicle Mod Kit (Data)")]
    public class VehicleModKitDefinition : ScriptableObject
    {
        public string modKitName;
        public int modKitId;
        public VehicleClass[] compatibleClasses;
        
        [Header("Engine")]
        public ModOption[] engineOptions;
        public ModOption[] turboOptions;
        public ModOption[] transmissionOptions;
        
        [Header("Suspension")]
        public ModOption[] suspensionOptions;
        public ModOption[] brakeOptions;
        
        [Header("Body")]
        public ModOption[] bodyKitOptions;
        public ModOption[] spoilerOptions;
        public ModOption[] hoodOptions;
        public ModOption[] exhaustOptions;
        public ModOption[] grilleOptions;
        public ModOption[] skirtOptions;
        public ModOption[] fenderOptions;
        public ModOption[] roofOptions;
        
        [Header("Wheels")]
        public ModOption[] wheelTypeOptions;
        public WheelColorOption[] wheelColorOptions;
        public ModOption[] tireSmokeOptions;
        public ModOption[] bulletproofTireOptions;
        
        [Header("Paint")]
        public PaintType[] paintTypes;
        public Color[] pearlescentColors;
        public Color[] wheelColors;
        public ModOption[] liveryOptions;
        public ModOption[] windowTintOptions;
        
        [Header("Lights")]
        public ModOption[] xenonOptions;
        public XenonColor[] xenonColors;
        public ModOption[] neonOptions;
        
        [Header("Plates")]
        public ModOption[] plateStyleOptions;
    }
    
    [System.Serializable]
    public class ModOption
    {
        public string name;
        public int modValue; // 0 = stock, 1+ = variants
        public float priceMultiplier = 1f;
        public string unlockRequirement = "";
        public GameObject visualPrefab; // For body kits, spoilers, etc.
    }
    
    [System.Serializable]
    public class WheelColorOption
    {
        public string name;
        public Color color;
        public int modValue;
    }
    
    [System.Serializable]
    public class PaintType
    {
        public string name;
        public int typeId; // 0=normal, 1=metallic, 2=pearl, 3=matte, 4=metal, 5=chrome
        public float priceMultiplier = 1f;
    }
    
    [System.Serializable]
    public class XenonColor
    {
        public string name;
        public Color color;
        public int colorId; // 0-12
    }

    /// <summary>
    /// Runtime vehicle instance with applied mods
    /// </summary>
    [System.Serializable]
    public class VehicleInstance
    {
        public string modelName;
        public VehicleDefinition definition;
        public VehicleModificationsDefinition mods = new VehicleModificationsDefinition();
        public VehicleConditionDefinition condition = new VehicleConditionDefinition();
        public int fuel = 100;
        public int engineHealth = 1000;
        public int bodyHealth = 1000;
        public string plateText = "";
        public int plateStyle = 0;
        
        public void ApplyMods(VehicleModKitDefinition kit)
        {
            // Apply modifications to vehicle controller
        }
        
        public float GetModifiedMaxSpeed()
        {
            float mult = 1f;
            if (mods.engineLevel > 0) mult += mods.engineLevel * 0.15f;
            if (mods.turboLevel > 0) mult *= 1.3f;
            if (mods.transmissionLevel > 0) mult += mods.transmissionLevel * 0.05f;
            return definition.maxSpeed * mult;
        }
        
        public float GetModifiedTorque()
        {
            float mult = 1f;
            if (mods.engineLevel > 0) mult += mods.engineLevel * 0.15f;
            if (mods.turboLevel > 0) mult *= 1.3f;
            return definition.maxTorque * mult;
        }
        
        public float GetModifiedBrakeForce()
        {
            float mult = 1f + mods.brakesLevel * 0.2f;
            return definition.brakeForce * mult;
        }
    }
    
    [System.Serializable]
    public class VehicleModificationsDefinition
    {
        // Performance
        public int engineLevel = 0;      // 0-4
        public int turboLevel = 0;       // 0-1
        public int transmissionLevel = 0; // 0-3
        public int suspensionLevel = 0;  // 0-4
        public float suspensionHeight = 0f; // -1 to 1
        public int brakesLevel = 0;      // 0-3
        public int armorLevel = 0;       // 0-4
        
        // Body
        public int bodyKit = 0;
        public int spoiler = 0;
        public int hood = 0;
        public int roof = 0;
        public int grille = 0;
        public int exhaust = 0;
        public int skirt = 0;
        public int fender = 0;
        
        // Wheels
        public int wheelType = 0;
        public int wheelVariant = 0;
        public Color tireSmokeColor = Color.white;
        public bool bulletproofTires = false;
        public bool customTires = false;
        
        // Appearance
        public Color primaryColor = Color.white;
        public Color secondaryColor = Color.white;
        public Color pearlescentColor = Color.white;
        public Color wheelColor = Color.white;
        public int paintType = 0;
        public string livery = "";
        public int windowTint = 0;
        
        // Lights
        public bool xenonLights = false;
        public int xenonColor = 0;
        public bool neonEnabled = false;
        public Color neonColor = Color.white;
        
        // Plates
        public int plateStyle = 0;
    }
    
    [System.Serializable]
    public class VehicleConditionDefinition
    {
        public float durability = 1f;
        public float dirt = 0f;
        public float carbon = 0f;
        public bool isJammed = false;
        public uint roundsFired = 0;
    }
}
