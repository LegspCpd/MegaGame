package components

import (
	"github.com/megame/server/internal/ecs"
	"math"
)

// Component IDs - these must match across server and client
const (
	CompTransform ecs.ComponentID = iota + 1
	CompPlayer
	CompVehicle
	CompWeapon
	CompNPC
	CompHealth
	CompInventory
	CompMission
	CompCharacterAppearance
	CompVehicleMods
	CompWeaponAttachments
	CompWeaponCondition
	CompPhysics
	CompAudio
	CompAI
	CompDialogue
	CompCutscene
	CompPhone
	CompStats
)

// Vector3 represents a 3D vector
type Vector3 struct {
	X, Y, Z float32
}

func (v Vector3) Add(o Vector3) Vector3      { return Vector3{v.X + o.X, v.Y + o.Y, v.Z + o.Z} }
func (v Vector3) Sub(o Vector3) Vector3      { return Vector3{v.X - o.X, v.Y - o.Y, v.Z - o.Z} }
func (v Vector3) Mul(s float32) Vector3      { return Vector3{v.X * s, v.Y * s, v.Z * s} }
func (v Vector3) Div(s float32) Vector3      { return Vector3{v.X / s, v.Y / s, v.Z / s} }
func (v Vector3) Length() float32            { return float32(math.Sqrt(float64(v.X*v.X + v.Y*v.Y + v.Z*v.Z))) }
func (v Vector3) LengthSq() float32          { return v.X*v.X + v.Y*v.Y + v.Z*v.Z }
func (v Vector3) Normalized() Vector3        { l := v.Length(); if l == 0 { return Vector3{} }; return v.Div(l) }
func (v Vector3) Dot(o Vector3) float32      { return v.X*o.X + v.Y*o.Y + v.Z*o.Z }
func (v Vector3) Cross(o Vector3) Vector3    { return Vector3{v.Y*o.Z - v.Z*o.Y, v.Z*o.X - v.X*o.Z, v.X*o.Y - v.Y*o.X} }
func (v Vector3) Distance(o Vector3) float32 { return v.Sub(o).Length() }
func (v Vector3) Lerp(o Vector3, t float32) Vector3 { return v.Add(o.Sub(v).Mul(t)) }

// Quaternion represents a rotation
type Quaternion struct {
	X, Y, Z, W float32
}

func (q Quaternion) Mul(o Quaternion) Quaternion {
	return Quaternion{
		X: q.W*o.X + q.X*o.W + q.Y*o.Z - q.Z*o.Y,
		Y: q.W*o.Y - q.X*o.Z + q.Y*o.W + q.Z*o.X,
		Z: q.W*o.Z + q.X*o.Y - q.Y*o.X + q.Z*o.W,
		W: q.W*o.W - q.X*o.X - q.Y*o.Y - q.Z*o.Z,
	}
}

func (q Quaternion) Normalized() Quaternion {
	l := float32(math.Sqrt(float64(q.X*q.X + q.Y*q.Y + q.Z*q.Z + q.W*q.W)))
	if l == 0 {
		return Quaternion{0, 0, 0, 1}
	}
	return Quaternion{q.X / l, q.Y / l, q.Z / l, q.W / l}
}

func (q Quaternion) RotateVector(v Vector3) Vector3 {
	// q * v * q^-1
	uv := Quaternion{q.X, q.Y, q.Z, 0}.Mul(Quaternion{v.X, v.Y, v.Z, 0})
	uv = uv.Mul(Quaternion{-q.X, -q.Y, -q.Z, q.W})
	return Vector3{uv.X, uv.Y, uv.Z}
}

func QuaternionFromEuler(x, y, z float32) Quaternion {
	cx := float32(math.Cos(float64(x * 0.5)))
	sx := float32(math.Sin(float64(x * 0.5)))
	cy := float32(math.Cos(float64(y * 0.5)))
	sy := float32(math.Sin(float64(y * 0.5)))
	cz := float32(math.Cos(float64(z * 0.5)))
	sz := float32(math.Sin(float64(z * 0.5)))

	return Quaternion{
		X: sx*cy*cz - cx*sy*sz,
		Y: cx*sy*cz + sx*cy*sz,
		Z: cx*cy*sz - sx*sy*cz,
		W: cx*cy*cz + sx*sy*sz,
	}.Normalized()
}

