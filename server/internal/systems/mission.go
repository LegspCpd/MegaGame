package systems

import (
	"encoding/json"
	"fmt"
	"math"
	"strings"
	"time"

	"github.com/megame/server/internal/components"
	"github.com/megame/server/internal/ecs"
)

// MissionSystem handles mission progression, objectives, and rewards
type MissionSystem struct {
	Priority_       int
	missionDatabase map[string]*MissionDefinition
}

func NewMissionSystem() *MissionSystem {
	return &MissionSystem{
		Priority_:       40,
		missionDatabase: make(map[string]*MissionDefinition),
	}
}

func (s *MissionSystem) Priority() int { return s.Priority_ }

func (s *MissionSystem) LoadMissions(data []byte) error {
	return json.Unmarshal(data, &s.missionDatabase)
}

func (s *MissionSystem) Update(world *ecs.World, dt float32) {
	query := world.Query(components.CompMission, components.CompPlayer)
	query.Iterate(func(entity ecs.EntityID) {
		missionComp, _ := world.GetComponent(entity, components.CompMission)
		player, _ := world.GetComponent(entity, components.CompPlayer)

		m := missionComp.(*components.MissionComponent)
		p := player.(*components.PlayerComponent)

		if m.ActiveMission == "" {
			return
		}

		def, ok := s.missionDatabase[m.ActiveMission]
		if !ok {
			return
		}

		progress, ok := m.MissionProgress[m.ActiveMission]
		if !ok {
			return
		}

		// Update objectives
		for _, objDef := range def.Objectives {
			objProgress, ok := progress.Objectives[objDef.ID]
			if !ok {
				continue
			}

			if objProgress.Status != components.ObjectiveActive {
				continue
			}

			// Check objective completion
			s.checkObjective(world, entity, objDef, objProgress, p)
		}

		// Check if all required objectives complete
		if s.allRequiredComplete(progress, def) {
			s.completeMission(world, entity, m, def)
		}

		// Check failure conditions
		if s.checkFailure(world, entity, def, progress) {
			s.failMission(world, entity, m, def)
		}
	})
}

func (s *MissionSystem) checkObjective(world *ecs.World, playerEntity ecs.EntityID, objDef *MissionObjective, objProgress *components.ObjectiveProgress, player *components.PlayerComponent) {
	switch objDef.Type {
	case ObjectiveGoTo:
		s.checkGoTo(world, playerEntity, objDef, objProgress)
	case ObjectiveKill:
		s.checkKill(world, playerEntity, objDef, objProgress)
	case ObjectiveDestroy:
		s.checkDestroy(world, playerEntity, objDef, objProgress)
	case ObjectiveCollect:
		s.checkCollect(world, playerEntity, objDef, objProgress)
	case ObjectiveDeliver:
		s.checkDeliver(world, playerEntity, objDef, objProgress)
	case ObjectiveSurviveWaves:
		s.checkSurvive(objDef, objProgress)
	case ObjectiveLoseWanted:
		s.checkLoseWanted(player, objDef, objProgress)
	case ObjectiveRaceCheckpoint:
		s.checkRaceCheckpoint(world, playerEntity, objDef, objProgress)
	}
}

func (s *MissionSystem) checkGoTo(world *ecs.World, playerEntity ecs.EntityID, objDef *MissionObjective, objProgress *components.ObjectiveProgress) {
	transform, ok := world.GetComponent(playerEntity, components.CompTransform)
	if !ok {
		return
	}

	t := transform.(*components.TransformComponent)
	dist := t.Position.Distance(objDef.TargetPosition)

	if dist <= objDef.TargetRadius {
		objProgress.Current = objProgress.Target
		objProgress.Status = components.ObjectiveCompleted
	}
}

