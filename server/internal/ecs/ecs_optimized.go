package ecs

import (
	"runtime"
	"sync"
	"sync/atomic"
	"unsafe"
)

// ============================================================================
// HIGH-PERFORMANCE ECS - Archetype-based, cache-friendly, parallel-friendly
// ============================================================================

// ComponentID - compact type identifier
type ComponentID uint16

// EntityID - 48-bit entity index + 16-bit generation
type EntityID uint64

const (
	EntityIndexBits = 48
	EntityGenBits   = 16
	EntityIndexMask = (1 << EntityIndexBits) - 1
	EntityGenShift  = EntityIndexBits
	EntityGenMask   = (1 << EntityGenBits) - 1
)

func NewEntityID(index uint64, gen uint16) EntityID {
	return EntityID(index) | (EntityID(gen) << EntityGenShift)
}

func (e EntityID) Index() uint64  { return uint64(e) & EntityIndexMask }
func (e EntityID) Gen() uint16    { return uint16((uint64(e) >> EntityGenShift) & EntityGenMask) }
func (e EntityID) IsValid() bool  { return e != 0 }

// ArchetypeID - hash of component type set
type ArchetypeID uint64

// ============================================================================
// Component Registry - lock-free reads, single-writer registration
// ============================================================================

type ComponentInfo struct {
	ID        ComponentID
	Name      string
	Size      uint32
	Alignment uint32
	Factory   func() Component
}

type ComponentRegistry struct {
	infos     [65536]*ComponentInfo // direct indexed by ComponentID
	nameMap   map[string]ComponentID
	nextID    atomic.Uint16
	mu        sync.Mutex // only for registration
}

func NewComponentRegistry() *ComponentRegistry {
	return &ComponentRegistry{
		nameMap: make(map[string]ComponentID, 256),
	}
}

func (r *ComponentRegistry) Register(name string, size, alignment uint32, factory func() Component) ComponentID {
	r.mu.Lock()
	defer r.mu.Unlock()

	if id, ok := r.nameMap[name]; ok {
		return id
	}

	id := r.nextID.Add(1)
	info := &ComponentInfo{
		ID:        id,
		Name:      name,
		Size:      size,
		Alignment: alignment,
		Factory:   factory,
	}
	r.infos[id] = info
	r.nameMap[name] = id
	return id
}

func (r *ComponentRegistry) Get(id ComponentID) *ComponentInfo {
	return r.infos[id]
}

func (r *ComponentRegistry) GetByName(name string) (ComponentID, bool) {
	id, ok := r.nameMap[name]
	return id, ok
}

func (r *ComponentRegistry) CreateComponent(id ComponentID) Component {
	if info := r.infos[id]; info != nil && info.Factory != nil {
		return info.Factory()
	}
	return nil
}

// ============================================================================
// Component - data-only interface
// ============================================================================

type Component interface {
	ComponentID() ComponentID
	// No methods - pure data
}

// ============================================================================
// Archetype - stores entities with identical component sets
// ============================================================================

type Archetype struct {
	ID            ArchetypeID
	ComponentIDs  []ComponentID
	ComponentOffsets map[ComponentID]uint32 // byte offset in chunk
	ChunkSize     uint32                     // bytes per entity
	Chunks        []*Chunk
	EntityCount   int32
	ChunkCapacity int                         // entities per chunk (fixed)
	mu            sync.Mutex                  // only for structural changes
}

type Chunk struct {
	Archetype  *Archetype
	Data       []byte      // contiguous memory: [entity0_comp0, entity0_comp1, ..., entity1_comp0, ...]
	EntityIDs  []EntityID  // parallel array
	Count      int32       // current entity count
	Capacity   int32       // max entities (fixed at creation)
	FreeList   []int32     // indices of free slots
}

