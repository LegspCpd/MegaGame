package network

import (
	"sync"
	"time"

	"github.com/megame/server/internal/components"
	"github.com/megame/server/internal/ecs"
	"github.com/megame/server/internal/proto/entity"
	"github.com/megame/server/internal/proto/network"
	"google.golang.org/grpc"
)

// GameServer implements the gRPC service
type GameServer struct {
	network.UnimplementedGameServiceServer
	world       *ecs.World
	systemMgr   *ecs.SystemManager
	clients     map[string]*ClientSession
	clientsMu   sync.RWMutex
	tickRate    int
	running     bool
	stopCh      chan struct{}
}

type ClientSession struct {
	PlayerID    ecs.EntityID
	Stream      network.GameService_GameStreamServer
	LastInput   *network.ClientInput
	LastAckTick uint64
	ConnectedAt time.Time
}

func NewGameServer(world *ecs.World, systemMgr *ecs.SystemManager) *GameServer {
	return &GameServer{
		world:     world,
		systemMgr: systemMgr,
		clients:   make(map[string]*ClientSession),
		tickRate:  60,
		stopCh:    make(chan struct{}),
	}
}

func (s *GameServer) Start() {
	s.running = true
	go s.gameLoop()
}

func (s *GameServer) Stop() {
	s.running = false
	close(s.stopCh)
}

func (s *GameServer) gameLoop() {
	ticker := time.NewTicker(time.Second / time.Duration(s.tickRate))
	defer ticker.Stop()

	var lastTime time.Time
	for {
		select {
		case <-s.stopCh:
			return
		case now := <-ticker.C:
			dt := float32(now.Sub(lastTime).Seconds())
			if lastTime.IsZero() {
				dt = 1.0 / float32(s.tickRate)
			}
			lastTime = now

			// Update systems
			s.systemMgr.Update(s.world, dt)

			// Send snapshots to clients
			s.broadcastSnapshots()
		}
	}
}

func (s *GameServer) broadcastSnapshots() {
	s.clientsMu.RLock()
	defer s.clientsMu.RUnlock()

	snapshot := s.buildSnapshot()

	for _, client := range s.clients {
		if client.Stream != nil {
			err := client.Stream.Send(&network.ServerMessage{
			Payload: &network.ServerMessage_Snapshot{Snapshot: snapshot},
		})
			if err != nil {
				// Client disconnected
				go s.handleDisconnect(client.PlayerID)
			}
		}
	}
}

