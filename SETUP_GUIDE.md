# MegaGame - Complete Setup Guide

This guide walks you through setting up and running the MegaGame project with your GTA5 vehicle mods.

---

## 📋 Prerequisites

### Software Requirements
| Tool | Version | Purpose |
|------|---------|---------|
| **Unity** | 2022.3.20f1 LTS | Game engine |
| **Go** | 1.23+ | Server backend |
| **Protocol Buffers** | 25.1+ | Code generation |
| **CodeWalker** | Latest | RPF extraction |
| **GIMS EVO** | Latest (Blender 3.6+/4.0+) | YFT/YTD → FBX conversion |
| **Blender** | 3.6+ or 4.0+ | Model conversion |
| **Docker** | Latest | Container builds (optional) |

### Unity Packages (auto-installed via manifest.json)
- TextMeshPro 3.0.6
- Input System 1.7.0
- Cinemachine 2.9.7
- Netcode for GameObjects 1.5.2
- Addressables 1.23.19
- Burst 1.8.4
- Mathematics 1.2.6
- Jobs 0.70.0

---

## 🚀 Quick Start (5 Steps)

### 1. Clone & Generate Protobuf
```bash
cd G:\megame
git clone <your-repo> .
cd shared/proto
./build_proto.sh
```

### 2. Install CodeWalker & GIMS EVO
- **CodeWalker**: Download from https://github.com/dexyfex/CodeWalker/releases
  - Install to `C:\Program Files\CodeWalker\`
- **GIMS EVO for Blender**: Download from https://github.com/GIMS-EVO/GIMS-EVO
  - Install as Blender addon: Edit → Preferences → Add-ons → Install from zip

### 3. Import Your GTA5 Mods
```bash
# Your mods are at F:\Users\Windows-users\
# Run the import pipeline
cd G:\megame\tools
python gta5_mod_import_pipeline.py \
    --source F:\Users\Windows-users \
    --unity G:\megame\client \
    --codewalker "C:\Program Files\CodeWalker\CodeWalker.CLI.exe"
```

This will:
- Extract all archives
- Classify vehicles (Police/Fire/Civilian)
- Parse meta files for specs
- Extract RPF packages via CodeWalker
- Generate JSON definitions in `client/Assets/Resources/VehicleDatabase/`

### 4. Convert Models (Blender + GIMS EVO)
```bash
# Open Blender and run the batch conversion
blender --background --python G:\megame\tools\blender_batch_convert.py -- \
    --source G:\megame\client\Assets\Imported\Models\Extracted \
    --output G:\megame\client\Assets\Imported\Vehicles \
    --log G:\megame\tools\conversion_log.txt
```

Or manually in Blender:
1. Open Blender → File → Import → GTA V (.yft/.ytd)
2. Select model file (e.g., `fbi.yft`)
3. Enable "Import Textures", "Import Collisions", "Import LODs"
4. File → Export → FBX (.fbx)
5. Save to `G:\megame\client\Assets\Imported\Vehicles\<model_name>\`

### 5. Open Unity & Import
1. Open Unity Hub → Add project → Select `G:\megame\client`
2. Wait for package resolution
3. **MegaGame → Tools → Vehicle Definition Importer**
   - JSON Folder: `Assets/Resources/VehicleDatabase`
   - Click "Create New Database" → "Import All JSON Definitions"
4. **MegaGame → Tools → Vehicle Prefab Generator**
   - Models Root: `Assets/Imported/Vehicles`
   - Select Vehicle Database
   - Click "Generate All Prefabs"
5. **MegaGame → Tools → Test Scene Generator**
   - Assign Vehicle Database
   - Check all options
   - Click "Generate Test Scene"

---

## 🎮 Running the Game

### Development Mode
```bash
# Terminal 1: Start Go Server
cd G:\megame\server
go run ./cmd/server -port=50051 -tick-rate=60

# Terminal 2: Unity Editor
# Open scene: Assets/Scenes/TestScene.unity
# Press Play
```

### Production Build
```bash
# Build server
cd G:\megame\server
CGO_ENABLED=0 GOOS=linux GOARCH=amd64 go build -ldflags="-s -w" -o megame-server ./cmd/server

# Build Unity client
/Applications/Unity/Hub/Editor/2022.3.20f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -projectPath G:\megame\client \
  -executeMethod Megame.Client.BuildScript.BuildAll \
  -quit
