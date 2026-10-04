package ecs

import "testing"

// Component ids are supplied by the component itself, exactly as the real
// components do with their Comp* constants: World.AddComponent keys the pool
// by component.ComponentID(), and lookups use that same id.
const (
	testTransformID ComponentID = 1
	testHealthID    ComponentID = 2
)

type testTransform struct {
	X, Y, Z float32
}

func (testTransform) ComponentID() ComponentID { return testTransformID }

type testHealth struct {
	HP float32
}

func (testHealth) ComponentID() ComponentID { return testHealthID }

func TestRegistryAssignsStableIDs(t *testing.T) {
	reg := NewComponentRegistry()

	first := reg.Register("TestTransform", func() Component { return &testTransform{} })
	second := reg.Register("TestHealth", func() Component { return &testHealth{} })

	if first == second {
		t.Fatalf("distinct components got the same id: %d", first)
	}
	if first == 0 || second == 0 {
		t.Fatalf("registry must not hand out the zero id: %d, %d", first, second)
	}

	// Re-registering the same name must be idempotent.
	if again := reg.Register("TestTransform", func() Component { return &testTransform{} }); again != first {
		t.Errorf("re-registering TestTransform returned %d, want %d", again, first)
	}
}

func TestEntityAndComponentLifecycle(t *testing.T) {
	world := NewWorld()

	entity := world.CreateEntity()

	world.AddComponent(entity, &testHealth{HP: 100})
	if !world.HasComponent(entity, testHealthID) {
		t.Fatal("component was not added")
	}

	got, ok := world.GetComponent(entity, testHealthID)
	if !ok {
		t.Fatal("component was not retrievable")
	}
	if hp := got.(*testHealth).HP; hp != 100 {
		t.Errorf("HP = %v, want 100", hp)
	}

	if !world.RemoveComponent(entity, testHealthID) {
		t.Error("RemoveComponent reported failure for a present component")
	}
	if world.HasComponent(entity, testHealthID) {
		t.Error("component still present after removal")
	}
	if world.RemoveComponent(entity, testHealthID) {
		t.Error("RemoveComponent reported success for an absent component")
	}
}

func TestQuerySelectsByComponent(t *testing.T) {
	world := NewWorld()

	both := world.CreateEntity()
	onlyTransform := world.CreateEntity()
	world.AddComponent(both, &testTransform{})
	world.AddComponent(both, &testHealth{HP: 50})
	world.AddComponent(onlyTransform, &testTransform{})

	entities := world.Query(testHealthID).Entities()
	if len(entities) != 1 {
		t.Fatalf("query returned %d entities, want 1", len(entities))
	}
	if entities[0] != both {
		t.Errorf("query returned entity %d, want %d", entities[0], both)
	}

	// Excluding health must yield the other entity only.
	excluded := world.QueryExcluding(nil, testHealthID).Entities()
	if len(excluded) != 1 || excluded[0] != onlyTransform {
		t.Errorf("QueryExcluding returned %v, want [%d]", excluded, onlyTransform)
	}
}

func TestSystemManagerRunsInPriorityOrder(t *testing.T) {
	world := NewWorld()
	mgr := NewSystemManager()

	var order []int
	mgr.Add(&recorderSystem{priority: 30, name: &order, label: 3})
	mgr.Add(&recorderSystem{priority: 10, name: &order, label: 1})
	mgr.Add(&recorderSystem{priority: 20, name: &order, label: 2})

	mgr.Update(world, 0.016)

	if len(order) != 3 {
		t.Fatalf("ran %d systems, want 3", len(order))
	}
	for i, want := range []int{1, 2, 3} {
		if order[i] != want {
			t.Errorf("system %d ran in slot %d (order %v), want %d", want, i, order, want)
		}
	}
}

func TestSystemSchedulerMatchesSystemManagerOrder(t *testing.T) {
	world := NewWorld()
	mgr := NewSystemManager()
	sched := NewSystemScheduler()

	var viaManager, viaScheduler []int
	for _, label := range []int{3, 1, 2} {
		mgr.Add(&recorderSystem{priority: label * 10, name: &viaManager, label: label})
		sched.Add(&recorderSystem{priority: label * 10, name: &viaScheduler, label: label})
	}

	mgr.Update(world, 0.016)
	sched.Update(world, 0.016)

	if len(viaManager) != 3 || len(viaScheduler) != 3 {
		t.Fatalf("manager ran %d, scheduler ran %d; want 3 each", len(viaManager), len(viaScheduler))
	}
	for i := range viaManager {
		if viaManager[i] != viaScheduler[i] {
			t.Errorf("ordering diverged at %d: manager %v vs scheduler %v", i, viaManager, viaScheduler)
		}
	}
}

func TestDestroyEntityRemovesComponents(t *testing.T) {
	reg := NewComponentRegistry()
	world := NewWorld()
	healthID := reg.Register("TestHealth", func() Component { return &testHealth{} })

	entity := world.CreateEntity()
	world.AddComponent(entity, &testHealth{HP: 10})
	world.DestroyEntity(entity)

	if world.HasComponent(entity, healthID) {
		t.Error("entity still reports the component after being destroyed")
	}
}

// recorderSystem appends its label to the shared slice when run.
type recorderSystem struct {
	priority int
	label    int
	name     *[]int
}

func (s *recorderSystem) Update(_ *World, _ float32) { *s.name = append(*s.name, s.label) }
func (s *recorderSystem) Priority() int              { return s.priority }
