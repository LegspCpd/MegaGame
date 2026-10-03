package ecs

// ============================================================================
// SYSTEM SCHEDULING
//
// The ECS core (World, ComponentRegistry, Component, EntityID, ...) lives in
// ecs_optimized.go. This file only holds the system-execution layer, which is
// shared by both the simple and the archetype backends.
// ============================================================================

// System is implemented by every game logic system.
type System interface {
	Update(world *World, dt float32)
	Priority() int
}

// SystemManager manages system execution order.
type SystemManager struct {
	systems []System
}

func NewSystemManager() *SystemManager {
	return &SystemManager{
		systems: make([]System, 0),
	}
}

func (sm *SystemManager) Add(system System) {
	sm.systems = append(sm.systems, system)
	// Sort by priority (lower = earlier)
	for i := len(sm.systems) - 1; i > 0; i-- {
		if sm.systems[i].Priority() < sm.systems[i-1].Priority() {
			sm.systems[i], sm.systems[i-1] = sm.systems[i-1], sm.systems[i]
		} else {
			break
		}
	}
}

func (sm *SystemManager) Update(world *World, dt float32) {
	for _, sys := range sm.systems {
		sys.Update(world, dt)
	}
}