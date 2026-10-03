#!/usr/bin/env python3
"""
Blender Batch Conversion Script for GTA5 Vehicle Mods
Converts YFT/YTD to FBX + Textures using GIMS EVO addon

Usage:
    blender --background --python blender_batch_convert.py -- \
        --source F:/gta5_mods_work/extracted \
        --output G:/megame/client/Assets/Imported/Vehicles \
        --log G:/megame/tools/conversion_log.txt

Requirements:
    - Blender 3.6+ or 4.0+
    - GIMS EVO addon installed (https://github.com/GIMS-EVO/GIMS-EVO)
"""

import bpy
import os
import sys
import argparse
import traceback
import json
from pathlib import Path
from typing import List, Dict, Tuple, Optional

# ============================================================================
# CONFIGURATION
# ============================================================================

class ConversionConfig:
    def __init__(self):
        self.source_dir = ""
        self.output_dir = ""
        self.log_file = ""
        self.process_yft = True
        self.process_ytd = True
        self.generate_lods = True
        self.generate_colliders = True
        self.apply_modifiers = True
        self.clean_mesh = True
        self.export_fbx = True
        self.export_gltf = False  # Alternative format
        self.texture_format = 'PNG'  # PNG, TGA, DDS
        self.fbx_version = 'BIN7400'  # FBX 7.4 binary
        self.axis_forward = '-Z'
        self.axis_up = 'Y'
        self.scale = 1.0
        # GIMS EVO specific
        self.gims_import_drawables = True
        self.gims_import_collisions = True
        self.gims_import_lods = True
        self.gims_separate_lods = False
        self.gims_import_textures = True
        self.gims_texture_format = 'PNG'

config = ConversionConfig()

# ============================================================================
# LOGGING
# ============================================================================

class Logger:
    def __init__(self, log_file: str):
        self.log_file = log_file
        self.entries = []
    
    def log(self, level: str, message: str):
        entry = f"[{level}] {message}"
        self.entries.append(entry)
        print(entry)
        if self.log_file:
            with open(self.log_file, 'a', encoding='utf-8') as f:
                f.write(entry + '\n')
    
    def info(self, msg): self.log("INFO", msg)
    def warning(self, msg): self.log("WARN", msg)
    def error(self, msg): self.log("ERROR", msg)
    def debug(self, msg): self.log("DEBUG", msg)
    
    def save_summary(self, summary: Dict):
        if self.log_file:
            with open(self.log_file.replace('.txt', '_summary.json'), 'w', encoding='utf-8') as f:
                json.dump(summary, f, indent=2, ensure_ascii=False)

logger = Logger("")

# ============================================================================
# BLENDER UTILITIES
# ============================================================================

def clear_scene():
    """Remove all objects from scene"""
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)
    
    # Clean orphaned data
    for block in bpy.data.meshes:
        if block.users == 0:
            bpy.data.meshes.remove(block)
    for block in bpy.data.materials:
        if block.users == 0:
            bpy.data.materials.remove(block)
    for block in bpy.data.textures:
        if block.users == 0:
            bpy.data.textures.remove(block)
    for block in bpy.data.images:
        if block.users == 0:
            bpy.data.images.remove(block)

def setup_gims_import_options():
    """Configure GIMS EVO import preferences"""
    try:
        prefs = bpy.context.preferences.addons.get('gims_evo')
        if not prefs:
            logger.warning("GIMS EVO addon not found! Install from https://github.com/GIMS-EVO/GIMS-EVO")
            return False
        
        prefs = prefs.preferences
        prefs.import_drawables = config.gims_import_drawables
        prefs.import_collisions = config.gims_import_collisions
        prefs.import_lods = config.gims_import_lods
        prefs.separate_lods = config.gims_separate_lods
        prefs.import_textures = config.gims_import_textures
        prefs.texture_format = config.gims_texture_format
        logger.debug("GIMS EVO import options configured")
        return True
    except Exception as e:
        logger.error(f"Failed to configure GIMS EVO: {e}")
        return False

