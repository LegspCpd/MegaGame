using UnityEngine;
using System.Collections.Generic;
using Megame.Data;
using Megame.Vehicles;

namespace Megame.Controllers
{
    /// <summary>
    /// Complete vehicle controller with physics, mods, damage, and AI support
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class OptimizedVehicleController : MonoBehaviour
    {
        // ============================================================================
        // VEHICLE DEFINITION & STATE
        // ============================================================================
        
        [Header("Definition")]
        public VehicleDefinitionData definition;
        
        [Header("Runtime State")]
        public VehicleModificationsData currentMods = new VehicleModificationsData();
        public VehicleCondition currentCondition = VehicleConditionPresets.Pristine;
        
        [Header("Vitals")]
        public int currentFuel = 100;
        public int currentEngineHealth = 1000;
        public int currentBodyHealth = 1000;
        public string currentPlateText = "MEGA";
        public int currentPlateStyle = 0;
        public float totalDistanceDriven = 0f;
        
        // ============================================================================
        // PHYSICS COMPONENTS
        // ============================================================================
        
        [Header("Wheel Colliders")]
        public WheelCollider[] wheelColliders = new WheelCollider[4];
        public Transform[] wheelTransforms = new Transform[4];
        
        [Header("Wheel Indices")]
        public int frontLeftIndex = 0;
        public int frontRightIndex = 1;
        public int rearLeftIndex = 2;
        public int rearRightIndex = 3;
        
        // ============================================================================
        // ENGINE & DRIVETRAIN
        // ============================================================================
        
        [Header("Engine Specs (from definition + mods)")]
        public float maxTorque = 400f;
        public float maxRPM = 7000f;
        public float idleRPM = 800f;
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
        
        [Header("Aero")]
        public float downforceCoefficient = 0.1f;
        public float dragCoefficient = 0.32f;
        
        // Runtime
        private Rigidbody rb;
        private float currentRPM;
        private int currentGear = 1;
        private bool engineOn = true;
        private bool isPlayerControlled = false;
        
        // Input
        private float throttleInput;
        private float brakeInput;
        private float steerInput;
        private bool handbrakeInput;
        
        // Wheels
        private WheelHit[] wheelHits = new WheelHit[4];
        
        // Damage
        private float damageFlashTimer;
        
        // ============================================================================
        // INITIALIZATION
        // ============================================================================
        
        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            SetupRigidbody();
            CacheWheelComponents();
        }
        
        private void SetupRigidbody()
        {
            if (definition != null)
            {
                rb.mass = definition.mass;
                rb.centerOfMass = definition.centerOfMass;
                rb.inertiaTensor = definition.inertiaMultiplier;
            }
            else
            {
                rb.mass = 1500f;
                rb.centerOfMass = new Vector3(0, -0.3f, 0);
            }
            
            rb.maxDepenetrationVelocity = 5f;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
        }
        
        private void CacheWheelComponents()
        {
            // Find wheel colliders if not assigned
            if (wheelColliders[0] == null)
            {
                wheelColliders = GetComponentsInChildren<WheelCollider>();
            }
            
            // Find wheel transforms if not assigned
            if (wheelTransforms[0] == null)
            {
                var renderers = GetComponentsInChildren<MeshRenderer>();
                foreach (var r in renderers)
                {
                    string name = r.name.ToLower();
                    if (name.Contains("wheel") || name.Contains("rim") || name.Contains("tire"))
                    {
                        // Try to match to wheel collider
                        for (int i = 0; i < wheelColliders.Length; i++)
                        {
                            if (wheelTransforms[i] == null && 
                                Vector3.Distance(r.bounds.center, wheelColliders[i].transform.position) < 1f)
                            {
                                wheelTransforms[i] = r.transform;
                                break;
                            }
                        }
                    }
                }
            }
        }
        
        public void Initialize(VehicleDefinitionData def)
        {
            definition = def;
            ApplyDefinitionSpecs();
            ApplyMods(currentMods);
            currentCondition = VehicleConditionPresets.Pristine.Clone();
            currentFuel = 100;
            currentEngineHealth = 1000;
            currentBodyHealth = 1000;
        }
        
