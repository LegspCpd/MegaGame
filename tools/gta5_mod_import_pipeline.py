#!/usr/bin/env python3
"""
GTA5 Vehicle Mod Import Pipeline for Unity
Handles: DLC RPF packages, Loose files (yft/ytd), DLS lighting
Outputs: Unity-ready FBX + textures + ScriptableObject database
"""

import os
import sys
import json
import shutil
import subprocess
from pathlib import Path
from typing import Dict, List, Optional, Tuple
from dataclasses import dataclass, asdict
from enum import Enum

# ============================================================================
# DATA STRUCTURES
# ============================================================================

class VehicleClass(Enum):
    COMPACT = "compact"
    SEDAN = "sedan"
    SUV = "suv"
    COUPE = "coupe"
    MUSCLE = "muscle"
    SPORTS_CLASSIC = "sportsclassic"
    SPORTS = "sports"
    SUPER = "super"
    MOTORCYCLE = "motorcycle"
    OFFROAD = "offroad"
    INDUSTRIAL = "industrial"
    UTILITY = "utility"
    VANS = "vans"
    CYCLES = "cycles"
    BOATS = "boats"
    HELICOPTERS = "helicopters"
    PLANES = "planes"
    SERVICE = "service"
    EMERGENCY = "emergency"
    MILITARY = "military"
    COMMERCIAL = "commercial"
    TRAINS = "trains"

class VehicleType(Enum):
    CIVILIAN = "civilian"
    POLICE = "police"
    FIRE = "fire"
    EMS = "ems"
    SHERIFF = "sheriff"
    STATE_TROOPER = "state_trooper"
    FEDERAL = "federal"
    PARK_RANGER = "park_ranger"
    MILITARY = "military"
    TAXI = "taxi"
    COMMERCIAL = "commercial"

@dataclass
class VehicleSpecs:
    # Basic info
    model_name: str
    display_name: str
    manufacturer: str
    vehicle_class: VehicleClass
    vehicle_type: VehicleType
    
    # Dimensions
    length: float = 4.5
    width: float = 1.8
    height: float = 1.4
    wheelbase: float = 2.7
    track_width_front: float = 1.5
    track_width_rear: float = 1.5
    ground_clearance: float = 0.15
    
    # Physics
    mass: float = 1500.0
    drag_coefficient: float = 0.32
    center_of_mass: Tuple[float, float, float] = (0.0, -0.3, 0.0)
    inertia_multiplier: Tuple[float, float, float] = (1.0, 1.0, 1.0)
    
    # Engine
    max_speed: float = 200.0  # km/h
    max_rpm: float = 7000.0
    idle_rpm: float = 800.0
    max_torque: float = 400.0
    torque_curve: List[float] = None
    gear_ratios: List[float] = None
    final_drive_ratio: float = 3.7
    drive_bias_front: float = 0.0  # 0=RWD, 0.5=AWD, 1=FWD
    
    # Steering
    max_steer_angle: float = 35.0
    steer_speed: float = 180.0
    
    # Braking
    brake_force: float = 3000.0
    handbrake_force: float = 5000.0
    
    # Suspension
    suspension_force: float = 30000.0
    suspension_damper: float = 5000.0
    suspension_travel: float = 0.2
    spring_length: float = 0.3
    
    # Aero
    downforce_coefficient: float = 0.1
    
    # Wheels
    wheel_radius_front: float = 0.35
    wheel_radius_rear: float = 0.35
    wheel_width_front: float = 0.25
    wheel_width_rear: float = 0.25
    
    # Audio
    engine_audio_hash: str = "generic"
    horn_audio_hash: str = "generic"
    
    # Mods
    mod_kit_id: int = 0
    has_livery: bool = False
    
    # Emergency
    has_siren: bool = False
    siren_audio_hash: str = ""
    light_pattern: str = ""
    els_config: str = ""
    
    def __post_init__(self):
        if self.torque_curve is None:
            self.torque_curve = [0.3, 0.5, 0.8, 1.0, 0.9, 0.7, 0.5]
        if self.gear_ratios is None:
            self.gear_ratios = [3.5, 2.2, 1.5, 1.1, 0.9, 0.7]