func (s *MissionSystem) checkKill(world *ecs.World, playerEntity ecs.EntityID, objDef *MissionObjective, objProgress *components.ObjectiveProgress) {
	// Would track kills via event system
	// For now, check if target entity is dead
	if objDef.TargetEntityID != 0 {
		if health, ok := world.GetComponent(objDef.TargetEntityID, components.CompHealth); ok {
			h := health.(*components.HealthComponent)
			if h.IsDead {
				objProgress.Current = objProgress.Target
				objProgress.Status = components.ObjectiveCompleted
			}
		}
	}
}

func (s *MissionSystem) checkDestroy(world *ecs.World, playerEntity ecs.EntityID, objDef *MissionObjective, objProgress *components.ObjectiveProgress) {
	// Similar to kill but for vehicles/props
	if objDef.TargetEntityID != 0 {
		if health, ok := world.GetComponent(objDef.TargetEntityID, components.CompHealth); ok {
			h := health.(*components.HealthComponent)
			if h.IsDead {
				objProgress.Current = objProgress.Target
				objProgress.Status = components.ObjectiveCompleted
			}
		}
	}
}

func (s *MissionSystem) checkCollect(world *ecs.World, playerEntity ecs.EntityID, objDef *MissionObjective, objProgress *components.ObjectiveProgress) {
	player, ok := world.GetComponent(playerEntity, components.CompInventory)
	if !ok {
		return
	}

	inv := player.(*components.InventoryComponent)
	count := inv.Items[objDef.TargetItemID]
	if count >= objProgress.Target {
		objProgress.Current = objProgress.Target
		objProgress.Status = components.ObjectiveCompleted
	} else {
		objProgress.Current = int32(count)
	}
}

func (s *MissionSystem) checkDeliver(world *ecs.World, playerEntity ecs.EntityID, objDef *MissionObjective, objProgress *components.ObjectiveProgress) {
	// Check if player has item and is at delivery location
	player, ok := world.GetComponent(playerEntity, components.CompInventory)
	if !ok {
		return
	}

	inv := player.(*components.InventoryComponent)
	if inv.Items[objDef.TargetItemID] > 0 {
		// Check position
		transform, ok := world.GetComponent(playerEntity, components.CompTransform)
		if ok {
			t := transform.(*components.TransformComponent)
			dist := t.Position.Distance(objDef.TargetPosition)
			if dist <= objDef.TargetRadius {
				// Remove item and complete
				inv.Items[objDef.TargetItemID]--
				objProgress.Current = objProgress.Target
				objProgress.Status = components.ObjectiveCompleted
			}
		}
	}
}

func (s *MissionSystem) checkSurvive(objDef *MissionObjective, objProgress *components.ObjectiveProgress) {
	// Time-based survival - would track via timer
	objProgress.Current++
	if objProgress.Current >= objProgress.Target {
		objProgress.Status = components.ObjectiveCompleted
	}
}

func (s *MissionSystem) checkLoseWanted(player *components.PlayerComponent, objDef *MissionObjective, objProgress *components.ObjectiveProgress) {
	if player.WantedLevel == 0 {
		objProgress.Current = objProgress.Target
		objProgress.Status = components.ObjectiveCompleted
	}
}

func (s *MissionSystem) checkRaceCheckpoint(world *ecs.World, playerEntity ecs.EntityID, objDef *MissionObjective, objProgress *components.ObjectiveProgress) {
	// Would track checkpoint progression
}

func (s *MissionSystem) allRequiredComplete(progress *components.MissionProgress, def *MissionDefinition) bool {
	for _, objDef := range def.Objectives {
		if objDef.IsOptional {
			continue
		}
		objProgress, ok := progress.Objectives[objDef.ID]
		if !ok || objProgress.Status != components.ObjectiveCompleted {
			return false
		}
	}
	return true
}

