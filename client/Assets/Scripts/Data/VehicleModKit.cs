using UnityEngine;
using System.Collections.Generic;
using Megame.Data;

namespace Megame.Vehicles
{
    /// <summary>
    /// Vehicle Mod Kit - defines available modifications for a vehicle class
    /// </summary>
    [CreateAssetMenu(fileName = "VehicleModKit", menuName = "MegaGame/Vehicle Mod Kit")]
    public class VehicleModKit : ScriptableObject
    {
        [Header("Identity")]
        public string kitName = "Standard Mod Kit";
        public int kitId = 0;
        public VehicleClass[] compatibleClasses = { VehicleClass.Sedan, VehicleClass.SUV, VehicleClass.Coupe, VehicleClass.Muscle, VehicleClass.Sports };
        public string[] compatibleModels = new string[0]; // Empty = all models in class

        [Header("Pricing")]
        public float globalPriceMultiplier = 1f;
        public AnimationCurve priceByLevel = AnimationCurve.Linear(0, 1, 4, 3);

        // ============================================================================
        // PERFORMANCE MODS
        // ============================================================================

        [Header("Engine")]
        public ModOption[] engineOptions = new ModOption[]
        {
            new ModOption { name = "Stock Engine", modValue = 0, priceMultiplier = 1f },
            new ModOption { name = "Level 1: ECU Tune", modValue = 1, priceMultiplier = 1.2f },
            new ModOption { name = "Level 2: Intake + Exhaust", modValue = 2, priceMultiplier = 1.5f },
            new ModOption { name = "Level 3: Camshafts + Pistons", modValue = 3, priceMultiplier = 2f },
            new ModOption { name = "Level 4: Forced Induction Ready", modValue = 4, priceMultiplier = 3f },
        };

        [Header("Turbo")]
        public ModOption[] turboOptions = new ModOption[]
        {
            new ModOption { name = "No Turbo", modValue = 0, priceMultiplier = 1f },
            new ModOption { name = "Turbocharger Kit", modValue = 1, priceMultiplier = 3f },
        };

        [Header("Transmission")]
        public ModOption[] transmissionOptions = new ModOption[]
        {
            new ModOption { name = "Stock Transmission", modValue = 0, priceMultiplier = 1f },
            new ModOption { name = "Level 1: Short Shifter", modValue = 1, priceMultiplier = 1.2f },
            new ModOption { name = "Level 2: Close Ratio Gears", modValue = 2, priceMultiplier = 1.5f },
            new ModOption { name = "Level 3: Sequential Gearbox", modValue = 3, priceMultiplier = 3f },
        };

        [Header("Suspension")]
        public ModOption[] suspensionOptions = new ModOption[]
        {
            new ModOption { name = "Stock Suspension", modValue = 0, priceMultiplier = 1f },
            new ModOption { name = "Level 1: Lowering Springs", modValue = 1, priceMultiplier = 1.2f },
            new ModOption { name = "Level 2: Coilovers", modValue = 2, priceMultiplier = 1.5f },
            new ModOption { name = "Level 3: Adjustable Arms", modValue = 3, priceMultiplier = 2f },
            new ModOption { name = "Level 4: Air Suspension", modValue = 4, priceMultiplier = 3f },
        };

        [Header("Brakes")]
        public ModOption[] brakeOptions = new ModOption[]
        {
            new ModOption { name = "Stock Brakes", modValue = 0, priceMultiplier = 1f },
            new ModOption { name = "Level 1: Sport Pads", modValue = 1, priceMultiplier = 1.2f },
            new ModOption { name = "Level 2: Slotted Rotors", modValue = 2, priceMultiplier = 1.5f },
            new ModOption { name = "Level 3: Big Brake Kit", modValue = 3, priceMultiplier = 2.5f },
        };

        // ============================================================================
        // BODY MODS
        // ============================================================================

        [Header("Body Kits")]
        public ModOption[] bodyKitOptions = new ModOption[0];

        [Header("Spoilers")]
        public ModOption[] spoilerOptions = new ModOption[]
        {
            new ModOption { name = "No Spoiler", modValue = 0, priceMultiplier = 1f },
            new ModOption { name = "Lip Spoiler", modValue = 1, priceMultiplier = 1.2f, visualPrefab = null },
            new ModOption { name = "Mid Spoiler", modValue = 2, priceMultiplier = 1.3f, visualPrefab = null },
            new ModOption { name = "Large Wing", modValue = 3, priceMultiplier = 1.5f, visualPrefab = null },
            new ModOption { name = "GT Wing", modValue = 4, priceMultiplier = 2f, visualPrefab = null },
        };

