package systems

import (
	"math"
	"math/rand"

	"github.com/megame/server/internal/components"
	"github.com/megame/server/internal/ecs"
)

// WeaponSystem handles shooting, reloading, weapon switching
type WeaponSystem struct {
	Priority_ int
	rng       *rand.Rand
}

func NewWeaponSystem() *WeaponSystem {
	return &WeaponSystem{
		Priority_: 30,
		rng:       rand.New(rand.NewSource(12345)),
	}
}

func (s *WeaponSystem) Priority() int { return s.Priority_ }

func (s *WeaponSystem) Update(world *ecs.World, dt float32) {
	currentTime := float64(dt) // Would use actual server time

	// Process weapon firing
	query := world.Query(components.CompWeapon, components.CompTransform)
	query.Iterate(func(entity ecs.EntityID) {
		weapon, _ := world.GetComponent(entity, components.CompWeapon)
		transform, _ := world.GetComponent(entity, components.CompTransform)

		w := weapon.(*components.WeaponComponent)
		t := transform.(*components.TransformComponent)

		// Handle reloading
		if w.IsReloading {
			w.ReloadProgress += dt / s.getReloadTime(w)
			if w.ReloadProgress >= 1.0 {
				w.IsReloading = false
				w.ReloadProgress = 0
				w.AmmoInClip = w.MaxClipSize
			}
			return
		}

		// Handle heat dissipation
		if w.Heat > 0 {
			w.Heat -= dt * 0.1 // Cool down rate
			if w.Heat < 0 {
				w.Heat = 0
			}
		}

		// Check if weapon wants to fire (would be triggered by input)
		// This is server-side simulation - actual firing triggered by RPC
		_ = currentTime
		_ = t
	})
}

// FireWeapon processes a fire request from a player
func (s *WeaponSystem) FireWeapon(world *ecs.World, weaponEntity ecs.EntityID, startPos, direction components.Vector3) *FireResult {
	weapon, ok := world.GetComponent(weaponEntity, components.CompWeapon)
	if !ok {
		return &FireResult{Success: false, Reason: "Weapon not found"}
	}

	w := weapon.(*components.WeaponComponent)

	// Check if can fire
	if w.IsReloading {
		return &FireResult{Success: false, Reason: "Reloading"}
	}

	if w.AmmoInClip <= 0 {
		// Auto-reload
		s.StartReload(world, weaponEntity)
		return &FireResult{Success: false, Reason: "Empty clip"}
	}

	// Check fire rate
	// Would check against last fire time

	// Check heat
	if w.Heat >= 1.0 {
		return &FireResult{Success: false, Reason: "Overheated"}
	}

	// Get weapon definition
	def := s.getWeaponDefinition(w.ModelID)

	// Consume ammo
	w.AmmoInClip--

	// Apply heat
	w.Heat += def.HeatPerShot

	// Calculate spread
	spread := s.calculateSpread(w, def)

	// Fire rays
	results := make([]HitResult, 0, def.Pellets)
	for i := 0; i < def.Pellets; i++ {
		rayDir := s.applySpread(direction, spread, def)
		hit := s.raycast(world, startPos, rayDir, def.Range, weaponEntity)
		results = append(results, hit)
	}

	// Apply damage
	for _, hit := range results {
		if hit.Entity != 0 {
			s.applyDamage(world, hit.Entity, def.Damage, hit.Point, hit.Normal, weaponEntity)
		}
	}

	// Update condition
	if w.Condition != nil {
		w.Condition.RoundsFired++
		w.Condition.Dirt += 0.001
		w.Condition.Carbon += 0.0005
		if w.Condition.Durability < 0.2 && s.rng.Float32() < 0.01 {
			w.Condition.IsJammed = true
		}
	}

	return &FireResult{
		Success:       true,
		Hits:          results,
		AmmoRemaining: w.AmmoInClip,
		Heat:          w.Heat,
	}
}

