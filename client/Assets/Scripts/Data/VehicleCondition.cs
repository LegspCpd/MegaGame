using UnityEngine;
using System;

namespace Megame.Vehicles
{
    /// <summary>
    /// Vehicle condition - tracks wear, damage, dirt
    /// </summary>
    [System.Serializable]
    public class VehicleCondition
    {
        [Header("Mechanical")]
        [Range(0f, 1f)] public float engineHealth = 1f;      // 0 = destroyed, 1 = perfect
        [Range(0f, 1f)] public float transmissionHealth = 1f;
        [Range(0f, 1f)] public float suspensionHealth = 1f;
        [Range(0f, 1f)] public float brakeHealth = 1f;
        [Range(0f, 1f)] public float tireHealth = 1f;        // Average across all tires

        [Header("Body")]
        [Range(0f, 1f)] public float bodyHealth = 1f;        // Panel damage
        [Range(0f, 1f)] public float glassHealth = 1f;       // Windows intact
        [Range(0f, 1f)] public float lightHealth = 1f;       // Lights functional

        [Header("Cosmetic")]
        [Range(0f, 1f)] public float dirtLevel = 0f;         // 0 = clean, 1 = filthy
        [Range(0f, 1f)] public float rustLevel = 0f;         // Rust on body
        [Range(0f, 1f)] public float paintFade = 0f;         // Paint degradation

        [Header("Wheels")]
        public bool tireBurstFL = false;
        public bool tireBurstFR = false;
        public bool tireBurstRL = false;
        public bool tireBurstRR = false;
        public bool rimDamagedFL = false;
        public bool rimDamagedFR = false;
        public bool rimDamagedRL = false;
        public bool rimDamagedRR = false;

        [Header("Doors")]
        public bool doorMissingFL = false;
        public bool doorMissingFR = false;
        public bool doorMissingRL = false;
        public bool doorMissingRR = false;
        public bool doorMissingHood = false;
        public bool doorMissingTrunk = false;

        [Header("Lights")]
        public bool headlightBrokenL = false;
        public bool headlightBrokenR = false;
        public bool taillightBrokenL = false;
        public bool taillightBrokenR = false;

        [Header("Mechanical State")]
        public bool engineOverheating = false;
        public bool transmissionSlipping = false;
        public bool brakesFading = false;
        public bool suspensionBottoming = false;

        // ============================================================================
        // COMPUTED PROPERTIES
        // ============================================================================

        public float OverallHealth => (engineHealth + transmissionHealth + suspensionHealth + brakeHealth + bodyHealth) / 5f;
        public float MechanicalHealth => (engineHealth + transmissionHealth + suspensionHealth + brakeHealth) / 4f;
        public float CosmeticHealth => 1f - (dirtLevel + rustLevel + paintFade) / 3f;
        public bool IsDrivable => engineHealth > 0.1f && transmissionHealth > 0.1f && !(tireBurstFL && tireBurstFR && tireBurstRL && tireBurstRR);
        public bool IsSeverelyDamaged => OverallHealth < 0.3f;
        public int BurstTireCount => (tireBurstFL ? 1 : 0) + (tireBurstFR ? 1 : 0) + (tireBurstRL ? 1 : 0) + (tireBurstRR ? 1 : 0);
        public int MissingDoorCount => (doorMissingFL ? 1 : 0) + (doorMissingFR ? 1 : 0) + (doorMissingRL ? 1 : 0) + (doorMissingRR ? 1 : 0) + (doorMissingHood ? 1 : 0) + (doorMissingTrunk ? 1 : 0);

        // ============================================================================
        // CLONE / COPY
        // ============================================================================

        public VehicleCondition Clone()
        {
            return (VehicleCondition)MemberwiseClone();
        }

        public void CopyFrom(VehicleCondition other)
        {
            if (other == null) return;
            
            engineHealth = other.engineHealth;
            transmissionHealth = other.transmissionHealth;
            suspensionHealth = other.suspensionHealth;
            brakeHealth = other.brakeHealth;
            tireHealth = other.tireHealth;
            bodyHealth = other.bodyHealth;
            glassHealth = other.glassHealth;
            lightHealth = other.lightHealth;
            dirtLevel = other.dirtLevel;
            rustLevel = other.rustLevel;
            paintFade = other.paintFade;
            tireBurstFL = other.tireBurstFL;
            tireBurstFR = other.tireBurstFR;
            tireBurstRL = other.tireBurstRL;
            tireBurstRR = other.tireBurstRR;
            rimDamagedFL = other.rimDamagedFL;
            rimDamagedFR = other.rimDamagedFR;
            rimDamagedRL = other.rimDamagedRL;
            rimDamagedRR = other.rimDamagedRR;
            doorMissingFL = other.doorMissingFL;
            doorMissingFR = other.doorMissingFR;
            doorMissingRL = other.doorMissingRL;
            doorMissingRR = other.doorMissingRR;
            doorMissingHood = other.doorMissingHood;
            doorMissingTrunk = other.doorMissingTrunk;
            headlightBrokenL = other.headlightBrokenL;
            headlightBrokenR = other.headlightBrokenR;
            taillightBrokenL = other.taillightBrokenL;
            taillightBrokenR = other.taillightBrokenR;
            engineOverheating = other.engineOverheating;
            transmissionSlipping = other.transmissionSlipping;
            brakesFading = other.brakesFading;
            suspensionBottoming = other.suspensionBottoming;
        }

