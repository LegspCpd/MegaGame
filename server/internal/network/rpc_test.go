package network

import (
	"testing"

	"github.com/megame/server/internal/proto/network"
)

// The client sends human-readable RPC names; the server has to resolve them to
// the generated enum. Every name the Unity client can send is covered here.
func TestRPCMethodFromName(t *testing.T) {
	cases := map[string]network.RPCMethod{
		"SpawnPlayer":           network.RPCMethod_RPC_SPAWN_PLAYER,
		"spawn_player":          network.RPCMethod_RPC_SPAWN_PLAYER,
		"RPC_SPAWN_PLAYER":      network.RPCMethod_RPC_SPAWN_PLAYER,
		"SpawnVehicle":          network.RPCMethod_RPC_SPAWN_VEHICLE,
		"FireWeapon":            network.RPCMethod_RPC_FIRE_WEAPON,
		"ReloadWeapon":          network.RPCMethod_RPC_RELOAD_WEAPON,
		"SwitchWeapon":          network.RPCMethod_RPC_SWITCH_WEAPON,
		"AttachWeaponAccessory": network.RPCMethod_RPC_ATTACH_WEAPON_ACCESSORY,
		"ProjectileHit":         network.RPCMethod_RPC_PROJECTILE_HIT,
		"GetCutscene":           network.RPCMethod_RPC_GET_CUTSCENE,
		"CutsceneFinished":      network.RPCMethod_RPC_CUTSCENE_FINISHED,
		"GetDialogueNode":       network.RPCMethod_RPC_GET_DIALOGUE_NODE,
		"SelectDialogueChoice":  network.RPCMethod_RPC_SELECT_DIALOGUE_CHOICE,
		"SendPhoneMessage":      network.RPCMethod_RPC_SEND_PHONE_MESSAGE,
		"TriggerMission":        network.RPCMethod_RPC_TRIGGER_MISSION,
		// Unknown and empty names must not resolve to a real method.
		"":         network.RPCMethod_RPC_UNSPECIFIED,
		"NotAMeth": network.RPCMethod_RPC_UNSPECIFIED,
	}

	for name, want := range cases {
		if got := rpcMethodFromName(name); got != want {
			t.Errorf("rpcMethodFromName(%q) = %v, want %v", name, got, want)
		}
	}
}