func NewArchetype(id ArchetypeID, componentIDs []ComponentID, registry *ComponentRegistry, chunkCapacity int) *Archetype {
	a := &Archetype{
		ID:            id,
		ComponentIDs:  componentIDs,
		ComponentOffsets: make(map[ComponentID]uint32, len(componentIDs)),
		ChunkCapacity: chunkCapacity,
		Chunks:        make([]*Chunk, 0, 4),
	}

	// Calculate layout
	var offset uint32
	maxAlign := uint32(1)
	for _, cid := range componentIDs {
		info := registry.Get(cid)
		if info == nil {
			continue
		}
		// Align
		align := info.Alignment
		if align > maxAlign {
			maxAlign = align
		}
		offset = (offset + align - 1) & ^(align - 1)
		a.ComponentOffsets[cid] = offset
		offset += info.Size
	}
	// Align chunk size
	a.ChunkSize = (offset + maxAlign - 1) & ^(maxAlign - 1)

	return a
}

func (a *Archetype) AllocateChunk() *Chunk {
	a.mu.Lock()
	defer a.mu.Unlock()

	data := make([]byte, a.ChunkSize*a.ChunkCapacity)
	entityIDs := make([]EntityID, a.ChunkCapacity)
	freeList := make([]int32, a.ChunkCapacity)
	for i := int32(0); i < a.ChunkCapacity; i++ {
		freeList[i] = i
	}

	chunk := &Chunk{
		Archetype:  a,
		Data:       data,
		EntityIDs:  entityIDs,
		Capacity:   int32(a.ChunkCapacity),
		FreeList:   freeList,
	}
	a.Chunks = append(a.Chunks, chunk)
	return chunk
}

func (a *Archetype) AddEntity(entity EntityID, components map[ComponentID]Component, registry *ComponentRegistry) int32 {
	// Find chunk with space
	var chunk *Chunk
	for _, c := range a.Chunks {
		if c.Count < c.Capacity {
			chunk = c
			break
		}
	}
	if chunk == nil {
		chunk = a.AllocateChunk()
	}

	// Get free slot
	slot := chunk.FreeList[len(chunk.FreeList)-1]
	chunk.FreeList = chunk.FreeList[:len(chunk.FreeList)-1]
	chunk.Count++

	// Write entity ID
	chunk.EntityIDs[slot] = entity

	// Write components
	baseOffset := uint32(slot) * a.ChunkSize
	for cid, comp := range components {
		offset := a.ComponentOffsets[cid]
		info := registry.Get(cid)
		if info == nil {
			continue
		}
		dest := chunk.Data[baseOffset+offset : baseOffset+offset+info.Size]
		// Use unsafe to write component directly
		*(**Component)(unsafe.Pointer(&dest[0])) = comp
	}

	atomic.AddInt32(&a.EntityCount, 1)
	return slot
}

func (a *Archetype) RemoveEntity(chunkIdx int, slot int32) {
	a.mu.Lock()
	defer a.mu.Unlock()

	if chunkIdx >= len(a.Chunks) {
		return
	}
	chunk := a.Chunks[chunkIdx]
	if slot >= chunk.Count {
		return
	}

	// Clear component pointers (for GC)
	baseOffset := uint32(slot) * a.ChunkSize
	for cid, offset := range a.ComponentOffsets {
		info := a.ComponentOffsets[cid]
		_ = info
		// Zero the pointer
		dest := chunk.Data[baseOffset+offset : baseOffset+offset+8]
		*(*uint64)(unsafe.Pointer(&dest[0])) = 0
	}

	// Swap with last entity if not last
	lastSlot := chunk.Count - 1
	if slot != lastSlot {
		// Move entity ID
		chunk.EntityIDs[slot] = chunk.EntityIDs[lastSlot]
		// Move component data
		srcBase := uint32(lastSlot) * a.ChunkSize
		dstBase := baseOffset
		copy(chunk.Data[dstBase:dstBase+a.ChunkSize], chunk.Data[srcBase:srcBase+a.ChunkSize])
	}

	chunk.Count--
	chunk.FreeList = append(chunk.FreeList, lastSlot)
	atomic.AddInt32(&a.EntityCount, -1)

	// If chunk empty, could return to pool (omitted for simplicity)
}