```

### Docker
```bash
# Build images
docker build -t megame/server -f G:\megame\server\Dockerfile.optimized G:\megame\server
docker build -t megame/client -f G:\megame\client\Dockerfile.optimized G:\megame\client

# Run
docker-compose up -d
```

---

## 📁 Project Structure

```
megame/
├── server/                    # Go server
│   ├── cmd/server/main.go     # Entry point
│   ├── internal/
│   │   ├── ecs/               # Archetype ECS
│   │   ├── components/        # Game components
│   │   ├── systems/           # Game systems
│   │   ├── network/           # gRPC + Delta compression
│   │   ├── profiling/         # pprof + Prometheus
│   │   └── persistence/       # Save/Load
│   ├── Dockerfile.optimized   # 15MB Distroless
│   └── go.mod
├── client/                    # Unity client
│   ├── Assets/
│   │   ├── Scripts/
│   │   │   ├── Core/          # GameClient, Camera, Input, EntityManager
│   │   │   ├── Controllers/   # Player, Vehicle, Weapon
│   │   │   ├── Systems/       # Traffic, DLS, Police, Persistence
│   │   │   ├── UI/            # HUD, ModShop, Garage, Dialogue
│   │   │   ├── Systems/       # Mission, Dialogue, Phone, World
│   │   │   └── Editor/        # BuildScript, PrefabGenerator, TestSceneGenerator
│   │   ├── Resources/         # VehicleDatabase, ModKits
│   │   └── StreamingAssets/   # DLS XML configs
│   ├── Packages/manifest.json
│   ├── Dockerfile.optimized
│   └── ProjectSettings/
├── shared/proto/              # Protobuf contracts
│   ├── *.proto                # 5 proto files
│   └── build_proto.sh
├── tools/                     # Import & conversion tools
│   ├── gta5_mod_import_pipeline.py
│   └── blender_batch_convert.py
├── .github/workflows/         # CI/CD
├── docs/                      # Documentation
└── README.md
```

---

## 🔧 Your 27 GTA5 Mods - Ready to Import

### Police (12)
| Mod | Type | Format | Agency |
|-----|------|--------|--------|
| `42d5a1-BCSO Dominator` | Muscle | DLC Pack | BCSO |
| `724db0-2008 CVPI Pack` | Sedan Pack | DLC (Addon+Replace) | LSPD+LAPD |
| `ad5bdb-Unmarked Charger` | Sedan | Loose Files | FBI |
| `b51351-BCSO Buffalo` | SUV | DLC Pack | BCSO |
| `cff72f-etrongt` | SUV | DLC Pack | Police |
| `e5df5c-Police Buffalo` | SUV | DLC Pack | Police |
| `f5939d-LAPD Utility` | SUV | Loose Files | LAPD |
| `f8f9c2-Vapid Stanier` | Sedan | Loose Files | Police |
| `9dc21a-Unmarked Buffalo` | SUV | DLC Pack | Police |
| `7cac29-NOOSE PIA` | Tactical | DLC Pack | NOOSE |
| `5e982e-Police Alamo` | SUV | DLC Pack | Police |
| `1d5e24-Prince County Sheriff` | Pack | ZIP | Sheriff |

### Fire/EMS (1)
| Mod | Type | Format |
|-----|------|--------|
| `c3b4b5-LSFD Pickup` | Fire Truck | DLC + DLS XML |

### Civilian Super/Hyper (5)
| Mod | Type | Format | Class |
|-----|------|--------|-------|
| `08dac6-Ferrari 296` | Super | DLC Pack | Super |
| `60cfb9-Koenigsegg Jesko` | Hyper | DLC Pack | Super |
| `d93972-Bugatti Centodieci` | Hyper | DLC Pack | Super |
| `2387cb-Xiaomi SU7` | Electric Sedan | DLC Pack | Sports |
| `64f944-Camaro ZL1` | Muscle | DLC Pack | Muscle |

### Civilian Luxury/Sport (5)
| Mod | Type | Format | Class |
|-----|------|--------|-------|
| `575a53-Mercedes S-Class` | Luxury Sedan | DLC Pack | Sedan |
| `c1f722-Xiaomi Skynomad` | SUV | DLC Pack | SUV |
| `6d1a24-S500C` | Luxury Coupe | ZIP | Sports |
| `173466-Buffalo VI` | Sports Sedan | DLC Pack | Sports |
| `296dd3-Unmarked FPIU` | Police SUV | DLC Pack | SUV |

### Other (4)
| Mod | Type | Format |
|-----|------|--------|
| `06a415-2018 Charger LSPD` | Police Muscle | DLC |
| `2dfc59-LHP V1.2` | Police Pack | RAR |
| `eb5885-mmeerruullaa` | Unknown | ZIP |
| `2dfc59-LHP V1.2` | Police Pack | RAR |

---

## 🛠️ Development Workflow

### Adding New Vehicles
1. Drop mod archive in `tools/source_mods/`
2. Run import pipeline
3. Convert models in Blender
3. Import definitions in Unity
4. Generate prefab
4. Test in showroom

### Adding New Weapons
1. Add WeaponDefinition to `VehicleDatabase` (or create WeaponDatabase)
4. Create ViewModel with attachments
4. Add to ModShopUI categories

### Adding Missions
1. Create mission JSON in `shared/proto/gameplay.proto` format
2. Load via MissionManager
3. Add dialogue via DialogueManager

---

## 📊 Monitoring & Debugging

### Server Metrics (Prometheus)
- `http://localhost:9090/metrics`
- Tick duration, entity count, network I/O, GC stats

