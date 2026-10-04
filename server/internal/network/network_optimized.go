package network

import (
	"sync"
	"sync/atomic"
	"time"
	"unsafe"

	"github.com/megame/server/internal/components"
	"github.com/megame/server/internal/ecs"
	"github.com/megame/server/internal/proto/entity"
	"github.com/megame/server/internal/proto/network"
	"google.golang.org/protobuf/proto"
)

// ============================================================================
// HIGH-PERFORMANCE NETWORK LAYER
// Delta compression, interest management, object pooling, zero-copy
// ============================================================================

const (
	MaxSnapshotSize        = 1024 * 1024 // 1MB max snapshot
	SnapshotHistorySize    = 128         // keep last 128 snapshots for delta
	MaxEntitiesPerSnapshot = 8192
	InterestRadius         = 500.0 // meters
	MaxSnapshotFrequency   = 60    // Hz
	MinSnapshotFrequency   = 10    // Hz for far entities
)

type EntitySnapshot struct {
	EntityID   uint64
	Type       entity.EntityType
	Transform  *entity.Transform
	Velocity   *entity.Vector3
	Timestamp  uint64
	Components map[uint16][]byte // componentID -> serialized data
}

type Snapshot struct {
	Tick           uint64
	Timestamp      int64
	Entities       []EntitySnapshot
	Destroyed      []uint64
	PlayerUpdates  []*network.PlayerStateUpdate
	VehicleUpdates []*network.VehicleStateUpdate
	WeaponUpdates  []*network.WeaponStateUpdate
	NPCUpdates     []*network.NPCStateUpdate
	WorldState     *network.WorldState
}

// SnapshotPool for zero-allocation snapshots
type SnapshotPool struct {
	pool sync.Pool
}

func NewSnapshotPool() *SnapshotPool {
	return &SnapshotPool{
		pool: sync.Pool{
			New: func() any {
				return &Snapshot{
					Entities:       make([]EntitySnapshot, 0, 1024),
					Destroyed:      make([]uint64, 0, 64),
					PlayerUpdates:  make([]*network.PlayerStateUpdate, 0, 128),
					VehicleUpdates: make([]*network.VehicleStateUpdate, 0, 128),
					WeaponUpdates:  make([]*network.WeaponStateUpdate, 0, 128),
					NPCUpdates:     make([]*network.NPCStateUpdate, 0, 128),
				}
			},
		},
	}
}

func (p *SnapshotPool) Get() *Snapshot {
	return p.pool.Get().(*Snapshot)
}

func (p *SnapshotPool) Put(s *Snapshot) {
	s.Tick = 0
	s.Timestamp = 0
	s.Entities = s.Entities[:0]
	s.Destroyed = s.Destroyed[:0]
	s.PlayerUpdates = s.PlayerUpdates[:0]
	s.VehicleUpdates = s.VehicleUpdates[:0]
	s.WeaponUpdates = s.WeaponUpdates[:0]
	s.NPCUpdates = s.NPCUpdates[:0]
	s.WorldState = nil
	p.pool.Put(s)
}

// DeltaSnapshot stores differences from previous snapshot
type DeltaSnapshot struct {
	BaseTick        uint64
	Tick            uint64
	Timestamp       int64
	NewEntities     []EntitySnapshot
	UpdatedEntities []EntitySnapshot // only changed components
	Destroyed       []uint64
	PlayerUpdates   []*network.PlayerStateUpdate
	VehicleUpdates  []*network.VehicleStateUpdate
	WeaponUpdates   []*network.WeaponStateUpdate
	NPCUpdates      []*network.NPCStateUpdate
}

var snapshotPool = NewSnapshotPool()

// SnapshotHistory maintains recent snapshots for delta compression
type SnapshotHistory struct {
	snapshots [SnapshotHistorySize]*Snapshot
	head      uint64
	mu        sync.RWMutex
}

func NewSnapshotHistory() *SnapshotHistory {
	h := &SnapshotHistory{}
	for i := 0; i < SnapshotHistorySize; i++ {
		h.snapshots[i] = snapshotPool.Get()
	}
	return h
}

func (h *SnapshotHistory) Add(snap *Snapshot) {
	h.mu.Lock()
	defer h.mu.Unlock()
	idx := snap.Tick % SnapshotHistorySize
	snapshotPool.Put(h.snapshots[idx])
	h.snapshots[idx] = snap
	h.head = snap.Tick
}