func (s *MissionSystem) completeMission(world *ecs.World, playerEntity ecs.EntityID, m *components.MissionComponent, def *MissionDefinition) {
	player, _ := world.GetComponent(playerEntity, components.CompPlayer)
	pl := player.(*components.PlayerComponent)

	// Give rewards
	pl.Money += def.RewardMoney
	pl.Experience += uint64(def.RewardXP)

	// Add items
	inv, _ := world.GetComponent(playerEntity, components.CompInventory)
	if inv != nil {
		inventory := inv.(*components.InventoryComponent)
		for _, item := range def.RewardItems {
			inventory.Items[item]++
		}
	}

	// Mark complete
	m.CompletedMissions = append(m.CompletedMissions, m.ActiveMission)
	progress := m.MissionProgress[m.ActiveMission]
	if progress != nil {
		progress.CurrentPhase = int32(len(def.Objectives))
	}

	// Unlock new missions
	for _, unlock := range def.Unlocks {
		if !contains(m.AvailableMissions, unlock) && !contains(m.CompletedMissions, unlock) {
			m.AvailableMissions = append(m.AvailableMissions, unlock)
		}
	}

	// Clear active
	m.ActiveMission = ""

	// Trigger completion event
	fmt.Printf("Mission completed: %s\n", def.Title)
}

func (s *MissionSystem) failMission(world *ecs.World, playerEntity ecs.EntityID, m *components.MissionComponent, def *MissionDefinition) {
	m.FailedMissions = append(m.FailedMissions, m.ActiveMission)
	m.ActiveMission = ""
	fmt.Printf("Mission failed: %s\n", def.Title)
}

func (s *MissionSystem) checkFailure(world *ecs.World, playerEntity ecs.EntityID, def *MissionDefinition, progress *components.MissionProgress) bool {
	// Check timeout
	if def.TimeLimit > 0 {
		elapsed := time.Now().Unix() - progress.StartTime
		if elapsed > int64(def.TimeLimit) {
			return true
		}
	}

	// Check player death
	if health, ok := world.GetComponent(playerEntity, components.CompHealth); ok {
		h := health.(*components.HealthComponent)
		if h.IsDead {
			return true
		}
	}

	// Check critical objective failure
	for _, objDef := range def.Objectives {
		if objDef.IsOptional {
			continue
		}
		objProgress, ok := progress.Objectives[objDef.ID]
		if ok && objProgress.Status == components.ObjectiveFailed {
			return true
		}
	}

	return false
}

func (s *MissionSystem) StartMission(world *ecs.World, playerEntity ecs.EntityID, missionID string) bool {
	missionComp, ok := world.GetComponent(playerEntity, components.CompMission)
	if !ok {
		return false
	}

	m := missionComp.(*components.MissionComponent)
	def, ok := s.missionDatabase[missionID]
	if !ok {
		return false
	}

	// Check prerequisites
	for _, prereq := range def.Prerequisites {
		if !contains(m.CompletedMissions, prereq) {
			return false
		}
	}

	// Check if already active/completed
	if m.ActiveMission == missionID || contains(m.CompletedMissions, missionID) {
		return false
	}

	// Initialize progress
	progress := &components.MissionProgress{
		MissionID:  missionID,
		Objectives: make(map[string]*components.ObjectiveProgress),
		StartTime:  time.Now().Unix(),
	}

	for _, objDef := range def.Objectives {
		status := components.ObjectivePending
		if !objDef.IsHidden {
			status = components.ObjectiveActive
		}
		progress.Objectives[objDef.ID] = &components.ObjectiveProgress{
			ObjectiveID: objDef.ID,
			Status:      status,
			Current:     0,
			Target:      objDef.TargetProgress,
			IsOptional:  objDef.IsOptional,
			IsHidden:    objDef.IsHidden,
		}
	}

	m.MissionProgress[missionID] = progress
	m.ActiveMission = missionID

	// Remove from available
	m.AvailableMissions = removeString(m.AvailableMissions, missionID)

	return true
}

func (s *MissionSystem) CancelMission(world *ecs.World, playerEntity ecs.EntityID) {
	missionComp, ok := world.GetComponent(playerEntity, components.CompMission)
	if !ok {
		return
	}

	m := missionComp.(*components.MissionComponent)
	if m.ActiveMission != "" {
		m.FailedMissions = append(m.FailedMissions, m.ActiveMission)
		m.ActiveMission = ""
	}
}