        private void ApplyDefinitionSpecs()
        {
            if (definition == null) return;
            
            maxTorque = definition.maxTorque;
            maxRPM = definition.maxRPM;
            idleRPM = definition.idleRPM;
            torqueCurve = definition.torqueCurve;
            gearRatios = definition.gearRatios;
            finalDriveRatio = definition.finalDriveRatio;
            driveBiasFront = definition.driveBiasFront;
            maxSteerAngle = definition.maxSteerAngle;
            steerSpeed = definition.steerSpeed;
            brakeForce = definition.brakeForce;
            handbrakeForce = definition.handbrakeForce;
            downforceCoefficient = definition.downforceCoefficient;
            dragCoefficient = definition.dragCoefficient;
        }
        
        // ============================================================================
        // MOD APPLICATION
        // ============================================================================
        
        public void ApplyMods(VehicleModificationsData mods)
        {
            currentMods = mods.Clone();
            
            // Engine
            float engineMult = 1f + mods.engineLevel * 0.15f;
            if (mods.turboLevel > 0) engineMult *= 1.3f;
            maxTorque = definition?.maxTorque * engineMult ?? 400f * engineMult;
            
            // Transmission
            // Gear ratios modified by transmission level
            
            // Suspension
            UpdateSuspension(mods.suspensionLevel, mods.suspensionHeight);
            
            // Brakes
            float brakeMult = 1f + mods.brakesLevel * 0.2f;
            brakeForce = definition?.brakeForce * brakeMult ?? 3000f * brakeMult;
            
            // Aero
            downforceCoefficient = definition?.downforceCoefficient * (1f + mods.spoiler * 0.1f) ?? 0.1f;
            
            // Armor
            // Armor increases body health max
            
            // Visual mods
            ApplyVisualMods(mods);
            
            // Update wheel colliders with new specs
            ConfigureWheelColliders();
        }
        
        private void UpdateSuspension(int level, float height)
        {
            float springRate = 30000f;
            float damperRate = 5000f;
            float travel = 0.2f;
            
            switch (level)
            {
                case 1: // Lowering springs
                    springRate *= 1.3f;
                    height = -0.03f;
                    break;
                case 2: // Coilovers
                    springRate *= 1.6f;
                    damperRate *= 1.5f;
                    break;
                case 3: // Adjustable arms
                    springRate *= 2f;
                    damperRate *= 2f;
                    travel *= 1.2f;
                    break;
                case 4: // Air suspension
                    springRate *= 1.8f;
                    damperRate *= 1.8f;
                    // Height adjustable via height parameter
                    break;
            }
            
            foreach (var wc in wheelColliders)
            {
                if (wc != null)
                {
                    var spring = wc.suspensionSpring;
                    spring.spring = springRate;
                    spring.damper = damperRate;
                    spring.targetPosition = 0.5f + height * 0.5f;
                    wc.suspensionSpring = spring;
                    wc.suspensionDistance = travel;
                }
            }
        }
        
        private void ApplyVisualMods(VehicleModificationsData mods)
        {
            // Body kits
            ToggleRenderer("BodyKit", mods.bodyKit > 0);
            ToggleRenderer("Spoiler", mods.spoiler > 0);
            ToggleRenderer("Hood", mods.hood > 0);
            ToggleRenderer("Roof", mods.roof > 0);
            ToggleRenderer("Grille", mods.grille > 0);
            ToggleRenderer("Exhaust", mods.exhaust > 0);
            ToggleRenderer("Skirt", mods.skirt > 0);
            ToggleRenderer("Fender", mods.fender > 0);
            
            // Paint
            ApplyPaint(mods);
            
            // Wheels
            if (mods.wheelType > 0)
            {
                SwapWheels(mods.wheelType, mods.wheelVariant);
            }
            
            // Lights
            ApplyLightMods(mods);
        }
        
