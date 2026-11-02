"""
FBX Geometric Optimization Pipeline — Blender Python API
=========================================================
Usage (headless):
    blender --background --python Tools/optimize_fbx.py -- [optional --models cpu gpu ventilador_refrigeracion]

Run from the project root.  Produces:
  • <model_dir>/<name>_opt.fbx   — optimized export, original is untouched
  • Tools/optimization_report.json — machine-readable results
  • Tools/optimization_report.md  — human-readable comparison table

Reduction ratios per object category (tweak here):
    HIGH_DETAIL  = 0.50  (cpu, gpu, chip — intricate surface detail)
    MEDIUM_DETAIL = 0.40 (ram, targeta_red, disco_almacenamiento, puertos_conexion)
    LOW_DETAIL   = 0.30  (bateria_CMOS, condensador, resistencia, ventilador, SCAR-H)
"""

import bpy
import os
import sys
import json
import math
import argparse
import time
from pathlib import Path

# ---------------------------------------------------------------------------
# Configuration
# ---------------------------------------------------------------------------

# Project root = two levels up from this file (Tools/ lives in the root)
PROJECT_ROOT = Path(__file__).resolve().parent.parent
MODELS_ENV_DIR = PROJECT_ROOT / "Assets" / "_Project" / "Models" / "Environment"

# Reduction ratios: fraction of faces to KEEP (1.0 = no reduction)
RATIO_MAP = {
    "cpu":                      0.50,
    "gpu":                      0.50,
    "chip":                     0.50,
    "ram":                      0.40,
    "targeta_red":              0.40,
    "disco_almacenamiento":     0.40,
    "puertos_conexion":         0.40,
    "batería_CMOS":             0.30,
    "bateria_CMOS":             0.30,  # accent-free fallback
    "condensador":              0.30,
    "resistencia":              0.30,
    "ventilador_refrigeracion": 0.30,
    "SCAR-H":                   0.40,
    "PC":                       0.35,
    "Casillero":                0.35,
    "Mapa":                     0.30,
    "Mesa":                     0.40,
    "Armas":                    0.40,
}
DEFAULT_RATIO = 0.40

# Pilot models (run these three unless --models is supplied)
PILOT_MODELS = ["cpu", "gpu", "ventilador_refrigeracion"]

# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

def count_triangles(obj) -> int:
    """Return the triangle count for a mesh object (pre-modifier)."""
    if obj.type != "MESH":
        return 0
    mesh = obj.data
    return sum(len(p.vertices) - 2 for p in mesh.polygons)


def count_triangles_evaluated(obj, depsgraph) -> int:
    """Return triangle count with all modifiers applied (post-decimate)."""
    eval_obj = obj.evaluated_get(depsgraph)
    mesh = eval_obj.to_mesh()
    count = sum(len(p.vertices) - 2 for p in mesh.polygons)
    eval_obj.to_mesh_clear()
    return count


def clear_scene():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for block in list(bpy.data.meshes):
        bpy.data.meshes.remove(block)
    for block in list(bpy.data.materials):
        bpy.data.materials.remove(block)


def import_fbx(fbx_path: Path):
    bpy.ops.import_scene.fbx(
        filepath=str(fbx_path),
        use_custom_normals=True,
        use_image_search=True,
    )
    return [o for o in bpy.context.scene.objects if o.type == "MESH"]


def assign_texture_to_materials(mesh_objects: list, texture_path: Path):
    """Assign a texture JPG/PNG to the Base Color of all mesh materials."""
    if not texture_path or not texture_path.exists():
        return

    try:
        img = bpy.data.images.load(str(texture_path), check_existing=True)
    except Exception as e:
        print(f"  [tex] Could not load texture {texture_path}: {e}")
        return

    assigned = 0
    for obj in mesh_objects:
        for slot in obj.material_slots:
            mat = slot.material
            if mat is None:
                continue
            mat.use_nodes = True
            nodes = mat.node_tree.nodes
            links = mat.node_tree.links

            bsdf = nodes.get("Principled BSDF")
            if bsdf is None:
                bsdf = next((n for n in nodes if n.type == "BSDF_PRINCIPLED"), None)
            if bsdf is None:
                continue

            # Re-use existing Image Texture node or create one
            tex_node = next((n for n in nodes if n.type == "TEX_IMAGE"), None)
            if tex_node is None:
                tex_node = nodes.new("ShaderNodeTexImage")

            tex_node.image = img
            # Link only if not already linked to Base Color
            already_linked = any(
                lnk.to_node == bsdf and lnk.to_socket == bsdf.inputs["Base Color"]
                for lnk in links
            )
            if not already_linked:
                links.new(tex_node.outputs["Color"], bsdf.inputs["Base Color"])
            assigned += 1

    if assigned:
        print(f"  [tex] Assigned {texture_path.name} to {assigned} material slot(s)")