func (s *MissionSystem) GetMissionProgress(world *ecs.World, playerEntity ecs.EntityID, missionID string) *components.MissionProgress {
	missionComp, ok := world.GetComponent(playerEntity, components.CompMission)
	if !ok {
		return nil
	}

	m := missionComp.(*components.MissionComponent)
	return m.MissionProgress[missionID]
}

// MissionDefinition represents a mission from data
type MissionDefinition struct {
	ID              string
	Title           string
	Description     string
	Type            MissionType
	Prerequisites   []string
	Unlocks         []string
	RewardMoney     uint32
	RewardItems     []string
	RewardVehicles  []string
	RewardWeapons   []string
	RewardXP        uint32
	GiverNPCID      string
	StartPosition   components.Vector3
	StartCutscene   string
	EndCutscene     string
	IsReplayable    bool
	MinLevel        int32
	Difficulty      string
	TimeLimit       int32 // seconds, 0 = no limit
	Objectives      []*MissionObjective
}

type MissionType int

const (
	MissionMain MissionType = iota
	MissionSide
	MissionStranger
	MissionRandom
	MissionRace
	MissionDeathmatch
	MissionSurvival
	MissionHeist
	MissionAssassination
	MissionDelivery
	MissionStealth
	MissionPhoto
	MissionCollectible
)

type MissionObjective struct {
	ID              string
	Description     string
	Type            ObjectiveType
	TargetProgress  int32
	TargetPosition  components.Vector3
	TargetRadius    float32
	TargetEntityID  ecs.EntityID
	TargetItemID    string
	TargetVehicleID string
	Conditions      []string
	IsOptional      bool
	IsHidden        bool
	OnCompleteDialogue string
	OnFailDialogue  string
}

type ObjectiveType int

const (
	ObjectiveGoTo ObjectiveType = iota
	ObjectiveKill
	ObjectiveDestroy
	ObjectiveCollect
	ObjectiveDeliver
	ObjectiveEscort
	ObjectiveDefend
	ObjectiveSteal
	ObjectiveHack
	ObjectivePhotograph
	ObjectiveRaceCheckpoint
	ObjectiveSurviveWaves
	ObjectiveFollow
	ObjectiveLoseWanted
	ObjectiveBuy
	ObjectiveCustom
)

// Helper functions
func contains(slice []string, item string) bool {
	for _, s := range slice {
		if s == item {
			return true
		}
	}
	return false
}

func removeString(slice []string, item string) []string {
	for i, s := range slice {
		if s == item {
			return append(slice[:i], slice[i+1:]...)
		}
	}
	return slice
}

// DialogueSystem handles conversations
type DialogueSystem struct {
	Priority_       int
	dialogueDatabase map[string]*DialogueDefinition
}

func NewDialogueSystem() *DialogueSystem {
	return &DialogueSystem{
		Priority_:        50,
		dialogueDatabase: make(map[string]*DialogueDefinition),
	}
}

func (s *DialogueSystem) Priority() int { return s.Priority_ }

func (s *DialogueSystem) LoadDialogues(data []byte) error {
	return json.Unmarshal(data, &s.dialogueDatabase)
}

func (s *DialogueSystem) Update(world *ecs.World, dt float32) {
	query := world.Query(components.CompDialogue)
	query.Iterate(func(entity ecs.EntityID) {
		dialogue, _ := world.GetComponent(entity, components.CompDialogue)
		d := dialogue.(*components.DialogueComponent)

		if !d.IsInDialogue {
			return
		}

		// Auto-advance timed nodes
		if len(d.SubtitleQueue) > 0 {
			sub := &d.SubtitleQueue[0]
			sub.Duration -= dt
			if sub.Duration <= 0 {
				d.SubtitleQueue = d.SubtitleQueue[1:]
				s.advanceDialogue(world, entity, d)
			}
		}
	})
}

