# Integración de Prefabs y Reconstrucción del Sistema de Ataques Visuales

Este documento detalla la arquitectura, integración y configuración del nuevo sistema de habilidades y efectos visuales cinemáticos para el jefe Windows y el jugador Linux en **SudoKill-9**.

---

## 1. Prefabs Encontrados y sus Rutas Reales

Se localizaron e integraron los 4 prefabs reales del paquete *Hovl Studio / Procedural fire*:

1. **`Magic fire 0`**
   - **Ruta:** `Assets/Hovl Studio/Procedural fire/Prefabs/Magic fire 0.prefab`
   - **GUID:** `08fd1b80cc24cc8439f45fda3c558255`
   - **Función:** Ataque aéreo orbital. Se genera en una posición elevada directamente sobre la posición inicial capturada de Linux y desciende verticalmente hacia el suelo permitiendo que Linux esquive.
2. **`Explosion`**
   - **Ruta:** `Assets/Hovl Studio/Procedural fire/Prefabs/Explosion.prefab`
   - **GUID:** `67a684e320da6e7439421a07e3fa265c`
   - **Función:** Efecto visual de impacto y detonación. Se reproduce una sola vez cuando Magic Fire 0 impacta exitosamente a Linux o cuando Magic Fire 1 detona (por impacto directo o al agotarse los 3 segundos de persecución).
3. **`RotatorPS2`**
   - **Ruta:** `Assets/Hovl Studio/Procedural fire/Prefabs/RotatorPS2.prefab`
   - **GUID:** `26255240385821a4185c7163c7a7aec6`
   - **Función:** Tornado mágico de plasma que rodea y sigue de manera centrada y estable al jugador Linux cuando es impactado por Magic Fire 0. Se va contrayendo progresivamente durante 5 segundos hasta disiparse.
4. **`Magic fire 1`**
   - **Ruta:** `Assets/Hovl Studio/Procedural fire/Prefabs/Magic fire 1.prefab`
   - **GUID:** `25093a0593dc83043a7bc17ebba0e95d`
   - **Función:** Proyectil telegrafiado de persecución mágica (*Homing*). Se dispara desde el punto de emisión frontal de Windows y persigue dinámicamente a Linux durante un máximo de 3 segundos con rotación suave y velocidad configurable.

---

## 2. Archivos Modificados y Creados

### Archivos Creados
- `Assets/_Project/Scripts/Combat/Widows/Windows_Combat/MagicFire0AereoController.cs`:
  Controlador del proyectil aéreo Magic Fire 0, captura fija de posición, detección de esquive por área, detonación condicional, activación de BSOD (5s) e instanciación del tornado.
- `Assets/_Project/Scripts/Combat/Widows/Windows_Combat/MagicFire1HomingController.cs`:
  Controlador del proyectil de persecución Magic Fire 1 con cinemática homing, limitador de giro angular (*turn rate*), detección de colisión con obstáculos y detonación tras 3 segundos.
- `Assets/_Project/Scripts/Combat/Widows/Windows_Combat/RotatorPS2TornadoController.cs`:
  Controlador del tornado RotatorPS2 con seguimiento en tiempo real de Linux, desactivación de colliders propios para no bloquear disparos y contracción progresiva de escala.
- `Assets/_Project/Scripts/Combat/Widows/Windows_Combat/ExplosionEffectController.cs`:
  Controlador auxiliar de Explosion que desactiva el looping en los ParticleSystems para garantizar una única reproducción limpia y autodestrucción controlada.
- `Assets/Documentation/ATTACK_PREFABS_INTEGRATION.md`:
  Documentación exhaustiva de la integración.

### Archivos Modificados
- `Assets/_Project/Scripts/Combat/Widows/Windows_Combat/WindowsCommandAttacks.cs`:
  Reemplazo del antiguo rayo rígido Kamehameha por Magic Fire 1 y orquestación del Ataque 1 (Magic Fire 0). Mantenimiento estricto de la máquina de estados, compatibilidad con reflexión y limpieza de ataques.
- `Assets/_Project/Scripts/UI/BSODGlitchEffect.cs`:
  Soporte para duración configurable en `DispararBSOD(float duracion = 5.0f)` y `ActivarBSOD(float duracion = 5.0f)`, asegurando los 5 segundos exactos requeridos.