func (q Quaternion) ToEuler() (x, y, z float32) {
	// Roll (x-axis rotation)
	sinr_cosp := 2 * (q.W*q.X + q.Y*q.Z)
	cosr_cosp := 1 - 2*(q.X*q.X+q.Y*q.Y)
	x = float32(math.Atan2(float64(sinr_cosp), float64(cosr_cosp)))

	// Pitch (y-axis rotation)
	sinp := 2 * (q.W*q.Y - q.Z*q.X)
	if float64(sinp) >= 1 {
		y = float32(math.Pi / 2)
	} else if float64(sinp) <= -1 {
		y = -float32(math.Pi / 2)
	} else {
		y = float32(math.Asin(float64(sinp)))
	}

	// Yaw (z-axis rotation)
	siny_cosp := 2 * (q.W*q.Z + q.X*q.Y)
	cosy_cosp := 1 - 2*(q.Y*q.Y+q.Z*q.Z)
	z = float32(math.Atan2(float64(siny_cosp), float64(cosy_cosp)))

	return
}

// TransformComponent holds position, rotation, scale
type TransformComponent struct {
	Position Vector3
	Rotation Quaternion
	Scale    Vector3
	PrevPos  Vector3
	PrevRot  Quaternion
}

func (TransformComponent) ComponentID() ecs.ComponentID { return CompTransform }

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

// PhysicsComponent holds physics state
type PhysicsComponent struct {
	Velocity        Vector3
	AngularVelocity Vector3
	Mass            float32
	Drag            float32
	AngularDrag     float32
	UseGravity      bool
	IsKinematic     bool
	Layer           int32
	Colliders       []Collider
}

func (PhysicsComponent) ComponentID() ecs.ComponentID { return CompPhysics }

type Collider struct {
	Type       ColliderType
	Center     Vector3
	Size       Vector3      // for box
	Radius     float32      // for sphere/capsule
	Height     float32      // for capsule
	Rotation   Quaternion
	IsTrigger  bool
	Material   string
}

type ColliderType int

const (
	ColliderBox ColliderType = iota
	ColliderSphere
	ColliderCapsule
	ColliderMesh
	ColliderWheel
)

// HealthComponent
type HealthComponent struct {
	CurrentHealth float32
	MaxHealth     float32
	Armor         float32
	MaxArmor      float32
	RegenRate     float32
	RegenDelay    float32
	LastDamageTime float32
	IsDead        bool
	Invulnerable  bool
}

func (HealthComponent) ComponentID() ecs.ComponentID { return CompHealth }

// PlayerComponent
type PlayerComponent struct {
	CharacterName string
	Money         uint32
	WantedLevel   uint8
	MaxWantedLevel uint8
	CameraMode    CameraMode
	CurrentWeapon ecs.EntityID
	CurrentVehicle ecs.EntityID
	// InventoryWeaponIDs and InventoryItemIDs mirror InventoryComponent for the
	// network snapshot, which serialises the player without a second lookup.
	InventoryWeaponIDs []ecs.EntityID
	InventoryItemIDs   []ecs.EntityID
	VehicleSeatIndex int32
	IsInVehicle   bool
	Stamina       float32
	MaxStamina    float32
	Experience    uint64
	Level         uint32
	SkillPoints   uint32
	Skills        map[string]uint32
	SafehouseID   string
	LastSaveTime  int64
	PlayTime      float64
}

func (PlayerComponent) ComponentID() ecs.ComponentID { return CompPlayer }

type CameraMode int

const (
	CameraThirdPerson CameraMode = iota
	CameraFirstPerson
	CameraVehicleThird
	CameraVehicleFirst
	CameraAim
)

// VehicleComponent
type VehicleComponent struct {
	ModelID       string
	DisplayName   string
	Manufacturer  string
	VehicleClass  string
	EngineHealth  float32
	BodyHealth    float32
	Fuel          float32
	MaxFuel       float32
	CurrentGear   int32
	RPM           float32
	SpeedKPH      float32
	EngineOn      bool
	LightsOn      bool
	SirenOn       bool
	Doors         []VehicleDoor
	Wheels        []VehicleWheel
	Occupants     []ecs.EntityID
	Mods          *VehicleModifications
	Handler       string // handling profile name
}

func (VehicleComponent) ComponentID() ecs.ComponentID { return CompVehicle }

type VehicleDoor struct {
	Index   int32
	Angle   float32
	Broken  bool
}

type VehicleWheel struct {
	Index           int32
	Rotation        float32
	SteerAngle      float32
	Burst           bool
	SuspensionComp  float32
	BrakeForce      float32
	ContactPoint    Vector3
	ContactNormal   Vector3
	Slip            float32
}