func (h *SnapshotHistory) Get(tick uint64) *Snapshot {
	h.mu.RLock()
	defer h.mu.RUnlock()
	if h.head-tick >= SnapshotHistorySize {
		return nil // too old
	}
	return h.snapshots[tick%SnapshotHistorySize]
}

func (h *SnapshotHistory) GetLatest() *Snapshot {
	h.mu.RLock()
	defer h.mu.RUnlock()
	return h.snapshots[h.head%SnapshotHistorySize]
}

// ============================================================================
// Interest Management - spatial partitioning for bandwidth optimization
// ============================================================================

type InterestManager struct {
	grid        map[GridCoord]*GridCell
	cellSize    float32
	entityCells map[uint64]GridCoord
	playerViews map[uint64]*PlayerView
	mu          sync.RWMutex
}

type GridCoord struct {
	X, Y, Z int32
}

type GridCell struct {
	Entities map[uint64]struct{}
}

type PlayerView struct {
	PlayerID   uint64
	Position   ecs.Vector3
	Radius     float32
	Relevant   map[uint64]bool // entityID -> relevant
	LastUpdate uint64
}

func NewInterestManager(cellSize float32) *InterestManager {
	return &InterestManager{
		grid:        make(map[GridCoord]*GridCell),
		cellSize:    cellSize,
		entityCells: make(map[uint64]GridCoord),
		playerViews: make(map[uint64]*PlayerView),
	}
}

func (im *InterestManager) gridCoord(pos ecs.Vector3) GridCoord {
	inv := 1.0 / im.cellSize
	return GridCoord{
		X: int32(pos.X * inv),
		Y: int32(pos.Y * inv),
		Z: int32(pos.Z * inv),
	}
}

func (im *InterestManager) AddEntity(entityID uint64, pos ecs.Vector3) {
	im.mu.Lock()
	defer im.mu.Unlock()

	coord := im.gridCoord(pos)
	cell := im.grid[coord]
	if cell == nil {
		cell = &GridCell{Entities: make(map[uint64]struct{})}
		im.grid[coord] = cell
	}
	cell.Entities[entityID] = struct{}{}
	im.entityCells[entityID] = coord
}

func (im *InterestManager) RemoveEntity(entityID uint64) {
	im.mu.Lock()
	defer im.mu.Unlock()

	coord, ok := im.entityCells[entityID]
	if !ok {
		return
	}
	cell := im.grid[coord]
	if cell != nil {
		delete(cell.Entities, entityID)
		if len(cell.Entities) == 0 {
			delete(im.grid, coord)
		}
	}
	delete(im.entityCells, entityID)
}

func (im *InterestManager) UpdateEntity(entityID uint64, newPos ecs.Vector3) {
	im.mu.Lock()
	defer im.mu.Unlock()

	oldCoord, ok := im.entityCells[entityID]
	newCoord := im.gridCoord(newPos)

	if ok && oldCoord == newCoord {
		return // same cell
	}

	if ok {
		// Remove from old cell
		if cell := im.grid[oldCoord]; cell != nil {
			delete(cell.Entities, entityID)
			if len(cell.Entities) == 0 {
				delete(im.grid, oldCoord)
			}
		}
	}

	// Add to new cell
	cell := im.grid[newCoord]
	if cell == nil {
		cell = &GridCell{Entities: make(map[uint64]struct{})}
		im.grid[newCoord] = cell
	}
	cell.Entities[entityID] = struct{}{}
	im.entityCells[entityID] = newCoord
}

func (im *InterestManager) RegisterPlayerView(playerID uint64, pos ecs.Vector3, radius float32) {
	im.mu.Lock()
	defer im.mu.Unlock()
	im.playerViews[playerID] = &PlayerView{
		PlayerID: playerID,
		Position: pos,
		Radius:   radius,
		Relevant: make(map[uint64]bool),
	}
}

func (im *InterestManager) UnregisterPlayerView(playerID uint64) {
	im.mu.Lock()
	defer im.mu.Unlock()
	delete(im.playerViews, playerID)
}

func (im *InterestManager) UpdatePlayerView(playerID uint64, pos ecs.Vector3) {
	im.mu.Lock()
	defer im.mu.Unlock()
	if view, ok := im.playerViews[playerID]; ok {
		view.Position = pos
	}
}