func (s *DialogueSystem) StartDialogue(world *ecs.World, playerEntity, npcEntity ecs.EntityID, dialogueID string) bool {
	dialogueDef, ok := s.dialogueDatabase[dialogueID]
	if !ok {
		return false
	}

	// Get or add dialogue component to player
	playerDialogue, ok := world.GetComponent(playerEntity, components.CompDialogue)
	if !ok {
		playerDialogue = &components.DialogueComponent{}
		world.AddComponent(playerEntity, playerDialogue)
	}

	d := playerDialogue.(*components.DialogueComponent)
	d.IsInDialogue = true
	d.CurrentDialogue = dialogueID
	d.CurrentNode = dialogueDef.StartNode
	d.SpeakerEntity = npcEntity
	d.NodeHistory = []string{}
	d.SubtitleQueue = []components.SubtitleEntry{}

	// Process first node
	s.processNode(world, d, dialogueDef.Nodes[d.CurrentNode])

	return true
}

func (s *DialogueSystem) processNode(world *ecs.World, d *components.DialogueComponent, node *DialogueNode) {
	d.NodeHistory = append(d.NodeHistory, d.CurrentNode)

	// Queue subtitle
	if node.Subtitle.Text != "" {
		d.SubtitleQueue = append(d.SubtitleQueue, node.Subtitle)
	}

	// Execute enter actions
	for _, action := range node.Actions {
		s.executeAction(world, action, d.SpeakerEntity)
	}

	// Set camera
	// Would send camera command to client

	// If no choices, auto-advance after duration
	if len(node.Choices) == 0 && node.NextNodeID != "" {
		d.CurrentNode = node.NextNodeID
		if nextNode, ok := s.dialogueDatabase[d.CurrentDialogue].Nodes[node.NextNodeID]; ok {
			s.processNode(world, d, nextNode)
		}
	} else {
		// Convert choices
		d.Choices = make([]components.DialogueChoice, len(node.Choices))
		for i, choice := range node.Choices {
			d.Choices[i] = components.DialogueChoice{
				ID:               choice.ID,
				Text:             choice.Text,
				NextNode:         choice.NextNodeID,
				Conditions:       choice.Conditions,
				Consequences:     choice.Consequences,
				EndsConversation: choice.EndsConversation,
			}
		}
	}
}

func (s *DialogueSystem) SelectChoice(world *ecs.World, playerEntity ecs.EntityID, choiceIndex int) {
	playerDialogue, ok := world.GetComponent(playerEntity, components.CompDialogue)
	if !ok {
		return
	}

	d := playerDialogue.(*components.DialogueComponent)
	if !d.IsInDialogue || choiceIndex >= len(d.Choices) {
		return
	}

	choice := d.Choices[choiceIndex]
	dialogueDef := s.dialogueDatabase[d.CurrentDialogue]

	// Execute consequences
	for _, consequence := range choice.Consequences {
		s.executeAction(world, consequence, d.SpeakerEntity)
	}

	// Change reputation
	// Would update faction reputation

	if choice.EndsConversation || choice.NextNode == "" {
		s.EndDialogue(world, playerEntity)
		return
	}

	d.CurrentNode = choice.NextNode
	if nextNode, ok := dialogueDef.Nodes[choice.NextNode]; ok {
		s.processNode(world, d, nextNode)
	}
}

func (s *DialogueSystem) advanceDialogue(world *ecs.World, playerEntity ecs.EntityID, d *components.DialogueComponent) {
	dialogueDef := s.dialogueDatabase[d.CurrentDialogue]
	node := dialogueDef.Nodes[d.CurrentNode]

	if len(node.Choices) == 0 && node.NextNodeID != "" {
		d.CurrentNode = node.NextNodeID
		if nextNode, ok := dialogueDef.Nodes[node.NextNodeID]; ok {
			s.processNode(world, d, nextNode)
		}
	}
}

