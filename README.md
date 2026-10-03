# MegaGame - GTA-like Open World Game

A modern open-world game built with **Go (server)** + **Unity (client)**, featuring realistic weapons, vehicles, deep customization, and rich storytelling.

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                     Unity Client                            │
│  ┌──────────┐ ┌──────────┐ ┌──────────┐ ┌────────────────┐ │
│  │ Rendering│ │ Physics  │ │  Input   │ │     UI/HUD     │ │
│  └──────────┘ └──────────┘ └──────────┘ └────────────────┘ │
│         ▲           ▲           ▲              ▲            │
│         └───────────┼───────────┼──────────────┘            │
│                     ▼                                       │
│              gRPC Streaming                                 │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                      Go Server                              │
│  ┌──────────┐ ┌──────────┐ ┌──────────┐ ┌────────────────┐ │
│  │   ECS    │ │  Systems │ │  Network │ │  Persistence   │ │
│  │  World   │ │ Movement │ │  (gRPC)  │ │   (Saves)      │ │
│  └──────────┘ └──────────┘ └──────────┘ └────────────────┘ │
│  ┌──────────┐ ┌──────────┐ ┌──────────┐ ┌────────────────┐ │
│  │ Vehicle  │ │ Weapon   │ │   AI     │ │   Mission/     │ │
│  │ Physics  │ │ Ballistics│ │ Behavior │ │   Dialogue     │ │
│  └──────────┘ └──────────┘ └──────────┘ └────────────────┘ │
└─────────────────────────────────────────────────────────────┘
```

## Features

### Core Gameplay
- **Third-person & First-person** camera with seamless switching (V key)
- **Realistic vehicle physics** with engine, transmission, suspension, tire simulation
- **Detailed weapon ballistics** with recoil, spread, penetration, heat, jamming
- **Vehicle customization** (engine, turbo, suspension, brakes, body kits, wheels, paint, neon, plates)
- **Weapon attachments** (optics, muzzles, barrels, grips, magazines, stocks, ammo types)
- **Character customization** (clothing slots, accessories, tattoos, body types)

### Open World
- Large streaming world with regions (downtown, residential, industrial, wilderness, beach, airport, military)
- Dynamic traffic & pedestrian population with scenarios
- Day/night cycle with dynamic weather
- Enterable interiors (shops, safehouses, missions)
- Navigation meshes for pedestrians, vehicles, boats, aircraft

### Story & Missions
- **Main storyline** with branching paths
- **Side missions** (strangers, random events, races, assassinations, deliveries, heists)
- **Dialogue system** with choices, consequences, reputation
- **Full subtitles** with speaker names, positioning, styling
- **Cutscene system** with camera tracks, animations, audio, subtitles
- **Phone system** (contacts, messages, apps, missions)

### Technical
- **ECS architecture** (Entity Component System) for performance
- **gRPC bidirectional streaming** for real-time sync
- **Server-authoritative** with client-side prediction
- **Protobuf** for efficient serialization
- **Modular systems** (movement, vehicle, weapon, AI, mission, dialogue)
- **Save/Load system** with auto-save
- **Docker support** for deployment

## Requirements

### Development
- Go 1.23+
- Unity 2022.3 LTS
- protoc 25+
- Docker (optional)

### Runtime (Minimum)
- OS: Windows 10 64-bit / Ubuntu 22.04
- CPU: Intel i5-6600K / AMD Ryzen 5 1600
- RAM: 8 GB
- GPU: NVIDIA GTX 970 4GB / AMD RX 570 4GB
- Storage: 50 GB SSD

### Runtime (Recommended)
- OS: Windows 11 64-bit / Ubuntu 24.04
- CPU: Intel i7-12700K / AMD Ryzen 7 7700X
- RAM: 16 GB
- GPU: NVIDIA RTX 3070 8GB / AMD RX 6800 16GB
- Storage: 100 GB NVMe SSD

## Quick Start

### 1. Generate Protobuf

```bash
cd shared/proto
./build_proto.sh
```

### 2. Run Go Server

```bash
cd server
go run ./cmd/server -port=50051 -tick-rate=60
```

### 3. Open Unity Project

```bash
# Open client/ in Unity Hub
# Unity 2022.3.20f1 or later
```

### 4. Run in Unity Editor

1. Open scene `Assets/Scenes/Main.unity`
2. Press Play

### 5. Build Client

```bash
# From Unity Editor: MegaGame > Build > All
# Or command line:
/Applications/Unity/Hub/Editor/2022.3.20f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -projectPath client \
  -executeMethod Megame.Client.BuildScript.BuildAll \
  -quit