type VehicleModifications struct {
	EngineLevel      int32
	TurboLevel       int32
	TransmissionLevel int32
	SuspensionLevel  int32
	SuspensionHeight float32
	BrakesLevel      int32
	ArmorLevel       int32
	BodyKit          int32
	Spoiler          int32
	Hood             int32
	Roof             int32
	Grille           int32
	Exhaust          int32
	Skirt            int32
	Fender           int32
	WheelType        int32
	WheelVariant     int32
	TireSmokeR       int32
	TireSmokeG       int32
	TireSmokeB       int32
	BulletproofTires bool
	CustomTires      bool
	PrimaryColor     string
	SecondaryColor   string
	PearlescentColor string
	WheelColor       string
	PaintType        int32
	Livery           string
	WindowTint       int32
	XenonLights      bool
	XenonColor       int32
	NeonEnabled      bool
	NeonR            int32
	NeonG            int32
	NeonB            int32
	PlateText        string
	PlateStyle       int32
}

// WeaponComponent
type WeaponComponent struct {
	ModelID         string
	DisplayName     string
	Manufacturer    string
	WeaponClass     string
	AmmoInClip      int32
	AmmoReserve     int32
	MaxClipSize     int32
	MaxReserve      int32
	NextFireTime    float64
	Heat            float32
	IsReloading     bool
	ReloadProgress  float32
	Attachments     *WeaponAttachments
	Condition       *WeaponCondition
	Owner           ecs.EntityID
}

func (WeaponComponent) ComponentID() ecs.ComponentID { return CompWeapon }

type WeaponAttachments struct {
	Equipped map[string]string // slot_id -> attachment_item_id
}

type WeaponCondition struct {
	Durability  float32
	Dirt        float32
	Carbon      float32
	IsJammed    bool
	RoundsFired uint32
}

// NPCComponent
type NPCComponent struct {
	NPCType         string
	BehaviorTree    string
	TargetEntity    ecs.EntityID
	RelationshipGroup int32
	Weapons         []ecs.EntityID
	CurrentWeaponIndex int32
	IsFleeing       bool
	IsInCombat      bool
	HomePosition    Vector3
	WanderRadius    float32
	PatrolPoints    []Vector3
	CurrentPatrolIndex int32
}

func (NPCComponent) ComponentID() ecs.ComponentID { return CompNPC }

// InventoryComponent
type InventoryComponent struct {
	Weapons  []ecs.EntityID
	Items    map[string]int32 // item_id -> count
	MaxWeight float32
	CurrentWeight float32
}

func (InventoryComponent) ComponentID() ecs.ComponentID { return CompInventory }

// CharacterAppearanceComponent
type CharacterAppearanceComponent struct {
	BodyType      string
	HeadModel     string
	HairStyle     string
	HairColor     string
	SkinTone      string
	EyeColor      string
	Clothing      []ClothingItem
	Accessories   []AccessoryItem
	Tattoos       []TattooItem
}

func (CharacterAppearanceComponent) ComponentID() ecs.ComponentID { return CompCharacterAppearance }

type ClothingItem struct {
	Slot        ClothingSlot
	ItemID      string
	Variant     string
	Color       string // hex
	IsDirty     bool
	Wear        float32
}

type ClothingSlot int

const (
	ClothingHead ClothingSlot = iota
	ClothingFace
	ClothingTorso
	ClothingLegs
	ClothingFeet
	ClothingHands
	ClothingOuter
	ClothingUnder
	ClothingFullBody
)

type AccessoryItem struct {
	Slot   AccessorySlot
	ItemID string
	Color  string
}

type AccessorySlot int

const (
	AccessoryEar AccessorySlot = iota
	AccessoryNeck
	AccessoryWrist
	AccessoryFinger
	AccessoryBack
	AccessoryBelt
	AccessoryPocket
)

type TattooItem struct {
	TattooID   string
	Position   Vector3
	Scale      float32
	Rotation   float32
	Color      string
}

// MissionComponent
type MissionComponent struct {
	ActiveMission   string
	MissionProgress map[string]*MissionProgress
	CompletedMissions []string
	FailedMissions    []string
	AvailableMissions []string
}

func (MissionComponent) ComponentID() ecs.ComponentID { return CompMission }

type MissionProgress struct {
	MissionID     string
	Objectives    map[string]*ObjectiveProgress
	StartTime     int64
	CurrentPhase  int32
}

type ObjectiveProgress struct {
	ObjectiveID   string
	Status        ObjectiveStatus
	Current       int32
	Target        int32
	IsOptional    bool
	IsHidden      bool
}

type ObjectiveStatus int

const (
	ObjectivePending ObjectiveStatus = iota
	ObjectiveActive
	ObjectiveCompleted
	ObjectiveFailed
)