def find_texture(model_dir: Path, model_name: str) -> Path:
    """
    Return the best available texture file for a model, or None.
    Search order:
      1. Texture/<model_name>.jpg  (standard env models)
      2. Texture/<model_name>.png
      3. Texture/Color_*.png       (Inicio_modelos style)
      4. Any .jpg or .png in Texture/
    """
    tex_dir = model_dir / "Texture"
    if not tex_dir.exists():
        # Try the parent's Texture folder (for models inside Inicio_modelos/)
        tex_dir = model_dir.parent / "Texture"
    if not tex_dir.exists():
        return None

    candidates = [
        tex_dir / f"{model_name}.jpg",
        tex_dir / f"{model_name}.png",
    ]
    for c in candidates:
        if c.exists():
            return c

    # Look for Color_*.png (Tripo texture naming)
    color_pngs = sorted(tex_dir.glob("Color_*.png"))
    if color_pngs:
        return color_pngs[0]

    # Any image
    for ext in ("*.jpg", "*.png", "*.jpeg"):
        found = sorted(tex_dir.glob(ext))
        if found:
            return found[0]

    return None


def apply_decimate(obj, ratio: float):
    """Add a Decimate modifier set to COLLAPSE mode and apply it."""
    mod = obj.modifiers.new(name="Decimate_Opt", type="DECIMATE")
    mod.decimate_type = "COLLAPSE"
    mod.ratio = ratio
    mod.use_collapse_triangulate = True

    # Apply the modifier so the export sees the reduced mesh
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.modifier_apply(modifier=mod.name)


def export_fbx(output_path: Path):
    bpy.ops.export_scene.fbx(
        filepath=str(output_path),
        use_selection=False,
        apply_scale_options="FBX_SCALE_NONE",
        bake_space_transform=False,
        use_mesh_modifiers=True,
        mesh_smooth_type="FACE",
        use_tspace=True,
        use_custom_props=False,
        path_mode="COPY",
        embed_textures=True,      # embed textures so FBX carries color data
        axis_forward="-Z",
        axis_up="Y",
    )


# ---------------------------------------------------------------------------
# Per-model pipeline
# ---------------------------------------------------------------------------

# Models that live outside their own named subfolder.
# Key: model name passed via --models
# Value: {fbx_relative (from MODELS_ENV_DIR), out_relative, texture_search_dir_relative}
CUSTOM_PATHS = {
    "SCAR-H": {
        "fbx": Path("SCAR-H.fbx"),
        "out": Path("SCAR-H_opt.fbx"),
        "tex_dir": None,
    },
    "PC": {
        "fbx": Path("Inicio_modelos/PC.fbx"),
        "out": Path("Inicio_modelos/PC_opt.fbx"),
        "tex_dir": Path("Inicio_modelos/Texture"),
    },
    "Casillero": {
        "fbx": Path("Inicio_modelos/Casillero.fbx"),
        "out": Path("Inicio_modelos/Casillero_opt.fbx"),
        "tex_dir": Path("Inicio_modelos/Texture"),
    },
    "Mapa": {
        "fbx": Path("Inicio_modelos/Mapa.fbx"),
        "out": Path("Inicio_modelos/Mapa_opt.fbx"),
        "tex_dir": Path("Inicio_modelos/Texture"),
    },
    "Mesa": {
        "fbx": Path("Inicio_modelos/Mesa.fbx"),
        "out": Path("Inicio_modelos/Mesa_opt.fbx"),
        "tex_dir": Path("Inicio_modelos/Texture"),
    },
    "Armas": {
        "fbx": Path("Inicio_modelos/Armas.fbx"),
        "out": Path("Inicio_modelos/Armas_opt.fbx"),
        "tex_dir": Path("Inicio_modelos/Texture"),
    },
}