def setup_gims_export_options():
    """Configure GIMS EVO export preferences"""
    try:
        prefs = bpy.context.preferences.addons.get('gims_evo')
        if not prefs:
            return False
        prefs = prefs.preferences
        prefs.export_lods = config.generate_lods
        prefs.export_collisions = config.generate_colliders
        return True
    except:
        return False

# ============================================================================
# YFT IMPORT
# ============================================================================

def import_yft(filepath: str, model_name: str) -> List[bpy.types.Object]:
    """Import YFT file using GIMS EVO"""
    logger.info(f"Importing YFT: {filepath}")
    
    try:
        # GIMS EVO import operator
        bpy.ops.import_scene.gta5_yft(
            filepath=filepath,
            import_drawables=config.gims_import_drawables,
            import_collisions=config.gims_import_collisions,
            import_lods=config.gims_import_lods,
            separate_lods=config.gims_separate_lods,
            import_textures=config.gims_import_textures,
        )
        
        # Get imported objects
        imported = [obj for obj in bpy.context.selected_objects]
        
        # Rename root object
        for obj in imported:
            if obj.parent is None:
                obj.name = model_name
                break
        
        logger.info(f"  Imported {len(imported)} objects")
        return imported
        
    except Exception as e:
        logger.error(f"Failed to import YFT: {e}")
        traceback.print_exc()
        return []

def import_ytd_textures(ytd_path: str, output_tex_dir: Path) -> List[str]:
    """Extract textures from YTD"""
    logger.info(f"Extracting textures from YTD: {ytd_path}")
    
    try:
        # GIMS EVO can import YTD textures
        bpy.ops.import_scene.gta5_ytd(filepath=ytd_path)
        
        # Save textures to output directory
        output_tex_dir.mkdir(parents=True, exist_ok=True)
        saved_textures = []
        
        for img in bpy.data.images:
            if img.source == 'FILE' and img.filepath:
                # Copy to output directory
                import shutil
                src = bpy.path.abspath(img.filepath)
                dst = output_tex_dir / Path(img.name).with_suffix(f'.{config.texture_format.lower()}')
                shutil.copy2(src, dst)
                saved_textures.append(str(dst))
                logger.debug(f"  Saved texture: {dst.name}")
        
        logger.info(f"  Extracted {len(saved_textures)} textures")
        return saved_textures
        
    except Exception as e:
        logger.error(f"Failed to import YTD: {e}")
        return []

# ============================================================================
# MESH PROCESSING
# ============================================================================

def clean_mesh(obj: bpy.types.Object):
    """Clean up mesh: remove doubles, fix normals, etc."""
    if obj.type != 'MESH':
        return
    
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    
    # Remove duplicate vertices
    bpy.ops.mesh.remove_doubles(threshold=0.0001)
    
    # Recalculate normals
    bpy.ops.mesh.normals_make_consistent(inside=False)
    
    # Convert tris to quads where possible
    bpy.ops.mesh.tris_convert_to_quads()
    
    bpy.ops.object.mode_set(mode='OBJECT')

def apply_modifiers_safe(obj: bpy.types.Object):
    """Apply all modifiers on object"""
    if obj.type != 'MESH':
        return
    
    bpy.context.view_layer.objects.active = obj
    for mod in obj.modifiers:
        try:
            bpy.ops.object.modifier_apply(modifier=mod.name)
        except:
            logger.warning(f"Could not apply modifier {mod.name} on {obj.name}")