@dataclass
class ModMetadata:
    source_file: str
    mod_type: str  # "dlc_pack", "loose_files", "replace"
    display_name: str
    author: str = ""
    version: str = "1.0"
    tags: List[str] = None
    dependencies: List[str] = None
    
    def __post_init__(self):
        if self.tags is None:
            self.tags = []
        if self.dependencies is None:
            self.dependencies = []

# ============================================================================
# RPF EXTRACTOR (requires CodeWalker or custom RPF parser)
# ============================================================================

class RPFExtractor:
    """Extract files from GTA5 RPF archives using CodeWalker CLI"""
    
    def __init__(self, codewalker_path: Optional[str] = None):
        self.codewalker_path = codewalker_path or self._find_codewalker()
    
    def _find_codewalker(self) -> Optional[str]:
        """Find CodeWalker installation"""
        paths = [
            r"C:\Program Files\CodeWalker\CodeWalker.CLI.exe",
            r"C:\Program Files (x86)\CodeWalker\CodeWalker.CLI.exe",
            os.path.expanduser(r"~\AppData\Local\CodeWalker\CodeWalker.CLI.exe"),
        ]
        for p in paths:
            if os.path.exists(p):
                return p
        return None
    
    def extract_rpf(self, rpf_path: str, output_dir: str, file_filter: Optional[List[str]] = None) -> bool:
        """Extract RPF using CodeWalker CLI"""
        if not self.codewalker_path:
            print("ERROR: CodeWalker not found. Please install CodeWalker from https://github.com/dexyfex/CodeWalker")
            return False
        
        os.makedirs(output_dir, exist_ok=True)
        
        # CodeWalker CLI command
        cmd = [
            self.codewalker_path,
            "export",
            rpf_path,
            output_dir,
        ]
        
        if file_filter:
            for f in file_filter:
                cmd.extend(["--filter", f])
        
        try:
            result = subprocess.run(cmd, capture_output=True, text=True, timeout=300)
            if result.returncode != 0:
                print(f"CodeWalker error: {result.stderr}")
                return False
            return True
        except subprocess.TimeoutExpired:
            print("CodeWalker timeout")
            return False
        except Exception as e:
            print(f"CodeWalker exception: {e}")
            return False

# ============================================================================
# YFT/YTD CONVERTER (using GIMS EVO or custom)
# ============================================================================

class YFTConverter:
    """Convert GTA5 YFT/YTD to FBX/PNG using GIMS EVO or standalone tools"""
    
    def __init__(self, gims_path: Optional[str] = None):
        self.gims_path = gims_path
    
    def convert_yft_to_fbx(self, yft_path: str, output_dir: str) -> bool:
        """Convert YFT to FBX - requires 3ds Max + GIMS EVO or Blender + GIMS"""
        # This is a placeholder - actual conversion needs:
        # Option 1: 3ds Max + GIMS EVO (paid)
        # Option 2: Blender + GIMS EVO (free, community)
        # Option 3: Custom YFT parser (complex)
        
        print(f"Converting {yft_path} -> FBX")
        print("NOTE: Requires GIMS EVO in 3ds Max or Blender")
        print("See: https://github.com/GIMS-EVO/GIMS-EVO")
        return False
    
    def convert_ytd_to_png(self, ytd_path: str, output_dir: str) -> bool:
        """Convert YTD to PNG textures"""
        # Can use Texture Toolkit or CodeWalker
        print(f"Converting {ytd_path} -> PNG")
        return False

# ============================================================================
# MOD CLASSIFIER
# ============================================================================

