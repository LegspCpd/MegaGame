package ecs

import (
	"strconv"
	"sync"
	"sync/atomic"
)

// EntityID is a unique identifier for an entity
type EntityID uint64

// ComponentID is a unique identifier for a component type
type ComponentID uint32

// String renders the numeric id. ComponentRegistry.GetName returns the
// registered name when one is available; this is the fallback form.
func (c ComponentID) String() string {
	return "ComponentID(" + strconv.FormatUint(uint64(c), 10) + ")"
}

// ArchetypeID identifies a unique combination of components
type ArchetypeID uint64

// Component interface for all components
type Component interface {
	ComponentID() ComponentID
}

// ComponentRegistry manages component type registration
type ComponentRegistry struct {
	mu          sync.RWMutex
	nameToID    map[string]ComponentID
	idToFactory map[ComponentID]func() Component
	nextID      ComponentID
}

func NewComponentRegistry() *ComponentRegistry {
	return &ComponentRegistry{
		nameToID:    make(map[string]ComponentID),
		idToFactory: make(map[ComponentID]func() Component),
		nextID:      1,
	}
}

func (r *ComponentRegistry) Register(name string, factory func() Component) ComponentID {
	r.mu.Lock()
	defer r.mu.Unlock()

	if id, ok := r.nameToID[name]; ok {
		return id
	}

	id := r.nextID
	r.nextID++
	r.nameToID[name] = id
	r.idToFactory[id] = factory
	return id
}

func (r *ComponentRegistry) GetID(name string) (ComponentID, bool) {
	r.mu.RLock()
	defer r.mu.RUnlock()
	id, ok := r.nameToID[name]
	return id, ok
}

func (r *ComponentRegistry) CreateComponent(id ComponentID) Component {
	r.mu.RLock()
	defer r.mu.RUnlock()
	if factory, ok := r.idToFactory[id]; ok {
		return factory()
	}
	return nil
}

// ComponentPool manages a pool of components of a specific type
type ComponentPool struct {
	componentID ComponentID
	data        []Component
	entityToIdx map[EntityID]int
	freeIndices []int
	mu          sync.RWMutex
}

func NewComponentPool(componentID ComponentID) *ComponentPool {
	return &ComponentPool{
		componentID: componentID,
		data:        make([]Component, 0, 1024),
		entityToIdx: make(map[EntityID]int),
		freeIndices: make([]int, 0, 64),
	}
}

func (p *ComponentPool) Add(entity EntityID, component Component) {
	p.mu.Lock()
	defer p.mu.Unlock()

	var idx int
	if len(p.freeIndices) > 0 {
		idx = p.freeIndices[len(p.freeIndices)-1]
		p.freeIndices = p.freeIndices[:len(p.freeIndices)-1]
		p.data[idx] = component
	} else {
		idx = len(p.data)
		p.data = append(p.data, component)
	}
	p.entityToIdx[entity] = idx
}

func (p *ComponentPool) Remove(entity EntityID) bool {
	p.mu.Lock()
	defer p.mu.Unlock()

	idx, ok := p.entityToIdx[entity]
	if !ok {
		return false
	}

	lastIdx := len(p.data) - 1
	if idx != lastIdx {
		// Find entity at lastIdx and update its mapping
		for e, i := range p.entityToIdx {
			if i == lastIdx {
				p.entityToIdx[e] = idx
				break
			}
		}
		p.data[idx] = p.data[lastIdx]
	}
	p.data = p.data[:lastIdx]
	p.freeIndices = append(p.freeIndices, idx)
	delete(p.entityToIdx, entity)
	return true
}

func (p *ComponentPool) Get(entity EntityID) (Component, bool) {
	p.mu.RLock()
	defer p.mu.RUnlock()

	idx, ok := p.entityToIdx[entity]
	if !ok {
		return nil, false
	}
	return p.data[idx], true
}

func (p *ComponentPool) Has(entity EntityID) bool {
	p.mu.RLock()
	defer p.mu.RUnlock()
	_, ok := p.entityToIdx[entity]
	return ok
}

func (p *ComponentPool) GetAll() []Component {
	p.mu.RLock()
	defer p.mu.RUnlock()
	result := make([]Component, len(p.data))
	copy(result, p.data)
	return result
}

func (p *ComponentPool) Entities() []EntityID {
	p.mu.RLock()
	defer p.mu.RUnlock()
	entities := make([]EntityID, 0, len(p.entityToIdx))
	for e := range p.entityToIdx {
		entities = append(entities, e)
	}
	return entities
}

// World is the main ECS world
type World struct {
	registry      *ComponentRegistry
	pools         map[ComponentID]*ComponentPool
	entityMasks   map[EntityID]map[ComponentID]bool
	entityVersion map[EntityID]uint32
	nextEntityID  atomic.Uint64
	mu            sync.RWMutex
	systems       []System
}