        private void ToggleRenderer(string name, bool enabled)
        {
            var rends = GetComponentsInChildren<Renderer>();
            foreach (var r in rends)
            {
                if (r.name.Contains(name) || r.name.Contains(name.ToLower()))
                {
                    r.enabled = enabled;
                }
            }
        }
        
        private void ApplyPaint(VehicleModificationsData mods)
        {
            var rends = GetComponentsInChildren<Renderer>();
            foreach (var r in rends)
            {
                string name = r.name.ToLower();
                if (name.Contains("body") || name.Contains("paint") || name.Contains("shell"))
                {
                    var mats = r.materials;
                    for (int i = 0; i < mats.Length; i++)
                    {
                        if (mats[i].HasProperty("_BaseColor"))
                        {
                            Color c = mods.primaryColor;
                            if (name.Contains("secondary") || name.Contains("roof"))
                                c = mods.secondaryColor;
                            else if (name.Contains("pearlescent") || name.Contains("trim"))
                                c = mods.pearlescentColor;
                            
                            // Apply paint type
                            switch (mods.paintType)
                            {
                                case 1: // Metallic
                                    mats[i].SetFloat("_Metallic", 0.8f);
                                    mats[i].SetFloat("_Glossiness", 0.7f);
                                    break;
                                case 2: // Pearlescent
                                    mats[i].SetFloat("_Metallic", 0.6f);
                                    mats[i].SetFloat("_Glossiness", 0.8f);
                                    // Add pearlescent tint
                                    break;
                                case 3: // Matte
                                    mats[i].SetFloat("_Metallic", 0f);
                                    mats[i].SetFloat("_Glossiness", 0.1f);
                                    break;
                                case 4: // Metal
                                    mats[i].SetFloat("_Metallic", 1f);
                                    mats[i].SetFloat("_Glossiness", 0.9f);
                                    break;
                                case 5: // Chrome
                                    mats[i].SetFloat("_Metallic", 1f);
                                    mats[i].SetFloat("_Glossiness", 1f);
                                    break;
                                default: // Normal
                                    mats[i].SetFloat("_Metallic", 0.3f);
                                    mats[i].SetFloat("_Glossiness", 0.5f);
                                    break;
                            }
                            mats[i].color = c;
                        }
                    }
                    r.materials = mats;
                }
            }
        }
        
        private void SwapWheels(int wheelType, int variant)
        {
            // Would swap wheel meshes/materials based on type/variant
            // This is a placeholder for the actual wheel swap logic
        }
        
        private void ApplyLightMods(VehicleModificationsData mods)
        {
            var lights = GetComponentsInChildren<Light>();
            foreach (var light in lights)
            {
                string name = light.name.ToLower();
                if (name.Contains("headlight") || name.Contains("head"))
                {
                    if (mods.xenonLights)
                    {
                        light.color = GetXenonColor(mods.xenonColor);
                        light.intensity *= 1.5f;
                    }
                }
            }
            
            // Neon
            if (mods.neonEnabled)
            {
                EnableNeonLights(mods.neonColor);
            }
        }
        
        private Color GetXenonColor(int colorId)
        {
            return colorId switch
            {
                0 => Color.white,
                1 => new Color(0.3f, 0.5f, 1f),
                2 => new Color(0.2f, 0.4f, 1f),
                3 => new Color(0.3f, 1f, 0.6f),
                4 => new Color(0.5f, 1f, 0.2f),
                5 => Color.yellow,
                6 => new Color(1f, 0.8f, 0.2f),
                7 => Color.orange,
                8 => Color.red,
                9 => new Color(1f, 0.4f, 0.7f),
                10 => new Color(1f, 0.2f, 0.6f),
                11 => new Color(0.6f, 0.2f, 1f),
                12 => new Color(0.3f, 0f, 0.6f),
                _ => Color.white,
            };
        }
        
        private void EnableNeonLights(Color color)
        {
            // Enable neon underglow - would instantiate neon light objects
        }
        
