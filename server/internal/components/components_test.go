package components

import "testing"

// Every component that gets registered must report a distinct id, because
// World.AddComponent keys its storage pools by ComponentID(). Two components
// sharing an id would silently overwrite each other.
func TestComponentIDsAreUnique(t *testing.T) {
	type entry struct {
		id   uint32
		name string
	}

	all := []entry{
		{uint32(CompTransform), "TransformComponent"},
		{uint32(CompPhysics), "PhysicsComponent"},
		{uint32(CompHealth), "HealthComponent"},
		{uint32(CompPlayer), "PlayerComponent"},
		{uint32(CompVehicle), "VehicleComponent"},
		{uint32(CompWeapon), "WeaponComponent"},
		{uint32(CompNPC), "NPCComponent"},
		{uint32(CompInventory), "InventoryComponent"},
		{uint32(CompMission), "MissionComponent"},
		{uint32(CompCharacterAppearance), "CharacterAppearanceComponent"},
		{uint32(CompVehicleMods), "VehicleModifications"},
		{uint32(CompWeaponAttachments), "WeaponAttachments"},
		{uint32(CompWeaponCondition), "WeaponCondition"},
		{uint32(CompAudio), "AudioComponent"},
		{uint32(CompAI), "AIComponent"},
		{uint32(CompDialogue), "DialogueComponent"},
		{uint32(CompCutscene), "CutsceneComponent"},
		{uint32(CompPhone), "PhoneComponent"},
		{uint32(CompStats), "StatsComponent"},
	}

	seen := make(map[uint32]string, len(all))
	for _, e := range all {
		if e.id == 0 {
			t.Errorf("%s has the reserved zero component id", e.name)
		}
		if prev, dup := seen[e.id]; dup {
			t.Errorf("%s and %s share component id %d", e.name, prev, e.id)
		}
		seen[e.id] = e.name
	}
}

// The id a component reports from its method must match the constant the rest
// of the codebase looks it up by.
func TestComponentsReportTheirDeclaredID(t *testing.T) {
	checks := []struct {
		got  uint32
		want uint32
		name string
	}{
		{uint32(TransformComponent{}.ComponentID()), uint32(CompTransform), "TransformComponent"},
		{uint32(PhysicsComponent{}.ComponentID()), uint32(CompPhysics), "PhysicsComponent"},
		{uint32(HealthComponent{}.ComponentID()), uint32(CompHealth), "HealthComponent"},
		{uint32(PlayerComponent{}.ComponentID()), uint32(CompPlayer), "PlayerComponent"},
		{uint32(VehicleComponent{}.ComponentID()), uint32(CompVehicle), "VehicleComponent"},
		{uint32(WeaponComponent{}.ComponentID()), uint32(CompWeapon), "WeaponComponent"},
		{uint32(NPCComponent{}.ComponentID()), uint32(CompNPC), "NPCComponent"},
		{uint32(InventoryComponent{}.ComponentID()), uint32(CompInventory), "InventoryComponent"},
		{uint32(MissionComponent{}.ComponentID()), uint32(CompMission), "MissionComponent"},
		{uint32(VehicleModifications{}.ComponentID()), uint32(CompVehicleMods), "VehicleModifications"},
		{uint32(WeaponAttachments{}.ComponentID()), uint32(CompWeaponAttachments), "WeaponAttachments"},
		{uint32(WeaponCondition{}.ComponentID()), uint32(CompWeaponCondition), "WeaponCondition"},
		{uint32(AudioComponent{}.ComponentID()), uint32(CompAudio), "AudioComponent"},
		{uint32(AIComponent{}.ComponentID()), uint32(CompAI), "AIComponent"},
		{uint32(DialogueComponent{}.ComponentID()), uint32(CompDialogue), "DialogueComponent"},
		{uint32(CutsceneComponent{}.ComponentID()), uint32(CompCutscene), "CutsceneComponent"},
		{uint32(PhoneComponent{}.ComponentID()), uint32(CompPhone), "PhoneComponent"},
		{uint32(StatsComponent{}.ComponentID()), uint32(CompStats), "StatsComponent"},
	}

	for _, c := range checks {
		if c.got != c.want {
			t.Errorf("%s reports id %d, but is looked up as %d", c.name, c.got, c.want)
		}
	}
}