func (im *InterestManager) GetRelevantEntities(playerID uint64) []uint64 {
	im.mu.RLock()
	defer im.mu.RUnlock()

	view, ok := im.playerViews[playerID]
	if !ok {
		return nil
	}

	relevant := make([]uint64, 0, 512)
	playerCell := im.gridCoord(view.Position)

	// Check surrounding cells
	for dx := int32(-1); dx <= 1; dx++ {
		for dy := int32(-1); dy <= 1; dy++ {
			for dz := int32(-1); dz <= 1; dz++ {
				coord := GridCoord{
					X: playerCell.X + dx,
					Y: playerCell.Y + dy,
					Z: playerCell.Z + dz,
				}
				cell := im.grid[coord]
				if cell == nil {
					continue
				}
				for entityID := range cell.Entities {
					// Could add distance check here if needed
					relevant = append(relevant, entityID)
				}
			}
		}
	}

	return relevant
}

// ============================================================================
// Delta Compression
// ============================================================================

type DeltaCompressor struct {
	lastSnapshots map[uint64]*Snapshot // per player
	mu            sync.RWMutex
}

func NewDeltaCompressor() *DeltaCompressor {
	return &DeltaCompressor{
		lastSnapshots: make(map[uint64]*Snapshot),
	}
}

func (dc *DeltaCompressor) Compress(current *Snapshot, playerID uint64) *DeltaSnapshot {
	dc.mu.Lock()
	defer dc.mu.Unlock()

	last := dc.lastSnapshots[playerID]
	dc.lastSnapshots[playerID] = current

	if last == nil {
		// First snapshot - send full
		return &DeltaSnapshot{
			BaseTick:       0,
			Tick:           current.Tick,
			Timestamp:      current.Timestamp,
			NewEntities:    current.Entities,
			Destroyed:      current.Destroyed,
			PlayerUpdates:  current.PlayerUpdates,
			VehicleUpdates: current.VehicleUpdates,
			WeaponUpdates:  current.WeaponUpdates,
			NPCUpdates:     current.NPCUpdates,
		}
	}

	// Build entity maps for diffing
	lastEntities := make(map[uint64]*EntitySnapshot, len(last.Entities))
	for i := range last.Entities {
		lastEntities[last.Entities[i].EntityID] = &last.Entities[i]
	}

	delta := &DeltaSnapshot{
		BaseTick:        last.Tick,
		Tick:            current.Tick,
		Timestamp:       current.Timestamp,
		NewEntities:     make([]EntitySnapshot, 0, 64),
		UpdatedEntities: make([]EntitySnapshot, 0, 256),
		Destroyed:       current.Destroyed,
		PlayerUpdates:   current.PlayerUpdates,
		VehicleUpdates:  current.VehicleUpdates,
		WeaponUpdates:   current.WeaponUpdates,
		NPCUpdates:      current.NPCUpdates,
	}

	// Find new and updated entities
	for i := range current.Entities {
		curr := &current.Entities[i]
		lastEnt, exists := lastEntities[curr.EntityID]

		if !exists {
			// New entity
			delta.NewEntities = append(delta.NewEntities, *curr)
		} else {
			// Check if changed
			if dc.entityChanged(lastEnt, curr) {
				delta.UpdatedEntities = append(delta.UpdatedEntities, *curr)
			}
			delete(lastEntities, curr.EntityID)
		}
	}

	// Remaining in lastEntities are destroyed (already in current.Destroyed)

	return delta
}

func (dc *DeltaCompressor) entityChanged(a, b *EntitySnapshot) bool {
	// Quick checks
	if a.Type != b.Type {
		return true
	}
	if a.Timestamp == b.Timestamp {
		return false // no change
	}

	// Compare transform
	if a.Transform != nil && b.Transform != nil {
		if !transformEqual(a.Transform, b.Transform) {
			return true
		}
	} else if a.Transform != b.Transform {
		return true
	}

	// Compare velocity
	if a.Velocity != nil && b.Velocity != nil {
		if a.Velocity.X != b.Velocity.X || a.Velocity.Y != b.Velocity.Y || a.Velocity.Z != b.Velocity.Z {
			return true
		}
	} else if a.Velocity != b.Velocity {
		return true
	}

	// Could compare components here
	return false
}