        [Header("Hoods")]
        public ModOption[] hoodOptions = new ModOption[0];

        [Header("Exhausts")]
        public ModOption[] exhaustOptions = new ModOption[]
        {
            new ModOption { name = "Stock Exhaust", modValue = 0, priceMultiplier = 1f },
            new ModOption { name = "Cat-Back", modValue = 1, priceMultiplier = 1.3f, visualPrefab = null },
            new ModOption { name = "Axle-Back", modValue = 2, priceMultiplier = 1.2f, visualPrefab = null },
            new ModOption { name = "Titanium Race", modValue = 3, priceMultiplier = 2f, visualPrefab = null },
        };

        [Header("Grilles")]
        public ModOption[] grilleOptions = new ModOption[0];

        [Header("Skirts")]
        public ModOption[] skirtOptions = new ModOption[0];

        [Header("Fenders")]
        public ModOption[] fenderOptions = new ModOption[0];

        [Header("Roofs")]
        public ModOption[] roofOptions = new ModOption[0];

        // ============================================================================
        // WHEELS
        // ============================================================================

        [Header("Wheel Types")]
        public ModOption[] wheelTypeOptions = new ModOption[]
        {
            new ModOption { name = "Stock Wheels", modValue = 0, priceMultiplier = 1f },
            new ModOption { name = "Sport", modValue = 1, priceMultiplier = 1.5f },
            new ModOption { name = "Muscle", modValue = 2, priceMultiplier = 1.5f },
            new ModOption { name = "Lowrider", modValue = 3, priceMultiplier = 1.5f },
            new ModOption { name = "SUV / Truck", modValue = 4, priceMultiplier = 1.5f },
            new ModOption { name = "Offroad", modValue = 5, priceMultiplier = 1.5f },
            new ModOption { name = "Tuner", modValue = 6, priceMultiplier = 1.5f },
            new ModOption { name = "High End", modValue = 7, priceMultiplier = 2f },
        };

        [Header("Wheel Variants")]
        public int maxWheelVariants = 24;

        [Header("Wheel Colors")]
        public WheelColorOption[] wheelColorOptions = new WheelColorOption[]
        {
            new WheelColorOption { name = "Default", color = Color.white, modValue = 0 },
            new WheelColorOption { name = "Black", color = Color.black, modValue = 1 },
            new WheelColorOption { name = "Graphite", color = new Color(0.2f, 0.2f, 0.2f), modValue = 2 },
            new WheelColorOption { name = "Silver", color = new Color(0.75f, 0.75f, 0.75f), modValue = 3 },
            new WheelColorOption { name = "Chrome", color = new Color(0.9f, 0.9f, 0.9f), modValue = 4 },
            new WheelColorOption { name = "Gold", color = new Color(1f, 0.84f, 0f), modValue = 5 },
            new WheelColorOption { name = "Bronze", color = new Color(0.8f, 0.5f, 0.2f), modValue = 6 },
            new WheelColorOption { name = "Red", color = Color.red, modValue = 7 },
            new WheelColorOption { name = "Blue", color = Color.blue, modValue = 8 },
            new WheelColorOption { name = "Neon Green", color = new Color(0.5f, 1f, 0f), modValue = 9 },
            new WheelColorOption { name = "Orange", color = Color.orange, modValue = 10 },
            new WheelColorOption { name = "Purple", color = new Color(0.5f, 0f, 0.5f), modValue = 11 },
            new WheelColorOption { name = "Pink", color = new Color(1f, 0.5f, 0.75f), modValue = 12 },
        };