func (s *GameServer) buildSnapshot() *network.ServerSnapshot {
	// Build entity states
	entityStates := make([]*entity.EntityState, 0)
	playerUpdates := make([]*network.PlayerStateUpdate, 0)
	vehicleUpdates := make([]*network.VehicleStateUpdate, 0)
	weaponUpdates := make([]*network.WeaponStateUpdate, 0)
	npcUpdates := make([]*network.NPCStateUpdate, 0)

	// Query all entities with transform
	query := s.world.Query(components.CompTransform)
	query.Iterate(func(e ecs.EntityID) {
		transform, _ := s.world.GetComponent(e, components.CompTransform)
		t := transform.(*components.TransformComponent)

		// Determine entity type
		entType := entity.EntityType_ENTITY_TYPE_PROP
		var playerUpd *network.PlayerStateUpdate
		var vehicleUpd *network.VehicleStateUpdate
		var weaponUpd *network.WeaponStateUpdate
		var npcUpd *network.NPCStateUpdate

		if _, ok := s.world.GetComponent(e, components.CompPlayer); ok {
			entType = entity.EntityType_ENTITY_TYPE_PLAYER
			playerUpd = s.buildPlayerUpdate(e)
		} else if _, ok := s.world.GetComponent(e, components.CompVehicle); ok {
			entType = entity.EntityType_ENTITY_TYPE_VEHICLE
			vehicleUpd = s.buildVehicleUpdate(e)
		} else if _, ok := s.world.GetComponent(e, components.CompWeapon); ok {
			entType = entity.EntityType_ENTITY_TYPE_WEAPON
			weaponUpd = s.buildWeaponUpdate(e)
		} else if _, ok := s.world.GetComponent(e, components.CompNPC); ok {
			entType = entity.EntityType_ENTITY_TYPE_NPC
			npcUpd = s.buildNPCUpdate(e)
		}

		entityStates = append(entityStates, &entity.EntityState{
			Ref: &entity.EntityRef{
				Id:   uint64(e),
				Type: entType,
			},
			Transform: &entity.Transform{
				Position: &entity.Vector3{X: t.Position.X, Y: t.Position.Y, Z: t.Position.Z},
				Rotation: &entity.Quaternion{X: t.Rotation.X, Y: t.Rotation.Y, Z: t.Rotation.Z, W: t.Rotation.W},
				Scale:    &entity.Vector3{X: t.Scale.X, Y: t.Scale.Y, Z: t.Scale.Z},
			},
			Velocity: &entity.Vector3{},
			Timestamp: uint64(time.Now().UnixMilli()),
		})

		if playerUpd != nil {
			playerUpdates = append(playerUpdates, playerUpd)
		}
		if vehicleUpd != nil {
			vehicleUpdates = append(vehicleUpdates, vehicleUpd)
		}
		if weaponUpd != nil {
			weaponUpdates = append(weaponUpdates, weaponUpd)
		}
		if npcUpd != nil {
			npcUpdates = append(npcUpdates, npcUpd)
		}
	})

	return &network.ServerSnapshot{
		Tick:             uint64(time.Now().UnixMilli()),
		ServerTimeMs:     uint64(time.Now().UnixMilli()),
		Entities:         entityStates,
		PlayerUpdates:    playerUpdates,
		VehicleUpdates:   vehicleUpdates,
		WeaponUpdates:    weaponUpdates,
		NpcUpdates:       npcUpdates,
		World:            s.buildWorldState(),
	}
}

func (s *GameServer) buildPlayerUpdate(e ecs.EntityID) *network.PlayerStateUpdate {
	player, _ := s.world.GetComponent(e, components.CompPlayer)
	health, _ := s.world.GetComponent(e, components.CompHealth)
	pl := player.(*components.PlayerComponent)
	h := health.(*components.HealthComponent)

	return &network.PlayerStateUpdate{
		EntityId:           uint64(e),
		Health:             h.CurrentHealth,
		Armor:              h.Armor,
		Stamina:            pl.Stamina,
		CameraMode:         entity.CameraMode(pl.CameraMode),
		CurrentWeaponId:    uint64(pl.CurrentWeapon),
		CurrentVehicleId:   uint64(pl.CurrentVehicle),
		IsInVehicle:        pl.IsInVehicle,
		VehicleSeatIndex:   pl.VehicleSeatIndex,
		Money:              pl.Money,
		WantedLevel:        uint32(pl.WantedLevel),
		InventoryWeaponIds: s.uint64Slice(pl.InventoryWeaponIDs),
		InventoryItemIds:   s.uint64Slice(pl.InventoryItemIDs),
	}
}

func (s *GameServer) buildVehicleUpdate(e ecs.EntityID) *network.VehicleStateUpdate {
	vehicle, _ := s.world.GetComponent(e, components.CompVehicle)
	v := vehicle.(*components.VehicleComponent)

	doors := make([]*entity.VehicleDoorState, len(v.Doors))
	for i, d := range v.Doors {
		doors[i] = &entity.VehicleDoorState{
			Index:     d.Index,
			Angle:     d.Angle,
			IsBroken:  d.Broken,
		}
	}

	wheels := make([]*entity.VehicleWheelState, len(v.Wheels))
	for i, w := range v.Wheels {
		wheels[i] = &entity.VehicleWheelState{
			Index:                 w.Index,
			Rotation:              w.Rotation,
			SteerAngle:            w.SteerAngle,
			IsBurst:               w.Burst,
			SuspensionCompression: w.SuspensionComp,
			BrakeForce:            w.BrakeForce,
		}
	}

	return &network.VehicleStateUpdate{
		EntityId:       uint64(e),
		EngineHealth:   v.EngineHealth,
		BodyHealth:     v.BodyHealth,
		Fuel:           v.Fuel,
		CurrentGear:    v.CurrentGear,
		Rpm:            v.RPM,
		SpeedKph:       v.SpeedKPH,
		EngineOn:       v.EngineOn,
		LightsOn:       v.LightsOn,
		SirenOn:        v.SirenOn,
		Doors:          doors,
		Wheels:         wheels,
		OccupantIds:    s.uint64Slice(v.Occupants),
	}
}