func NewWorld() *World {
	return &World{
		registry:      NewComponentRegistry(),
		pools:         make(map[ComponentID]*ComponentPool),
		entityMasks:   make(map[EntityID]map[ComponentID]bool),
		entityVersion: make(map[EntityID]uint32),
		systems:       make([]System, 0),
	}
}

func (w *World) Registry() *ComponentRegistry {
	return w.registry
}

func (w *World) CreateEntity() EntityID {
	id := EntityID(w.nextEntityID.Add(1))
	w.mu.Lock()
	w.entityMasks[id] = make(map[ComponentID]bool)
	w.entityVersion[id] = 1
	w.mu.Unlock()
	return id
}

func (w *World) DestroyEntity(entity EntityID) {
	w.mu.Lock()
	defer w.mu.Unlock()

	mask := w.entityMasks[entity]
	for compID := range mask {
		if pool, ok := w.pools[compID]; ok {
			pool.Remove(entity)
		}
	}
	delete(w.entityMasks, entity)
	delete(w.entityVersion, entity)
}

func (w *World) AddComponent(entity EntityID, component Component) {
	compID := component.ComponentID()

	w.mu.Lock()
	pool, ok := w.pools[compID]
	if !ok {
		pool = NewComponentPool(compID)
		w.pools[compID] = pool
	}
	w.mu.Unlock()

	pool.Add(entity, component)

	w.mu.Lock()
	w.entityMasks[entity][compID] = true
	w.entityVersion[entity]++
	w.mu.Unlock()
}

func (w *World) RemoveComponent(entity EntityID, compID ComponentID) bool {
	w.mu.Lock()
	pool, ok := w.pools[compID]
	w.mu.Unlock()

	if !ok {
		return false
	}

	removed := pool.Remove(entity)
	if removed {
		w.mu.Lock()
		delete(w.entityMasks[entity], compID)
		w.entityVersion[entity]++
		w.mu.Unlock()
	}
	return removed
}

func (w *World) GetComponent(entity EntityID, compID ComponentID) (Component, bool) {
	w.mu.RLock()
	pool, ok := w.pools[compID]
	w.mu.RUnlock()

	if !ok {
		return nil, false
	}
	return pool.Get(entity)
}

func (w *World) HasComponent(entity EntityID, compID ComponentID) bool {
	w.mu.RLock()
	mask, ok := w.entityMasks[entity]
	w.mu.RUnlock()

	if !ok {
		return false
	}
	_, has := mask[compID]
	return has
}

func (w *World) GetEntityVersion(entity EntityID) uint32 {
	w.mu.RLock()
	defer w.mu.RUnlock()
	return w.entityVersion[entity]
}

// Query builds a query for entities with specific components
func (w *World) Query(required ...ComponentID) *Query {
	return &Query{
		world:    w,
		required: required,
		excluded: nil,
		anyOf:    nil,
	}
}

func (w *World) QueryExcluding(required []ComponentID, excluded ...ComponentID) *Query {
	return &Query{
		world:    w,
		required: required,
		excluded: excluded,
		anyOf:    nil,
	}
}

func (w *World) QueryAny(required []ComponentID, anyOf ...ComponentID) *Query {
	return &Query{
		world:    w,
		required: required,
		excluded: nil,
		anyOf:    anyOf,
	}
}

// Query represents an entity query
type Query struct {
	world    *World
	required []ComponentID
	excluded []ComponentID
	anyOf    []ComponentID
	cached   []EntityID
	dirty    bool
}

func (q *Query) Iterate(fn func(EntityID)) {
	entities := q.Entities()
	for _, e := range entities {
		fn(e)
	}
}

func (q *Query) Entities() []EntityID {
	if !q.dirty && q.cached != nil {
		return q.cached
	}

	q.world.mu.RLock()
	defer q.world.mu.RUnlock()

	// Start with entities that have the first required component
	var candidates []EntityID
	if len(q.required) > 0 {
		if pool, ok := q.world.pools[q.required[0]]; ok {
			candidates = pool.Entities()
		}
	} else {
		// No required components - all entities
		for e := range q.world.entityMasks {
			candidates = append(candidates, e)
		}
	}

	result := make([]EntityID, 0, len(candidates))
	for _, e := range candidates {
		if q.matches(e) {
			result = append(result, e)
		}
	}

	q.cached = result
	q.dirty = false
	return result
}

func (q *Query) matches(entity EntityID) bool {
	mask := q.world.entityMasks[entity]

	// Check required
	for _, req := range q.required {
		if !mask[req] {
			return false
		}
	}

	// Check excluded
	for _, exc := range q.excluded {
		if mask[exc] {
			return false
		}
	}

	// Check anyOf
	if len(q.anyOf) > 0 {
		hasAny := false
		for _, any := range q.anyOf {
			if mask[any] {
				hasAny = true
				break
			}
		}
		if !hasAny {
			return false
		}
	}

	return true
}

func (q *Query) Invalidate() {
	q.dirty = true
}

// System interface
type System interface {
	Update(world *World, dt float32)
	Priority() int
}

// SystemManager manages system execution order
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
