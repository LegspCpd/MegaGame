# GTA5 Vehicle Mod Import Guide

## Overview
This guide explains how to import your GTA5 vehicle mods into the MegaGame Unity project using the automated pipeline.

---

## Mod Formats Supported

| Format | Description | Example |
|--------|-------------|---------|
| **DLC Pack (.rpf)** | Single archive with all assets | `bcsodominator/dlc.rpf`, `grotti296a/dlc.rpf` |
| **Loose Files** | Separate `.yft` (model), `.ytd` (texture), meta files | `fbi.yft`, `fbi.ytd`, `vehicles.meta` |
| **DLS Lighting** | Emergency lighting with XML configs | `police2_lspdcvpi08.xml` |

---

## Your Mods Analysis

Based on the 27 mods you provided, here's the classification:

### 🚓 Police / Emergency Vehicles
| Mod | Type | Format | Agency |
|-----|------|--------|--------|
| `42d5a1-BCSO Dominator` | Police Muscle | DLC Pack | BCSO (Blaine County) |
| `724db0-2008 CVPI - LSPD-LAPD Pack` | Police Sedan Pack | DLC Pack (Addon + Replace) | LSPD + LAPD |
| `ad5bdb-2016 Unmarked Dodge Charger` | Unmarked Police | Loose Files | FBI/Police |
| `b51351-BCSO 14 Buffalo` | Police SUV | DLC Pack | BCSO |
| `cff72f-etrongt` | Police SUV | DLC Pack | Unknown |
| `e5df5c-Police Buffalo (final)` | Police SUV | DLC Pack | Police |
| `f5939d-LAPD Utility 2.0A` | Police SUV | Loose Files | LAPD |
| `f8f9c2-Vapid Stanier 1992-1997` | Police Sedan | Loose Files | Police |
| `9dc21a-Buffalo Unmarked` | Unmarked Police | DLC Pack | Police |
| `7cac29-NOOSE PIA Pack` | Federal/Tactical | DLC Pack | NOOSE |
| `5e982e-Police Alamo` | Police SUV | DLC Pack | Police |
| `1d5e24-Prince County Sheriff Mini-Pack` | Sheriff Pack | ZIP | Prince County |

### 🚒 Fire / EMS
| Mod | Type | Format |
|-----|------|--------|
| `c3b4b5-2016 Ram - LSFD Pickup` | Fire Pickup | DLC Pack + DLS |

### 🏎️ Civilian / Super Cars
| Mod | Type | Format | Class |
|-----|------|--------|-------|
| `08dac6-(2.0) Ferrari 296 speciale a` | Super Car | DLC Pack | Super |
| `60cfb9-Koenigsegg Jesko Attack HAMMER` | Hyper Car | DLC Pack | Super |
| `d93972-Bugatti Centodieci 1.1` | Hyper Car | DLC Pack | Super |
| `2387cb-xiaomi su7 ultra production` | Electric Sedan | DLC Pack | Sports |
| `c1f722-2.0 Xiaomi Skynomad N90` | SUV | DLC Pack | SUV |
| `64f944-2012 Camaro ZL1 (Enhanced)` | Muscle Car | DLC Pack | Muscle |
| `575a53-2027 Mercedes-Benz S-Class` | Luxury Sedan | DLC Pack | Sedan |
| `6d1a24-s500c` | Luxury Coupe | ZIP | Sports |
| `06a415-2018 Charger - LSPD Unmarked` | Police Muscle | DLC Pack | Muscle |
| `173466-buffalo VI` | Sports Sedan | DLC Pack | Sports |
| `296dd3-2020 Unmarked FPIU` | Police SUV | DLC Pack | SUV |
| `2dfc59-LHP V1.2` | Police Pack | RAR | Various |

---

## Required Tools