func (s *GameServer) buildWeaponUpdate(e ecs.EntityID) *network.WeaponStateUpdate {
	weapon, _ := s.world.GetComponent(e, components.CompWeapon)
	w := weapon.(*components.WeaponComponent)

	return &network.WeaponStateUpdate{
		EntityId:       uint64(e),
		AmmoInClip:     w.AmmoInClip,
		AmmoReserve:    w.AmmoReserve,
		Heat:           w.Heat,
		IsReloading:    w.IsReloading,
		ReloadProgress: w.ReloadProgress,
	}
}

func (s *GameServer) buildNPCUpdate(e ecs.EntityID) *network.NPCStateUpdate {
	npc, _ := s.world.GetComponent(e, components.CompNPC)
	health, _ := s.world.GetComponent(e, components.CompHealth)
	n := npc.(*components.NPCComponent)
	h := health.(*components.HealthComponent)

	return &network.NPCStateUpdate{
		EntityId:        uint64(e),
		Health:          h.CurrentHealth,
		TargetEntityId:  uint64(n.TargetEntity),
		IsFleeing:       n.IsFleeing,
		IsInCombat:      n.IsInCombat,
	}
}

func (s *GameServer) buildWorldState() *network.WorldState {
	return &network.WorldState{
		TimeOfDay:    1200, // 12:00
		WeatherBlend: 0,
		CurrentWeather: "clear",
		WindSpeed:    2.0,
	}
}

func (s *GameServer) uint64Slice(ids []ecs.EntityID) []uint64 {
	result := make([]uint64, len(ids))
	for i, id := range ids {
		result[i] = uint64(id)
	}
	return result
}

// GameStream handles bidirectional streaming
func (s *GameServer) GameStream(stream network.GameService_GameStreamServer) error {
	ctx := stream.Context()
	clientID := "" // Would extract from auth

	// Create session
	session := &ClientSession{
		Stream:      stream,
		ConnectedAt: time.Now(),
	}

	s.clientsMu.Lock()
	s.clients[clientID] = session
	s.clientsMu.Unlock()

	defer func() {
		s.clientsMu.Lock()
		delete(s.clients, clientID)
		s.clientsMu.Unlock()
	}()

	for {
		select {
		case <-ctx.Done():
			return ctx.Err()
		default:
			msg, err := stream.Recv()
			if err != nil {
				return err
			}
			s.handleClientMessage(session, msg)
		}
	}
}

func (s *GameServer) handleClientMessage(session *ClientSession, msg *network.ClientMessage) {
	switch msg.Payload.(type) {
	case *network.ClientMessage_Input:
		session.LastInput = msg.GetInput()
		s.processInput(session, msg.GetInput())
	case *network.ClientMessage_Rpc:
		s.handleRPC(session, msg.GetRpc())
	}
}

func (s *GameServer) processInput(session *ClientSession, input *network.ClientInput) {
	if session.PlayerID == 0 {
		return
	}

	// Apply input to player entity
	// This would update physics, movement, etc.
	// For now, just store for systems to use
}

func (s *GameServer) handleRPC(session *ClientSession, rpc *network.RPCRequest) {
	var response *network.RPCResponse

	switch network.RPCMethod(rpc.Method) {
	case network.RPCMethod_RPC_SPAWN_PLAYER:
		response = s.rpcSpawnPlayer(session, rpc)
	case network.RPCMethod_RPC_SPAWN_VEHICLE:
		response = s.rpcSpawnVehicle(session, rpc)
	case network.RPCMethod_RPC_GIVE_WEAPON:
		response = s.rpcGiveWeapon(session, rpc)
	case network.RPCMethod_RPC_SET_VEHICLE_MODS:
		response = s.rpcSetVehicleMods(session, rpc)
	case network.RPCMethod_RPC_TRIGGER_MISSION:
		response = s.rpcTriggerMission(session, rpc)
	case network.RPCMethod_RPC_START_CUTSCENE:
		response = s.rpcStartCutscene(session, rpc)
	default:
		response = &network.RPCResponse{
			RequestId: rpc.RequestId,
			Success:   false,
			Error:     "Unknown method",
		}
	}

	// Send response via stream
	if session.Stream != nil {
		session.Stream.Send(&network.ServerMessage{
			Payload: &network.ServerMessage_Rpc{response},
		})
	}
}