def separate_lods(objects: List[bpy.types.Object]) -> Dict[str, List[bpy.types.Object]]:
    """Separate objects by LOD level based on naming"""
    lods = {'lod0': [], 'lod1': [], 'lod2': [], 'lod3': [], 'hi': [], 'collision': [], 'other': []}
    
    for obj in objects:
        name_lower = obj.name.lower()
        if '_hi' in name_lower or '.hi' in name_lower:
            lods['hi'].append(obj)
        elif '_lod0' in name_lower or '.lod0' in name_lower:
            lods['lod0'].append(obj)
        elif '_lod1' in name_lower or '.lod1' in name_lower:
            lods['lod1'].append(obj)
        elif '_lod2' in name_lower or '.lod2' in name_lower:
            lods['lod2'].append(obj)
        elif '_lod3' in name_lower or '.lod3' in name_lower:
            lods['lod3'].append(obj)
        elif 'ucx' in name_lower or 'collision' in name_lower or 'col_' in name_lower:
            lods['collision'].append(obj)
        else:
            lods['lod0'].append(obj)  # Default to LOD0
    
    return lods

def setup_lod_groups(lods: Dict[str, List[bpy.types.Object]], model_name: str):
    """Create LOD Group component setup (for Unity)"""
    # In Blender, we organize as collections for export
    # Unity will use LODGroup component
    
    for lod_name, objects in lods.items():
        if not objects:
            continue
        
        # Create collection
        coll_name = f"{model_name}_{lod_name}"
        coll = bpy.data.collections.get(coll_name) or bpy.data.collections.new(coll_name)
        bpy.context.scene.collection.children.link(coll)
        
        for obj in objects:
            # Move to collection
            for old_coll in obj.users_collection:
                old_coll.objects.unlink(obj)
            coll.objects.link(obj)
            
            # Set custom property for Unity LOD level
            obj["unity_lod_level"] = lod_name

def process_collisions(objects: List[bpy.types.Object], model_name: str):
    """Process collision meshes (UCX_)"""
    collision_objects = [o for o in objects if 'ucx' in o.name.lower() or 'col_' in o.name.lower()]
    
    if not collision_objects:
        return
    
    # Create collision collection
    coll = bpy.data.collections.get(f"{model_name}_collisions") or \
           bpy.data.collections.new(f"{model_name}_collisions")
    bpy.context.scene.collection.children.link(coll)
    
    for obj in collision_objects:
        # Mark as collision
        obj["unity_collision"] = True
        obj["unity_layer"] = "VehicleCollision"
        
        # Move to collision collection
        for old_coll in obj.users_collection:
            old_coll.objects.unlink(obj)
        coll.objects.link(obj)
        
        # Set collision mesh properties
        if obj.type == 'MESH':
            obj.display_type = 'WIRE'
            # Ensure convex for PhysX
            obj["unity_convex"] = True

# ============================================================================
# MATERIAL SETUP
# ============================================================================