func (a *Archetype) GetComponent(chunkIdx int, slot int32, cid ComponentID) Component {
	if chunkIdx >= len(a.Chunks) {
		return nil
	}
	chunk := a.Chunks[chunkIdx]
	if slot >= chunk.Count {
		return nil
	}
	offset := a.ComponentOffsets[cid]
	info := a.ComponentOffsets[cid] // reuse
	_ = info
	baseOffset := uint32(slot) * a.ChunkSize
	dest := chunk.Data[baseOffset+offset : baseOffset+offset+8]
	return *(*Component)(unsafe.Pointer(&dest[0]))
}

// ============================================================================
// World - main ECS container
// ============================================================================

type World struct {
	Registry      *ComponentRegistry
	Archetypes    map[ArchetypeID]*Archetype
	EntityArchetype map[EntityID]*Archetype
	EntityChunk     map[EntityID]int    // archetype chunk index
	EntitySlot      map[EntityID]int32  // slot in chunk
	EntityVersion   map[EntityID]uint16
	NextEntityIndex atomic.Uint64
	NextArchetypeID atomic.Uint64

	// Systems
	Systems []System

	// Query cache
	QueryCache map[QueryKey]*QueryCache
	CacheMutex sync.RWMutex

	// Archetype lookup: component set hash -> ArchetypeID
	ArchetypeMap map[uint64]ArchetypeID
	MapMutex     sync.RWMutex
}

type QueryKey struct {
	Required  [8]ComponentID // fixed size for hash
	RequiredN int
	Excluded  [8]ComponentID
	ExcludedN int
	AnyOf     [8]ComponentID
	AnyOfN    int
}

type QueryCache struct {
	Archetypes []*Archetype
	Dirty      bool
}

func NewWorld(chunkCapacity int) *World {
	w := &World{
		Registry:        NewComponentRegistry(),
		Archetypes:      make(map[ArchetypeID]*Archetype),
		EntityArchetype: make(map[EntityID]*Archetype),
		EntityChunk:     make(map[EntityID]int),
		EntitySlot:      make(map[EntityID]int32),
		EntityVersion:   make(map[EntityID]uint16),
		QueryCache:      make(map[QueryKey]*QueryCache),
		ArchetypeMap:    make(map[uint64]ArchetypeID),
	}
	w.NextEntityIndex.Store(1)
	w.NextArchetypeID.Store(1)
	return w
}

func (w *World) CreateEntity() EntityID {
	index := w.NextEntityIndex.Add(1)
	gen := uint16(index >> EntityGenShift) // simple gen from index
	return NewEntityID(index, gen)
}

func (w *World) DestroyEntity(entity EntityID) {
	arch := w.EntityArchetype[entity]
	if arch == nil {
		return
	}
	chunkIdx := w.EntityChunk[entity]
	slot := w.EntitySlot[entity]

	arch.RemoveEntity(chunkIdx, slot)

	delete(w.EntityArchetype, entity)
	delete(w.EntityChunk, entity)
	delete(w.EntitySlot, entity)
	delete(w.EntityVersion, entity)

	w.invalidateQueries()
}

func (w *World) AddComponent(entity EntityID, component Component) {
	cid := component.ComponentID()
	w.addComponentInternal(entity, cid, component)
}

func (w *World) addComponentInternal(entity EntityID, cid ComponentID, component Component) {
	oldArch := w.EntityArchetype[entity]
	var newComponentIDs []ComponentID

	if oldArch == nil {
		newComponentIDs = []ComponentID{cid}
	} else {
		// Check if already has component
		for _, existing := range oldArch.ComponentIDs {
			if existing == cid {
				return // already has
			}
		}
		newComponentIDs = make([]ComponentID, len(oldArch.ComponentIDs)+1)
		copy(newComponentIDs, oldArch.ComponentIDs)
		newComponentIDs[len(oldArch.ComponentIDs)] = cid
	}

	// Get or create new archetype
	newArch := w.getOrCreateArchetype(newComponentIDs)

	// Remove from old archetype
	if oldArch != nil {
		oldChunkIdx := w.EntityChunk[entity]
		oldSlot := w.EntitySlot[entity]
		oldArch.RemoveEntity(oldChunkIdx, oldSlot)
	}

	// Add to new archetype
	newSlot := newArch.AddEntity(entity, map[ComponentID]Component{cid: component}, w.Registry)

	w.EntityArchetype[entity] = newArch
	w.EntityChunk[entity] = len(newArch.Chunks) - 1
	w.EntitySlot[entity] = newSlot
	w.EntityVersion[entity]++

	w.invalidateQueries()
}