func (s *GameServer) rpcSpawnPlayer(session *ClientSession, rpc *network.RPCRequest) *network.RPCResponse {
	// Parse request
	// Spawn player entity
	playerEntity := s.world.CreateEntity()

	transform := &components.TransformComponent{
		Position: components.Vector3{0, 50, 0},
		Rotation: components.Quaternion{0, 0, 0, 1},
		Scale:    components.Vector3{1, 1, 1},
	}
	s.world.AddComponent(playerEntity, transform)

	physics := &components.PhysicsComponent{
		Velocity:    components.Vector3{},
		Mass:        80,
		Drag:        0.1,
		UseGravity:  true,
		IsKinematic: false,
	}
	s.world.AddComponent(playerEntity, physics)

	health := &components.HealthComponent{
		CurrentHealth: 100,
		MaxHealth:     100,
		Armor:         0,
		MaxArmor:      50,
	}
	s.world.AddComponent(playerEntity, health)

	player := &components.PlayerComponent{
		CharacterName:    "Player",
		Money:            5000,
		CameraMode:       components.CameraThirdPerson,
		Stamina:          100,
		MaxStamina:       100,
		InventoryWeaponIDs: []ecs.EntityID{},
		InventoryItemIDs:   []ecs.EntityID{},
	}
	s.world.AddComponent(playerEntity, player)

	appearance := &components.CharacterAppearanceComponent{
		BodyType:  "male_avg",
		HeadModel: "head_01",
		HairStyle: "hair_01",
		HairColor: "#333333",
		SkinTone:  "#DEB887",
		EyeColor:  "#444444",
	}
	s.world.AddComponent(playerEntity, appearance)

	inventory := &components.InventoryComponent{
		Items:       make(map[string]int32),
		MaxWeight:   50,
	}
	s.world.AddComponent(playerEntity, inventory)

	stats := &components.StatsComponent{
		WeaponKills:   make(map[string]uint32),
		VehicleUsage:  make(map[string]float32),
		LocationVisits: make(map[string]uint32),
	}
	s.world.AddComponent(playerEntity, stats)

	phone := &components.PhoneComponent{
		Contacts: []components.PhoneContact{},
		Messages: []components.PhoneMessage{},
		Apps:     make(map[string]components.PhoneApp),
	}
	s.world.AddComponent(playerEntity, phone)

	mission := &components.MissionComponent{
		MissionProgress:    make(map[string]*components.MissionProgress),
		CompletedMissions:  []string{},
		FailedMissions:     []string{},
		AvailableMissions:  []string{"mission_intro_01"},
	}
	s.world.AddComponent(playerEntity, mission)

	session.PlayerID = playerEntity

	return &network.RPCResponse{
		RequestId: rpc.RequestId,
		Success:   true,
		Payload:   nil, // Would serialize player entity ID
	}
}