def setup_vehicle_materials(model_name: str):
    """Setup standard vehicle materials for Unity HDRP/URP"""
    
    # Standard vehicle material templates
    materials = {
        'body': {'metallic': 0.8, 'smoothness': 0.9, 'color': (0.8, 0.8, 0.8)},
        'glass': {'metallic': 0.0, 'smoothness': 1.0, 'color': (0.2, 0.2, 0.3), 'transmission': 0.9},
        'lights': {'emission': (1.0, 0.9, 0.8), 'emission_strength': 2.0},
        'interior': {'metallic': 0.1, 'smoothness': 0.3, 'color': (0.1, 0.1, 0.1)},
        'tire': {'metallic': 0.0, 'smoothness': 0.1, 'color': (0.05, 0.05, 0.05)},
        'rim': {'metallic': 0.9, 'smoothness': 0.7, 'color': (0.6, 0.6, 0.6)},
        'chrome': {'metallic': 1.0, 'smoothness': 1.0, 'color': (0.9, 0.9, 0.9)},
        'plastic': {'metallic': 0.0, 'smoothness': 0.2, 'color': (0.2, 0.2, 0.2)},
    }
    
    created = {}
    for name, props in materials.items():
        mat = bpy.data.materials.get(f"{model_name}_{name}") or \
              bpy.data.materials.new(name=f"{model_name}_{name}")
        mat.use_nodes = True
        nodes = mat.node_tree.nodes
        links = mat.node_tree.links
        
        # Clear existing nodes
        nodes.clear()
        
        # Create Principled BSDF
        bsdf = nodes.new('ShaderNodeBsdfPrincipled')
        bsdf.location = (0, 0)
        bsdf.inputs['Metallic'].default_value = props.get('metallic', 0.5)
        bsdf.inputs['Roughness'].default_value = 1.0 - props.get('smoothness', 0.5)
        bsdf.inputs['Base Color'].default_value = (*props.get('color', (0.5, 0.5, 0.5)), 1.0)
        
        if 'transmission' in props:
            bsdf.inputs['Transmission Weight'].default_value = props['transmission']
            bsdf.inputs['Alpha'].default_value = props['transmission']
            mat.blend_method = 'BLEND'
            mat.shadow_method = 'HASHED'
        
        if 'emission' in props:
            bsdf.inputs['Emission Color'].default_value = (*props['emission'], 1.0)
            bsdf.inputs['Emission Strength'].default_value = props.get('emission_strength', 1.0)
        
        # Output
        output = nodes.new('ShaderNodeOutputMaterial')
        output.location = (300, 0)
        links.new(bsdf.outputs['BSDF'], output.inputs['Surface'])
        
        created[name] = mat
    
    return created

def assign_materials_by_name(objects: List[bpy.types.Object], materials: Dict[str, bpy.types.Material]):
    """Auto-assign materials based on object name patterns"""
    patterns = {
        'body': ['body', 'chassis', 'shell', 'frame'],
        'glass': ['glass', 'window', 'windshield', 'windscreen'],
        'lights': ['light', 'headlight', 'taillight', 'indicator', 'brake_light', 'reverse_light'],
        'interior': ['interior', 'dashboard', 'seat', 'steering', 'pedal', 'console'],
        'tire': ['tire', 'tyre', 'wheel_tire'],
        'rim': ['rim', 'wheel_rim', 'wheel_', 'hubcap', 'hub'],
        'chrome': ['chrome', 'grille', 'exhaust', 'trim', 'badge', 'emblem'],
        'plastic': ['plastic', 'bumper', 'mirror', 'handle', 'trim_', 'skirt'],
    }
    
    for obj in objects:
        if obj.type != 'MESH':
            continue
        
        name_lower = obj.name.lower()
        assigned = False
        
        for mat_type, keywords in patterns.items():
            for kw in keywords:
                if kw in name_lower:
                    if obj.data.materials:
                        obj.data.materials[0] = materials[mat_type]
                    else:
                        obj.data.materials.append(materials[mat_type])
                    assigned = True
                    break
            if assigned:
                break
        
        if not assigned:
            # Default to body
            if obj.data.materials:
                obj.data.materials[0] = materials['body']
            else:
                obj.data.materials.append(materials['body'])

# ============================================================================
# FBX EXPORT
# ============================================================================

def export_fbx(output_path: Path, model_name: str, selected_only: bool = True):
    """Export to FBX with Unity-compatible settings"""
    logger.info(f"Exporting FBX: {output_path}")
    
    output_path.parent.mkdir(parents=True, exist_ok=True)
    
    try:
        bpy.ops.export_scene.fbx(
            filepath=str(output_path),
            use_selection=selected_only,
            apply_unit_scale=True,
            apply_scale_options='FBX_SCALE_ALL',
            global_scale=config.scale,
            axis_forward=config.axis_forward,
            axis_up=config.axis_up,
            object_types={'MESH', 'ARMATURE', 'EMPTY'},
            use_mesh_modifiers=config.apply_modifiers,
            use_mesh_modifiers_render=config.apply_modifiers,
            mesh_smooth_type='FACE',
            use_subsurf=False,
            use_triangles=False,
            use_tspace=True,
            use_custom_props=True,
            add_leaf_bones=False,
            primary_bone_axis='Y',
            secondary_bone_axis='X',
            armature_nodetype='NULL',
            bake_anim=False,
        )
        logger.info(f"  FBX exported successfully")
        return True
    except Exception as e:
        logger.error(f"FBX export failed: {e}")
        return False