        // ============================================================================
        // DAMAGE / REPAIR METHODS
        // ============================================================================

        public void ApplyDamage(VehicleDamageType type, float amount, Vector3? hitPoint = null)
        {
            amount = Mathf.Clamp01(amount);
            
            switch (type)
            {
                case VehicleDamageType.Engine:
                    engineHealth = Mathf.Max(0f, engineHealth - amount);
                    if (engineHealth < 0.3f) engineOverheating = true;
                    break;
                case VehicleDamageType.Transmission:
                    transmissionHealth = Mathf.Max(0f, transmissionHealth - amount);
                    if (transmissionHealth < 0.3f) transmissionSlipping = true;
                    break;
                case VehicleDamageType.Suspension:
                    suspensionHealth = Mathf.Max(0f, suspensionHealth - amount);
                    if (suspensionHealth < 0.3f) suspensionBottoming = true;
                    break;
                case VehicleDamageType.Brakes:
                    brakeHealth = Mathf.Max(0f, brakeHealth - amount);
                    if (brakeHealth < 0.3f) brakesFading = true;
                    break;
                case VehicleDamageType.Tire:
                    tireHealth = Mathf.Max(0f, tireHealth - amount);
                    break;
                case VehicleDamageType.Body:
                    bodyHealth = Mathf.Max(0f, bodyHealth - amount);
                    break;
                case VehicleDamageType.Glass:
                    glassHealth = Mathf.Max(0f, glassHealth - amount);
                    break;
                case VehicleDamageType.Lights:
                    lightHealth = Mathf.Max(0f, lightHealth - amount);
                    break;
            }
            
            // Random cosmetic damage
            if (amount > 0.2f && UnityEngine.Random.value < 0.3f)
            {
                dirtLevel = Mathf.Min(1f, dirtLevel + amount * 0.5f);
            }
        }

        public void Repair(VehicleDamageType type, float amount)
        {
            amount = Mathf.Clamp01(amount);
            
            switch (type)
            {
                case VehicleDamageType.Engine:
                    engineHealth = Mathf.Min(1f, engineHealth + amount);
                    if (engineHealth > 0.5f) engineOverheating = false;
                    break;
                case VehicleDamageType.Transmission:
                    transmissionHealth = Mathf.Min(1f, transmissionHealth + amount);
                    if (transmissionHealth > 0.5f) transmissionSlipping = false;
                    break;
                case VehicleDamageType.Suspension:
                    suspensionHealth = Mathf.Min(1f, suspensionHealth + amount);
                    if (suspensionHealth > 0.5f) suspensionBottoming = false;
                    break;
                case VehicleDamageType.Brakes:
                    brakeHealth = Mathf.Min(1f, brakeHealth + amount);
                    if (brakeHealth > 0.5f) brakesFading = false;
                    break;
                case VehicleDamageType.Tire:
                    tireHealth = Mathf.Min(1f, tireHealth + amount);
                    break;
                case VehicleDamageType.Body:
                    bodyHealth = Mathf.Min(1f, bodyHealth + amount);
                    break;
                case VehicleDamageType.Glass:
                    glassHealth = Mathf.Min(1f, glassHealth + amount);
                    break;
                case VehicleDamageType.Lights:
                    lightHealth = Mathf.Min(1f, lightHealth + amount);
                    break;
            }
        }

        public void FullRepair()
        {
            engineHealth = transmissionHealth = suspensionHealth = brakeHealth = 1f;
            tireHealth = bodyHealth = glassHealth = lightHealth = 1f;
            dirtLevel = rustLevel = paintFade = 0f;
            tireBurstFL = tireBurstFR = tireBurstRL = tireBurstRR = false;
            rimDamagedFL = rimDamagedFR = rimDamagedRL = rimDamagedRR = false;
            doorMissingFL = doorMissingFR = doorMissingRL = doorMissingRR = doorMissingHood = doorMissingTrunk = false;
            headlightBrokenL = headlightBrokenR = taillightBrokenL = taillightBrokenR = false;
            engineOverheating = transmissionSlipping = brakesFading = suspensionBottoming = false;
        }

        public void CleanExterior()
        {
            dirtLevel = 0f;
            rustLevel = Mathf.Max(0f, rustLevel - 0.1f); // Can't fully remove rust by cleaning
        }

        public void WashVehicle()
        {
            dirtLevel = 0f;
        }

        public void BurstTire(TirePosition position)
        {
            switch (position)
            {
                case TirePosition.FrontLeft: tireBurstFL = true; break;
                case TirePosition.FrontRight: tireBurstFR = true; break;
                case TirePosition.RearLeft: tireBurstRL = true; break;
                case TirePosition.RearRight: tireBurstRR = true; break;
            }
            tireHealth = Mathf.Min(tireHealth, 0.5f);
        }

