# FBX Optimization Report

Generated: 2026-11-02 15:45

## Summary

| Model | Ratio | Original Tris | Optimized Tris | Reduction | Status |
|-------|-------|--------------|----------------|-----------|--------|
| cpu | 0.5 | 1,876,243 | 938,121 | 50.0% | ✅ OK |
| gpu | 0.5 | 1,926,753 | 963,376 | 50.0% | ✅ OK |
| chip | 0.5 | 1,983,800 | 991,900 | 50.0% | ✅ OK |
| ram | 0.4 | 1,912,648 | 765,059 | 60.0% | ✅ OK |
| resistencia | 0.3 | 1,944,344 | 583,303 | 70.0% | ✅ OK |
| condensador | 0.3 | 1,910,399 | 573,119 | 70.0% | ✅ OK |
| disco_almacenamiento | 0.4 | 1,925,374 | 770,149 | 60.0% | ✅ OK |
| targeta_red | 0.4 | 1,959,447 | 783,778 | 60.0% | ✅ OK |
| puertos_conexion | 0.4 | 1,959,824 | 783,929 | 60.0% | ✅ OK |
| batería_CMOS | 0.3 | 1,857,269 | 557,179 | 70.0% | ✅ OK |
| ventilador_refrigeracion | 0.3 | 1,878,603 | 563,579 | 70.0% | ✅ OK |
| SCAR-H | 0.4 | 11,482 | 4,590 | 60.0% | ✅ OK |
| PC | 0.35 | 960,092 | 336,032 | 65.0% | ✅ OK |
| Casillero | 0.35 | 948,878 | 332,106 | 65.0% | ✅ OK |
| Mapa | 0.3 | 978,241 | 293,472 | 70.0% | ✅ OK |
| Mesa | 0.4 | 1,916,608 | 766,642 | 60.0% | ✅ OK |
| Armas | 0.4 | 1,936,105 | 774,441 | 60.0% | ✅ OK |

**Total original triangles:** 27,886,110
**Total optimized triangles:** 10,780,775
**Overall reduction:** 61.3%

## Per-model Details

### cpu
- Source: `Assets\_Project\Models\Environment\cpu\cpu.fbx`
- Output: `Assets\_Project\Models\Environment\cpu\cpu_opt.fbx`
- Decimate ratio: 0.5 (keep 50% of faces)
- 1,876,243 → 938,121 triangles (50.0% reduction)
- Processing time: 23.45s

| Mesh Object | Original | Optimized |
|-------------|----------|-----------|
| tripo_node_023e66b6-5abc-4770-a5cb-19d5dc63c858 | 1,876,243 | 938,121 |

### gpu
- Source: `Assets\_Project\Models\Environment\gpu\gpu.fbx`
- Output: `Assets\_Project\Models\Environment\gpu\gpu_opt.fbx`
- Decimate ratio: 0.5 (keep 50% of faces)
- 1,926,753 → 963,376 triangles (50.0% reduction)
- Processing time: 23.27s

| Mesh Object | Original | Optimized |
|-------------|----------|-----------|
| tripo_node_9a67cb33-2fda-473f-aa10-3de853c4d181 | 1,926,753 | 963,376 |

### chip
- Source: `Assets\_Project\Models\Environment\chip\chip.fbx`
- Output: `Assets\_Project\Models\Environment\chip\chip_opt.fbx`
- Decimate ratio: 0.5 (keep 50% of faces)
- 1,983,800 → 991,900 triangles (50.0% reduction)
- Processing time: 23.53s

| Mesh Object | Original | Optimized |
|-------------|----------|-----------|
| tripo_node_4d04f195-47d6-4466-9135-8cbc274ae162 | 1,983,800 | 991,900 |

### ram
- Source: `Assets\_Project\Models\Environment\ram\ram.fbx`
- Output: `Assets\_Project\Models\Environment\ram\ram_opt.fbx`
- Decimate ratio: 0.4 (keep 40% of faces)
- 1,912,648 → 765,059 triangles (60.0% reduction)
- Processing time: 23.93s