type FireResult struct {
	Success       bool
	Reason        string
	Hits          []HitResult
	AmmoRemaining int32
	Heat          float32
}

type HitResult struct {
	Entity   ecs.EntityID
	Point    components.Vector3
	Normal   components.Vector3
	Distance float32
	Bone     string
	Damage   float32
}

func (s *WeaponSystem) StartReload(world *ecs.World, weaponEntity ecs.EntityID) {
	weapon, ok := world.GetComponent(weaponEntity, components.CompWeapon)
	if !ok {
		return
	}

	w := weapon.(*components.WeaponComponent)

	if w.AmmoReserve <= 0 || w.AmmoInClip >= w.MaxClipSize {
		return
	}

	w.IsReloading = true
	w.ReloadProgress = 0

	// Reload time would come from the weapon definition
}

func (s *WeaponSystem) getReloadTime(w *components.WeaponComponent) float32 {
	// Would come from weapon definition
	if w.AmmoInClip > 0 {
		return 2.0 // Tactical reload
	}
	return 3.0 // Empty reload
}

func (s *WeaponSystem) calculateSpread(w *components.WeaponComponent, def *WeaponDefinition) float32 {
	baseSpread := def.AccuracyHip
	if w.Owner != 0 {
		// Check if aiming
		// Would check player component for aim state
	}

	// Apply attachments
	if w.Attachments != nil {
		// Grip reduces spread
		// Laser reduces hip spread
	}

	// Apply condition
	if w.Condition != nil {
		baseSpread *= (1.0 + w.Condition.Dirt*0.5)
		baseSpread *= (1.0 + w.Condition.Carbon*0.3)
	}

	return baseSpread
}

func (s *WeaponSystem) applySpread(baseDir components.Vector3, spread float32, def *WeaponDefinition) components.Vector3 {
	if spread <= 0 {
		return baseDir
	}

	// Generate random point in cone
	angle := spread * s.rng.Float32() * 2 * math.Pi
	radius := spread * float32(math.Sqrt(s.rng.Float64()))

	// Create orthogonal basis
	up := components.Vector3{X: 0, Y: 1, Z: 0}
	if math.Abs(float64(baseDir.Dot(up))) > 0.99 {
		up = components.Vector3{X: 1, Y: 0, Z: 0}
	}
	right := baseDir.Cross(up).Normalized()
	up = right.Cross(baseDir).Normalized()

	offset := right.Mul(float32(radius) * float32(math.Cos(float64(angle)))).Add(
		up.Mul(float32(radius) * float32(math.Sin(float64(angle)))))

	return baseDir.Add(offset).Normalized()
}

func (s *WeaponSystem) raycast(world *ecs.World, origin, direction components.Vector3, maxDist float32, ignoreEntity ecs.EntityID) HitResult {
	// Simplified raycast - in real implementation would use physics engine
	// For now, check entity bounding boxes

	query := world.Query(components.CompTransform, components.CompHealth, components.CompPhysics)
	var closestHit HitResult
	closestDist := maxDist

	query.Iterate(func(entity ecs.EntityID) {
		if entity == ignoreEntity {
			return
		}

		transform, _ := world.GetComponent(entity, components.CompTransform)
		physics, _ := world.GetComponent(entity, components.CompPhysics)

		t := transform.(*components.TransformComponent)
		p := physics.(*components.PhysicsComponent)

		// Simple sphere intersection
		for _, col := range p.Colliders {
			if col.Type == components.ColliderSphere || col.Type == components.ColliderCapsule {
				center := t.Position.Add(t.Rotation.RotateVector(col.Center))
				radius := col.Radius

				// Ray-sphere intersection
				oc := origin.Sub(center)
				a := direction.Dot(direction)
				b := 2.0 * oc.Dot(direction)
				c := oc.Dot(oc) - radius*radius
				disc := b*b - 4*a*c

				if disc >= 0 {
					t1 := (-b - float32(math.Sqrt(float64(disc)))) / (2 * a)
					if t1 > 0.01 && t1 < closestDist {
						closestDist = t1
						hitPoint := origin.Add(direction.Mul(t1))
						normal := hitPoint.Sub(center).Normalized()
						closestHit = HitResult{
							Entity:   entity,
							Point:    hitPoint,
							Normal:   normal,
							Distance: t1,
						}
					}
				}
			}
		}
	})

	return closestHit
}