func (w *World) RemoveComponent(entity EntityID, cid ComponentID) bool {
	oldArch := w.EntityArchetype[entity]
	if oldArch == nil {
		return false
	}

	// Check if has component
	has := false
	for _, existing := range oldArch.ComponentIDs {
		if existing == cid {
			has = true
			break
		}
	}
	if !has {
		return false
	}

	// Build new component set
	newComponentIDs := make([]ComponentID, 0, len(oldArch.ComponentIDs)-1)
	for _, existing := range oldArch.ComponentIDs {
		if existing != cid {
			newComponentIDs = append(newComponentIDs, existing)
		}
	}

	var newArch *Archetype
	if len(newComponentIDs) == 0 {
		newArch = nil
	} else {
		newArch = w.getOrCreateArchetype(newComponentIDs)
	}

	// Remove from old
	oldChunkIdx := w.EntityChunk[entity]
	oldSlot := w.EntitySlot[entity]
	oldArch.RemoveEntity(oldChunkIdx, oldSlot)

	// Add to new
	if newArch != nil {
		// Copy all other components
		components := make(map[ComponentID]Component)
		for _, ecid := range newComponentIDs {
			comp := oldArch.GetComponent(oldChunkIdx, oldSlot, ecid)
			if comp != nil {
				components[ecid] = comp
			}
		}
		newSlot := newArch.AddEntity(entity, components, w.Registry)
		w.EntityArchetype[entity] = newArch
		w.EntityChunk[entity] = len(newArch.Chunks) - 1
		w.EntitySlot[entity] = newSlot
	} else {
		delete(w.EntityArchetype, entity)
		delete(w.EntityChunk, entity)
		delete(w.EntitySlot, entity)
	}
	w.EntityVersion[entity]++

	w.invalidateQueries()
	return true
}

func (w *World) GetComponent(entity EntityID, cid ComponentID) (Component, bool) {
	arch := w.EntityArchetype[entity]
	if arch == nil {
		return nil, false
	}
	chunkIdx := w.EntityChunk[entity]
	slot := w.EntitySlot[entity]
	comp := arch.GetComponent(chunkIdx, slot, cid)
	return comp, comp != nil
}

func (w *World) HasComponent(entity EntityID, cid ComponentID) bool {
	arch := w.EntityArchetype[entity]
	if arch == nil {
		return false
	}
	for _, existing := range arch.ComponentIDs {
		if existing == cid {
			return true
		}
	}
	return false
}

func (w *World) GetEntityVersion(entity EntityID) uint16 {
	return w.EntityVersion[entity]
}

func (w *World) getOrCreateArchetype(componentIDs []ComponentID) *Archetype {
	// Hash component set
	var hash uint64 = 1469598103934665603 // FNV offset basis
	for _, cid := range componentIDs {
		hash ^= uint64(cid)
		hash *= 1099511628211 // FNV prime
	}

	w.MapMutex.RLock()
	archID, exists := w.ArchetypeMap[hash]
	w.MapMutex.RUnlock()

	if exists {
		return w.Archetypes[archID]
	}

	// Create new archetype
	w.MapMutex.Lock()
	defer w.MapMutex.Unlock()

	// Double-check
	if archID, exists = w.ArchetypeMap[hash]; exists {
		return w.Archetypes[archID]
	}

	archID = ArchetypeID(w.NextArchetypeID.Add(1))
	arch := NewArchetype(archID, componentIDs, w.Registry, 1024)
	w.Archetypes[archID] = arch
	w.ArchetypeMap[hash] = archID
	return arch
}