```

## Project Structure

```
megame/
├── server/                 # Go server
│   ├── cmd/server/         # Entry point
│   ├── internal/
│   │   ├── ecs/           # ECS framework
│   │   ├── components/    # Game components
│   │   ├── systems/       # Game systems
│   │   ├── network/       # gRPC server
│   │   └── persistence/   # Save/Load
│   ├── go.mod
│   └── Dockerfile
├── client/                 # Unity client
│   ├── Assets/
│   │   ├── Scripts/
│   │   │   ├── Core/      # Core systems (GameClient, EntityManager, Camera, Input)
│   │   │   ├── Controllers/ (Player, Vehicle, Weapon)
│   │   │   ├── UI/        # HUD, Subtitles, Dialogue, Phone
│   │   │   ├── Systems/   # Mission, Dialogue, Phone, Cutscene, World
│   │   │   └── Editor/    # Build scripts
│   │   └── Scenes/
│   ├── Packages/
│   ├── ProjectSettings/
│   └── Dockerfile
├── shared/
│   └── proto/             # Protobuf definitions
│       ├── common.proto
│       ├── entity.proto
│       ├── network.proto
│       ├── gameplay.proto
│       ├── world.proto
│       └── build_proto.sh
├── .github/workflows/     # CI/CD
└── docs/                  # Documentation
```

## Development

### Adding New Components

1. Define in `server/internal/components/components.go`
2. Register in `RegisterAllComponents()`
3. Add protobuf definition in `shared/proto/entity.proto`
4. Run `./build_proto.sh`
5. Create system in `server/internal/systems/`

### Adding New RPC Methods

1. Add method to `network.proto` (RPCMethod enum + request/response messages)
2. Run `./build_proto.sh`
3. Implement handler in `server/internal/network/grpc_server.go`
4. Call from Unity via `GameClient.SendRPCAsync()`

### Creating Missions

Missions are defined in JSON and loaded by `MissionSystem`:

```json
{
  "id": "mission_heist_01",
  "title": "The Big Score",
  "type": "heist",
  "prerequisites": ["mission_intro_05"],
  "rewardMoney": 250000,
  "objectives": [
    {"id": "goto_planning", "type": "goto", "targetPosition": [100, 50, 200], "targetRadius": 5},
    {"id": "steal_truck", "type": "steal", "targetVehicleId": "vehicle_armored_truck"},
    {"id": "deliver_truck", "type": "deliver", "targetPosition": [500, 30, -200], "targetRadius": 10}
  ]
}
```

### Creating Dialogues

```json
{
  "id": "dialogue_lester_01",
  "startNode": "intro",
  "nodes": {
    "intro": {
      "speakerName": "Lester",
      "text": "So, you want to make some real money?",
      "audioClip": "lester_intro",
      "choices": [
        {"id": "yes", "text": "I'm in.", "nextNodeId": "briefing"},
        {"id": "no", "text": "Not interested.", "nextNodeId": "refuse", "endsConversation": true}
      ]
    }
  }
}
```

## CI/CD Pipeline

The GitHub Actions workflow (`.github/workflows/ci.yml`) handles:

1. **Protobuf Generation** - Generates Go & C# code
2. **Go Build & Test** - Compiles server, runs tests, builds Linux/Windows binaries
3. **Unity Build** - Builds client for Linux & Windows (IL2CPP, stripped)
4. **Unity Tests** - Runs EditMode & PlayMode tests
5. **Docker Images** - Builds & pushes server/client images
6. **Release** - Creates GitHub Release with all artifacts

### Required Secrets

- `UNITY_LICENSE` - Unity Personal/Pro license
- `DOCKERHUB_USERNAME` / `DOCKERHUB_TOKEN` - For Docker images

## Asset Pipeline (GTA5/BNG Models)

### Importing Models

1. **Extract** from GTA5 using OpenIV/CodeWalker → FBX/OBJ
2. **Clean** in Blender:
   - Apply transforms, reset pivot
   - Create LODs (LOD0-LOD3)
   - Generate collision meshes (UCX_ prefix)
   - Separate first-person view model parts
3. **Import** to Unity:
   - Use `Assets/Imported/Models/`
   - Configure import settings (scale 1.0, generate colliders)
4. **Configure** in ScriptableObject databases:
   - `VehicleDatabase.asset` - specs, handling, seats, audio
   - `WeaponDatabase.asset` - ballistics, attachments, view models
   - `ClothingDatabase.asset` - slots, variants, textures

### First-Person View Models

GTA5/BNG models include first-person arms/weapon meshes. In Unity:
1. Separate `v_weapon_*` meshes from world model
2. Create ViewModel prefab with Animator
3. Set up attachment points (optic, muzzle, grip, mag)
4. Configure offsets in `WeaponDefinition.FirstPersonData`

## Configuration

### Server Config (config.yaml)

```yaml
server:
  port: 50051
  tickRate: 60
  maxPlayers: 32
  saveDir: "./saves"

world:
  name: "Liberty City"
  streamingDistance: 500
  populationDensity: 0.8

gameplay:
  wantedSystem: true
  fuelConsumption: true
  weaponDurability: true
  vehicleDamage: true
```

## Contributing

1. Fork the repository
2. Create feature branch (`git checkout -b feature/amazing-feature`)
3. Commit changes (`git commit -m 'Add amazing feature'`)
4. Push to branch (`git push origin feature/amazing-feature`)
5. Open Pull Request

## License

This project is licensed under the MIT License - see [LICENSE](LICENSE) for details.

## Acknowledgments

- [Defy](https://github.com/openfw-game/defy) - Open world framework inspiration
- [Veloren](https://github.com/veloren/veloren) - ECS architecture patterns
- [Xonotic](https://github.com/xonotic/xonotic) - FPS networking
- [OpenMW](https://github.com/OpenMW/openmw) - Open world RPG systems
- GTA5 modding community for asset references