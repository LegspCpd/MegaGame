package persistence

import (
	"encoding/json"
	"fmt"
	"os"
	"path/filepath"
	"time"

	"github.com/megame/server/internal/components"
	"github.com/megame/server/internal/ecs"
)

// SaveManager handles game state persistence
type SaveManager struct {
	saveDir string
	world   *ecs.World
}

func NewSaveManager(saveDir string, world *ecs.World) *SaveManager {
	os.MkdirAll(saveDir, 0755)
	return &SaveManager{
		saveDir: saveDir,
		world:   world,
	}
}

// SaveGame saves the entire world state to a file
func (sm *SaveManager) SaveGame(slot string) error {
	saveData := &SaveData{
		Version:    1,
		Timestamp:  time.Now().Unix(),
		Slot:       slot,
		Entities:   make([]EntitySaveData, 0),
		WorldState: sm.captureWorldState(),
	}

	// Iterate all entities
	query := sm.world.Query(components.CompTransform)
	query.Iterate(func(entity ecs.EntityID) {
		data := sm.serializeEntity(entity)
		if data != nil {
			saveData.Entities = append(saveData.Entities, *data)
		}
	})

	// Write to file
	data, err := json.MarshalIndent(saveData, "", "  ")
	if err != nil {
		return err
	}

	filename := filepath.Join(sm.saveDir, fmt.Sprintf("%s.save.json", slot))
	return os.WriteFile(filename, data, 0644)
}

// LoadGame loads a save file
func (sm *SaveManager) LoadGame(slot string) error {
	filename := filepath.Join(sm.saveDir, fmt.Sprintf("%s.save.json", slot))
	data, err := os.ReadFile(filename)
	if err != nil {
		return err
	}

	var saveData SaveData
	if err := json.Unmarshal(data, &saveData); err != nil {
		return err
	}

	// Clear current world (except systems)
	sm.clearWorld()

	// Restore entities
	for _, entData := range saveData.Entities {
		entity := sm.world.CreateEntity()
		sm.deserializeEntity(entity, &entData)
	}

	// Restore world state
	sm.restoreWorldState(&saveData.WorldState)

	return nil
}

func (sm *SaveManager) ListSaves() ([]SaveInfo, error) {
	entries, err := os.ReadDir(sm.saveDir)
	if err != nil {
		return nil, err
	}

	saves := make([]SaveInfo, 0)
	for _, entry := range entries {
		if entry.IsDir() || filepath.Ext(entry.Name()) != ".json" {
			continue
		}

		filename := filepath.Join(sm.saveDir, entry.Name())
		data, err := os.ReadFile(filename)
		if err != nil {
			continue
		}

		var saveData SaveData
		if err := json.Unmarshal(data, &saveData); err != nil {
			continue
		}

		saves = append(saves, SaveInfo{
			Slot:      saveData.Slot,
			Timestamp: saveData.Timestamp,
			Version:   saveData.Version,
		})
	}

	return saves, nil
}

func (sm *SaveManager) DeleteSave(slot string) error {
	filename := filepath.Join(sm.saveDir, fmt.Sprintf("%s.save.json", slot))
	return os.Remove(filename)
}

func (sm *SaveManager) serializeEntity(entity ecs.EntityID) *EntitySaveData {
	data := &EntitySaveData{
		ID:         uint64(entity),
		Components: make(map[string]json.RawMessage),
	}

	// Serialize each component
	componentTypes := []ecs.ComponentID{
		components.CompTransform,
		components.CompPhysics,
		components.CompHealth,
		components.CompPlayer,
		components.CompVehicle,
		components.CompWeapon,
		components.CompNPC,
		components.CompInventory,
		components.CompCharacterAppearance,
		components.CompVehicleMods,
		components.CompWeaponAttachments,
		components.CompWeaponCondition,
		components.CompMission,
		components.CompDialogue,
		components.CompPhone,
		components.CompStats,
	}

	for _, compID := range componentTypes {
		if comp, ok := sm.world.GetComponent(entity, compID); ok {
			jsonData, err := json.Marshal(comp)
			if err == nil {
				data.Components[compID.String()] = jsonData
			}
		}
	}

	return data
}

func (sm *SaveManager) deserializeEntity(entity ecs.EntityID, data *EntitySaveData) {
	for compIDStr, jsonData := range data.Components {
		// Parse component ID
		// In real implementation, would use registry
		_ = compIDStr
		_ = jsonData
		// Would unmarshal to appropriate component type and add to entity
	}
}

func (sm *SaveManager) captureWorldState() WorldState {
	return WorldState{
		TimeOfDay:        1200,
		Weather:          "clear",
		WeatherBlend:     0,
		WindSpeed:        2.0,
		WindDirection:    components.Vector2{X: 1, Y: 0},
		WantedMultiplier: 100,
	}
}

func (sm *SaveManager) restoreWorldState(state *WorldState) {
	// Would apply to world systems
	_ = state
}

func (sm *SaveManager) clearWorld() {
	// Destroy all entities
	query := sm.world.Query(components.CompTransform)
	entities := query.Entities()
	for _, e := range entities {
		sm.world.DestroyEntity(e)
	}
}

// SaveData represents a complete game save
type SaveData struct {
	Version    int              `json:"version"`
	Timestamp  int64            `json:"timestamp"`
	Slot       string           `json:"slot"`
	Entities   []EntitySaveData `json:"entities"`
	WorldState WorldState       `json:"world_state"`
}

type EntitySaveData struct {
	ID         uint64                     `json:"id"`
	Components map[string]json.RawMessage `json:"components"`
}

type WorldState struct {
	TimeOfDay        int32              `json:"time_of_day"`
	Weather          string             `json:"weather"`
	WeatherBlend     float32            `json:"weather_blend"`
	WindSpeed        float32            `json:"wind_speed"`
	WindDirection    components.Vector2 `json:"wind_direction"`
	WantedMultiplier int32              `json:"wanted_multiplier"`
}

type SaveInfo struct {
	Slot      string `json:"slot"`
	Timestamp int64  `json:"timestamp"`
	Version   int    `json:"version"`
}

// AutoSave runs periodic saves
type AutoSave struct {
	manager  *SaveManager
	interval time.Duration
	slot     string
	stopCh   chan struct{}
}

func NewAutoSave(manager *SaveManager, interval time.Duration, slot string) *AutoSave {
	return &AutoSave{
		manager:  manager,
		interval: interval,
		slot:     slot,
		stopCh:   make(chan struct{}),
	}
}

func (a *AutoSave) Start() {
	go func() {
		ticker := time.NewTicker(a.interval)
		defer ticker.Stop()

		for {
			select {
			case <-a.stopCh:
				return
			case <-ticker.C:
				if err := a.manager.SaveGame(a.slot); err != nil {
					fmt.Printf("Auto-save failed: %v\n", err)
				}
			}
		}
	}()
}

func (a *AutoSave) Stop() {
	close(a.stopCh)
}
