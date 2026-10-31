import bpy
import sys
import os

# Procesar argumentos tras '--'
argv = sys.argv
if "--" in argv:
    args = argv[argv.index("--") + 1:]
else:
    args = []

if len(args) < 1:
    print("\nUso: blender -b -P glb2fbx.py -- <archivo.glb> [destino.fbx o carpeta_destino]\n")
    sys.exit(1)

glb_path = os.path.abspath(args[0])
if not os.path.exists(glb_path):
    print(f"[ERROR] No se encontró el archivo origen: {glb_path}")
    sys.exit(1)

if len(args) >= 2:
    target = os.path.abspath(args[1])
    if os.path.isdir(target) or not target.lower().endswith(".fbx"):
        os.makedirs(target, exist_ok=True)
        fbx_name = os.path.splitext(os.path.basename(glb_path))[0] + ".fbx"
        fbx_path = os.path.join(target, fbx_name)
    else:
        fbx_path = target
        os.makedirs(os.path.dirname(fbx_path), exist_ok=True)
else:
    fbx_path = os.path.splitext(glb_path)[0] + ".fbx"

out_dir = os.path.dirname(fbx_path)
tex_dir = os.path.join(out_dir, "Texture")
os.makedirs(tex_dir, exist_ok=True)

print(f"--> Importando GLB: {glb_path}")
# 1. Limpiar escena actual en Blender
bpy.ops.wm.read_factory_settings(use_empty=True)

# 2. Importar GLB / glTF
bpy.ops.import_scene.gltf(filepath=glb_path)

# 3. Eliminar cámaras y luces por defecto (para que Unity no cree cámaras o luces extras)
for obj in list(bpy.data.objects):
    if obj.type in {'CAMERA', 'LIGHT'}:
        bpy.data.objects.remove(obj, do_unlink=True)

# 4. Extraer todas las texturas empaquetadas a la carpeta Texture/
print(f"--> Extrayendo texturas a: {tex_dir}")
for img in bpy.data.images:
    if img.has_data or img.packed_file:
        clean_name = img.name.replace('/', '_').replace('\\', '_')
        if not clean_name.lower().endswith(('.png', '.jpg', '.jpeg')):
            clean_name += '.png'
        save_path = os.path.join(tex_dir, clean_name)
        img.filepath_raw = save_path
        img.file_format = 'PNG'
        try:
            img.save()
            print(f"  [+] Textura guardada: {clean_name}")
        except Exception as e:
            print(f"  [!] Advertencia al guardar {img.name}: {e}")

# 5. Exportar a FBX con modo COPY e incrustación de texturas
print(f"--> Exportando FBX a: {fbx_path}")
bpy.ops.export_scene.fbx(
    filepath=fbx_path,
    check_existing=False,
    use_selection=False,
    path_mode='COPY',
    embed_textures=True,
    axis_forward='-Z',
    axis_up='Y',
    apply_scale_options='FBX_SCALE_ALL'
)

print(f"\n[EXITO COMPLETO]")
print(f"• FBX generado: {fbx_path}")
print(f"• Texturas extraidas en: {tex_dir}\n")