# ============================================================================
# MAIN CONVERSION PIPELINE
# ============================================================================

def process_model_directory(model_dir: Path, output_base: Path, model_name: str) -> Dict:
    """Process a single model directory containing YFT/YTD files"""
    logger.info(f"\n{'='*60}")
    logger.info(f"Processing: {model_name}")
    logger.info(f"Source: {model_dir}")
    
    result = {
        'model_name': model_name,
        'source_dir': str(model_dir),
        'output_dir': str(output_base / model_name),
        'success': False,
        'fbx_path': '',
        'textures': [],
        'lod_counts': {},
        'errors': [],
    }
    
    output_model_dir = output_base / model_name
    output_model_dir.mkdir(parents=True, exist_ok=True)
    output_tex_dir = output_model_dir / "Textures"
    
    try:
        # Clear scene
        clear_scene()
        
        # Setup GIMS
        if not setup_gims_import_options():
            raise RuntimeError("GIMS EVO not configured")
        
        # Find YFT files
        yft_files = list(model_dir.rglob("*.yft")) + list(model_dir.rglob("*.YFT"))
        ytd_files = list(model_dir.rglob("*.ytd")) + list(model_dir.rglob("*.YTD"))
        
        if not yft_files:
            raise FileNotFoundError(f"No YFT files found in {model_dir}")
        
        logger.info(f"Found {len(yft_files)} YFT, {len(ytd_files)} YTD files")
        
        # Import primary YFT (usually the largest or _hi version)
        primary_yft = max(yft_files, key=lambda f: f.stat().st_size)
        imported_objects = import_yft(str(primary_yft), model_name)
        
        if not imported_objects:
            raise RuntimeError("No objects imported from YFT")
        
        # Import additional YFTs (LODs, collisions)
        all_objects = list(imported_objects)
        for yft in yft_files:
            if yft != primary_yft:
                more = import_yft(str(yft), f"{model_name}_{yft.stem}")
                all_objects.extend(more)
        
        # Import textures from YTD
        for ytd in ytd_files:
            textures = import_ytd_textures(str(ytd), output_tex_dir)
            result['textures'].extend(textures)
        
        # Separate by LOD
        lods = separate_lods(all_objects)
        for lod_name, objs in lods.items():
            result['lod_counts'][lod_name] = len(objs)
        
        logger.info(f"LOD distribution: {result['lod_counts']}")
        
        # Process collisions
        if config.generate_colliders:
            process_collisions(all_objects, model_name)
        
        # Setup materials
        materials = setup_vehicle_materials(model_name)
        assign_materials_by_name(all_objects, materials)
        
        # Clean meshes
        if config.clean_mesh:
            for obj in all_objects:
                clean_mesh(obj)
        
        # Apply modifiers
        if config.apply_modifiers:
            for obj in all_objects:
                apply_modifiers_safe(obj)
        
        # Setup LOD groups
        if config.generate_lods:
            separate_lods(all_objects)
            setup_lod_groups(lods, model_name)
        
        # Select all for export
        bpy.ops.object.select_all(action='DESELECT')
        for obj in all_objects:
            obj.select_set(True)
        
        # Export FBX
        fbx_path = output_model_dir / f"{model_name}.fbx"
        if export_fbx(fbx_path, model_name):
            result['fbx_path'] = str(fbx_path)
            result['success'] = True
        else:
            result['errors'].append("FBX export failed")
        
        # Save blend file for reference
        blend_path = output_model_dir / f"{model_name}.blend"
        bpy.ops.wm.save_as_mainfile(filepath=str(blend_path))
        logger.info(f"Saved Blender file: {blend_path}")
        
    except Exception as e:
        logger.error(f"Processing failed: {e}")
        traceback.print_exc()
        result['errors'].append(str(e))
    
    return result