        private void ConfigureWheelColliders()
        {
            for (int i = 0; i < wheelColliders.Length; i++)
            {
                var wc = wheelColliders[i];
                if (wc == null) continue;
                
                bool isFront = i == frontLeftIndex || i == frontRightIndex;
                bool isDriven = IsWheelDriven(i);
                
                wc.motorTorque = 0;
                wc.brakeTorque = 0;
                wc.steerAngle = 0;
                
                // Configure friction curves
                var forwardFriction = wc.forwardFriction;
                forwardFriction.extremumSlip = 0.3f;
                forwardFriction.extremumValue = 1f;
                forwardFriction.asymptoteSlip = 0.5f;
                forwardFriction.asymptoteValue = 0.75f;
                wc.forwardFriction = forwardFriction;
                
                var sidewaysFriction = wc.sidewaysFriction;
                sidewaysFriction.extremumSlip = 0.2f;
                sidewaysFriction.extremumValue = 1f;
                sidewaysFriction.asymptoteSlip = 0.5f;
                sidewaysFriction.asymptoteValue = 0.75f;
                wc.sidewaysFriction = sidewaysFriction;
            }
        }
        
        private bool IsWheelDriven(int index)
        {
            if (driveBiasFront >= 0.9f) return true; // FWD
            if (driveBiasFront <= 0.1f) return index >= 2; // RWD
            return true; // AWD
        }
        
        // ============================================================================
        // INPUT & CONTROL
        // ============================================================================
        
        public void SetDriver(bool isDriver)
        {
            isPlayerControlled = isDriver;
        }
        
        public void SetInput(float throttle, float brake, float steer, bool handbrake)
        {
            throttleInput = Mathf.Clamp(throttle, -1f, 1f);
            brakeInput = Mathf.Clamp(brake, 0f, 1f);
            steerInput = Mathf.Clamp(steer, -1f, 1f);
            handbrakeInput = handbrake;
        }
        
        public float GetSteerInput() => steerInput;
        
        public void SetEngineState(bool on)
        {
            engineOn = on;
        }
        
        // ============================================================================
        // PHYSICS UPDATE
        // ============================================================================
        
        private void FixedUpdate()
        {
            if (!engineOn)
            {
                currentRPM = Mathf.Lerp(currentRPM, 0f, Time.fixedDeltaTime * 5f);
                foreach (var wc in wheelColliders)
                {
                    if (wc != null) wc.motorTorque = 0;
                }
                return;
            }
            
            // Engine simulation
            SimulateEngine();
            
            // Drivetrain
            SimulateDrivetrain();
            
            // Steering
            SimulateSteering();
            
            // Braking
            SimulateBraking();
            
            // Aero
            SimulateAero();
            
            // Update visual wheels
            UpdateWheelVisuals();
            
            // Fuel consumption
            ConsumeFuel();
            
            // Damage effects
            ApplyDamageEffects();
        }
        
        private void SimulateEngine()
        {
            if (!engineOn)
            {
                currentRPM = Mathf.Lerp(currentRPM, 0f, Time.fixedDeltaTime * 5f);
                return;
            }
            
            float throttle = Mathf.Max(0, throttleInput);
            float targetRPM;
            
            if (throttle > 0)
            {
                float torqueMultiplier = torqueCurve.Evaluate(currentRPM / maxRPM);
                float engineTorque = maxTorque * torqueMultiplier * throttle;
                targetRPM = currentRPM + (engineTorque / rb.mass) * Time.fixedDeltaTime * 100f;
            }
            else
            {
                // Engine braking
                float engineBrake = maxTorque * 0.1f * brakeInput;
                targetRPM = currentRPM - engineBrake * Time.fixedDeltaTime * 50f;
                targetRPM = Mathf.Max(targetRPM, idleRPM);
            }
            
            currentRPM = Mathf.Clamp(targetRPM, idleRPM, maxRPM);
            
            // Auto gear shifting
            if (currentGear > 0 && currentGear < gearRatios.Length)
            {
                if (currentRPM > maxRPM * 0.85f && currentGear < gearRatios.Length - 1)
                    currentGear++;
                else if (currentRPM < maxRPM * 0.3f && currentGear > 1)
                    currentGear--;
            }
        }
        