class ModClassifier:
    """Classify and parse GTA5 vehicle mods"""
    
    POLICE_KEYWORDS = [
        'police', 'sheriff', 'trooper', 'lspd', 'lapd', 'bcso', 'fbi', 'noose',
        'highway patrol', 'state patrol', 'park ranger', 'marshal', 'constable',
        'unmarked', 'undercover', 'detective', 'polic', 'cop'
    ]
    
    FIRE_KEYWORDS = [
        'fire', 'lsfd', 'fdny', 'fd', 'rescue', 'engine', 'ladder', 'tender',
        'pumper', 'ambulance', 'ems', 'medic', 'paramedic'
    ]
    
    EMERGENCY_KEYWORDS = POLICE_KEYWORDS + FIRE_KEYWORDS + [
        'emergency', 'response', 'command', 'supervisor', 'sergeant', 'lieutenant'
    ]
    
    def classify_mod(self, mod_path: Path) -> Tuple[VehicleType, Dict]:
        """Classify mod based on folder/file names and meta files"""
        path_str = str(mod_path).lower()
        
        # Check for emergency keywords
        for kw in self.POLICE_KEYWORDS:
            if kw in path_str:
                return VehicleType.POLICE, {"subtype": "police", "agency": self._extract_agency(path_str)}
        
        for kw in self.FIRE_KEYWORDS:
            if kw in path_str:
                return VehicleType.FIRE, {"subtype": "fire", "department": self._extract_department(path_str)}
        
        # Check meta files for vehicle class
        meta_info = self._parse_meta_files(mod_path)
        if meta_info.get('vehicle_class'):
            return VehicleType.CIVILIAN, meta_info
        
        return VehicleType.CIVILIAN, {}
    
    def _extract_agency(self, path_str: str) -> str:
        agencies = ['lspd', 'lapd', 'bcso', 'fbi', 'noose', 'sheriff', 'trooper', 'highway patrol']
        for agency in agencies:
            if agency in path_str:
                return agency.upper()
        return "POLICE"
    
    def _extract_department(self, path_str: str) -> str:
        depts = ['lsfd', 'fdny', 'lafd', 'nyfd']
        for dept in depts:
            if dept in path_str:
                return dept.upper()
        return "FIRE"
    
    def _parse_meta_files(self, mod_path: Path) -> Dict:
        """Parse vehicles.meta, carvariations.meta, handling.meta"""
        info = {}
        
        for meta_file in mod_path.rglob("*.meta"):
            try:
                content = meta_file.read_text(encoding='utf-8', errors='ignore')
                # Simple XML parsing for key values
                if 'vehicleClass' in content:
                    # Extract vehicle class
                    pass
                if 'FLAG_EMERGENCY' in content or 'FLAG_EMERGENCY_SERVICE' in content:
                    info['is_emergency'] = True
            except:
                pass
        
        return info

# ============================================================================
# UNITY IMPORTER
# ============================================================================

