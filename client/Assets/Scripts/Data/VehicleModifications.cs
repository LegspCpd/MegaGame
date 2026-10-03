using UnityEngine;
using System.Collections.Generic;
using Megame.Data;

namespace Megame.Vehicles
{
    /// <summary>
    /// Vehicle modifications - runtime mutable state
    /// </summary>
    [System.Serializable]
    public class VehicleModifications
    {
        // ============================================================================
        // PERFORMANCE MODS (0 = stock, higher = better)
        // ============================================================================
        [Range(0, 4)] public int engineLevel = 0;          // Engine: 0-4
        [Range(0, 1)] public int turboLevel = 0;           // Turbo: 0-1
        [Range(0, 3)] public int transmissionLevel = 0;    // Transmission: 0-3
        [Range(0, 4)] public int suspensionLevel = 0;      // Suspension: 0-4
        [Range(-1f, 1f)] public float suspensionHeight = 0f; // -1 = slammed, 1 = raised
        [Range(0, 3)] public int brakesLevel = 0;          // Brakes: 0-3
        [Range(0, 4)] public int armorLevel = 0;           // Armor: 0-4

        // ============================================================================
        // BODY MODS (0 = stock, 1+ = variant index)
        // ============================================================================
        [Range(0, 10)] public int bodyKit = 0;
        [Range(0, 10)] public int spoiler = 0;
        [Range(0, 10)] public int hood = 0;
        [Range(0, 10)] public int roof = 0;
        [Range(0, 10)] public int grille = 0;
        [Range(0, 10)] public int exhaust = 0;
        [Range(0, 10)] public int skirt = 0;
        [Range(0, 10)] public int fender = 0;

        // ============================================================================
        // WHEELS
        // ============================================================================
        [Range(0, 7)] public int wheelType = 0;            // 0=Sport, 1=Muscle, 2=Lowrider, 3=SUV, 4=Offroad, 5=Tuner, 6=Bike, 7=HighEnd
        [Range(0, 23)] public int wheelVariant = 0;        // Variant within type
        public Color tireSmokeColor = Color.white;
        public bool bulletproofTires = false;
        public bool customTires = false;

        // ============================================================================
        // PAINT & APPEARANCE
        // ============================================================================
        public Color primaryColor = Color.white;
        public Color secondaryColor = Color.white;
        public Color pearlescentColor = Color.white;
        public Color wheelColor = Color.white;
        [Range(0, 5)] public int paintType = 0;            // 0=Normal, 1=Metallic, 2=Pearlescent, 3=Matte, 4=Metal, 5=Chrome
        [Range(0, 20)] public int livery = 0;              // Livery index
        [Range(0, 5)] public int windowTint = 0;           // 0=None, 1=Light, 2=Medium, 3=Dark, 4=Limo, 5=Green

        // ============================================================================
        // LIGHTS
        // ============================================================================
        public bool xenonLights = false;
        [Range(0, 12)] public int xenonColor = 0;          // 0=White, 1=Blue, 2=Electric Blue, 3=Mint, 4=Lime, 5=Yellow, 6=Golden, 7=Orange, 8=Red, 9=Pink, 10=Hot Pink, 11=Purple, 12=Blacklight
        public bool neonEnabled = false;
        public Color neonColor = Color.cyan;

        // ============================================================================
        // PLATES
        // ============================================================================
        public string plateText = "MEGA";
        [Range(0, 5)] public int plateStyle = 0;           // 0=Standard, 1=Style1, 2=Style2, 3=Style3, 4=Style4, 5=Style5

        // ============================================================================
        // EMERGENCY (for police/fire vehicles)
        // ============================================================================
        public string sirenType = "standard";
        public string lightPattern = "lspd";
        public bool hasPushBar = false;
        public bool hasPartition = false;
        public bool hasGunRack = false;
        public bool hasLaptop = false;
        public bool hasRadar = false;
        public bool hasSpotlights = false;

        // ============================================================================
        // CLONE / COPY / EQUALITY
        // ============================================================================

        public VehicleModifications Clone()
        {
            return (VehicleModifications)MemberwiseClone();
        }