        [Header("Tire Smoke")]
        public ModOption[] tireSmokeOptions = new ModOption[]
        {
            new ModOption { name = "None", modValue = 0, priceMultiplier = 1f },
            new ModOption { name = "White", modValue = 1, priceMultiplier = 1.2f },
            new ModOption { name = "Red", modValue = 2, priceMultiplier = 1.2f },
            new ModOption { name = "Blue", modValue = 3, priceMultiplier = 1.2f },
            new ModOption { name = "Green", modValue = 4, priceMultiplier = 1.2f },
            new ModOption { name = "Yellow", modValue = 5, priceMultiplier = 1.2f },
            new ModOption { name = "Purple", modValue = 6, priceMultiplier = 1.2f },
            new ModOption { name = "Orange", modValue = 7, priceMultiplier = 1.2f },
            new ModOption { name = "Pink", modValue = 8, priceMultiplier = 1.2f },
            new ModOption { name = "Black", modValue = 9, priceMultiplier = 1.2f },
        };

        [Header("Special Tires")]
        public ModOption[] bulletproofTireOptions = new ModOption[]
        {
            new ModOption { name = "Standard", modValue = 0, priceMultiplier = 1f },
            new ModOption { name = "Bulletproof", modValue = 1, priceMultiplier = 2f },
        };

        public ModOption[] customTireOptions = new ModOption[]
        {
            new ModOption { name = "Standard", modValue = 0, priceMultiplier = 1f },
            new ModOption { name = "Custom (Whitewalls, etc.)", modValue = 1, priceMultiplier = 1.5f },
        };

        // ============================================================================
        // PAINT
        // ============================================================================

        [Header("Paint Types")]
        public PaintType[] paintTypes = new PaintType[]
        {
            new PaintType { name = "Normal", typeId = 0, priceMultiplier = 1f },
            new PaintType { name = "Metallic", typeId = 1, priceMultiplier = 1.3f },
            new PaintType { name = "Pearlescent", typeId = 2, priceMultiplier = 1.5f },
            new PaintType { name = "Matte", typeId = 3, priceMultiplier = 1.5f },
            new PaintType { name = "Metal", typeId = 4, priceMultiplier = 2f },
            new PaintType { name = "Chrome", typeId = 5, priceMultiplier = 3f },
        };

        [Header("Pearlescent Colors")]
        public Color[] pearlescentColors = new Color[]
        {
            Color.white, Color.black, Color.red, Color.blue, Color.green, Color.yellow,
            Color.magenta, Color.cyan, Color.gray, new Color(1f, 0.5f, 0f), // Orange
            new Color(0.5f, 0f, 0.5f), // Purple
            new Color(1f, 0.75f, 0.8f), // Pink
            new Color(0.5f, 1f, 0f), // Lime
            new Color(0f, 1f, 1f), // Cyan
            new Color(1f, 0.65f, 0f), // Orange
            new Color(1f, 0.84f, 0f), // Gold
        };

        [Header("Wheel Colors")]
        public Color[] wheelColors = new Color[]
        {
            Color.white, Color.black, Color.red, Color.blue, Color.green, Color.yellow,
            Color.magenta, Color.cyan, Color.gray, new Color(1f, 0.5f, 0f),
            new Color(0.5f, 0f, 0.5f), new Color(1f, 0.75f, 0.8f),
        };

        [Header("Livery")]
        public ModOption[] liveryOptions = new ModOption[0];

        [Header("Window Tint")]
        public ModOption[] windowTintOptions = new ModOption[]
        {
            new ModOption { name = "None", modValue = 0, priceMultiplier = 1f },
            new ModOption { name = "Light Smoke", modValue = 1, priceMultiplier = 1.1f },
            new ModOption { name = "Medium Smoke", modValue = 2, priceMultiplier = 1.2f },
            new ModOption { name = "Dark Smoke", modValue = 3, priceMultiplier = 1.3f },
            new ModOption { name = "Limo", modValue = 4, priceMultiplier = 1.5f },
            new ModOption { name = "Green", modValue = 5, priceMultiplier = 1.5f },
        };

        // ============================================================================
        // LIGHTS
        // ============================================================================

        [Header("Xenon Lights")]
        public ModOption[] xenonOptions = new ModOption[]
        {
            new ModOption { name = "Halogen (Stock)", modValue = 0, priceMultiplier = 1f },
            new ModOption { name = "Xenon / HID", modValue = 1, priceMultiplier = 1.5f },
        };