class UnityVehicleImporter:
    """Import processed vehicle assets into Unity project"""
    
    def __init__(self, unity_project_path: str):
        self.project_path = Path(unity_project_path)
        self.models_path = self.project_path / "Assets" / "Imported" / "Vehicles"
        self.textures_path = self.project_path / "Assets" / "Imported" / "Textures" / "Vehicles"
        self.database_path = self.project_path / "Assets" / "Resources" / "VehicleDatabase"
        
        self.models_path.mkdir(parents=True, exist_ok=True)
        self.textures_path.mkdir(parents=True, exist_ok=True)
        self.database_path.mkdir(parents=True, exist_ok=True)
    
    def import_vehicle(self, spec: VehicleSpecs, model_dir: Path, meta: ModMetadata) -> bool:
        """Import a vehicle into Unity"""
        vehicle_dir = self.models_path / spec.model_name
        vehicle_dir.mkdir(exist_ok=True)
        
        # 1. Copy FBX model
        fbx_files = list(model_dir.glob("*.fbx")) + list(model_dir.glob("*.FBX"))
        if fbx_files:
            for fbx in fbx_files:
                dst = vehicle_dir / fbx.name
                shutil.copy2(fbx, dst)
                print(f"  Copied model: {fbx.name}")
        
        # 2. Copy textures
        tex_dir = self.textures_path / spec.model_name
        tex_dir.mkdir(exist_ok=True)
        for tex_ext in ['.png', '.jpg', '.jpeg', '.tga', '.dds']:
            for tex in model_dir.glob(f"*{tex_ext}"):
                dst = tex_dir / tex.name
                shutil.copy2(tex, dst)
        
        # 3. Create VehicleDefinition ScriptableObject
        self._create_vehicle_definition(spec, meta)
        
        # 4. Generate Prefab setup script
        self._generate_prefab_setup(spec)
        
        return True
    
    def _create_vehicle_definition(self, spec: VehicleSpecs, meta: ModMetadata):
        """Create Unity ScriptableObject asset as JSON (for Addressables)"""
        data = {
            "modelName": spec.model_name,
            "displayName": spec.display_name,
            "manufacturer": spec.manufacturer,
            "vehicleClass": spec.vehicle_class.value,
            "vehicleType": spec.vehicle_type.value,
            "specs": asdict(spec),
            "metadata": asdict(meta),
            "prefabPath": f"Assets/Imported/Vehicles/{spec.model_name}/{spec.model_name}.prefab",
            "modelPath": f"Assets/Imported/Vehicles/{spec.model_name}/{spec.model_name}.fbx",
            "texturePath": f"Assets/Imported/Textures/Vehicles/{spec.model_name}/",
        }
        
        # Convert tuples to lists for JSON
        data["specs"]["center_of_mass"] = list(spec.center_of_mass)
        data["specs"]["inertia_multiplier"] = list(spec.inertia_multiplier)
        
        output_file = self.database_path / f"{spec.model_name}.json"
        with open(output_file, 'w', encoding='utf-8') as f:
            json.dump(data, f, indent=2, ensure_ascii=False)
        
        print(f"  Created definition: {output_file}")
    
    def _generate_prefab_setup(self, spec: VehicleSpecs):
        """Generate C# script to setup prefab automatically"""
        script_content = f'''// Auto-generated vehicle prefab setup for {spec.model_name}
using UnityEngine;

namespace Megame.Vehicles
{{
    public static class {spec.model_name.Replace("-", "_").Replace(" ", "_")}Setup
    {{
        public static GameObject CreatePrefab()
        {{
            var go = new GameObject("{spec.model_name}");
            
            // Add Rigidbody
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = {spec.mass}f;
            rb.centerOfMass = new Vector3({spec.center_of_mass[0]}f, {spec.center_of_mass[1]}f, {spec.center_of_mass[2]}f);
            
            // Add VehicleController
            var controller = go.AddComponent<OptimizedVehicleController>();
            
            // Configure from specs
            controller.MaxTorque = {spec.max_torque}f;
            controller.MaxRPM = {spec.max_rpm}f;
            controller.IdleRPM = {spec.idle_rpm}f;
            controller.GearRatios = new float[] {{ {", ".join(f"{r}f" for r in spec.gear_ratios)} }};
            controller.FinalDriveRatio = {spec.final_drive_ratio}f;
            controller.DriveBias = {spec.drive_bias_front}f;
            controller.MaxSteerAngle = {spec.max_steer_angle}f;
            controller.BrakeForce = {spec.brake_force}f;
            controller.HandbrakeForce = {spec.handbrake_force}f;
            controller.DownforceCoefficient = {spec.downforce_coefficient}f;
            controller.DragCoefficient = {spec.drag_coefficient}f;
            
            // Emergency
            controller.HasSiren = {str(spec.has_siren).lower()};
            
            return go;
        }}
    }}
}}
'''
        script_path = self.models_path / spec.model_name / f"{spec.model_name}Setup.cs"
        script_path.write_text(script_content, encoding='utf-8')
        print(f"  Generated setup script: {script_path}")

# ============================================================================
# MAIN PIPELINE
# ============================================================================