def process_model(model_name: str) -> dict:
    """Import, decimate, export one model. Returns a result dict."""
    # Resolve paths
    if model_name in CUSTOM_PATHS:
        cp = CUSTOM_PATHS[model_name]
        fbx_in  = MODELS_ENV_DIR / cp["fbx"]
        fbx_out = MODELS_ENV_DIR / cp["out"]
        model_dir = fbx_in.parent
        tex_override = (MODELS_ENV_DIR / cp["tex_dir"]) if cp["tex_dir"] else None
    else:
        model_dir = MODELS_ENV_DIR / model_name
        fbx_in    = model_dir / f"{model_name}.fbx"
        fbx_out   = model_dir / f"{model_name}_opt.fbx"
        tex_override = None

    if not fbx_in.exists():
        return {
            "model": model_name,
            "status": "ERROR",
            "error": f"Source not found: {fbx_in}",
        }

    ratio = RATIO_MAP.get(model_name, DEFAULT_RATIO)
    print(f"\n[optimize] {model_name}  ratio={ratio}  input={fbx_in}")

    clear_scene()
    t0 = time.time()

    mesh_objects = import_fbx(fbx_in)
    if not mesh_objects:
        return {"model": model_name, "status": "ERROR", "error": "No mesh objects imported"}

    # Find and assign texture so FBX carries color data when exported
    if tex_override:
        # For Inicio_modelos: tex_dir is already specified
        tex_path = tex_override / f"Color_{model_name}.png" if (tex_override / f"Color_{model_name}.png").exists() else None
        if not tex_path:
            color_pngs = sorted(tex_override.glob("Color_*.png"))
            tex_path = color_pngs[0] if color_pngs else None
    else:
        tex_path = find_texture(model_dir, model_name)

    assign_texture_to_materials(mesh_objects, tex_path)

    # Measure original triangle counts
    original_tris = {o.name: count_triangles(o) for o in mesh_objects}
    total_original = sum(original_tris.values())

    # Apply decimate to every mesh object
    for obj in mesh_objects:
        bpy.ops.object.select_all(action="DESELECT")
        apply_decimate(obj, ratio)

    # Measure optimized triangle counts
    optimized_tris = {o.name: count_triangles(o) for o in mesh_objects}
    total_optimized = sum(optimized_tris.values())

    export_fbx(fbx_out)
    elapsed = round(time.time() - t0, 2)

    reduction_pct = round((1 - total_optimized / max(total_original, 1)) * 100, 1)
    print(f"  {total_original:,} → {total_optimized:,} tris  ({reduction_pct}% reduction)  {elapsed}s")

    return {
        "model": model_name,
        "status": "OK",
        "source_fbx": str(fbx_in.relative_to(PROJECT_ROOT)),
        "output_fbx": str(fbx_out.relative_to(PROJECT_ROOT)),
        "decimate_ratio": ratio,
        "original_tris": total_original,
        "optimized_tris": total_optimized,
        "reduction_pct": reduction_pct,
        "elapsed_s": elapsed,
        "objects": [
            {
                "name": o.name,
                "original_tris": original_tris[o.name],
                "optimized_tris": optimized_tris[o.name],
            }
            for o in mesh_objects
        ],
    }


# ---------------------------------------------------------------------------
# Report generation
# ---------------------------------------------------------------------------