        [Header("Xenon Colors")]
        public XenonColor[] xenonColors = new XenonColor[]
        {
            new XenonColor { name = "White (4300K)", color = Color.white, colorId = 0 },
            new XenonColor { name = "Blue (5000K)", color = new Color(0.3f, 0.5f, 1f), colorId = 1 },
            new XenonColor { name = "Electric Blue (6000K)", color = new Color(0.2f, 0.4f, 1f), colorId = 2 },
            new XenonColor { name = "Mint Green (7000K)", color = new Color(0.3f, 1f, 0.6f), colorId = 3 },
            new XenonColor { name = "Lime Green (8000K)", color = new Color(0.5f, 1f, 0.2f), colorId = 4 },
            new XenonColor { name = "Yellow (3000K)", color = Color.yellow, colorId = 5 },
            new XenonColor { name = "Golden (2500K)", color = new Color(1f, 0.8f, 0.2f), colorId = 6 },
            new XenonColor { name = "Orange (2000K)", color = Color.orange, colorId = 6 },
            new XenonColor { name = "Red", color = Color.red, colorId = 8 },
            new XenonColor { name = "Pink", color = new Color(1f, 0.4f, 0.7f), colorId = 9 },
            new XenonColor { name = "Hot Pink", color = new Color(1f, 0.2f, 0.6f), colorId = 10 },
            new XenonColor { name = "Purple", color = new Color(0.6f, 0.2f, 1f), colorId = 11 },
            new XenonColor { name = "Blacklight (UV)", color = new Color(0.3f, 0f, 0.6f), colorId = 12 },
        };

        [Header("Neon Lights")]
        public ModOption[] neonOptions = new ModOption[]
        {
            new ModOption { name = "None", modValue = 0, priceMultiplier = 1f },
            new ModOption { name = "Neon Kit", modValue = 1, priceMultiplier = 2f },
        };

        // ============================================================================
        // PLATES
        // ============================================================================

        [Header("Plate Styles")]
        public ModOption[] plateStyleOptions = new ModOption[]
        {
            new ModOption { name = "Standard", modValue = 0, priceMultiplier = 1f },
            new ModOption { name = "Style 1", modValue = 1, priceMultiplier = 1.1f },
            new ModOption { name = "Style 2", modValue = 2, priceMultiplier = 1.1f },
            new ModOption { name = "Style 3", modValue = 3, priceMultiplier = 1.1f },
            new ModOption { name = "Style 4", modValue = 4, priceMultiplier = 1.1f },
            new ModOption { name = "Style 5", modValue = 5, priceMultiplier = 1.1f },
        };

        // ============================================================================
        // EMERGENCY (Police/Fire)
        // ============================================================================

        [Header("Emergency Equipment")]
        public ModOption[] pushBarOptions = new ModOption[]
        {
            new ModOption { name = "None", modValue = 0, priceMultiplier = 1f },
            new ModOption { name = "Push Bar", modValue = 1, priceMultiplier = 2f, visualPrefab = null },
            new ModOption { name = "Heavy Duty Bar", modValue = 2, priceMultiplier = 3f, visualPrefab = null },
        };

        public ModOption[] partitionOptions = new ModOption[]
        {
            new ModOption { name = "None", modValue = 0, priceMultiplier = 1f },
            new ModOption { name = "Mesh Partition", modValue = 1, priceMultiplier = 1.5f, visualPrefab = null },
            new ModOption { name = "Solid Partition", modValue = 2, priceMultiplier = 2f, visualPrefab = null },
        };

        public ModOption[] gunRackOptions = new ModOption[]
        {
            new ModOption { name = "None", modValue = 0, priceMultiplier = 1f },
            new ModOption { name = "Single Rifle", modValue = 1, priceMultiplier = 1.5f, visualPrefab = null },
            new ModOption { name = "Dual Rifle + Shotgun", modValue = 2, priceMultiplier = 2f, visualPrefab = null },
        };

        public ModOption[] laptopMountOptions = new ModOption[]
        {
            new ModOption { name = "None", modValue = 0, priceMultiplier = 1f },
            new ModOption { name = "Laptop Mount", modValue = 1, priceMultiplier = 1.5f, visualPrefab = null },
        };

        public ModOption[] radarOptions = new ModOption[]
        {
            new ModOption { name = "None", modValue = 0, priceMultiplier = 1f },
            new ModOption { name = "Front Radar", modValue = 1, priceMultiplier = 1.5f, visualPrefab = null },
            new ModOption { name = "Dual Radar", modValue = 2, priceMultiplier = 2f, visualPrefab = null },
        };