func (s *WeaponSystem) applyDamage(world *ecs.World, targetEntity ecs.EntityID, damage float32, hitPoint, hitNormal components.Vector3, weaponEntity ecs.EntityID) {
	health, ok := world.GetComponent(targetEntity, components.CompHealth)
	if !ok {
		return
	}

	h := health.(*components.HealthComponent)
	if h.IsDead || h.Invulnerable {
		return
	}

	// Apply armor first
	actualDamage := damage
	if h.Armor > 0 {
		armorDamage := damage * 0.5 // Armor absorbs 50%
		if armorDamage > h.Armor {
			armorDamage = h.Armor
			actualDamage = damage - armorDamage*2
		} else {
			actualDamage = damage * 0.5
		}
		h.Armor -= armorDamage
		if h.Armor < 0 {
			h.Armor = 0
		}
	}

	h.CurrentHealth -= actualDamage
	h.LastDamageTime = 0 // Would set to current time

	if h.CurrentHealth <= 0 {
		h.CurrentHealth = 0
		h.IsDead = true
		// Trigger death event
		s.onDeath(world, targetEntity, weaponEntity)
	}
}

func (s *WeaponSystem) onDeath(world *ecs.World, victim, killer ecs.EntityID) {
	// Update stats
	if killer != 0 {
		if killerStats, ok := world.GetComponent(killer, components.CompStats); ok {
			stats := killerStats.(*components.StatsComponent)
			stats.Kills++
			// Check for headshot
			// stats.Headshots++
		}
	}

	if victimStats, ok := world.GetComponent(victim, components.CompStats); ok {
		stats := victimStats.(*components.StatsComponent)
		stats.Deaths++
	}

	// Drop weapon, spawn pickup, etc.
}

type WeaponDefinition struct {
	ModelID            string
	DisplayName        string
	WeaponClass        string
	Damage             float32
	Pellets            int
	Range              float32
	AccuracyHip        float32
	AccuracyADS        float32
	FireRateRPM        float32
	HeatPerShot        float32
	MaxClipSize        int32
	MaxReserve         int32
	ReloadTimeTactical float32
	ReloadTimeEmpty    float32
	RecoilVertical     float32
	RecoilHorizontal   float32
	RecoilRecovery     float32
	MuzzleVelocity     float32
}