def write_reports(results: list, tools_dir: Path):
    json_path = tools_dir / "optimization_report.json"
    md_path   = tools_dir / "optimization_report.md"

    # JSON
    with open(json_path, "w", encoding="utf-8") as f:
        json.dump({"results": results, "generated": time.strftime("%Y-%m-%d %H:%M")}, f, indent=2, ensure_ascii=False)

    # Markdown table
    lines = [
        "# FBX Optimization Report",
        "",
        f"Generated: {time.strftime('%Y-%m-%d %H:%M')}",
        "",
        "## Summary",
        "",
        "| Model | Ratio | Original Tris | Optimized Tris | Reduction | Status |",
        "|-------|-------|--------------|----------------|-----------|--------|",
    ]
    for r in results:
        if r["status"] == "OK":
            lines.append(
                f"| {r['model']} | {r['decimate_ratio']} "
                f"| {r['original_tris']:,} | {r['optimized_tris']:,} "
                f"| {r['reduction_pct']}% | ✅ OK |"
            )
        else:
            lines.append(f"| {r['model']} | — | — | — | — | ❌ {r.get('error','')} |")

    ok = [r for r in results if r["status"] == "OK"]
    if ok:
        total_orig = sum(r["original_tris"] for r in ok)
        total_opt  = sum(r["optimized_tris"] for r in ok)
        lines += [
            "",
            f"**Total original triangles:** {total_orig:,}",
            f"**Total optimized triangles:** {total_opt:,}",
            f"**Overall reduction:** {round((1-total_opt/max(total_orig,1))*100,1)}%",
        ]

    lines += [
        "",
        "## Per-model Details",
        "",
    ]
    for r in results:
        lines.append(f"### {r['model']}")
        if r["status"] != "OK":
            lines.append(f"- Error: {r.get('error','')}")
        else:
            lines += [
                f"- Source: `{r['source_fbx']}`",
                f"- Output: `{r['output_fbx']}`",
                f"- Decimate ratio: {r['decimate_ratio']} (keep {int(r['decimate_ratio']*100)}% of faces)",
                f"- {r['original_tris']:,} → {r['optimized_tris']:,} triangles ({r['reduction_pct']}% reduction)",
                f"- Processing time: {r['elapsed_s']}s",
                "",
                "| Mesh Object | Original | Optimized |",
                "|-------------|----------|-----------|",
            ]
            for o in r.get("objects", []):
                lines.append(f"| {o['name']} | {o['original_tris']:,} | {o['optimized_tris']:,} |")
        lines.append("")

    lines += [
        "---",
        "",
        "## Validation Checklist (manual)",
        "",
        "For each optimized model, verify in Unity / Blender before substituting:",
        "",
        "- [ ] Visual silhouette preserved",
        "- [ ] UV seams / texture mapping intact",
        "- [ ] Material slots unchanged",
        "- [ ] Pivot / origin in same world position",
        "- [ ] Scale and dimensions match original",
        "- [ ] No flipped normals",
        "- [ ] Collider geometry unaffected (colliders are separate objects, not in FBX)",
        "",
        "**Do NOT replace scene references until all checklist items pass.**",
    ]

    with open(md_path, "w", encoding="utf-8") as f:
        f.write("\n".join(lines) + "\n")

    print(f"\n[report] JSON  → {json_path}")
    print(f"[report] Markdown → {md_path}")
    return json_path, md_path


# ---------------------------------------------------------------------------
# Entry point
# ---------------------------------------------------------------------------

def main():
    # Strip Blender's own args before the '--' separator
    argv = sys.argv
    if "--" in argv:
        argv = argv[argv.index("--") + 1:]
    else:
        argv = []

    parser = argparse.ArgumentParser(description="FBX optimization pipeline")
    parser.add_argument(
        "--models", nargs="+", default=None,
        help="Model names to process (default: pilot set)"
    )
    args = parser.parse_args(argv)

    models_to_process = args.models if args.models else PILOT_MODELS

    tools_dir = PROJECT_ROOT / "Tools"
    tools_dir.mkdir(exist_ok=True)

    print("=" * 60)
    print("FBX Optimization Pipeline")
    print(f"Project root : {PROJECT_ROOT}")
    print(f"Models dir   : {MODELS_ENV_DIR}")
    print(f"Processing   : {models_to_process}")
    print("=" * 60)

    results = []
    for name in models_to_process:
        result = process_model(name)
        results.append(result)

    clear_scene()
    json_path, md_path = write_reports(results, tools_dir)

    # Print summary
    print("\n" + "=" * 60)
    ok_count = sum(1 for r in results if r["status"] == "OK")
    print(f"Done: {ok_count}/{len(results)} models optimized successfully.")
    for r in results:
        mark = "✅" if r["status"] == "OK" else "❌"
        if r["status"] == "OK":
            print(f"  {mark} {r['model']:30s} {r['original_tris']:>8,} → {r['optimized_tris']:>8,} tris  ({r['reduction_pct']}%)")
        else:
            print(f"  {mark} {r['model']:30s} ERROR: {r.get('error','')}")
    print("=" * 60)
    print(f"\nReport: {md_path}")


if __name__ == "__main__":
    main()