        public ModOption[] spotlightOptions = new ModOption[]
        {
            new ModOption { name = "None", modValue = 0, priceMultiplier = 1f },
            new ModOption { name = "Driver Side", modValue = 1, priceMultiplier = 1.3f, visualPrefab = null },
            new ModOption { name = "Dual Spotlights", modValue = 2, priceMultiplier = 2f, visualPrefab = null },
        };

        public ModOption[] sirenSpeakerOptions = new ModOption[]
        {
            new ModOption { name = "Stock Horn", modValue = 0, priceMultiplier = 1f },
            new ModOption { name = "Siren Speaker", modValue = 1, priceMultiplier = 2f, visualPrefab = null },
        };

        public ModOption[] antennaOptions = new ModOption[]
        {
            new ModOption { name = "Stock Antenna", modValue = 0, priceMultiplier = 1f },
            new ModOption { name = "Police Antenna Array", modValue = 1, priceMultiplier = 1.5f, visualPrefab = null },
            new ModOption { name = "Low Profile", modValue = 2, priceMultiplier = 1.3f, visualPrefab = null },
        };

        // ============================================================================
        // HELPER METHODS
        // ============================================================================

        public ModOption GetOptionByCategory(ModCategory category, int modValue)
        {
            var options = GetOptionsForCategory(category);
            return System.Array.Find(options, o => o.modValue == modValue);
        }

        public ModOption[] GetOptionsForCategory(ModCategory category)
        {
            return category switch
            {
                ModCategory.Engine => engineOptions,
                ModCategory.Turbo => turboOptions,
                ModCategory.Transmission => transmissionOptions,
                ModCategory.Suspension => suspensionOptions,
                ModCategory.Brakes => brakeOptions,
                ModCategory.BodyKit => bodyKitOptions,
                ModCategory.Spoiler => spoilerOptions,
                ModCategory.Hood => hoodOptions,
                ModCategory.Exhaust => exhaustOptions,
                ModCategory.Grille => grilleOptions,
                ModCategory.Skirt => skirtOptions,
                ModCategory.Fender => fenderOptions,
                ModCategory.Roof => roofOptions,
                ModCategory.Wheels => wheelTypeOptions,
                ModCategory.TireSmoke => tireSmokeOptions,
                ModCategory.BulletproofTires => bulletproofTireOptions,
                ModCategory.CustomTires => customTireOptions,
                ModCategory.Paint => paintTypes.Select(p => new ModOption { name = p.name, modValue = p.typeId, priceMultiplier = p.priceMultiplier }).ToArray(),
                ModCategory.PearlescentColor => pearlescentColors.Select((c, i) => new ModOption { name = $"Pearlescent {i}", modValue = i, priceMultiplier = 1f }).ToArray(),
                ModCategory.WheelColor => wheelColorOptions.Select(w => new ModOption { name = w.name, modValue = w.modValue, priceMultiplier = 1f }).ToArray(),
                ModCategory.TireSmoke => tireSmokeOptions,
                ModCategory.BulletproofTires => bulletproofTireOptions,
                ModCategory.CustomTires => customTireOptions,
                ModCategory.Livery => liveryOptions,
                ModCategory.WindowTint => windowTintOptions,
                ModCategory.XenonLights => xenonOptions,
                ModCategory.XenonColor => xenonColors.Select(x => new ModOption { name = x.name, modValue = x.colorId, priceMultiplier = 1f }).ToArray(),
                ModCategory.Neon => neonOptions,
                ModCategory.Plates => plateStyleOptions,
                ModCategory.PushBar => pushBarOptions,
                ModCategory.Partition => partitionOptions,
                ModCategory.GunRack => gunRackOptions,
                ModCategory.LaptopMount => laptopMountOptions,
                ModCategory.Radar => radarOptions,
                ModCategory.Spotlights => spotlightOptions,
                ModCategory.SirenSpeaker => sirenSpeakerOptions,
                ModCategory.Antenna => antennaOptions,
                _ => new ModOption[0],
            };
        }