class GTA5ModImportPipeline:
    def __init__(self, mods_source_dir: str, unity_project_path: str):
        self.source_dir = Path(mods_source_dir)
        self.unity_importer = UnityVehicleImporter(unity_project_path)
        self.classifier = ModClassifier()
        self.rpf_extractor = RPFExtractor()
        
        # Working directories
        self.extract_dir = self.source_dir / "extracted"
        self.processed_dir = self.source_dir / "processed"
        self.extract_dir.mkdir(exist_ok=True)
        self.processed_dir.mkdir(exist_ok=True)
    
    def run(self):
        """Run the complete import pipeline"""
        print("=" * 60)
        print("GTA5 Vehicle Mod Import Pipeline")
        print("=" * 60)
        
        # Find all mod archives
        mod_archives = self._find_mod_archives()
        print(f"Found {len(mod_archives)} mod archives")
        
        for archive in mod_archives:
            try:
                self._process_mod(archive)
            except Exception as e:
                print(f"ERROR processing {archive.name}: {e}")
        
        print("\n" + "=" * 60)
        print("Pipeline complete!")
        print("=" * 60)
    
    def _find_mod_archives(self) -> List[Path]:
        """Find all mod archives in source directory"""
        archives = []
        for ext in ['.zip', '.rar', '.7z']:
            archives.extend(self.source_dir.glob(f"*{ext}"))
        return archives
    
    def _process_mod(self, archive_path: Path):
        """Process a single mod archive"""
        print(f"\nProcessing: {archive_path.name}")
        
        # Extract archive
        extract_dir = self.extract_dir / archive_path.stem
        if extract_dir.exists():
            shutil.rmtree(extract_dir)
        extract_dir.mkdir(parents=True)
        
        if archive_path.suffix.lower() == '.zip':
            shutil.unpack_archive(archive_path, extract_dir)
        elif archive_path.suffix.lower() in ['.rar', '.7z']:
            subprocess.run([
                "C:\\Program Files\\7-Zip\\7z.exe", "x", str(archive_path), 
                f"-o{extract_dir}", "-y"
            ], capture_output=True)
        
        # Find vehicle content
        vehicle_dirs = self._find_vehicle_dirs(extract_dir)
        
        for vdir in vehicle_dirs:
            self._process_vehicle_directory(vdir, archive_path)
    
    def _find_vehicle_dirs(self, root: Path) -> List[Path]:
        """Find directories containing vehicle files"""
        vehicle_dirs = []
        
        for item in root.rglob("*"):
            if item.is_dir():
                # Check for vehicle files
                has_yft = any(item.glob("*.yft"))
                has_ytd = any(item.glob("*.ytd"))
                has_rpf = any(item.glob("*.rpf"))
                has_meta = any(item.glob("*.meta"))
                
                if has_yft or has_ytd or has_rpf or has_meta:
                    vehicle_dirs.append(item)
        
        return vehicle_dirs
    
    def _process_vehicle_directory(self, vdir: Path, source_archive: Path):
        """Process a directory containing vehicle files"""
        # Classify
        vehicle_type, type_info = self.classifier.classify_mod(vdir)
        
        # Parse meta files for specs
        spec = self._create_vehicle_spec(vdir, vehicle_type, type_info)
        
        # Create metadata
        meta = ModMetadata(
            source_file=source_archive.name,
            mod_type=self._detect_mod_type(vdir),
            display_name=spec.display_name,
            tags=[vehicle_type.value]
        )
        
        # Handle RPF extraction if needed
        rpf_files = list(vdir.glob("*.rpf"))
        model_dir = vdir
        
        if rpf_files:
            print(f"  Found RPF pack: {rpf_files[0].name}")
            # Extract RPF
            rpf_output = self.processed_dir / spec.model_name / "rpf_extracted"
            if self.rpf_extractor.extract_rpf(str(rpf_files[0]), str(rpf_output)):
                model_dir = rpf_output
        
        # Convert YFT/YTD if needed (placeholder - needs GIMS EVO)
        self._convert_models(model_dir)
        
        # Import to Unity
        self.unity_importer.import_vehicle(spec, model_dir, meta)
    
    def _detect_mod_type(self, vdir: Path) -> str:
        if any(vdir.glob("*.rpf")):
            return "dlc_pack"
        elif any(vdir.glob("*.yft")):
            return "loose_files"
        return "unknown"
    
    def _create_vehicle_spec(self, vdir: Path, vehicle_type: VehicleType, type_info: Dict) -> VehicleSpecs:
        """Create VehicleSpecs from directory name and meta files"""
        # Use directory name as model name
        model_name = vdir.name.lower().replace(" ", "_").replace("-", "_")
        
        # Try to read display name from meta or use directory name
        display_name = vdir.name
        
        # Try to find manufacturer from folder structure
        manufacturer = "Unknown"
        parts = vdir.parts
        for part in reversed(parts):
            if part.lower() not in ['addon', 'replace', 'files', 'data', 'gta', 'update', 'x64', 'dlcpacks']:
                manufacturer = part
                break
        
        # Determine vehicle class from name
        vehicle_class = self._guess_vehicle_class(display_name)
        
        return VehicleSpecs(
            model_name=model_name,
            display_name=display_name,
            manufacturer=manufacturer,
            vehicle_class=vehicle_class,
            vehicle_type=vehicle_type,
            has_siren=(vehicle_type in [VehicleType.POLICE, VehicleType.FIRE, VehicleType.EMS]),
        )
    
    def _guess_vehicle_class(self, name: str) -> VehicleClass:
        name_lower = name.lower()
        
        class_keywords = {
            VehicleClass.SUPER: ['super', 'hyper', 'bugatti', 'koenigsegg', 'pagani', 'centodieci', 'jesko'],
            VehicleClass.SPORTS: ['sports', 'ferrari', 'lamborghini', 'mclaren', 'porsche', 'corvette', 'gt3', 'gt4'],
            VehicleClass.MUSCLE: ['muscle', 'charger', 'challenger', 'camaro', 'mustang', 'cuda', 'chevelle'],
            VehicleClass.SEDAN: ['sedan', 'charger', '300', 'gta', 'stanier', 'schafter', 'oracle'],
            VehicleClass.SUV: ['suv', 'x5', 'x6', 'q7', 'cayenne', 'urus', 'escalade', 'suburban', 'tahoe'],
            VehicleClass.COUPE: ['coupe', '2door', 'rc', 'f-type'],
            VehicleClass.OFFROAD: ['offroad', 'rebel', 'sandking', 'trophy', 'dune'],
            VehicleClass.TRUCK: ['truck', 'ram', 'silverado', 'f150', 'pickup'],
            VehicleClass.VAN: ['van', 'sprinter', 'transit', 'econoline'],
            VehicleClass.MOTORCYCLE: ['bike', 'motorcycle', 'bmx', 'scorcher'],
        }
        
        for vclass, keywords in class_keywords.items():
            for kw in keywords:
                if kw in name_lower:
                    return vclass
        
        return VehicleClass.SEDAN
    
    def _convert_models(self, model_dir: Path):
        """Convert YFT/YTD to Unity formats"""
        # This requires external tools (GIMS EVO, CodeWalker, etc.)
        # Placeholder for actual conversion
        yft_files = list(model_dir.glob("*.yft"))
        ytd_files = list(model_dir.glob("*.ytd"))
        
        if yft_files or ytd_files:
            print(f"  Found {len(yft_files)} YFT and {len(ytd_files)} YTD files")
            print("  NOTE: Convert using GIMS EVO in Blender/3ds Max")
            print("  See: https://github.com/GIMS-EVO/GIMS-EVO")


# ============================================================================
# CLI ENTRY POINT
# ============================================================================

def main():
    import argparse
    
    parser = argparse.ArgumentParser(description="GTA5 Vehicle Mod Import Pipeline")
    parser.add_argument("--source", required=True, help="Source directory with mod archives")
    parser.add_argument("--unity", required=True, help="Unity project path")
    parser.add_argument("--codewalker", help="Path to CodeWalker CLI")
    parser.add_argument("--dry-run", action="store_true", help="Only analyze, don't import")
    
    args = parser.parse_args()
    
    pipeline = GTA5ModImportPipeline(args.source, args.unity)
    
    if args.codewalker:
        pipeline.rpf_extractor.codewalker_path = args.codewalker
    
    if args.dry_run:
        print("DRY RUN MODE - Analyzing mods only")
        archives = pipeline._find_mod_archives()
        for arch in archives:
            print(f"  {arch.name}")
    else:
        pipeline.run()

if __name__ == "__main__":
    main()