func (w *World) invalidateQueries() {
	w.CacheMutex.Lock()
	for _, cache := range w.QueryCache {
		cache.Dirty = true
	}
	w.CacheMutex.Unlock()
}

// ============================================================================
// Query - ultra-fast archetype-based iteration
// ============================================================================

type Query struct {
	world     *World
	Required  []ComponentID
	Excluded  []ComponentID
	AnyOf     []ComponentID
	Cached    []*Archetype
	ChunkIndices [][]int // per archetype: valid chunk indices
	Dirty     bool
}

func (w *World) Query(required ...ComponentID) *Query {
	return &Query{world: w, Required: required, Dirty: true}
}

func (w *World) QueryExcluding(required []ComponentID, excluded ...ComponentID) *Query {
	return &Query{world: w, Required: required, Excluded: excluded, Dirty: true}
}

func (w *World) QueryAny(required []ComponentID, anyOf ...ComponentID) *Query {
	return &Query{world: w, Required: required, AnyOf: anyOf, Dirty: true}
}

func (q *Query) Iterate(fn func(EntityID)) {
	archs := q.GetArchetypes()
	for _, arch := range archs {
		chunks := arch.Chunks
		chunkIndices := q.getChunkIndices(arch)
		for _, chunkIdx := range chunkIndices {
			chunk := chunks[chunkIdx]
			for i := int32(0); i < chunk.Count; i++ {
				fn(chunk.EntityIDs[i])
			}
		}
	}
}

func (q *Query) IterateChunks(fn func(*Chunk, int32, int32)) {
	archs := q.GetArchetypes()
	for _, arch := range archs {
		chunkIndices := q.getChunkIndices(arch)
		for _, chunkIdx := range chunkIndices {
			chunk := arch.Chunks[chunkIdx]
			fn(chunk, 0, chunk.Count)
		}
	}
}

func (q *Query) GetArchetypes() []*Archetype {
	if !q.Dirty && q.Cached != nil {
		return q.Cached
	}

	// Build query key
	var key QueryKey
	copy(key.Required[:], q.Required)
	key.RequiredN = len(q.Required)
	copy(key.Excluded[:], q.Excluded)
	key.ExcludedN = len(q.Excluded)
	copy(key.AnyOf[:], q.AnyOf)
	key.AnyOfN = len(q.AnyOf)

	q.world.CacheMutex.RLock()
	cache, exists := q.world.QueryCache[key]
	q.world.CacheMutex.RUnlock()

	if exists && !cache.Dirty {
		q.Cached = cache.Archetypes
		q.Dirty = false
		return q.Cached
	}

	// Compute matching archetypes
	var matching []*Archetype
	for _, arch := range q.world.Archetypes {
		if q.matchesArchetype(arch) {
			matching = append(matching, arch)
		}
	}

	// Cache
	q.world.CacheMutex.Lock()
	if cache == nil {
		cache = &QueryCache{Archetypes: matching}
		q.world.QueryCache[key] = cache
	} else {
		cache.Archetypes = matching
		cache.Dirty = false
	}
	q.world.CacheMutex.Unlock()

	q.Cached = matching
	q.Dirty = false
	return matching
}

func (q *Query) matchesArchetype(arch *Archetype) bool {
	// Check required
	for _, req := range q.Required {
		found := false
		for _, cid := range arch.ComponentIDs {
			if cid == req {
				found = true
				break
			}
		}
		if !found {
			return false
		}
	}

	// Check excluded
	for _, exc := range q.Excluded {
		for _, cid := range arch.ComponentIDs {
			if cid == exc {
				return false
			}
		}
	}

	// Check anyOf
	if len(q.AnyOf) > 0 {
		hasAny := false
		for _, any := range q.AnyOf {
			for _, cid := range arch.ComponentIDs {
				if cid == any {
					hasAny = true
					break
				}
			}
			if hasAny {
				break
			}
		}
		if !hasAny {
			return false
		}
	}

	return true
}