// DialogueComponent
type DialogueComponent struct {
	CurrentDialogue   string
	CurrentNode       string
	NodeHistory       []string
	IsInDialogue      bool
	SpeakerEntity     ecs.EntityID
	Choices           []DialogueChoice
	SubtitleQueue     []SubtitleEntry
}

func (DialogueComponent) ComponentID() ecs.ComponentID { return CompDialogue }

type DialogueChoice struct {
	ID          string
	Text        string
	NextNode    string
	Conditions  []string
	Consequences []string
	// EndsConversation is set when picking this choice closes the dialogue.
	EndsConversation bool
}

type SubtitleEntry struct {
	Text          string
	Speaker       string
	Duration      float32
	Position      SubtitlePosition
	Style         SubtitleStyle
}

type SubtitlePosition int

const (
	SubtitleBottom SubtitlePosition = iota
	SubtitleTop
	SubtitleCenter
	SubtitleSpeaker
)

type SubtitleStyle struct {
	FontSize      int32
	Color         string
	OutlineColor  string
	OutlineWidth  float32
	Background    bool
	BackgroundColor string
	ShowSpeaker   bool
}

// PhoneComponent
type PhoneComponent struct {
	Contacts    []PhoneContact
	Messages    []PhoneMessage
	Apps        map[string]PhoneApp
	Wallpaper   string
	Ringtone    string
}

func (PhoneComponent) ComponentID() ecs.ComponentID { return CompPhone }

type PhoneContact struct {
	ID       string
	Name     string
	Number   string
	Avatar   string
	Favorite bool
	Blocked  bool
	Type     ContactType
}

type ContactType int

const (
	ContactStory ContactType = iota
	ContactFriend
	ContactService
	ContactShop
	ContactEmergency
)

type PhoneMessage struct {
	ID           string
	ContactID    string
	Text         string
	FromPlayer   bool
	Timestamp    int64
	Read         bool
	Type         MessageType
	MediaURL     string
	ActionText   string
	ActionCallback string
}

type MessageType int

const (
	MessageText MessageType = iota
	MessageMissionInvite
	MessageMissionUpdate
	MessageSystem
	MessageMedia
	MessageEmail
)

type PhoneApp struct {
	ID          string
	Name        string
	Icon        string
	Version     string
	IsSystemApp bool
}

// StatsComponent
type StatsComponent struct {
	PlaytimeSeconds    uint64
	MissionsCompleted  uint32
	MissionsFailed     uint32
	Kills              uint32
	Deaths             uint32
	Headshots          uint32
	VehiclesDestroyed  uint32
	DistanceTraveled   float64
	MoneyEarned        uint64
	MoneySpent         uint64
	MaxWantedLevel     uint8
	TimeAtMaxWanted    float32
	Achievements       []string
	WeaponKills        map[string]uint32
	VehicleUsage       map[string]float32 // seconds driven
	LocationVisits     map[string]uint32
}

func (StatsComponent) ComponentID() ecs.ComponentID { return CompStats }

// AudioComponent
type AudioComponent struct {
	EmitterID     string
	ActiveSounds  map[string]ActiveSound
	Occlusion     float32
}

func (AudioComponent) ComponentID() ecs.ComponentID { return CompAudio }

type ActiveSound struct {
	Clip       string
	Volume     float32
	Pitch      float32
	Position   Vector3
	Is3D       bool
	Loop       bool
	StartTime  float64
	Duration   float32
}

// AIComponent
type AIComponent struct {
	BehaviorTree    string
	CurrentNode     string
	Blackboard      map[string]interface{}
	TargetEntity    ecs.EntityID
	LastTargetPos   Vector3
	AlertLevel      AIAlertLevel
	Memory          map[string]AIMemory
}

func (AIComponent) ComponentID() ecs.ComponentID { return CompAI }

type AIAlertLevel int

const (
	AIAlertRelaxed AIAlertLevel = iota
	AIAlertSuspicious
	AIAlertInvestigating
	AIAlertCombat
	AIAlertFleeing
)

type AIMemory struct {
	Fact         string
	Value        interface{}
	Confidence   float32
	Timestamp    float64
	ExpiresAt    float64
}