        public void RepairTire(TirePosition position)
        {
            switch (position)
            {
                case TirePosition.FrontLeft: tireBurstFL = false; break;
                case TirePosition.FrontRight: tireBurstFR = false; break;
                case TirePosition.RearLeft: tireBurstRL = false; break;
                case TirePosition.RearRight: tireBurstRR = false; break;
            }
            RecalculateTireHealth();
        }

        public void RemoveDoor(DoorPosition position)
        {
            switch (position)
            {
                case DoorPosition.FrontLeft: doorMissingFL = true; break;
                case DoorPosition.FrontRight: doorMissingFR = true; break;
                case DoorPosition.RearLeft: doorMissingRL = true; break;
                case DoorPosition.RearRight: doorMissingRR = true; break;
                case DoorPosition.Hood: doorMissingHood = true; break;
                case DoorPosition.Trunk: doorMissingTrunk = true; break;
            }
            bodyHealth = Mathf.Min(bodyHealth, 0.8f);
        }

        public void BreakLight(LightPosition position)
        {
            switch (position)
            {
                case LightPosition.HeadlightLeft: headlightBrokenL = true; break;
                case LightPosition.HeadlightRight: headlightBrokenR = true; break;
                case LightPosition.TaillightLeft: taillightBrokenL = true; break;
                case LightPosition.TaillightRight: taillightBrokenR = true; break;
            }
            lightHealth = Mathf.Min(lightHealth, 0.5f);
        }

        private void RecalculateTireHealth()
        {
            int goodTires = 0;
            if (!tireBurstFL) goodTires++;
            if (!tireBurstFR) goodTires++;
            if (!tireBurstRL) goodTires++;
            if (!tireBurstRR) goodTires++;
            tireHealth = goodTires / 4f;
        }
    }

    // ============================================================================
    // ENUMS
    // ============================================================================

    public enum VehicleDamageType
    {
        Engine,
        Transmission,
        Suspension,
        Brakes,
        Tire,
        Body,
        Glass,
        Lights,
        All,
    }

    public enum TirePosition
    {
        FrontLeft,
        FrontRight,
        RearLeft,
        RearRight,
    }

    public enum DoorPosition
    {
        FrontLeft,
        FrontRight,
        RearLeft,
        RearRight,
        Hood,
        Trunk,
    }

    public enum LightPosition
    {
        HeadlightLeft,
        HeadlightRight,
        TaillightLeft,
        TaillightRight,
    }

    // ============================================================================
    // PRESETS
    // ============================================================================

    public static class VehicleConditionPresets
    {
        public static VehicleCondition Pristine => new VehicleCondition
        {
            engineHealth = 1f,
            transmissionHealth = 1f,
            suspensionHealth = 1f,
            brakeHealth = 1f,
            tireHealth = 1f,
            bodyHealth = 1f,
            glassHealth = 1f,
            lightHealth = 1f,
            dirtLevel = 0f,
            rustLevel = 0f,
            paintFade = 0f,
        };

        public static VehicleCondition Worn => new VehicleCondition
        {
            engineHealth = 0.8f,
            transmissionHealth = 0.85f,
            suspensionHealth = 0.75f,
            brakeHealth = 0.8f,
            tireHealth = 0.7f,
            bodyHealth = 0.85f,
            glassHealth = 0.9f,
            lightHealth = 0.95f,
            dirtLevel = 0.3f,
            rustLevel = 0.1f,
            paintFade = 0.15f,
        };

        public static VehicleCondition Beater => new VehicleCondition
        {
            engineHealth = 0.4f,
            transmissionHealth = 0.5f,
            suspensionHealth = 0.3f,
            brakeHealth = 0.45f,
            tireHealth = 0.4f,
            bodyHealth = 0.5f,
            glassHealth = 0.7f,
            lightHealth = 0.6f,
            dirtLevel = 0.7f,
            rustLevel = 0.4f,
            paintFade = 0.5f,
            engineOverheating = true,
            transmissionSlipping = true,
            brakesFading = true,
            suspensionBottoming = true,
        };

        public static VehicleCondition Crashed => new VehicleCondition
        {
            engineHealth = 0.2f,
            transmissionHealth = 0.3f,
            suspensionHealth = 0.1f,
            brakeHealth = 0.2f,
            tireHealth = 0.3f,
            bodyHealth = 0.1f,
            glassHealth = 0.2f,
            lightHealth = 0.3f,
            dirtLevel = 0.8f,
            rustLevel = 0.2f,
            paintFade = 0.3f,
            tireBurstFL = true,
            tireBurstFR = false,
            tireBurstRL = true,
            tireBurstRR = false,
            doorMissingFL = true,
            doorMissingHood = true,
            headlightBrokenL = true,
            headlightBrokenR = true,
            taillightBrokenL = true,
            taillightBrokenR = true,
            engineOverheating = true,
            transmissionSlipping = true,
            brakesFading = true,
            suspensionBottoming = true,
        };
    }
}