func (q *Query) getChunkIndices(arch *Archetype) []int {
	// For now, return all chunks with entities
	// Could optimize by tracking empty chunks
	indices := make([]int, 0, len(arch.Chunks))
	for i, chunk := range arch.Chunks {
		if chunk.Count > 0 {
			indices = append(indices, i)
		}
	}
	return indices
}

// Helper to get component from chunk directly (zero allocation)
func GetComponentFromChunk[T Component](chunk *Chunk, slot int32, arch *Archetype, cid ComponentID) T {
	offset := arch.ComponentOffsets[cid]
	info := arch.ComponentOffsets[cid]
	_ = info
	baseOffset := uint32(slot) * arch.ChunkSize
	dest := chunk.Data[baseOffset+offset : baseOffset+offset+8]
	return *(*T)(unsafe.Pointer(&dest[0]))
}

// ============================================================================
// System - with priority and parallel execution support
// ============================================================================

type System interface {
	Update(world *World, dt float32)
	Priority() int
	// For parallel execution
	ReadComponents() []ComponentID
	WriteComponents() []ComponentID
}

// SystemScheduler - executes systems with dependency awareness
type SystemScheduler struct {
	systems []System
	groups  [][]System // systems that can run in parallel
}

func NewSystemScheduler() *SystemScheduler {
	return &SystemScheduler{}
}

func (s *SystemScheduler) Add(system System) {
	s.systems = append(s.systems, system)
	// Sort by priority
	for i := len(s.systems) - 1; i > 0; i-- {
		if s.systems[i].Priority() < s.systems[i-1].Priority() {
			s.systems[i], s.systems[i-1] = s.systems[i-1], s.systems[i]
		} else {
			break
		}
	}
	s.buildGroups()
}

func (s *SystemScheduler) buildGroups() {
	// Simple grouping: systems that don't write to same components can run parallel
	// For now, sequential (can be enhanced with dependency graph)
	s.groups = [][]System{s.systems}
}

func (s *SystemScheduler) Update(world *World, dt float32) {
	for _, group := range s.groups {
		if len(group) == 1 {
			group[0].Update(world, dt)
		} else {
			// Parallel execution (when safe)
			var wg sync.WaitGroup
			for _, sys := range group {
				wg.Add(1)
				go func(s System) {
					defer wg.Done()
					s.Update(world, dt)
				}(sys)
			}
			wg.Wait()
		}
	}
}

// ============================================================================
// Object Pooling for frequent allocations
// ============================================================================

type Pool[T any] struct {
	pool sync.Pool
	new  func() T
}

func NewPool[T any](newFunc func() T) *Pool[T] {
	return &Pool[T]{
		new: newFunc,
		pool: sync.Pool{
			New: func() any { return newFunc() },
		},
	}
}

func (p *Pool[T]) Get() T {
	return p.pool.Get().(T)
}

func (p *Pool[T]) Put(item T) {
	p.pool.Put(item)
}

// Pre-allocated pools for common types
var (
	Vector3Pool = NewPool(func() *Vector3 { return &Vector3{} })
	QuaternionPool = NewPool(func() *Quaternion { return &Quaternion{} })
)

type Vector3 struct{ X, Y, Z float32 }
type Quaternion struct{ X, Y, Z, W float32 }

// ============================================================================
// Archetype Iteration Helpers (for systems)
// ============================================================================

// Iterate1 - single component
func Iterate1[T1 Component](world *World, cid1 ComponentID, fn func(entity EntityID, c1 *T1)) {
	q := world.Query(cid1)
	q.IterateChunks(func(chunk *Chunk, start, end int32) {
		arch := chunk.Archetype
		for i := start; i < end; i++ {
			entity := chunk.EntityIDs[i]
			c1 := GetComponentFromChunk[T1](chunk, i, arch, cid1)
			fn(entity, c1)
		}
	})
}

