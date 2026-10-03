package systems

import (
	"github.com/megame/server/internal/components"
	"github.com/megame/server/internal/ecs"
)

// MovementSystem handles player and NPC movement
type MovementSystem struct {
	Priority_ int
}

func NewMovementSystem() *MovementSystem {
	return &MovementSystem{Priority_: 10}
}

func (s *MovementSystem) Priority() int { return s.Priority_ }

func (s *MovementSystem) Update(world *ecs.World, dt float32) {
	// Player movement
	playerQuery := world.Query(components.CompTransform, components.CompPlayer, components.CompPhysics)
	playerQuery.Iterate(func(entity ecs.EntityID) {
		transform, _ := world.GetComponent(entity, components.CompTransform)
		physics, _ := world.GetComponent(entity, components.CompPhysics)
		player, _ := world.GetComponent(entity, components.CompPlayer)

		t := transform.(*components.TransformComponent)
		p := physics.(*components.PhysicsComponent)
		pl := player.(*components.PlayerComponent)

		if pl.IsInVehicle {
			return // Vehicle movement handled by VehicleSystem
		}

		// Apply gravity
		if p.UseGravity && !p.IsKinematic {
			p.Velocity.Y -= 9.81 * dt
		}

		// Apply drag
		if p.Velocity.LengthSq() > 0.001 {
			drag := p.Drag * dt
			p.Velocity = p.Velocity.Mul(1 - drag)
		}

		// Update position
		t.PrevPos = t.Position
		t.Position = t.Position.Add(p.Velocity.Mul(dt))

		// Update rotation from physics angular velocity
		if p.AngularVelocity.LengthSq() > 0.001 {
			// Simplified: just update yaw for now
			yaw := p.AngularVelocity.Y * dt
			currentYaw, _, _ := t.Rotation.ToEuler()
			t.Rotation = components.QuaternionFromEuler(0, currentYaw+yaw, 0)
		}

		t.PrevRot = t.Rotation
	})

	// NPC movement (AI-driven)
	npcQuery := world.Query(components.CompTransform, components.CompNPC, components.CompPhysics, components.CompAI)
	npcQuery.Iterate(func(entity ecs.EntityID) {
		transform, _ := world.GetComponent(entity, components.CompTransform)
		physics, _ := world.GetComponent(entity, components.CompPhysics)
		npc, _ := world.GetComponent(entity, components.CompNPC)
		ai, _ := world.GetComponent(entity, components.CompAI)

		t := transform.(*components.TransformComponent)
		p := physics.(*components.PhysicsComponent)
		n := npc.(*components.NPCComponent)
		a := ai.(*components.AIComponent)

		if n.IsFleeing {
			s.handleFlee(t, p, n, a, dt)
		} else if n.IsInCombat {
			s.handleCombatMovement(t, p, n, a, world, dt)
		} else {
			s.handlePatrolWander(t, p, n, a, dt)
		}

		// Apply gravity
		if p.UseGravity && !p.IsKinematic {
			p.Velocity.Y -= 9.81 * dt
		}

		// Apply drag
		if p.Velocity.LengthSq() > 0.001 {
			drag := p.Drag * dt
			p.Velocity = p.Velocity.Mul(1 - drag)
		}

		t.PrevPos = t.Position
		t.Position = t.Position.Add(p.Velocity.Mul(dt))
		t.PrevRot = t.Rotation
	})
}

func (s *MovementSystem) handleFlee(t *components.TransformComponent, p *components.PhysicsComponent, n *components.NPCComponent, a *components.AIComponent, dt float32) {
	// Flee from target
	if n.TargetEntity != 0 {
		if targetTransform, ok := world.GetComponent(n.TargetEntity, components.CompTransform); ok {
			targetT := targetTransform.(*components.TransformComponent)
			dir := t.Position.Sub(targetT.Position).Normalized()
			p.Velocity = p.Velocity.Add(dir.Mul(5.0 * dt)) // Flee speed
		}
	}
}