### 1. CodeWalker (for RPF extraction)
- **Download**: https://github.com/dexyfex/CodeWalker/releases
- **Install**: Extract to `C:\Program Files\CodeWalker\`
- **CLI**: `CodeWalker.CLI.exe` must be accessible

### 2. GIMS EVO (for YFT/YTD → FBX conversion)
- **For Blender (Free)**: https://github.com/GIMS-EVO/GIMS-EVO
- **For 3ds Max**: Commercial plugin
- **Install**: Follow GIMS EVO installation guide

### 3. Python 3.8+
- **Required**: For running the import pipeline script

---

## Step-by-Step Import Process

### 1. Prepare Source Directory
```bash
# Your mods are already at:
F:\gta5_mods_work\
```

### 2. Run Import Pipeline (Dry Run First)
```bash
cd G:\megame\tools
python gta5_mod_import_pipeline.py \
    --source F:\gta5_mods_work \
    --unity G:\megame\client \
    --codewalker "C:\Program Files\CodeWalker\CodeWalker.CLI.exe" \
    --dry-run
```

This will list all detected mods without importing.

### 3. Run Actual Import
```bash
python gta5_mod_import_pipeline.py \
    --source F:\gta5_mods_work \
    --unity G:\megame\client \
    --codewalker "C:\Program Files\CodeWalker\CodeWalker.CLI.exe"
```

### 4. Convert Models (Manual Step - GIMS EVO)
The pipeline will extract RPF files and identify YFT/YTD files, but **actual conversion to FBX requires GIMS EVO**:

#### Using Blender + GIMS EVO:
1. Open Blender
2. Install GIMS EVO addon (Edit → Preferences → Add-ons → Install from zip)
3. File → Import → GTA V (.yft/.ytd)
4. Select model file (e.g., `fbi.yft`)
5. Ensure "Import Textures" is checked
6. File → Export → FBX (.fbx)
7. Save to `G:\megame\client\Assets\Imported\Vehicles\<model_name>\`

#### Batch Conversion Script (Blender Python):
```python
# blender_batch_convert.py
import bpy
import os

source_dir = r"F:\gta5_mods_work\extracted"
output_dir = r"G:\megame\client\Assets\Imported\Vehicles"

for root, dirs, files in os.walk(source_dir):
    for file in files:
        if file.endswith(".yft"):
            yft_path = os.path.join(root, file)
            model_name = os.path.splitext(file)[0]
            
            # Import
            bpy.ops.import_scene.gta5_yft(filepath=yft_path)
            
            # Export FBX
            fbx_path = os.path.join(output_dir, model_name, f"{model_name}.fbx")
            os.makedirs(os.path.dirname(fbx_path), exist_ok=True)
            bpy.ops.export_scene.fbx(filepath=fbx_path, use_selection=True)
            
            # Clean up
            bpy.ops.object.select_all(action='SELECT')
            bpy.ops.object.delete()
```

Run with: `blender --background --python blender_batch_convert.py`

### 5. Import Definitions to Unity
1. Open Unity project: `G:\megame\client`
2. Open **MegaGame → Tools → Vehicle Definition Importer**
3. Set JSON Folder: `Assets/Resources/VehicleDatabase`
4. Select Target Database: Create new or select existing `VehicleDatabase.asset`
5. Click **Import All JSON Definitions**

### 6. Setup Vehicle Prefabs
For each imported vehicle:
1. Drag FBX model into scene
2. Add `OptimizedVehicleController` component
3. Run the generated setup script (in `Assets/Imported/Vehicles/<model>/`)
4. Configure WheelColliders (positions, radii, suspension)
5. Create Prefab in `Assets/Imported/Vehicles/<model>/<model>.prefab`
6. Assign prefab to `VehicleDefinition.modelPrefab`

---

## Emergency Vehicle Special Setup

### Police Vehicles
```csharp
// Auto-configured by pipeline for police type
definition.vehicleType = VehicleType.Police;
definition.hasSiren = true;
definition.lightPattern = "lspd"; // or "bcso", "sheriff", etc.
definition.elsConfig = "police2_lspdcvpi08.xml"; // from DLS folder
```

### DLS Lighting Setup
1. Copy DLS XML files to `Assets/StreamingAssets/DLS/`
2. Add `DLSController` component to vehicle
3. Assign XML config in inspector

### Siren Audio
1. Place siren `.wav/.ogg` in `Assets/Imported/Audio/Sirens/`
2. Assign to `VehicleDefinition.sirenSound`

---

## Mod Kit Configuration

Create `VehicleModKit` assets for each vehicle class:

```csharp
// Example: Police Mod Kit
var policeKit = CreateInstance<VehicleModKit>();
policeKit.modKitName = "Police Standard";
policeKit.modKitId = 1;
policeKit.compatibleClasses = new[] { VehicleClass.Sedan, VehicleClass.SUV, VehicleClass.Muscle };