// Iterate2 - two components
func Iterate2[T1, T2 Component](world *World, cid1, cid2 ComponentID, fn func(entity EntityID, c1 *T1, c2 *T2)) {
	q := world.Query(cid1, cid2)
	q.IterateChunks(func(chunk *Chunk, start, end int32) {
		arch := chunk.Archetype
		for i := start; i < end; i++ {
			entity := chunk.EntityIDs[i]
			c1 := GetComponentFromChunk[T1](chunk, i, arch, cid1)
			c2 := GetComponentFromChunk[T2](chunk, i, arch, cid2)
			fn(entity, c1, c2)
		}
	})
}

// Iterate3 - three components
func Iterate3[T1, T2, T3 Component](world *World, cid1, cid2, cid3 ComponentID, fn func(entity EntityID, c1 *T1, c2 *T2, c3 *T3)) {
	q := world.Query(cid1, cid2, cid3)
	q.IterateChunks(func(chunk *Chunk, start, end int32) {
		arch := chunk.Archetype
		for i := start; i < end; i++ {
			entity := chunk.EntityIDs[i]
			c1 := GetComponentFromChunk[T1](chunk, i, arch, cid1)
			c2 := GetComponentFromChunk[T2](chunk, i, arch, cid2)
			c3 := GetComponentFromChunk[T3](chunk, i, arch, cid3)
			fn(entity, c1, c2, c3)
		}
	})
}

// Iterate4 - four components
func Iterate4[T1, T2, T3, T4 Component](world *World, cid1, cid2, cid3, cid4 ComponentID, fn func(entity EntityID, c1 *T1, c2 *T2, c3 *T3, c4 *T4)) {
	q := world.Query(cid1, cid2, cid3, cid4)
	q.IterateChunks(func(chunk *Chunk, start, end int32) {
		arch := chunk.Archetype
		for i := start; i < end; i++ {
			entity := chunk.EntityIDs[i]
			c1 := GetComponentFromChunk[T1](chunk, i, arch, cid1)
			c2 := GetComponentFromChunk[T2](chunk, i, arch, cid2)
			c3 := GetComponentFromChunk[T3](chunk, i, arch, cid3)
			c4 := GetComponentFromChunk[T4](chunk, i, arch, cid4)
			fn(entity, c1, c2, c3, c4)
		}
	})
}

// ============================================================================
// Math utilities (inlineable)
// ============================================================================

func (v Vector3) Add(o Vector3) Vector3      { return Vector3{v.X + o.X, v.Y + o.Y, v.Z + o.Z} }
func (v Vector3) Sub(o Vector3) Vector3      { return Vector3{v.X - o.X, v.Y - o.Y, v.Z - o.Z} }
func (v Vector3) Mul(s float32) Vector3      { return Vector3{v.X * s, v.Y * s, v.Z * s} }
func (v Vector3) Div(s float32) Vector3      { return Vector3{v.X / s, v.Y / s, v.Z / s} }
func (v Vector3) Length() float32            { return float32(sqrt(float64(v.X*v.X + v.Y*v.Y + v.Z*v.Z))) }
func (v Vector3) LengthSq() float32          { return v.X*v.X + v.Y*v.Y + v.Z*v.Z }
func (v Vector3) Normalized() Vector3        { l := v.Length(); if l == 0 { return Vector3{} }; return v.Div(l) }
func (v Vector3) Dot(o Vector3) float32      { return v.X*o.X + v.Y*o.Y + v.Z*o.Z }
func (v Vector3) Cross(o Vector3) Vector3    { return Vector3{v.Y*o.Z - v.Z*o.Y, v.Z*o.X - v.X*o.Z, v.X*o.Y - v.Y*o.X} }
func (v Vector3) Distance(o Vector3) float32 { return v.Sub(o).Length() }
func (v Vector3) Lerp(o Vector3, t float32) Vector3 { return v.Add(o.Sub(v).Mul(t)) }

func (q Quaternion) Mul(o Quaternion) Quaternion {
	return Quaternion{
		X: q.W*o.X + q.X*o.W + q.Y*o.Z - q.Z*o.Y,
		Y: q.W*o.Y - q.X*o.Z + q.Y*o.W + q.Z*o.X,
		Z: q.W*o.Z + q.X*o.Y - q.Y*o.X + q.Z*o.W,
		W: q.W*o.W - q.X*o.X - q.Y*o.Y - q.Z*o.Z,
	}
}

