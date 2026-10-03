package ecs

import "sort"

// ============================================================================
// SYSTEM SCHEDULING
//
// SystemManager (see ecs.go) is the simple priority-ordered runner.
// SystemScheduler is the variant used by the optimized network server; it runs
// the same ordered set but keeps the grouping metadata available for future
// parallel dispatch.
// ============================================================================

// SystemScheduler executes systems in priority order.
type SystemScheduler struct {
	systems []System
}

// NewSystemScheduler builds an empty scheduler.
func NewSystemScheduler() *SystemScheduler {
	return &SystemScheduler{systems: make([]System, 0)}
}

// Add appends a system, keeping the slice sorted by ascending priority.
func (s *SystemScheduler) Add(system System) {
	s.systems = append(s.systems, system)
	sort.SliceStable(s.systems, func(i, j int) bool {
		return s.systems[i].Priority() < s.systems[j].Priority()
	})
}

// Systems returns the scheduled systems in execution order.
func (s *SystemScheduler) Systems() []System {
	return s.systems
}

// Update runs every system once, in priority order.
func (s *SystemScheduler) Update(world *World, dt float32) {
	for _, sys := range s.systems {
		sys.Update(world, dt)
	}
}