- `Assets/_Project/Scripts/Editor/SetupCombatUIAndBossAbilities.cs`:
  Configuración automática de las referencias a los 4 prefabs reales de Hovl Studio en WindowsCommandAttacks.
- `Assets/_Project/Scripts/Editor/SetupWindowsBoss.cs`:
  Ampliación de la suite de pruebas unitarias con Tests 18 al 22 para validación automatizada de prefabs, controladores y tiempos.
- `Assets/_Project/Scenes/Experimento_K/prueba_Expe_K.unity`:
  Actualización de la escena de pruebas con los componentes calibrados, prefabs vinculados por GUID y BSOD ajustado a 5.0s.
- `Assembly-CSharp.csproj`:
  Inclusión de los nuevos scripts de combate.

---

## 3. Ataque 1: Magic Fire 0 (Secuencia Aérea + Esquive + Impacto)

### 3.1. Lanzamiento y Captura de Posición
- Al activarse el ataque, Windows captura la posición de Linux en ese instante exacto (`Vector3 posicionCapturadaLinux = playerTarget.position;`).
- Esta posición se almacena en una variable fija independiente.
- El proyectil Magic Fire 0 se genera a una altura configurable (`alturaInicialMagicFire0 = 14m`) sobre el punto capturado, orientado hacia abajo (`Quaternion.Euler(90, 0, 0)`).
- **Regla Fundamental Cumplida:** Durante la caída, Magic Fire 0 **no** actualiza su destino hacia la nueva posición del pingüino; desciende rígidamente hacia el punto capturado.

### 3.2. Mecánica de Esquive
- Mientras el efecto cae a `velocidadCaidaMagicFire0 = 16 m/s`, Linux puede correr, saltar o esquivar libremente.
- Si Linux se aparta del punto registrado y supera el radio de impacto (`radioImpactoMagicFire0 = 3.2m`):
  - **MISS:** El ataque finaliza al tocar el suelo. No hay daño, no se reproduce la explosión, no se activa el tornado ni se dispara la pantalla azul.

### 3.3. Impacto y Consecuencias (HIT)
- Si Linux se mantiene dentro del radio de impacto:
  - **HIT:**
    1. Se reproduce el prefab `Explosion` exactamente en el punto de contacto.
    2. Se aplica el daño configurado (`danoMagicFire0 = 1`) una única vez a través del sistema `IDamageable` / `PlayerHealth`.
    3. Se activa la Pantalla Azul (BSOD) durante 5 segundos exactos.
    4. Se instancia el prefab `RotatorPS2` centrado en Linux.

---

## 4. Efecto de Pantalla Azul (BSOD)

- Se reutilizó el sistema existente `BSODGlitchEffect.cs`.
- Se implementó `BSODGlitchEffect.DispararBSOD(float duracion = 5.0f)` y el evento `WindowsCommandAttacks.OnBSODTriggeredWithDuration`.
- **Duración:** Exactamente 5.0 segundos activos antes del desvanecimiento suave (*fade-out* de 0.35s).
- **Seguridad:** El efecto es puramente cosmético dentro del canvas de juego; no congela la aplicación, no altera el sistema operativo del usuario y restaura el estado visual al finalizar.
- **Deduplicación:** Si ya existe un BSOD en curso, reinicia suavemente el temporizador sin solapar múltiples instancias de canvas.

---

## 5. RotatorPS2 (Tornado Alrededor de Linux)

- Se activa **únicamente** cuando Magic Fire 0 acierta a Linux. Si Linux esquiva, el tornado no se genera.
- El componente `RotatorPS2TornadoController` vincula la posición del tornado al `Transform` de Linux en `LateUpdate`, garantizando que siga al personaje de forma suave y sin vibraciones.
- Se desactivan automáticamente los `BoxCollider` originales del prefab para evitar que interfieran con los disparos de Linux o la física del escenario.
- Durante sus 5 segundos de duración, la escala del tornado se contrae gradualmente (`1.2` -> `0.08`), transmitiendo la sensación de energía que se comprime hasta disiparse.
- Se destruye limpiamente al terminar su ciclo de vida.

---

## 6. Ataque 2: Magic Fire 1 (Proyectil Homing de Persecución)