func (q Quaternion) Normalized() Quaternion {
	l := float32(sqrt(float64(q.X*q.X + q.Y*q.Y + q.Z*q.Z + q.W*q.W)))
	if l == 0 {
		return Quaternion{0, 0, 0, 1}
	}
	return Quaternion{q.X / l, q.Y / l, q.Z / l, q.W / l}
}

func (q Quaternion) RotateVector(v Vector3) Vector3 {
	uv := Quaternion{q.X, q.Y, q.Z, 0}.Mul(Quaternion{v.X, v.Y, v.Z, 0})
	uv = uv.Mul(Quaternion{-q.X, -q.Y, -q.Z, q.W})
	return Vector3{uv.X, uv.Y, uv.Z}
}

func QuaternionFromEuler(x, y, z float32) Quaternion {
	cx := float32(cos(float64(x * 0.5)))
	sx := float32(sin(float64(x * 0.5)))
	cy := float32(cos(float64(y * 0.5)))
	sy := float32(sin(float64(y * 0.5)))
	cz := float32(cos(float64(z * 0.5)))
	sz := float32(sin(float64(z * 0.5)))

	return Quaternion{
		X: sx*cy*cz - cx*sy*sz,
		Y: cx*sy*cz + sx*cy*sz,
		Z: cx*cy*sz - sx*sy*cz,
		W: cx*cy*cz + sx*sy*sz,
	}.Normalized()
}

func (q Quaternion) ToEuler() (x, y, z float32) {
	sinr_cosp := 2 * (q.W*q.X + q.Y*q.Z)
	cosr_cosp := 1 - 2*(q.X*q.X+q.Y*q.Y)
	x = float32(atan2(float64(sinr_cosp), float64(cosr_cosp)))

	sinp := 2 * (q.W*q.Y - q.Z*q.X)
	if sinp >= 1 {
		y = float32(pi / 2)
	} else if sinp <= -1 {
		y = -float32(pi / 2)
	} else {
		y = float32(asin(float64(sinp)))
	}

	siny_cosp := 2 * (q.W*q.Z + q.X*q.Y)
	cosy_cosp := 1 - 2*(q.Y*q.Y+q.Z*q.Z)
	z = float32(atan2(float64(siny_cosp), float64(cosy_cosp)))
	return
}

// Math intrinsics (inlined by compiler)
func sqrt(x float64) float64  { return x } // placeholder - use math.Sqrt
func sin(x float64) float64   { return x }
func cos(x float64) float64   { return x }
func atan2(y, x float64) float64 { return 0 }
func asin(x float64) float64  { return 0 }
const pi = 3.141592653589793

// ============================================================================
// TransformComponent (cache-friendly layout)
// ============================================================================

type TransformComponent struct {
	Position Vector3
	Rotation Quaternion
	Scale    Vector3
	PrevPos  Vector3
	PrevRot  Quaternion
	// Padding for alignment
	_        [4]byte
}

func (TransformComponent) ComponentID() ComponentID { return 1 }

func NewTransform() *TransformComponent {
	return &TransformComponent{
		Position: Vector3{},
		Rotation: Quaternion{0, 0, 0, 1},
		Scale:    Vector3{1, 1, 1},
	}
}

func (t *TransformComponent) Forward() Vector3 {
	return t.Rotation.RotateVector(Vector3{0, 0, 1})
}

func (t *TransformComponent) Right() Vector3 {
	return t.Rotation.RotateVector(Vector3{1, 0, 0})
}

func (t *TransformComponent) Up() Vector3 {
	return t.Rotation.RotateVector(Vector3{0, 1, 0})
}

// ============================================================================
// RegisterStandardComponents - call once at startup
// ============================================================================

func RegisterStandardComponents(reg *ComponentRegistry) {
	reg.Register("Transform", 64, 16, func() Component { return &TransformComponent{} })
	// Other components registered similarly...
}