### pprof Profiling
```bash
# CPU Profile
go tool pprof http://localhost:9091/debug/pprof/profile

# Heap Profile
go tool pprof http://localhost:9091/debug/pprof/heap

# Goroutine dump
go tool pprof http://localhost:9091/debug/pprof/goroutine
```

### Unity Profiler
- Window → Analysis → Profiler
- Enable "Development Build" + "Autoconnect Profiler"
- Use ProfilerMarkers.cs markers for custom profiling

### Debug Keys (in Test Scene)
| Key | Action |
|-----|--------|
| F1 | Return to Main Menu |
| F2 | Spawn Random Test Vehicle |
| F3 | Toggle Time Scale (1x/0.1x) |
| F4 | Trigger Random Police Event |
| ESC | Pause/Resume |
| `[` `]` | Cycle DLS Pattern |
| L.Ctrl | Toggle Siren |
| L.Shift | Cycle Siren Tone |
| H | Horn |

---

## 🐳 CI/CD Pipeline

The GitHub Actions workflow (`.github/workflows/ci-optimized.yml`) handles:

1. **Protobuf Generation** - Hash-based caching
2. **Go Build & Test** - Race detector, coverage >60%
3. **Unity Build** - Linux & Windows, IL2CPP, LZ4HC compression
3. **Security Scan** - Trivy (container), Nancy (deps), gosec (code)
4. **Docker Build** - Multi-stage, BuildKit cache, GHCR push
5. **Release** - Auto-create GitHub Release with all artifacts

### Required Secrets
| Secret | Purpose |
|--------|---------|
| `UNITY_LICENSE` | Unity Personal/Pro license |
| `DOCKERHUB_USERNAME` / `DOCKERHUB_TOKEN` | Docker Hub (optional, uses GHCR) |

---

## 📚 Key Documentation

| File | Purpose |
|------|---------|
| `README.md` | Project overview |
| `OPTIMIZATIONS.md` | Performance optimization details |
| `GTA5_MOD_IMPORT_GUIDE.md` | Detailed mod import guide |
| `SETUP_GUIDE.md` | This file |
| `tools/blender_batch_convert.py` | Blender conversion script |

---

## ❓ Troubleshooting

| Issue | Solution |
|-------|----------|
| `CodeWalker not found` | Install CodeWalker, verify CLI path in pipeline |
| `YFT import fails` | Update GIMS EVO, check Blender version (3.6+/4.0+) |
| `Textures missing` | Ensure YTD imported with textures enabled in GIMS |
| `WheelColliders misaligned` | Adjust in prefab: match visual wheel positions |
| `Vehicle floats/sinks` | Check `centerOfMass` Y value (should be negative) |
| `Siren not playing` | Verify AudioClip assigned, AudioSource on vehicle |
| `DLS lights not working` | Ensure DLS plugin in Unity, XML path correct |
| `Garage spawn fails` | Check GarageSpawnPoint obstruction mask |

---

## 📞 Support

- Check `OPTIMIZATIONS.md` for performance tuning
- Check `GTA5_MOD_IMPORT_GUIDE.md` for detailed mod import
- Check Unity Console for errors
- Check server logs for gRPC errors

---

*Generated for MegaGame - GTA5-style Open World Game*
*Last updated: 2024*