        public void CopyFrom(VehicleModifications other)
        {
            if (other == null) return;
            
            engineLevel = other.engineLevel;
            turboLevel = other.turboLevel;
            transmissionLevel = other.transmissionLevel;
            suspensionLevel = other.suspensionLevel;
            suspensionHeight = other.suspensionHeight;
            brakesLevel = other.brakesLevel;
            armorLevel = other.armorLevel;
            bodyKit = other.bodyKit;
            spoiler = other.spoiler;
            hood = other.hood;
            roof = other.roof;
            grille = other.grille;
            exhaust = other.exhaust;
            skirt = other.skirt;
            fender = other.fender;
            wheelType = other.wheelType;
            wheelVariant = other.wheelVariant;
            tireSmokeColor = other.tireSmokeColor;
            bulletproofTires = other.bulletproofTires;
            customTires = other.customTires;
            primaryColor = other.primaryColor;
            secondaryColor = other.secondaryColor;
            pearlescentColor = other.pearlescentColor;
            wheelColor = other.wheelColor;
            paintType = other.paintType;
            livery = other.livery;
            windowTint = other.windowTint;
            xenonLights = other.xenonLights;
            xenonColor = other.xenonColor;
            neonEnabled = other.neonEnabled;
            neonColor = other.neonColor;
            plateText = other.plateText;
            plateStyle = other.plateStyle;
            sirenType = other.sirenType;
            lightPattern = other.lightPattern;
            hasPushBar = other.hasPushBar;
            hasPartition = other.hasPartition;
            hasGunRack = other.hasGunRack;
            hasLaptop = other.hasLaptop;
            hasRadar = other.hasRadar;
            hasSpotlights = other.hasSpotlights;
        }

        public bool Equals(VehicleModifications other)
        {
            if (other == null) return false;
            return engineLevel == other.engineLevel &&
                   turboLevel == other.turboLevel &&
                   transmissionLevel == other.transmissionLevel &&
                   suspensionLevel == other.suspensionLevel &&
                   Mathf.Approximately(suspensionHeight, other.suspensionHeight) &&
                   brakesLevel == other.brakesLevel &&
                   armorLevel == other.armorLevel &&
                   bodyKit == other.bodyKit &&
                   spoiler == other.spoiler &&
                   hood == other.hood &&
                   roof == other.roof &&
                   grille == other.grille &&
                   exhaust == other.exhaust &&
                   skirt == other.skirt &&
                   fender == other.fender &&
                   wheelType == other.wheelType &&
                   wheelVariant == other.wheelVariant &&
                   tireSmokeColor == other.tireSmokeColor &&
                   bulletproofTires == other.bulletproofTires &&
                   customTires == other.customTires &&
                   primaryColor == other.primaryColor &&
                   secondaryColor == other.secondaryColor &&
                   pearlescentColor == other.pearlescentColor &&
                   wheelColor == other.wheelColor &&
                   paintType == other.paintType &&
                   livery == other.livery &&
                   windowTint == other.windowTint &&
                   xenonLights == other.xenonLights &&
                   xenonColor == other.xenonColor &&
                   neonEnabled == other.neonEnabled &&
                   neonColor == other.neonColor &&
                   plateText == other.plateText &&
                   plateStyle == other.plateStyle;
        }

        public override bool Equals(object obj) => obj is VehicleModifications other && Equals(other);
        public override int GetHashCode() => base.GetHashCode();

        // ============================================================================
        // PRESETS
        // ============================================================================

        public static VehicleModifications Stock => new VehicleModifications();

        public static VehicleModifications MaxPerformance => new VehicleModifications
        {
            engineLevel = 4,
            turboLevel = 1,
            transmissionLevel = 3,
            suspensionLevel = 4,
            brakesLevel = 3,
            armorLevel = 4,
        };

        public static VehicleModifications PolicePatrol => new VehicleModifications
        {
            engineLevel = 2,
            transmissionLevel = 2,
            suspensionLevel = 2,
            brakesLevel = 2,
            armorLevel = 2,
            hasPushBar = true,
            hasPartition = true,
            hasGunRack = true,
            hasLaptop = true,
            hasRadar = true,
            hasSpotlights = true,
            sirenType = "wail",
            lightPattern = "lspd",
        };

        public static VehicleModifications Undercover => new VehicleModifications
        {
            engineLevel = 3,
            transmissionLevel = 2,
            suspensionLevel = 1,
            brakesLevel = 2,
            hasRadar = true,
            hasLaptop = true,
        };

        public static VehicleModifications Offroad => new VehicleModifications
        {
            engineLevel = 2,
            suspensionLevel = 4,
            suspensionHeight = 0.5f,
            brakesLevel = 2,
            armorLevel = 1,
            wheelType = 4, // Offroad
            bulletproofTires = true,
            armorLevel = 2,
        };

        public static VehicleModifications StreetRacer => new VehicleModifications
        {
            engineLevel = 4,
            turboLevel = 1,
            transmissionLevel = 3,
            suspensionLevel = 3,
            suspensionHeight = -0.5f,
            brakesLevel = 3,
            wheelType = 0, // Sport
            wheelVariant = 5,
            paintType = 2, // Pearlescent
            neonEnabled = true,
            neonColor = Color.cyan,
        };
    }
}