def find_model_directories(source_dir: Path) -> List[Tuple[Path, str]]:
    """Find directories containing vehicle model files"""
    model_dirs = []
    
    for item in source_dir.rglob("*"):
        if item.is_dir():
            # Check for YFT/YTD files
            has_yft = any(item.rglob("*.yft")) or any(item.rglob("*.YFT"))
            has_ytd = any(item.rglob("*.ytd")) or any(item.rglob("*.YTD"))
            has_rpf = any(item.rglob("*.rpf")) or any(item.rglob("*.RPF"))
            
            if has_yft or has_ytd:
                # Use directory name as model name
                model_name = item.name.lower().replace(" ", "_").replace("-", "_")
                model_dirs.append((item, model_name))
            elif has_rpf:
                # RPF directory - will be extracted by pipeline
                model_name = item.name.lower().replace(" ", "_").replace("-", "_")
                model_dirs.append((item, model_name))
    
    # Deduplicate by model name
    seen = set()
    unique = []
    for dir_path, name in model_dirs:
        if name not in seen:
            seen.add(name)
            unique.append((dir_path, name))
    
    return unique

def main():
    parser = argparse.ArgumentParser(description="Blender GTA5 Vehicle Batch Converter")
    parser.add_argument("--source", required=True, help="Source directory with extracted mods")
    parser.add_argument("--output", required=True, help="Output directory for Unity assets")
    parser.add_argument("--log", default="", help="Log file path")
    parser.add_argument("--model", help="Process specific model only")
    parser.add_argument("--list-only", action="store_true", help="List models without converting")
    
    # Parse Blender's arguments (after --)
    argv = sys.argv
    if "--" in argv:
        argv = argv[argv.index("--") + 1:]
    else:
        argv = argv[1:]
    
    args = parser.parse_args(argv)
    
    global logger
    logger = Logger(args.log)
    
    source_dir = Path(args.source)
    output_dir = Path(args.output)
    
    if not source_dir.exists():
        logger.error(f"Source directory not found: {source_dir}")
        return 1
    
    logger.info(f"Source: {source_dir}")
    logger.info(f"Output: {output_dir}")
    
    # Find model directories
    model_dirs = find_model_directories(source_dir)
    logger.info(f"Found {len(model_dirs)} model directories")
    
    for dir_path, name in model_dirs:
        logger.info(f"  {name}: {dir_path}")
    
    if args.list_only:
        return 0
    
    # Filter specific model
    if args.model:
        model_dirs = [(d, n) for d, n in model_dirs if n == args.model.lower()]
        if not model_dirs:
            logger.error(f"Model not found: {args.model}")
            return 1
    
    # Process each model
    summary = {
        'total': len(model_dirs),
        'success': 0,
        'failed': 0,
        'models': [],
    }
    
    for model_dir, model_name in model_dirs:
        result = process_model_directory(model_dir, output_dir, model_name)
        summary['models'].append(result)
        
        if result['success']:
            summary['success'] += 1
        else:
            summary['failed'] += 1
    
    # Save summary
    logger.save_summary(summary)
    
    logger.info(f"\n{'='*60}")
    logger.info(f"CONVERSION COMPLETE")
    logger.info(f"Total: {summary['total']}, Success: {summary['success']}, Failed: {summary['failed']}")
    
    for m in summary['models']:
        status = "OK" if m['success'] else "FAIL"
        logger.info(f"  [{status}] {m['model_name']}")
        if m['errors']:
            for err in m['errors']:
                logger.info(f"    Error: {err}")
    
    return 0 if summary['failed'] == 0 else 1

if __name__ == "__main__":
    sys.exit(main())