func (s *DialogueSystem) EndDialogue(world *ecs.World, playerEntity ecs.EntityID) {
	playerDialogue, ok := world.GetComponent(playerEntity, components.CompDialogue)
	if !ok {
		return
	}

	d := playerDialogue.(*components.DialogueComponent)
	d.IsInDialogue = false
	d.CurrentDialogue = ""
	d.CurrentNode = ""
	d.Choices = nil
	d.SubtitleQueue = nil
	d.SpeakerEntity = 0
}

func (s *DialogueSystem) executeAction(world *ecs.World, action string, speaker ecs.EntityID) {
	// Parse and execute lua-like actions
	// e.g., "give_item(player, weapon_pistol)", "start_mission(mission_01)"
	// For now, just log
	fmt.Printf("Executing action: %s\n", action)
}

type DialogueDefinition struct {
	ID       string
	StartNode string
	Nodes    map[string]*DialogueNode
}

type DialogueNode struct {
	ID           string
	SpeakerID    string
	SpeakerName  string
	Text         string
	AudioClip    string
	Duration     float32
	Choices      []*DialogueChoice
	NextNodeID   string
	Conditions   []string
	Actions      []string
	CameraShot   *CameraShot
	Subtitle     components.SubtitleData
	Animations   []components.AnimationCue
}

type DialogueChoice struct {
	ID             string
	Text           string
	NextNodeID     string
	Conditions     []string
	Consequences   []string
	ReputationChange int32
	EndsConversation bool
}

type CameraShot struct {
	Type            CameraShotType
	TargetEntity    ecs.EntityID
	PositionOffset  components.Vector3
	LookAtOffset    components.Vector3
	FOV             float32
	Duration        float32
	ShakeIntensity  float32
}

type CameraShotType int

const (
	CameraShotStatic CameraShotType = iota
	CameraShotTrack
	CameraShotOrbit
	CameraShotCrane
	CameraShotHandheld
	CameraShotDrone
	CameraShotFirstPerson
	CameraShotOverShoulder
	CameraShotCloseUp
	CameraShotWide
	CameraShotDutch
)

// AI System
type AISystem struct {
	Priority_ int
}

func NewAISystem() *AISystem {
	return &AISystem{Priority_: 60}
}

func (s *AISystem) Priority() int { return s.Priority_ }

func (s *AISystem) Update(world *ecs.World, dt float32) {
	query := world.Query(components.CompAI, components.CompNPC, components.CompTransform)
	query.Iterate(func(entity ecs.EntityID) {
		ai, _ := world.GetComponent(entity, components.CompAI)
		npc, _ := world.GetComponent(entity, components.CompNPC)
		transform, _ := world.GetComponent(entity, components.CompTransform)

		a := ai.(*components.AIComponent)
		n := npc.(*components.NPCComponent)
		t := transform.(*components.TransformComponent)

		// Simple behavior tree execution
		s.executeBehavior(world, entity, a, n, t, dt)
	})
}

func (s *AISystem) executeBehavior(world *ecs.World, entity ecs.EntityID, a *components.AIComponent, n *components.NPCComponent, t *components.TransformComponent, dt float32) {
	switch a.AlertLevel {
	case components.AIAlertRelaxed:
		s.behaviorRelaxed(world, entity, a, n, t, dt)
	case components.AIAlertSuspicious:
		s.behaviorSuspicious(world, entity, a, n, t, dt)
	case components.AIAlertInvestigating:
		s.behaviorInvestigating(world, entity, a, n, t, dt)
	case components.AIAlertCombat:
		s.behaviorCombat(world, entity, a, n, t, dt)
	case components.AIAlertFleeing:
		s.behaviorFleeing(world, entity, a, n, t, dt)
	}
}

func (s *AISystem) behaviorRelaxed(world *ecs.World, entity ecs.EntityID, a *components.AIComponent, n *components.NPCComponent, t *components.TransformComponent, dt float32) {
	// Check for threats
	s.scanForThreats(world, entity, a, n, t)

	// Continue patrol/wander
	if len(n.PatrolPoints) > 0 {
		// Patrol logic handled by movement system
	}
}