| Mesh Object | Original | Optimized |
|-------------|----------|-----------|
| tripo_node_329f4863-6258-4e1b-9091-27a046abae21 | 1,912,648 | 765,059 |

### resistencia
- Source: `Assets\_Project\Models\Environment\resistencia\resistencia.fbx`
- Output: `Assets\_Project\Models\Environment\resistencia\resistencia_opt.fbx`
- Decimate ratio: 0.3 (keep 30% of faces)
- 1,944,344 → 583,303 triangles (70.0% reduction)
- Processing time: 25.54s

| Mesh Object | Original | Optimized |
|-------------|----------|-----------|
| tripo_node_b068d886-1a42-4bad-a844-e55a85511df3 | 1,944,344 | 583,303 |

### condensador
- Source: `Assets\_Project\Models\Environment\condensador\condensador.fbx`
- Output: `Assets\_Project\Models\Environment\condensador\condensador_opt.fbx`
- Decimate ratio: 0.3 (keep 30% of faces)
- 1,910,399 → 573,119 triangles (70.0% reduction)
- Processing time: 26.56s

| Mesh Object | Original | Optimized |
|-------------|----------|-----------|
| tripo_node_67bba0f4-f921-4f18-92bc-972479e78cc3 | 1,910,399 | 573,119 |

### disco_almacenamiento
- Source: `Assets\_Project\Models\Environment\disco_almacenamiento\disco_almacenamiento.fbx`
- Output: `Assets\_Project\Models\Environment\disco_almacenamiento\disco_almacenamiento_opt.fbx`
- Decimate ratio: 0.4 (keep 40% of faces)
- 1,925,374 → 770,149 triangles (60.0% reduction)
- Processing time: 25.26s

| Mesh Object | Original | Optimized |
|-------------|----------|-----------|
| tripo_node_2fb9d8ad-6118-418c-aa66-a4b4025dee82 | 1,925,374 | 770,149 |

### targeta_red
- Source: `Assets\_Project\Models\Environment\targeta_red\targeta_red.fbx`
- Output: `Assets\_Project\Models\Environment\targeta_red\targeta_red_opt.fbx`
- Decimate ratio: 0.4 (keep 40% of faces)
- 1,959,447 → 783,778 triangles (60.0% reduction)
- Processing time: 24.63s

| Mesh Object | Original | Optimized |
|-------------|----------|-----------|
| tripo_node_c878b8da-19c5-4d48-a2d4-b4287271c4f4 | 1,959,447 | 783,778 |

### puertos_conexion
- Source: `Assets\_Project\Models\Environment\puertos_conexion\puertos_conexion.fbx`
- Output: `Assets\_Project\Models\Environment\puertos_conexion\puertos_conexion_opt.fbx`
- Decimate ratio: 0.4 (keep 40% of faces)
- 1,959,824 → 783,929 triangles (60.0% reduction)
- Processing time: 24.61s

| Mesh Object | Original | Optimized |
|-------------|----------|-----------|
| tripo_node_f64b9735-0b61-4089-a98d-1e0c19b31a04 | 1,959,824 | 783,929 |

### batería_CMOS
- Source: `Assets\_Project\Models\Environment\batería_CMOS\batería_CMOS.fbx`
- Output: `Assets\_Project\Models\Environment\batería_CMOS\batería_CMOS_opt.fbx`
- Decimate ratio: 0.3 (keep 30% of faces)
- 1,857,269 → 557,179 triangles (70.0% reduction)
- Processing time: 24.74s

| Mesh Object | Original | Optimized |
|-------------|----------|-----------|
| tripo_node_edf7029b-489a-4416-b52d-bc4c8ddf88fd | 1,857,269 | 557,179 |

### ventilador_refrigeracion
- Source: `Assets\_Project\Models\Environment\ventilador_refrigeracion\ventilador_refrigeracion.fbx`
- Output: `Assets\_Project\Models\Environment\ventilador_refrigeracion\ventilador_refrigeracion_opt.fbx`
- Decimate ratio: 0.3 (keep 30% of faces)
- 1,878,603 → 563,579 triangles (70.0% reduction)
- Processing time: 24.47s

