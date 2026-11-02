"""
generate_opt_meta.py
====================
Creates (or force-updates) _opt.fbx.meta files so Unity imports optimized
FBX assets with the SAME material remapping as the originals.

Usage:
    python Tools/generate_opt_meta.py           # skip existing correct ones
    python Tools/generate_opt_meta.py --force   # overwrite ALL (fixes broken metas)

Run AFTER the Blender optimization and BEFORE opening Unity.
Requires Python 3.6+ (no third-party packages).
"""

import re
import sys
import uuid
from pathlib import Path

PROJECT_ROOT = Path(__file__).resolve().parent.parent
MODELS_ENV   = PROJECT_ROOT / "Assets" / "_Project" / "Models" / "Environment"

# (orig_fbx_path_relative_to_MODELS_ENV, opt_fbx_path_relative_to_MODELS_ENV)
MODELS = [
    # Standard env models (each in its own subfolder)
    ("cpu/cpu.fbx",                                       "cpu/cpu_opt.fbx"),
    ("gpu/gpu.fbx",                                       "gpu/gpu_opt.fbx"),
    ("chip/chip.fbx",                                     "chip/chip_opt.fbx"),
    ("ram/ram.fbx",                                       "ram/ram_opt.fbx"),
    ("resistencia/resistencia.fbx",                       "resistencia/resistencia_opt.fbx"),
    ("condensador/condensador.fbx",                       "condensador/condensador_opt.fbx"),
    ("disco_almacenamiento/disco_almacenamiento.fbx",     "disco_almacenamiento/disco_almacenamiento_opt.fbx"),
    ("targeta_red/targeta_red.fbx",                       "targeta_red/targeta_red_opt.fbx"),
    ("puertos_conexion/puertos_conexion.fbx",             "puertos_conexion/puertos_conexion_opt.fbx"),
    ("ventilador_refrigeracion/ventilador_refrigeracion.fbx", "ventilador_refrigeracion/ventilador_refrigeracion_opt.fbx"),
    ("batería_CMOS/batería_CMOS.fbx",                     "batería_CMOS/batería_CMOS_opt.fbx"),
    # Flat / shared-folder models
    ("SCAR-H.fbx",                                        "SCAR-H_opt.fbx"),
    ("Inicio_modelos/PC.fbx",                             "Inicio_modelos/PC_opt.fbx"),
    ("Inicio_modelos/Casillero.fbx",                      "Inicio_modelos/Casillero_opt.fbx"),
    ("Inicio_modelos/Mapa.fbx",                           "Inicio_modelos/Mapa_opt.fbx"),
    ("Inicio_modelos/Mesa.fbx",                           "Inicio_modelos/Mesa_opt.fbx"),
    ("Inicio_modelos/Armas.fbx",                          "Inicio_modelos/Armas_opt.fbx"),
]


def make_guid() -> str:
    return uuid.uuid4().hex


def has_correct_external_objects(meta_path: Path) -> bool:
    """Return True if the meta file has a non-empty externalObjects section."""
    content = meta_path.read_text(encoding="utf-8")
    # If the file has "externalObjects: {}" it's empty/broken
    if re.search(r"externalObjects:\s*\{\}", content):
        return False
    # If it has at least one material mapping entry it's good
    return bool(re.search(r"externalObjects:\s*\n\s*-\s*first:", content))


def process(orig_rel: str, opt_rel: str, force: bool):
    orig_fbx  = MODELS_ENV / orig_rel
    orig_meta = Path(str(orig_fbx) + ".meta")
    opt_fbx   = MODELS_ENV / opt_rel
    opt_meta  = Path(str(opt_fbx) + ".meta")

    if not orig_meta.exists():
        print(f"  [SKIP] Original meta missing: {orig_meta.name}")
        return
    if not opt_fbx.exists():
        print(f"  [SKIP] Optimized FBX not found: {opt_fbx.name}")
        return

    # Skip if already correct (unless --force)
    if opt_meta.exists() and not force:
        if has_correct_external_objects(opt_meta):
            print(f"  [OK]   {opt_meta.name}  (already correct)")
            return
        print(f"  [FIX]  {opt_meta.name}  (broken externalObjects — rewriting)")

    content  = orig_meta.read_text(encoding="utf-8")
    new_guid = make_guid()
    content  = re.sub(r"^guid: [0-9a-f]{32}", f"guid: {new_guid}", content, count=1, flags=re.MULTILINE)
    opt_meta.write_text(content, encoding="utf-8")
    action = "FORCE" if force else "CREATED"
    print(f"  [{action}] {opt_meta.name}  (guid: {new_guid})")


def main():
    force = "--force" in sys.argv
    print(f"Generating _opt.fbx.meta files  (force={force}) ...")
    for orig_rel, opt_rel in MODELS:
        process(orig_rel, opt_rel, force)
    print("Done.")


if __name__ == "__main__":
    main()