        public ModOption GetEmergencyOption(EmergencyEquipmentSlot slot, int modValue)
        {
            ModOption[] options = slot switch
            {
                EmergencyEquipmentSlot.PushBar => pushBarOptions,
                EmergencyEquipmentSlot.Partition => partitionOptions,
                EmergencyEquipmentSlot.GunRack => gunRackOptions,
                EmergencyEquipmentSlot.LaptopMount => laptopMountOptions,
                EmergencyEquipmentSlot.Radar => radarOptions,
                EmergencyEquipmentSlot.Spotlights => spotlightOptions,
                EmergencyEquipmentSlot.SirenSpeaker => sirenSpeakerOptions,
                EmergencyEquipmentSlot.Antenna => antennaOptions,
                _ => new ModOption[0],
            };
            return System.Array.Find(options, o => o.modValue == modValue);
        }

        public bool IsCompatible(VehicleDefinition def)
        {
            if (def == null) return false;
            
            // Check class
            if (!System.Array.Exists(compatibleClasses, c => c == def.vehicleClass))
            {
                // Check specific model override
                if (compatibleModels.Length > 0 && !System.Array.Exists(compatibleModels, m => m == def.modelName))
                {
                    return false;
                }
            }
            return true;
        }

        public int CalculatePrice(ModCategory category, int modValue)
        {
            var option = GetOptionByCategory(category, modValue);
            if (option == null) return 0;
            
            int basePrice = GetBasePrice(category);
            return Mathf.RoundToInt(basePrice * option.priceMultiplier * globalPriceMultiplier * priceByLevel.Evaluate(modValue));
        }

        private int GetBasePrice(ModCategory category)
        {
            return category switch
            {
                ModCategory.Engine => 5000,
                ModCategory.Turbo => 10000,
                ModCategory.Transmission => 3000,
                ModCategory.Suspension => 2000,
                ModCategory.Brakes => 1500,
                ModCategory.BodyKit => 3000,
                ModCategory.Spoiler => 1500,
                ModCategory.Exhaust => 1000,
                ModCategory.Wheels => 2000,
                ModCategory.TireSmoke => 500,
                ModCategory.BulletproofTires => 2000,
                ModCategory.Paint => 1000,
                ModCategory.Livery => 2000,
                ModCategory.WindowTint => 300,
                ModCategory.XenonLights => 800,
                ModCategory.XenonColor => 500,
                ModCategory.Neon => 1000,
                ModCategory.Plates => 200,
                ModCategory.PushBar => 2000,
                ModCategory.Partition => 1500,
                ModCategory.GunRack => 1000,
                ModCategory.LaptopMount => 800,
                ModCategory.Radar => 1200,
                ModCategory.Spotlights => 800,
                ModCategory.SirenSpeaker => 500,
                ModCategory.Antenna => 300,
                _ => 1000,
            };
        }
    }

    // ============================================================================
    // SUPPORTING TYPES
    // ============================================================================

    public enum ModCategory
    {
        Engine,
        Turbo,
        Transmission,
        Suspension,
        Brakes,
        BodyKit,
        Spoiler,
        Hood,
        Exhaust,
        Grille,
        Skirt,
        Fender,
        Roof,
        Wheels,
        TireSmoke,
        BulletproofTires,
        CustomTires,
        Paint,
        PearlescentColor,
        WheelColor,
        Livery,
        WindowTint,
        XenonLights,
        XenonColor,
        Neon,
        Plates,
        PushBar,
        Partition,
        GunRack,
        LaptopMount,
        Radar,
        Spotlights,
        SirenSpeaker,
        Antenna,
    }

    public enum EmergencyEquipmentSlot
    {
        PushBar,
        Partition,
        GunRack,
        LaptopMount,
        Radar,
        Spotlights,
        SirenSpeaker,
        Antenna,
    }

    [System.Serializable]
    public class ModOption
    {
        public string name = "Option";
        public int modValue = 0;
        public float priceMultiplier = 1f;
        public string unlockRequirement = "";
        public int requiredRank = 0;
        public string requiredAgency = "";
        public GameObject visualPrefab; // For body kits, spoilers, etc.
        public string description = "";
        public Sprite icon;
    }

    [System.Serializable]
    public class WheelColorOption
    {
        public string name = "Color";
        public Color color = Color.white;
        public int modValue = 0;
        public float priceMultiplier = 1f;
    }

    [System.Serializable]
    public class PaintType
    {
        public string name = "Paint";
        public int typeId = 0;
        public float priceMultiplier = 1f;
    }

    [System.Serializable]
    public class XenonColor
    {
        public string name = "Color";
        public Color color = Color.white;
        public int colorId = 0;
        public float priceMultiplier = 1f;
    }
}