- Reemplaza el anterior rayo LineRenderer estático del Kamehameha.
- Se instancia en el punto de disparo de Windows (`ObtenerPuntoDisparo()`).
- Inicia una persecución cinemática activa hacia Linux durante **máximo 3 segundos**:
  - Velocidad lineal configurable: `velocidadPersecucionMagicFire1 = 15 m/s`.
  - Velocidad de giro limitada: `velocidadGiroMagicFire1 = 160 °/s` (curva fluida y natural).
  - Traza detección volumétrica frontal (`Physics.SphereCast`) para evitar atravesar paredes u obstáculos.
- **Finalización:**
  - **Impacto Directo:** Si alcanza a Linux antes de 3 segundos, genera `Explosion`, aplica el daño una vez y se destruye.
  - **Tiempo Agotado:** Si transcurren los 3 segundos sin impactar, frena la persecución, genera `Explosion` en su última posición y aplica daño de área únicamente si Linux está dentro de `radioExplosionMagicFire1 = 2.8m`.
- Tras finalizar el disparo, Windows pasa a `PoseCansado` durante 3.0 segundos, quedando vulnerable a 1.5x de daño crítico (conservando la mecánica táctica original).

---

## 7. Sustitución de Efectos Anteriores

- **Reemplazados:**
  - El láser rígido `hazKamehameha` (`LineRenderer`) y la guía estática `guiaLaserCarga` fueron desactivados (`enabled = false`).
  - El rayo estático fue sustituido por el proyectil dinámico móvil 3D `Magic fire 1.prefab`.
- **Conservados:**
  - El escudo frontal holográfico `netsh firewall`.
  - La evasión táctica `SaltaLoco`.
  - El debuff de velocidad `taskkill`.
  - Todos los ataques físicos cuerpo a cuerpo (`Patada`, `Ataque_Patada`, `Tornado`, `Salto`).

---

## 8. Parámetros Configurables en el Inspector

En el componente `WindowsCommandAttacks` del GameObject `windows`:

| Parámetro | Valor por Defecto | Descripción |
|---|---|---|
| `alturaInicialMagicFire0` | `14.0` | Altura sobre el punto registrado desde donde cae Magic Fire 0 (m). |
| `velocidadCaidaMagicFire0` | `16.0` | Velocidad de descenso vertical del proyectil aéreo (m/s). |
| `radioImpactoMagicFire0` | `3.2` | Radio para evaluar si Linux esquivó o fue alcanzado (m). |
| `danoMagicFire0` | `1.0` | Daño aplicado a Linux al acertar Magic Fire 0. |
| `duracionBSODMagicFire0` | `5.0` | Duración en segundos de la Pantalla Azul activada por Magic Fire 0. |
| `duracionRotatorPS2` | `5.0` | Tiempo en segundos que dura el tornado alrededor de Linux. |
| `escalaRotatorPS2` | `(1.2, 1.2, 1.2)` | Escala inicial del tornado antes de comenzar su contracción. |
| `velocidadPersecucionMagicFire1` | `15.0` | Velocidad de avance del proyectil de persecución Magic Fire 1 (m/s). |
| `velocidadGiroMagicFire1` | `160.0` | Velocidad angular de rotación de Magic Fire 1 (grados/s). |
| `duracionHomingMagicFire1` | `3.0` | Duración máxima de persecución activa de Magic Fire 1 (segundos). |
| `radioContactoMagicFire1` | `1.1` | Distancia de proximidad para registrar impacto directo en Linux. |
| `radioExplosionMagicFire1` | `2.8` | Radio de daño por onda expansiva al detonar tras los 3 segundos. |
| `danoMagicFire1` | `1.0` | Daño causado por el proyectil o la explosión de Magic Fire 1. |
| `escalaExplosion` | `(1.5, 1.5, 1.5)` | Escala del efecto Explosion al detonar. |

---

## 9. Controles de Prueba en Play Mode

Durante el juego o en el Editor de Unity:
- Presionar **`1`**: Dispara inmediatamente el **Ataque 1** (Magic Fire 0).
- Presionar **`2`**: Dispara inmediatamente el **Ataque 2** (Magic Fire 1 Homing).
- Presionar **`3`**: Dispara inmediatamente la **Pantalla Azul (BSOD)** por 5 segundos.
- Alternativamente, clic derecho sobre el componente `WindowsCommandAttacks` en el Inspector y seleccionar las opciones del menú contextual.