// RegisterAllComponents registers all game components with the registry
func RegisterAllComponents(reg *ecs.ComponentRegistry) {
	reg.Register("Transform", func() ecs.Component { return &TransformComponent{} })
	reg.Register("Physics", func() ecs.Component { return &PhysicsComponent{} })
	reg.Register("Health", func() ecs.Component { return &HealthComponent{} })
	reg.Register("Player", func() ecs.Component { return &PlayerComponent{} })
	reg.Register("Vehicle", func() ecs.Component { return &VehicleComponent{} })
	reg.Register("Weapon", func() ecs.Component { return &WeaponComponent{} })
	reg.Register("NPC", func() ecs.Component { return &NPCComponent{} })
	reg.Register("Inventory", func() ecs.Component { return &InventoryComponent{} })
	reg.Register("CharacterAppearance", func() ecs.Component { return &CharacterAppearanceComponent{} })
	reg.Register("VehicleMods", func() ecs.Component { return &VehicleModifications{} })
	reg.Register("WeaponAttachments", func() ecs.Component { return &WeaponAttachments{} })
	reg.Register("WeaponCondition", func() ecs.Component { return &WeaponCondition{} })
	reg.Register("Audio", func() ecs.Component { return &AudioComponent{} })
	reg.Register("AI", func() ecs.Component { return &AIComponent{} })
	reg.Register("Dialogue", func() ecs.Component { return &DialogueComponent{} })
	reg.Register("Cutscene", func() ecs.Component { return &CutsceneComponent{} })
	reg.Register("Phone", func() ecs.Component { return &PhoneComponent{} })
	reg.Register("Stats", func() ecs.Component { return &StatsComponent{} })
	reg.Register("Mission", func() ecs.Component { return &MissionComponent{} })
}

// CutsceneComponent
type CutsceneComponent struct {
	CutsceneID     string
	CurrentTime    float32
	Duration       float32
	IsPlaying      bool
	IsSkippable    bool
	Tracks         []CutsceneTrack
	OnComplete     string
}

func (CutsceneComponent) ComponentID() ecs.ComponentID { return CompCutscene }

type CutsceneTrack struct {
	Type       CutsceneTrackType
	TargetID   string
	Keyframes  []CutsceneKeyframe
}

type CutsceneTrackType int

const (
	TrackCamera CutsceneTrackType = iota
	TrackEntityTransform
	TrackEntityAnimation
	TrackAudio
	TrackVisualEffect
	TrackTimeOfDay
	TrackWeather
	TrackPostProcess
	TrackSubtitle
	TrackScript
)

type CutsceneKeyframe struct {
	Time   float32
	Value  interface{}
	Easing EasingType
}

type EasingType int

const (
	EasingLinear EasingType = iota
	EasingInQuad
	EasingOutQuad
	EasingInOutQuad
	EasingInCubic
	EasingOutCubic
	EasingInOutCubic
	EasingInQuart
	EasingOutQuart
	EasingInOutQuart
	EasingInSine
	EasingOutSine
	EasingInOutSine
	EasingInExpo
	EasingOutExpo
	EasingInOutExpo
	EasingInCirc
	EasingOutCirc
	EasingInOutCirc
	EasingInBack
	EasingOutBack
	EasingInOutBack
	EasingInElastic
	EasingOutElastic
	EasingInOutElastic
	EasingInBounce
	EasingOutBounce
	EasingInOutBounce
)

// Component identity for the components that carry plain data without their own
// ComponentID method. Registered in RegisterAllComponents above.
func (VehicleModifications) ComponentID() ecs.ComponentID { return CompVehicleMods }

func (WeaponAttachments) ComponentID() ecs.ComponentID { return CompWeaponAttachments }

func (WeaponCondition) ComponentID() ecs.ComponentID { return CompWeaponCondition }

// Vector2 is a 2-component vector, used for input axes.
type Vector2 struct {
	X float32
	Y float32
}

func (Vector2) Add(o Vector2) Vector2   { return Vector2{X: o.X, Y: o.Y} }
func (v Vector2) Sub(o Vector2) Vector2    { return Vector2{v.X - o.X, v.Y - o.Y} }
func (v Vector2) Mul(s float32) Vector2    { return Vector2{v.X * s, v.Y * s} }
func (v Vector2) LengthSq() float32        { return v.X*v.X + v.Y*v.Y }
func (v Vector2) Length() float32          { return float32(math.Sqrt(float64(v.LengthSq()))) }
func (v Vector2) Normalized() Vector2 {
	l := v.Length()
	if l == 0 {
		return Vector2{}
	}
	return Vector2{v.X / l, v.Y / l}
}

// SubtitleData is the dialogue subtitle payload attached to a dialogue node.
type SubtitleData struct {
	Text     string
	Speaker  string
	Duration float32
}

// AnimationCue describes a one-shot animation attached to a dialogue node.
type AnimationCue struct {
	Name      string
	StartTime float32
	Duration  float32
	Loop      bool
}