func transformEqual(a, b *entity.Transform) bool {
	if a.Position != nil && b.Position != nil {
		if a.Position.X != b.Position.X || a.Position.Y != b.Position.Y || a.Position.Z != b.Position.Z {
			return false
		}
	} else if a.Position != b.Position {
		return false
	}
	if a.Rotation != nil && b.Rotation != nil {
		if a.Rotation.X != b.Rotation.X || a.Rotation.Y != b.Rotation.Y || a.Rotation.Z != b.Rotation.Z || a.Rotation.W != b.Rotation.W {
			return false
		}
	} else if a.Rotation != b.Rotation {
		return false
	}
	return true
}

// ============================================================================
// Optimized GameServer with all above
// ============================================================================

type OptimizedGameServer struct {
	world           *ecs.World
	scheduler       *ecs.SystemScheduler
	clients         map[string]*OptimizedClientSession
	clientsMu       sync.RWMutex
	tickRate        int
	running         atomic.Bool
	stopCh          chan struct{}
	snapshotHistory *SnapshotHistory
	interestManager *InterestManager
	deltaCompressor *DeltaCompressor
	snapshotPool    *SnapshotPool

	// Metrics
	metrics *ServerMetrics
}

type OptimizedClientSession struct {
	PlayerID         ecs.EntityID
	Stream           network.GameService_GameStreamServer
	LastAckTick      uint64
	SnapshotRate     int
	LastSnapshotTime time.Time
	ViewRadius       float32
	Compression      bool
	SendQueue        chan *network.ServerMessage
}

type ServerMetrics struct {
	TickCount         atomic.Uint64
	EntitiesCount     atomic.Int32
	PlayersCount      atomic.Int32
	BytesSent         atomic.Uint64
	BytesReceived     atomic.Uint64
	SnapshotBuildTime atomic.Int64 // nanoseconds
	DeltaCompressTime atomic.Int64
	NetworkTime       atomic.Int64
}

func NewOptimizedGameServer(world *ecs.World, scheduler *ecs.SystemScheduler) *OptimizedGameServer {
	s := &OptimizedGameServer{
		world:           world,
		scheduler:       scheduler,
		clients:         make(map[string]*OptimizedClientSession),
		tickRate:        60,
		stopCh:          make(chan struct{}),
		snapshotHistory: NewSnapshotHistory(),
		interestManager: NewInterestManager(100.0), // 100m cells
		deltaCompressor: NewDeltaCompressor(),
		snapshotPool:    NewSnapshotPool(),
		metrics:         &ServerMetrics{},
	}
	s.running.Store(true)
	return s
}

func (s *OptimizedGameServer) Start() {
	go s.gameLoop()
}

func (s *OptimizedGameServer) Stop() {
	s.running.Store(false)
	close(s.stopCh)
}

func (s *OptimizedGameServer) gameLoop() {
	ticker := time.NewTicker(time.Second / time.Duration(s.tickRate))
	defer ticker.Stop()

	var lastTime time.Time
	for s.running.Load() {
		select {
		case <-s.stopCh:
			return
		case now := <-ticker.C:
			dt := float32(now.Sub(lastTime).Seconds())
			if lastTime.IsZero() {
				dt = 1.0 / float32(s.tickRate)
			}
			lastTime = now

			// Update ECS systems
			s.scheduler.Update(s.world, dt)

			// Build and send snapshots
			s.buildAndSendSnapshots()

			s.metrics.TickCount.Add(1)
		}
	}
}

func (s *OptimizedGameServer) buildAndSendSnapshots() {
	start := time.Now()

	// Build full snapshot
	snap := s.snapshotPool.Get()
	snap.Tick = uint64(time.Now().UnixNano())
	snap.Timestamp = time.Now().UnixMilli()

	// Query relevant entities (could use interest management)
	query := s.world.Query(components.CompTransform)
	query.Iterate(func(entity ecs.EntityID) {
		// Build entity snapshot
		// ... (similar to original but with pooling)
	})

	// Store in history for delta compression
	s.snapshotHistory.Add(snap)

	// Send to each client with interest management + delta compression
	s.clientsMu.RLock()
	for _, client := range s.clients {
		if client.Stream == nil {
			continue
		}

		// Get relevant entities for this player
		relevant := s.interestManager.GetRelevantEntities(uint64(client.PlayerID))

		// Build filtered snapshot or delta
		var msg *network.ServerMessage
		if client.Compression {
			delta := s.deltaCompressor.Compress(snap, uint64(client.PlayerID))
			msg = s.buildDeltaMessage(delta, relevant)
		} else {
			msg = s.buildFullMessage(snap, relevant)
		}

		// Non-blocking send
		select {
		case client.SendQueue <- msg:
			s.metrics.BytesSent.Add(uint64(proto.Size(msg)))
		default:
			// Queue full - drop or buffer
		}
	}
	s.clientsMu.RUnlock()

	s.metrics.SnapshotBuildTime.Add(int64(time.Since(start)))
}