| Mesh Object | Original | Optimized |
|-------------|----------|-----------|
| tripo_node_69f79b3d-de19-41a6-92c9-2556bea85c90 | 1,878,603 | 563,579 |

### SCAR-H
- Source: `Assets\_Project\Models\Environment\SCAR-H.fbx`
- Output: `Assets\_Project\Models\Environment\SCAR-H_opt.fbx`
- Decimate ratio: 0.4 (keep 40% of faces)
- 11,482 → 4,590 triangles (60.0% reduction)
- Processing time: 0.08s

| Mesh Object | Original | Optimized |
|-------------|----------|-----------|
| ScarMag | 628 | 250 |
| ScarBody | 10,854 | 4,340 |

### PC
- Source: `Assets\_Project\Models\Environment\Inicio_modelos\PC.fbx`
- Output: `Assets\_Project\Models\Environment\Inicio_modelos\PC_opt.fbx`
- Decimate ratio: 0.35 (keep 35% of faces)
- 960,092 → 336,032 triangles (65.0% reduction)
- Processing time: 11.72s

| Mesh Object | Original | Optimized |
|-------------|----------|-----------|
| tripo_node_f7774676-321d-4b59-9dd4-210118f72dd8 | 960,092 | 336,032 |

### Casillero
- Source: `Assets\_Project\Models\Environment\Inicio_modelos\Casillero.fbx`
- Output: `Assets\_Project\Models\Environment\Inicio_modelos\Casillero_opt.fbx`
- Decimate ratio: 0.35 (keep 35% of faces)
- 948,878 → 332,106 triangles (65.0% reduction)
- Processing time: 11.67s

| Mesh Object | Original | Optimized |
|-------------|----------|-----------|
| tripo_node_43e8b7e2-5c23-44ea-8539-ec1d82ff6e45 | 948,878 | 332,106 |

### Mapa
- Source: `Assets\_Project\Models\Environment\Inicio_modelos\Mapa.fbx`
- Output: `Assets\_Project\Models\Environment\Inicio_modelos\Mapa_opt.fbx`
- Decimate ratio: 0.3 (keep 30% of faces)
- 978,241 → 293,472 triangles (70.0% reduction)
- Processing time: 12.62s

| Mesh Object | Original | Optimized |
|-------------|----------|-----------|
| tripo_node_1fdfef7b-3180-42ed-aee7-4f4b0e6fd271 | 978,241 | 293,472 |

### Mesa
- Source: `Assets\_Project\Models\Environment\Inicio_modelos\Mesa.fbx`
- Output: `Assets\_Project\Models\Environment\Inicio_modelos\Mesa_opt.fbx`
- Decimate ratio: 0.4 (keep 40% of faces)
- 1,916,608 → 766,642 triangles (60.0% reduction)
- Processing time: 23.98s

| Mesh Object | Original | Optimized |
|-------------|----------|-----------|
| tripo_node_219712cd-544a-4019-b56a-ed8a28409e19 | 1,916,608 | 766,642 |

### Armas
- Source: `Assets\_Project\Models\Environment\Inicio_modelos\Armas.fbx`
- Output: `Assets\_Project\Models\Environment\Inicio_modelos\Armas_opt.fbx`
- Decimate ratio: 0.4 (keep 40% of faces)
- 1,936,105 → 774,441 triangles (60.0% reduction)
- Processing time: 24.24s

| Mesh Object | Original | Optimized |
|-------------|----------|-----------|
| tripo_node_7979483c-aa5d-4184-b806-2b83ac2f29cb | 1,936,105 | 774,441 |

---

## Validation Checklist (manual)

For each optimized model, verify in Unity / Blender before substituting:

- [ ] Visual silhouette preserved
- [ ] UV seams / texture mapping intact
- [ ] Material slots unchanged
- [ ] Pivot / origin in same world position
- [ ] Scale and dimensions match original
- [ ] No flipped normals
- [ ] Collider geometry unaffected (colliders are separate objects, not in FBX)

**Do NOT replace scene references until all checklist items pass.**