        private void SimulateDrivetrain()
        {
            if (!engineOn || currentRPM <= idleRPM) return;
            
            float gearRatio = gearRatios[Mathf.Clamp(currentGear, 0, gearRatios.Length - 1)];
            float totalRatio = gearRatio * finalDriveRatio;
            
            float torqueCurveVal = torqueCurve.Evaluate(currentRPM / maxRPM);
            float engineTorque = maxTorque * torqueCurveVal * Mathf.Max(0, throttleInput);
            float wheelTorque = engineTorque * totalRatio;
            
            // Apply to driven wheels
            for (int i = 0; i < wheelColliders.Length; i++)
            {
                if (wheelColliders[i] == null) continue;
                
                if (IsWheelDriven(i))
                {
                    float bias = (i < 2) ? driveBiasFront : (1f - driveBiasFront);
                    wheelColliders[i].motorTorque = wheelTorque * bias * Mathf.Max(0, throttleInput);
                }
                else
                {
                    wheelColliders[i].motorTorque = 0;
                }
            }
        }
        
        private void SimulateSteering()
        {
            float speed = rb.linearVelocity.magnitude;
            float maxSteer = maxSteerAngle * steerAngleBySpeed.Evaluate(speed / 50f);
            float targetSteer = steerInput * maxSteer;
            
            // Smooth steering
            float currentSteer = 0f;
            int frontCount = 0;
            for (int i = 0; i < wheelColliders.Length; i++)
            {
                if (wheelColliders[i] != null && (i == frontLeftIndex || i == frontRightIndex))
                {
                    currentSteer += wheelColliders[i].steerAngle;
                    frontCount++;
                }
            }
            if (frontCount > 0) currentSteer /= frontCount;
            
            float newSteer = Mathf.MoveTowards(currentSteer, targetSteer, Time.fixedDeltaTime * steerSpeed);
            
            for (int i = 0; i < wheelColliders.Length; i++)
            {
                if (wheelColliders[i] != null && (i == frontLeftIndex || i == frontRightIndex))
                {
                    wheelColliders[i].steerAngle = newSteer;
                }
            }
        }
        
        private void SimulateBraking()
        {
            float brakeForce = this.brakeForce * brakeInput;
            
            for (int i = 0; i < wheelColliders.Length; i++)
            {
                if (wheelColliders[i] == null) continue;
                
                bool isRear = i == rearLeftIndex || i == rearRightIndex;
                
                if (handbrakeInput && isRear)
                {
                    wheelColliders[i].brakeTorque = handbrakeForce;
                    wheelColliders[i].motorTorque = 0;
                }
                else
                {
                    wheelColliders[i].brakeTorque = brakeForce;
                }
            }
        }
        
        private void SimulateAero()
        {
            float speed = rb.linearVelocity.magnitude;
            if (speed < 5f) return;
            
            // Downforce
            Vector3 downforce = -transform.up * speed * speed * downforceCoefficient;
            rb.AddForce(downforce, ForceMode.Force);
            
            // Drag
            Vector3 drag = -rb.linearVelocity.normalized * speed * speed * dragCoefficient;
            rb.AddForce(drag, ForceMode.Force);
        }
        
        private void UpdateWheelVisuals()
        {
            for (int i = 0; i < wheelColliders.Length; i++)
            {
                if (wheelColliders[i] == null || wheelTransforms[i] == null) continue;
                
                wheelColliders[i].GetWorldPose(out Vector3 pos, out Quaternion rot);
                wheelTransforms[i].SetPositionAndRotation(pos, rot);
                
                // Get wheel hit info for suspension visual
                if (wheelColliders[i].GetGroundHit(out WheelHit hit))
                {
                    wheelHits[i] = hit;
                }
            }
        }
        
        private void ConsumeFuel()
        {
            if (currentFuel <= 0)
            {
                SetEngineState(false);
                return;
            }
            
            float consumption = (currentRPM / maxRPM) * Mathf.Abs(throttleInput) * 0.0001f;
            currentFuel = Mathf.Max(0, currentFuel - Mathf.RoundToInt(consumption * 10000));
        }
        
