package systems

import (
	"math"

	"github.com/megame/server/internal/components"
	"github.com/megame/server/internal/ecs"
)

// VehicleSystem handles vehicle physics and logic
type VehicleSystem struct {
	Priority_ int
}

func NewVehicleSystem() *VehicleSystem {
	return &VehicleSystem{Priority_: 20}
}

func (s *VehicleSystem) Priority() int { return s.Priority_ }

func (s *VehicleSystem) Update(world *ecs.World, dt float32) {
	query := world.Query(components.CompTransform, components.CompVehicle, components.CompPhysics)
	query.Iterate(func(entity ecs.EntityID) {
		transform, _ := world.GetComponent(entity, components.CompTransform)
		vehicle, _ := world.GetComponent(entity, components.CompVehicle)
		physics, _ := world.GetComponent(entity, components.CompPhysics)

		t := transform.(*components.TransformComponent)
		v := vehicle.(*components.VehicleComponent)
		p := physics.(*components.PhysicsComponent)

		if !v.EngineOn {
			// Engine off - apply stronger drag
			p.Velocity = p.Velocity.Mul(1.0 - 0.1*dt)
			v.RPM = 0
			v.SpeedKPH = 0
			v.CurrentGear = 0
			return
		}

		// Find driver
		var driverInput *VehicleInput
		for _, occID := range v.Occupants {
			if occ, ok := world.GetComponent(occID, components.CompPlayer); ok {
				player := occ.(*components.PlayerComponent)
				if player.VehicleSeatIndex == 0 { // Driver seat
					// Get input from player (would come from network)
					driverInput = s.getDriverInput(occID)
					break
				}
			}
		}

		if driverInput != nil {
			s.simulateVehicle(t, v, p, driverInput, dt)
		} else {
			// No driver - idle
			v.RPM = v.RPM * 0.95
			if v.RPM < 800 {
				v.RPM = 800
			}
		}

		// Update wheel states
		s.updateWheels(t, v, p, dt)

		// Update position
		t.PrevPos = t.Position
		t.Position = t.Position.Add(p.Velocity.Mul(dt))
		t.PrevRot = t.Rotation
	})
}

type VehicleInput struct {
	Throttle  float32 // -1 to 1 (negative = brake/reverse)
	Steer     float32 // -1 to 1
	Handbrake bool
	Horn      bool
	Lights    bool
	Siren     bool
}

func (s *VehicleSystem) getDriverInput(driverID ecs.EntityID) *VehicleInput {
	// In real implementation, this would fetch from network input queue
	// For now, return nil (idle)
	return nil
}

func (s *VehicleSystem) simulateVehicle(t *components.TransformComponent, v *components.VehicleComponent, p *components.PhysicsComponent, input *VehicleInput, dt float32) {
	// Get vehicle specs (would come from definition database)
	specs := s.getVehicleSpecs(v.ModelID)

	// Calculate engine torque based on RPM and gear
	engineTorque := s.calculateEngineTorque(v, specs)

	// Apply throttle
	if input.Throttle > 0 {
		v.RPM += engineTorque * input.Throttle * dt * 100
	} else if input.Throttle < 0 {
		// Braking
		brakeForce := specs.BrakeForce * (-input.Throttle) * dt
		speed := p.Velocity.Length()
		if speed > 0.1 {
			brakeDir := p.Velocity.Normalized().Mul(-1)
			p.Velocity = p.Velocity.Add(brakeDir.Mul(brakeForce))
		}
		v.RPM -= 200 * dt
	} else {
		// Idle
		v.RPM += (800 - v.RPM) * 5 * dt
	}

	// Clamp RPM
	maxRPM := specs.MaxRPM
	if v.RPM > maxRPM {
		v.RPM = maxRPM
	}
	if v.RPM < 800 {
		v.RPM = 800
	}

	// Gear shifting logic
	s.updateGear(v, specs)

	// Calculate wheel torque
	wheelTorque := engineTorque * specs.GearRatios[v.CurrentGear] * specs.FinalDriveRatio * specs.DriveBiasFront

	// Apply to velocity (simplified - only forward)
	forward := t.Forward()
	driveForce := forward.Mul(wheelTorque * input.Throttle * dt)
	p.Velocity = p.Velocity.Add(driveForce)

	// Steering
	if input.Steer != 0 && p.Velocity.Length() > 0.5 {
		steerAngle := input.Steer * specs.MaxSteerAngle
		s.applySteering(t, p, steerAngle, dt)
	}

	// Handbrake
	if input.Handbrake {
		s.applyHandbrake(p, v, dt)
	}

	// Drag and rolling resistance
	speed := p.Velocity.Length()
	dragForce := specs.DragCoefficient * speed * speed * dt
	rollingResistance := specs.RollingResistance * dt
	p.Velocity = p.Velocity.Mul(1.0 - dragForce - rollingResistance)

	// Update derived values
	v.SpeedKPH = speed * 3.6
}