// Engine
policeKit.engineOptions = new[] {
    new ModOption { name = "Stock", modValue = 0 },
    new ModOption { name = "Level 1", modValue = 1 },
    new ModOption { name = "Level 2", modValue = 2 },
    new ModOption { name = "Level 3", modValue = 3 },
    new ModOption { name = "Level 4", modValue = 4 },
};
policeKit.turboOptions = new[] {
    new ModOption { name = "None", modValue = 0 },
    new ModOption { name = "Turbo", modValue = 1 },
};
// ... etc
```

---

## Pipeline Output Structure

```
G:\megame\client\
├── Assets/
│   ├── Imported/
│   │   ├── Vehicles/
│   │   │   ├── bcsodominator/
│   │   │   │   ├── bcsodominator.fbx
│   │   │   │   ├── bcsodominator.prefab
│   │   │   │   └── bcsodominatorSetup.cs
│   │   │   ├── grotti296a/
│   │   │   │   └── ...
│   │   └── Textures/
│   │       └── Vehicles/
│   │           ├── bcsodominator/
│   │           │   ├── bcsodominator_body.png
│   │           │   ├── bcsodominator_lights.png
│   │           │   └── ...
│   │           └── ...
│   ├── Resources/
│   │   ├── VehicleDatabase.asset          # Main database
│   │   └── VehicleDefinitions/            # Individual definitions
│   │       ├── bcsodominator.asset
│   │       ├── grotti296a.asset
│   │       └── ...
│   └── StreamingAssets/
│       └── DLS/
│           ├── police2_lspdcvpi08.xml
│           └── ...
```

---

## Testing Checklist

After import, verify each vehicle:

- [ ] Model loads without errors
- [ ] Textures applied correctly
- [ ] Colliders generated (UCX_ meshes)
- [ ] WheelColliders positioned correctly
- [ ] Center of mass reasonable
- [ ] Engine audio plays
- [ ] Horn works
- [ ] **Police**: Siren works, lights flash (DLS)
- [ ] **Police**: ELS patterns cycle correctly
- [ ] Mods apply visually (spoilers, wheels, paint)
- [ ] Physics feel correct (mass, torque, grip)
- [ ] First-person view model works (for player vehicles)
- [ ] LODs switch correctly
- [ ] Damage deformation works

---

## Troubleshooting

| Issue | Solution |
|-------|----------|
| `CodeWalker not found` | Install CodeWalker, verify CLI path |
| `YFT import fails` | Update GIMS EVO, check Blender version compatibility |
| `Textures missing` | Ensure YTD imported with textures enabled |
| `WheelColliders misaligned` | Adjust in prefab: match visual wheel positions |
| `Vehicle floats/sinks` | Check `centerOfMass` Y value (should be negative) |
| `Siren not playing` | Verify AudioClip assigned, AudioSource on vehicle |
| `DLS lights not working` | Ensure DLS plugin installed in Unity, XML path correct |

---

## Next Steps

1. **Run dry-run** to verify all mods detected
2. **Install CodeWalker + GIMS EVO**
3. **Run full import pipeline**
4. **Batch convert models** via Blender script
5. **Import definitions** in Unity
6. **Create prefabs** for each vehicle
7. **Test drive** each vehicle type
8. **Configure mod kits** for customization
9. **Set up traffic spawning** with new vehicles

---

## File Locations Reference

| File | Purpose |
|------|---------|
| `tools/gta5_mod_import_pipeline.py` | Main import pipeline |
| `client/Assets/Scripts/Data/VehicleDatabase.cs` | Runtime database |
| `client/Assets/Scripts/Editor/VehicleDefinitionImporter.cs` | Unity import tool |
| `client/Assets/Resources/VehicleDatabase/` | Pipeline JSON output |
| `client/Assets/Imported/Vehicles/` | FBX models + prefabs |
| `client/Assets/StreamingAssets/DLS/` | Emergency lighting configs |

---

*Generated for MegaGame GTA5 Mod Import Pipeline*