func (s *MovementSystem) handleCombatMovement(t *components.TransformComponent, p *components.PhysicsComponent, n *components.NPCComponent, a *components.AIComponent, world *ecs.World, dt float32) {
	if n.TargetEntity == 0 {
		return
	}

	targetTransform, ok := world.GetComponent(n.TargetEntity, components.CompTransform)
	if !ok {
		return
	}

	targetT := targetTransform.(*components.TransformComponent)
	toTarget := targetT.Position.Sub(t.Position)
	distance := toTarget.Length()

	// Get weapon range
	var optimalRange float32 = 15.0
	if n.CurrentWeaponIndex >= 0 && n.CurrentWeaponIndex < int32(len(n.Weapons)) {
		if weaponComp, ok := world.GetComponent(n.Weapons[n.CurrentWeaponIndex], components.CompWeapon); ok {
			weapon := weaponComp.(*components.WeaponComponent)
			// Use weapon definition for range
			optimalRange = 20.0 // Default, would come from weapon definition
		}
	}

	// Move to optimal range
	if distance > optimalRange+2 {
		// Move closer
		dir := toTarget.Normalized()
		p.Velocity = p.Velocity.Add(dir.Mul(3.0 * dt))
	} else if distance < optimalRange-5 {
		// Move back
		dir := toTarget.Normalized().Mul(-1)
		p.Velocity = p.Velocity.Add(dir.Mul(2.0 * dt))
	}

	// Face target
	if toTarget.LengthSq() > 0.001 {
		yaw := float32(math.Atan2(float64(toTarget.X), float64(toTarget.Z)))
		t.Rotation = components.QuaternionFromEuler(0, yaw, 0)
	}
}

func (s *MovementSystem) handlePatrolWander(t *components.TransformComponent, p *components.PhysicsComponent, n *components.NPCComponent, a *components.AIComponent, dt float32) {
	// Simple patrol logic
	if len(n.PatrolPoints) > 0 {
		target := n.PatrolPoints[n.CurrentPatrolIndex]
		toTarget := target.Sub(t.Position)
		distance := toTarget.Length()

		if distance < 1.0 {
			n.CurrentPatrolIndex = (n.CurrentPatrolIndex + 1) % int32(len(n.PatrolPoints))
			return
		}

		dir := toTarget.Normalized()
		p.Velocity = p.Velocity.Add(dir.Mul(1.5 * dt))

		// Face movement direction
		if dir.LengthSq() > 0.001 {
			yaw := float32(math.Atan2(float64(dir.X), float64(dir.Z)))
			t.Rotation = components.QuaternionFromEuler(0, yaw, 0)
		}
	} else if n.WanderRadius > 0 {
		// Wander around home position
		toHome := n.HomePosition.Sub(t.Position)
		distance := toHome.Length()

		if distance > n.WanderRadius {
			dir := toHome.Normalized()
			p.Velocity = p.Velocity.Add(dir.Mul(1.0 * dt))
		}
	}
}

// InputSystem processes player input (server-authoritative)
type InputSystem struct {
	Priority_ int
}

func NewInputSystem() *InputSystem {
	return &InputSystem{Priority_: 5}
}

func (s *InputSystem) Priority() int { return s.Priority_ }

type PlayerInput struct {
	Move         components.Vector2
	Look         components.Vector2
	Jump         bool
	Sprint       bool
	Crouch       bool
	Prone        bool
	Interact     bool
	Attack       bool
	Aim          bool
	Reload       bool
	WeaponWheel  bool
	WeaponSlot   int32
	EnterVehicle bool
	ExitVehicle  bool
	VehicleControl components.Vector2
	VehicleHandbrake bool
	VehicleHorn  bool
	VehicleLights bool
	VehicleSiren bool
}

func (s *InputSystem) Update(world *ecs.World, dt float32) {
	// This would process queued inputs from network
	// For now, just a placeholder
	_ = dt
}