func (s *AISystem) behaviorSuspicious(world *ecs.World, entity ecs.EntityID, a *components.AIComponent, n *components.NPCComponent, t *components.TransformComponent, dt float32) {
	// Look toward last known threat position
	if a.LastTargetPos != (components.Vector3{}) {
		dir := a.LastTargetPos.Sub(t.Position)
		if dir.LengthSq() > 0.001 {
			yaw := float32(math.Atan2(float64(dir.X), float64(dir.Z)))
			t.Rotation = components.QuaternionFromEuler(0, yaw, 0)
		}
	}

	// Timer to return to relaxed
	// Would track in blackboard
}

func (s *AISystem) behaviorInvestigating(world *ecs.World, entity ecs.EntityID, a *components.AIComponent, n *components.NPCComponent, t *components.TransformComponent, dt float32) {
	// Move to investigation position
	if a.LastTargetPos != (components.Vector3{}) {
		dist := t.Position.Distance(a.LastTargetPos)
		if dist < 2.0 {
			// Arrived, look around
			a.AlertLevel = components.AIAlertSuspicious
		}
	}
}

func (s *AISystem) behaviorCombat(world *ecs.World, entity ecs.EntityID, a *components.AIComponent, n *components.NPCComponent, t *components.TransformComponent, dt float32) {
	if n.TargetEntity == 0 {
		a.AlertLevel = components.AIAlertSuspicious
		return
	}

	// Check if target still valid
	targetHealth, ok := world.GetComponent(n.TargetEntity, components.CompHealth)
	if !ok {
		n.TargetEntity = 0
		a.AlertLevel = components.AIAlertSuspicious
		return
	}

	h := targetHealth.(*components.HealthComponent)
	if h.IsDead {
		n.TargetEntity = 0
		a.AlertLevel = components.AIAlertSuspicious
		return
	}

	// Target logic handled by movement and weapon systems
}

func (s *AISystem) behaviorFleeing(world *ecs.World, entity ecs.EntityID, a *components.AIComponent, n *components.NPCComponent, t *components.TransformComponent, dt float32) {
	// Flee logic handled by movement system
	// Check if safe
	if n.TargetEntity != 0 {
		targetTransform, ok := world.GetComponent(n.TargetEntity, components.CompTransform)
		if ok {
			targetT := targetTransform.(*components.TransformComponent)
			dist := t.Position.Distance(targetT.Position)
			if dist > 50.0 {
				n.IsFleeing = false
				a.AlertLevel = components.AIAlertSuspicious
			}
		}
	}
}

func (s *AISystem) scanForThreats(world *ecs.World, entity ecs.EntityID, a *components.AIComponent, n *components.NPCComponent, t *components.TransformComponent) {
	// Scan for players/enemies in range
	query := world.Query(components.CompPlayer, components.CompTransform, components.CompHealth)
	query.Iterate(func(other ecs.EntityID) {
		if other == entity {
			return
		}

		otherTransform, _ := world.GetComponent(other, components.CompTransform)
		otherHealth, _ := world.GetComponent(other, components.CompHealth)

		ot := otherTransform.(*components.TransformComponent)
		oh := otherHealth.(*components.HealthComponent)

		if oh.IsDead {
			return
		}

		dist := t.Position.Distance(ot.Position)
		if dist < 30.0 { // Detection range
			// Check relationship
			// If hostile, enter combat
			if s.isHostile(n.RelationshipGroup, other) {
				n.TargetEntity = other
				n.IsInCombat = true
				a.AlertLevel = components.AIAlertCombat
				a.LastTargetPos = ot.Position
			}
		}
	})
}

func (s *AISystem) isHostile(groupA, groupB int32) bool {
	// Simplified: different groups are hostile
	// In reality, would use relationship matrix
	return groupA != groupB
}