func (s *GameServer) rpcSpawnVehicle(session *ClientSession, rpc *network.RPCRequest) *network.RPCResponse {
	if session.PlayerID == 0 {
		return &network.RPCResponse{RequestId: rpc.RequestId, Success: false, Error: "Not spawned"}
	}

	// Parse vehicle spawn request
	// Create vehicle entity
	vehicleEntity := s.world.CreateEntity()

	transform := &components.TransformComponent{
		Position: components.Vector3{0, 51, 0},
		Rotation: components.Quaternion{0, 0, 0, 1},
		Scale:    components.Vector3{1, 1, 1},
	}
	s.world.AddComponent(vehicleEntity, transform)

	physics := &components.PhysicsComponent{
		Velocity:    components.Vector3{},
		Mass:        1500,
		Drag:        0.3,
		UseGravity:  true,
		IsKinematic: false,
	}
	s.world.AddComponent(vehicleEntity, physics)

	health := &components.HealthComponent{
		CurrentHealth: 1000,
		MaxHealth:     1000,
	}
	s.world.AddComponent(vehicleEntity, health)

	vehicle := &components.VehicleComponent{
		ModelID:      "vehicle_sultan_rs",
		DisplayName:  "Sultan RS",
		Manufacturer: "Karin",
		VehicleClass: "sports",
		EngineHealth: 1000,
		BodyHealth:   1000,
		Fuel:         100,
		MaxFuel:      100,
		EngineOn:     false,
		Doors: []components.VehicleDoor{
			{Index: 0}, {Index: 1}, {Index: 2}, {Index: 3},
		},
		Wheels: []components.VehicleWheel{
			{Index: 0}, {Index: 1}, {Index: 2}, {Index: 3},
		},
		Occupants: []ecs.EntityID{},
		Mods: &components.VehicleModifications{},
	}
	s.world.AddComponent(vehicleEntity, vehicle)

	// Warp player in if requested
	// ...

	return &network.RPCResponse{
		RequestId: rpc.RequestId,
		Success:   true,
	}
}

func (s *GameServer) rpcGiveWeapon(session *ClientSession, rpc *network.RPCRequest) *network.RPCResponse {
	if session.PlayerID == 0 {
		return &network.RPCResponse{RequestId: rpc.RequestId, Success: false, Error: "Not spawned"}
	}

	// Create weapon entity
	weaponEntity := s.world.CreateEntity()

	weapon := &components.WeaponComponent{
		ModelID:      "weapon_pistol_glock17",
		DisplayName:  "Glock 17",
		Manufacturer: "Glock",
		WeaponClass:  "pistol",
		AmmoInClip:   17,
		AmmoReserve:  170,
		MaxClipSize:  17,
		MaxReserve:   170,
		Owner:        session.PlayerID,
		Attachments:  &components.WeaponAttachments{Equipped: make(map[string]string)},
		Condition:    &components.WeaponCondition{Durability: 1.0},
	}
	s.world.AddComponent(weaponEntity, weapon)

	// Add to player inventory
	if inv, ok := s.world.GetComponent(session.PlayerID, components.CompInventory); ok {
		i := inv.(*components.InventoryComponent)
		i.Weapons = append(i.Weapons, weaponEntity)
	}

	if player, ok := s.world.GetComponent(session.PlayerID, components.CompPlayer); ok {
		p := player.(*components.PlayerComponent)
		p.InventoryWeaponIDs = append(p.InventoryWeaponIDs, weaponEntity)
	}

	return &network.RPCResponse{
		RequestId: rpc.RequestId,
		Success:   true,
	}
}

func (s *GameServer) rpcSetVehicleMods(session *ClientSession, rpc *network.RPCRequest) *network.RPCResponse {
	// Parse and apply vehicle modifications
	return &network.RPCResponse{RequestId: rpc.RequestId, Success: true}
}

func (s *GameServer) rpcTriggerMission(session *ClientSession, rpc *network.RPCRequest) *network.RPCResponse {
	if session.PlayerID == 0 {
		return &network.RPCResponse{RequestId: rpc.RequestId, Success: false, Error: "Not spawned"}
	}

	// Would parse mission ID and start mission
	return &network.RPCResponse{RequestId: rpc.RequestId, Success: true}
}

func (s *GameServer) rpcStartCutscene(session *ClientSession, rpc *network.RPCRequest) *network.RPCResponse {
	// Start cutscene for player
	return &network.RPCResponse{RequestId: rpc.RequestId, Success: true}
}

func (s *GameServer) handleDisconnect(playerID ecs.EntityID) {
	// Clean up player entity
	s.world.DestroyEntity(playerID)
}

// RegisterGRPC registers the game service with gRPC server
func RegisterGRPC(server *grpc.Server, gameServer *GameServer) {
	network.RegisterGameServiceServer(server, gameServer)
}