func (s *VehicleSystem) getVehicleSpecs(modelID string) *VehicleSpecs {
	// In real implementation, load from definition database
	// Return defaults for now
	return &VehicleSpecs{
		MaxSpeed:          200.0 / 3.6, // m/s
		MaxRPM:            7000,
		IdleRPM:           800,
		MaxTorque:         400.0,
		TorqueCurve:       []float32{0.3, 0.5, 0.8, 1.0, 0.9, 0.7, 0.5},
		GearRatios:        []float32{3.5, 2.2, 1.5, 1.1, 0.9, 0.7},
		FinalDriveRatio:   3.7,
		DriveBiasFront:    0.0, // RWD
		MaxSteerAngle:     0.6, // radians
		BrakeForce:        8000.0,
		DragCoefficient:   0.32,
		RollingResistance: 0.015,
		Mass:              1500.0,
		WheelBase:         2.8,
		TrackWidth:        1.6,
		CenterOfMass:      components.Vector3{X: 0, Y: -0.3, Z: 0},
	}
}

type VehicleSpecs struct {
	MaxSpeed          float32
	MaxRPM            float32
	IdleRPM           float32
	MaxTorque         float32
	TorqueCurve       []float32
	GearRatios        []float32
	FinalDriveRatio   float32
	DriveBiasFront    float32
	MaxSteerAngle     float32
	BrakeForce        float32
	DragCoefficient   float32
	RollingResistance float32
	Mass              float32
	WheelBase         float32
	TrackWidth        float32
	CenterOfMass      components.Vector3
}

func (s *VehicleSystem) calculateEngineTorque(v *components.VehicleComponent, specs *VehicleSpecs) float32 {
	// Simple torque curve based on RPM
	normalizedRPM := (v.RPM - specs.IdleRPM) / (specs.MaxRPM - specs.IdleRPM)
	if normalizedRPM < 0 {
		normalizedRPM = 0
	}
	if normalizedRPM > 1 {
		normalizedRPM = 1
	}

	index := normalizedRPM * float32(len(specs.TorqueCurve)-1)
	i := int(index)
	if i >= len(specs.TorqueCurve)-1 {
		return specs.MaxTorque * specs.TorqueCurve[len(specs.TorqueCurve)-1]
	}
	t := index - float32(i)
	torque := specs.TorqueCurve[i]*(1-t) + specs.TorqueCurve[i+1]*t

	// Apply engine mods
	torque *= 1.0 + float32(v.Mods.EngineLevel)*0.15
	if v.Mods.TurboLevel > 0 {
		torque *= 1.3
	}

	return specs.MaxTorque * torque
}

func (s *VehicleSystem) updateGear(v *components.VehicleComponent, specs *VehicleSpecs) {
	// Simple automatic transmission
	if v.CurrentGear == 0 && v.RPM > specs.IdleRPM {
		v.CurrentGear = 1
	}

	if v.CurrentGear > 0 && v.CurrentGear < int32(len(specs.GearRatios)) {
		// Shift up at high RPM
		if v.RPM > specs.MaxRPM*0.85 && v.CurrentGear < int32(len(specs.GearRatios))-1 {
			v.CurrentGear++
		}
		// Shift down at low RPM
		if v.RPM < specs.MaxRPM*0.3 && v.CurrentGear > 1 {
			v.CurrentGear--
		}
	}
}

func (s *VehicleSystem) applySteering(t *components.TransformComponent, p *components.PhysicsComponent, steerAngle float32, dt float32) {
	speed := p.Velocity.Length()
	if speed < 0.1 {
		return
	}

	// Calculate angular velocity from steering
	// Angular velocity = speed * tan(steerAngle) / wheelbase
	wheelbase := float32(2.8) // Would come from specs
	angularVel := speed * float32(math.Tan(float64(steerAngle))) / wheelbase

	// Apply to physics
	p.AngularVelocity.Y = angularVel

	// Update rotation
	yaw := angularVel * dt
	currentYaw, _, _ := t.Rotation.ToEuler()
	t.Rotation = components.QuaternionFromEuler(0, currentYaw+yaw, 0)
}

func (s *VehicleSystem) applyHandbrake(p *components.PhysicsComponent, v *components.VehicleComponent, dt float32) {
	// Lock rear wheels
	speed := p.Velocity.Length()
	if speed > 0.5 {
		// Apply lateral friction to simulate slide
		right := components.Vector3{X: p.Velocity.Z, Y: 0, Z: -p.Velocity.X}.Normalized()
		lateralSpeed := p.Velocity.Dot(right)
		if lateralSpeed != 0 {
			friction := right.Mul(-lateralSpeed * 5.0 * dt)
			p.Velocity = p.Velocity.Add(friction)
		}
	}
}

func (s *VehicleSystem) updateWheels(t *components.TransformComponent, v *components.VehicleComponent, p *components.PhysicsComponent, dt float32) {
	speed := p.Velocity.Length()

	for i := range v.Wheels {
		wheel := &v.Wheels[i]

		// Rotation based on speed
		wheelRadius := float32(0.35) // Would come from wheel definition
		wheel.Rotation += speed / wheelRadius * dt

		// Steering angle for front wheels
		if i == 0 || i == 1 { // Front wheels
			// Would get from input
			wheel.SteerAngle = 0
		}

		// Suspension (simplified)
		wheel.SuspensionComp = 0.5 // Would do raycast

		// Brake force
		wheel.BrakeForce = 0
	}
}