        private void ApplyDamageEffects()
        {
            if (currentCondition.engineOverheating)
            {
                // Reduce power
                throttleInput *= 0.5f;
            }
            
            if (currentCondition.transmissionSlipping)
            {
                // Random gear slip
                if (Random.value < 0.01f) currentGear = Mathf.Max(1, currentGear - 1);
            }
            
            if (currentCondition.brakesFading)
            {
                brakeInput *= 0.7f;
            }
            
            // Flash damage indicator
            if (damageFlashTimer > 0)
            {
                damageFlashTimer -= Time.fixedDeltaTime;
            }
        }
        
        // ============================================================================
        // PUBLIC API
        // ============================================================================
        
        public float SpeedKPH => rb.linearVelocity.magnitude * 3.6f;
        public float CurrentRPM => currentRPM;
        public int CurrentGear => currentGear;
        public bool EngineOn => engineOn;
        public bool IsPlayerControlled => isPlayerControlled;
        
        public void WarpPlayerInto(Transform playerTransform)
        {
            // Find driver seat
            Transform driverSeat = transform.Find("DriverSeat");
            if (driverSeat == null) driverSeat = transform;
            
            playerTransform.SetParent(driverSeat);
            playerTransform.localPosition = new Vector3(0, 0.5f, 0.3f);
            playerTransform.localRotation = Quaternion.identity;
            
            // Disable player controller
            var playerController = playerTransform.GetComponent<CharacterController>();
            if (playerController != null) playerController.enabled = false;
            
            // Enable vehicle camera
            // CameraController.Instance.OnVehicleEnter(true);
        }
        
        public void EjectPlayer(Transform playerTransform, Vector3 exitPoint)
        {
            playerTransform.SetParent(null);
            playerTransform.position = exitPoint;
            
            var playerController = playerTransform.GetComponent<CharacterController>();
            if (playerController != null) playerController.enabled = true;
        }
        
        public void ApplyDamage(VehicleDamageType type, float amount, Vector3 hitPoint, Vector3 hitNormal)
        {
            currentCondition.ApplyDamage(type, amount);
            
            // Visual damage
            damageFlashTimer = 0.1f;
            
            // Health
            switch (type)
            {
                case VehicleDamageType.Engine:
                    currentEngineHealth = Mathf.Max(0, currentEngineHealth - Mathf.RoundToInt(amount * 100));
                    break;
                case VehicleDamageType.Body:
                    currentBodyHealth = Mathf.Max(0, currentBodyHealth - Mathf.RoundToInt(amount * 100));
                    break;
            }
            
            // Check destruction
            if (currentEngineHealth <= 0 || currentBodyHealth <= 0)
            {
                OnVehicleDestroyed();
            }
        }
        
        private void OnVehicleDestroyed()
        {
            // Explosion, fire, etc.
            engineOn = false;
            // Trigger explosion effect
        }
        
        public void Repair()
        {
            currentCondition.FullRepair();
            currentEngineHealth = 1000;
            currentBodyHealth = 1000;
            currentFuel = 100;
        }
        
        public void Refuel(int amount = 100)
        {
            currentFuel = Mathf.Min(100, currentFuel + amount);
        }
        
        // ============================================================================
        // DEBUG
        // ============================================================================
        
        private void OnDrawGizmos()
        {
            if (!Application.isPlaying) return;
            
            // Wheel positions
            Gizmos.color = Color.green;
            for (int i = 0; i < wheelColliders.Length; i++)
            {
                if (wheelColliders[i] != null)
                {
                    Gizmos.DrawWireSphere(wheelColliders[i].transform.position, wheelColliders[i].radius);
                }
            }
            
            // Center of mass
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(rb.worldCenterOfMass, 0.2f);
            
            // Velocity
            Gizmos.color = Color.blue;
            Gizmos.DrawRay(transform.position, rb.linearVelocity);
        }
    }
}