func (s *WeaponSystem) getWeaponDefinition(modelID string) *WeaponDefinition {
	// In real implementation, load from database
	// Return defaults based on weapon class
	defs := map[string]*WeaponDefinition{
		"weapon_pistol_glock17": {
			ModelID:            "weapon_pistol_glock17",
			DisplayName:        "Glock 17",
			WeaponClass:        "pistol",
			Damage:             26,
			Pellets:            1,
			Range:              50,
			AccuracyHip:        0.03,
			AccuracyADS:        0.01,
			FireRateRPM:        450,
			HeatPerShot:        0.02,
			MaxClipSize:        17,
			MaxReserve:         170,
			ReloadTimeTactical: 1.8,
			ReloadTimeEmpty:    2.5,
			RecoilVertical:     0.8,
			RecoilHorizontal:   0.3,
			RecoilRecovery:     8.0,
			MuzzleVelocity:     375,
		},
		"weapon_rifle_ak47": {
			ModelID:            "weapon_rifle_ak47",
			DisplayName:        "AK-47",
			WeaponClass:        "rifle",
			Damage:             32,
			Pellets:            1,
			Range:              300,
			AccuracyHip:        0.08,
			AccuracyADS:        0.015,
			FireRateRPM:        600,
			HeatPerShot:        0.015,
			MaxClipSize:        30,
			MaxReserve:         300,
			ReloadTimeTactical: 2.2,
			ReloadTimeEmpty:    3.0,
			RecoilVertical:     1.2,
			RecoilHorizontal:   0.5,
			RecoilRecovery:     6.0,
			MuzzleVelocity:     715,
		},
		"weapon_shotgun_pump": {
			ModelID:            "weapon_shotgun_pump",
			DisplayName:        "Pump Shotgun",
			WeaponClass:        "shotgun",
			Damage:             18,
			Pellets:            8,
			Range:              40,
			AccuracyHip:        0.15,
			AccuracyADS:        0.08,
			FireRateRPM:        120,
			HeatPerShot:        0.05,
			MaxClipSize:        8,
			MaxReserve:         80,
			ReloadTimeTactical: 0.5, // Per shell
			ReloadTimeEmpty:    4.0,
			RecoilVertical:     2.5,
			RecoilHorizontal:   0.8,
			RecoilRecovery:     4.0,
			MuzzleVelocity:     400,
		},
		"weapon_sniper_heavy": {
			ModelID:            "weapon_sniper_heavy",
			DisplayName:        "Heavy Sniper",
			WeaponClass:        "sniper",
			Damage:             216,
			Pellets:            1,
			Range:              1500,
			AccuracyHip:        0.5,
			AccuracyADS:        0.001,
			FireRateRPM:        30,
			HeatPerShot:        0.1,
			MaxClipSize:        6,
			MaxReserve:         40,
			ReloadTimeTactical: 3.5,
			ReloadTimeEmpty:    4.2,
			RecoilVertical:     5.0,
			RecoilHorizontal:   1.0,
			RecoilRecovery:     2.0,
			MuzzleVelocity:     950,
		},
	}

	if def, ok := defs[modelID]; ok {
		return def
	}

	// Default pistol
	return defs["weapon_pistol_glock17"]
}

// WeaponSwitchSystem handles weapon switching
type WeaponSwitchSystem struct {
	Priority_ int
}

func NewWeaponSwitchSystem() *WeaponSwitchSystem {
	return &WeaponSwitchSystem{Priority_: 25}
}

func (s *WeaponSwitchSystem) Priority() int { return s.Priority_ }

func (s *WeaponSwitchSystem) Update(world *ecs.World, dt float32) {
	// Handle weapon switch requests (from input queue)
	_ = dt
}

func (s *WeaponSwitchSystem) SwitchWeapon(world *ecs.World, playerEntity ecs.EntityID, weaponEntity ecs.EntityID) bool {
	player, ok := world.GetComponent(playerEntity, components.CompPlayer)
	if !ok {
		return false
	}

	p := player.(*components.PlayerComponent)

	// Check if player owns the weapon (inventory lives on InventoryComponent)
	inventory, hasInventory := world.GetComponent(playerEntity, components.CompInventory)
	if !hasInventory {
		return false
	}

	hasWeapon := false
	for _, w := range inventory.(*components.InventoryComponent).Weapons {
		if w == weaponEntity {
			hasWeapon = true
			break
		}
	}

	if !hasWeapon {
		return false
	}

	// Unequip current
	if p.CurrentWeapon != 0 {
		if oldWeapon, ok := world.GetComponent(p.CurrentWeapon, components.CompWeapon); ok {
			w := oldWeapon.(*components.WeaponComponent)
			w.Owner = 0
		}
	}

	// Equip new
	if newWeapon, ok := world.GetComponent(weaponEntity, components.CompWeapon); ok {
		w := newWeapon.(*components.WeaponComponent)
		w.Owner = playerEntity
		p.CurrentWeapon = weaponEntity
		return true
	}

	return false
}