func (s *OptimizedGameServer) buildFullMessage(snap *Snapshot, relevant []uint64) *network.ServerMessage {
	// Convert to protobuf
	// ... implementation
	return nil
}

func (s *OptimizedGameServer) buildDeltaMessage(delta *DeltaSnapshot, relevant []uint64) *network.ServerMessage {
	// Convert delta to protobuf
	// ... implementation
	return nil
}

func (s *OptimizedGameServer) HandleClientMessage(session *OptimizedClientSession, msg *network.ClientMessage) {
	// Process input, RPCs
	s.metrics.BytesReceived.Add(uint64(proto.Size(msg)))
}

// ============================================================================
// Binary Serialization (alternative to protobuf for hot path)
// ============================================================================

type BinaryWriter struct {
	buf []byte
	pos int
}

func NewBinaryWriter(cap int) *BinaryWriter {
	return &BinaryWriter{buf: make([]byte, 0, cap)}
}

func (w *BinaryWriter) WriteUint64(v uint64) {
	w.buf = append(w.buf,
		byte(v), byte(v>>8), byte(v>>16), byte(v>>24),
		byte(v>>32), byte(v>>40), byte(v>>48), byte(v>>56))
}

func (w *BinaryWriter) WriteUint32(v uint32) {
	w.buf = append(w.buf, byte(v), byte(v>>8), byte(v>>16), byte(v>>24))
}

func (w *BinaryWriter) WriteFloat32(v float32) {
	b := *(*uint32)(unsafe.Pointer(&v))
	w.WriteUint32(b)
}

func (w *BinaryWriter) WriteVector3(v ecs.Vector3) {
	w.WriteFloat32(v.X)
	w.WriteFloat32(v.Y)
	w.WriteFloat32(v.Z)
}

func (w *BinaryWriter) WriteQuaternion(q ecs.Quaternion) {
	w.WriteFloat32(q.X)
	w.WriteFloat32(q.Y)
	w.WriteFloat32(q.Z)
	w.WriteFloat32(q.W)
}

func (w *BinaryWriter) Bytes() []byte {
	return w.buf
}

func (w *BinaryWriter) Reset() {
	w.buf = w.buf[:0]
	w.pos = 0
}

type BinaryReader struct {
	data []byte
	pos  int
}

func NewBinaryReader(data []byte) *BinaryReader {
	return &BinaryReader{data: data}
}

func (r *BinaryReader) ReadUint64() uint64 {
	v := uint64(r.data[r.pos]) | uint64(r.data[r.pos+1])<<8 | uint64(r.data[r.pos+2])<<16 | uint64(r.data[r.pos+3])<<24 |
		uint64(r.data[r.pos+4])<<32 | uint64(r.data[r.pos+5])<<40 | uint64(r.data[r.pos+6])<<48 | uint64(r.data[r.pos+7])<<56
	r.pos += 8
	return v
}

func (r *BinaryReader) ReadUint32() uint32 {
	v := uint32(r.data[r.pos]) | uint32(r.data[r.pos+1])<<8 | uint32(r.data[r.pos+2])<<16 | uint32(r.data[r.pos+3])<<24
	r.pos += 4
	return v
}

func (r *BinaryReader) ReadFloat32() float32 {
	v := r.ReadUint32()
	return *(*float32)(unsafe.Pointer(&v))
}

func (r *BinaryReader) ReadVector3() ecs.Vector3 {
	return ecs.Vector3{X: r.ReadFloat32(), Y: r.ReadFloat32(), Z: r.ReadFloat32()}
}

func (r *BinaryReader) ReadQuaternion() ecs.Quaternion {
	return ecs.Quaternion{X: r.ReadFloat32(), Y: r.ReadFloat32(), Z: r.ReadFloat32(), W: r.ReadFloat32()}
}
