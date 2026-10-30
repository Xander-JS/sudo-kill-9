# Guía Oficial de Estructura de Carpetas — Proyecto: sudo-kill-9

Este documento describe la arquitectura interna de carpetas dentro del directorio `Assets/` en Unity. Su propósito es garantizar la modularidad, evitar colisiones en Git y facilitar la integración técnica entre los 3 integrantes y las herramientas del pipeline (Krita/Generadores, Tripo3D, Blender, Mixamo y Claude).

---

## Regla de Oro
Todo el contenido propio del videojuego debe guardarse exclusivamente dentro de:
```text
Assets/_Project/
```
> **Nota:** La carpeta raíz `Assets/` debe mantenerse limpia para paquetes nativos, plugins externos o dependencias del Package Manager. El prefijo `_` en `_Project` asegura que nuestra carpeta de trabajo aparezca siempre en la parte superior del explorador de Unity.

---

## Árbol de Directorios

```text
Assets/
└── _Project/
    ├── Animations/
    │   ├── Clips/
    │   └── Controllers/
    ├── Audio/
    │   ├── BGM/
    │   ├── SFX/
    │   └── Voice/
    ├── Localization/
    │   ├── Tables/
    │   └── Locales/
    ├── Materials/
    ├── Models/
    │   ├── Characters/
    │   └── Environment/
    ├── Prefabs/
    │   ├── Characters/
    │   ├── Props/
    │   └── UI/
    ├── Scenes/
    ├── Scripts/
    │   ├── Combat/
    │   ├── Controllers/
    │   ├── Dialogue/
    │   └── Managers/
    └── Sprites/
        ├── Dialogue/
        ├── Environment/
        └── UI/
```

---

## Contenido Esperado por Carpeta

### 1. `Animations/`
Contenedor de todos los datos de animación bípeda y elementos dinámicos.
*   **`Clips/`**: Archivos de animación descargados de Mixamo en formato `.fbx` (exportados estrictamente como **Without Skin**, Humanoid). Contiene ataques, guardias, caídas, patadas, idle y reacciones de impacto.
*   **`Controllers/`**: Archivos `Animator Controller` de Unity que configuran las máquinas de estados (transiciones entre combate, guardia y movimiento).

### 2. `Audio/`
Todos los recursos sonoros del juego (formatos `.wav` o `.ogg`).
*   **`BGM/`**: Pistas de música ambiental y temas de combate (estilo retro/synthwave/electrónico).
*   **`SFX/`**: Efectos de sonido puntuales provenientes de Bfxr o Freesound (golpes, bloqueos, ruidos de terminal, clicks de interfaz).
*   **`Voice/`**: Líneas de voz grabadas para la entidad narradora (*BIOS Primigenio*), organizadas por acto o identificador numérico.

### 3. `Localization/`
Archivos generados por el paquete oficial *Unity Localization*.
*   **`Tables/`**: *String Tables* donde se asignan las claves de texto y sus correspondientes versiones en inglés y español (diálogos, menús, descripciones de ataques).
*   **`Locales/`**: Configuraciones de idioma regional (es-ES / en-US).

### 4. `Materials/`
Materiales (`.mat`) y Shaders configurados para los combatientes y los escenarios 3D (iluminación, efectos de brillo de terminal de fósforo verde/ámbar, superficies de cristal).

### 5. `Models/`
Mallas tridimensionales sin prefabicar, procesadas a través de Tripo3D, optimizadas en Blender y pasadas por Mixamo.
*   **`Characters/`**: Modelos base con esqueleto descargados de Mixamo en formato `.fbx` (**With Skin**, rig Humanoid).
*   **`Environment/`**: Mallas estáticas 3D de las arenas de combate (piezas de placas madre, racks de servidores, muros de cristal corporativo).

### 6. `Prefabs/`
Entidades prefabricadas listas para ser instanciadas en escena con todos sus componentes (Colliders, Hitboxes, Scripts y Renderers ya enlazados).
*   **`Characters/`**: Luchadores terminados y funcionales listos para entrar al ring.
*   **`Props/`**: Objetos interactivos de escenario o elementos rompibles.
*   **`UI/`**: Ventanas de diálogo 2D preensambladas, barras de vida/memoria RAM flotantes y pantallas de opciones.

### 7. `Scenes/`
Escenas principales del juego:
*   `MainMenu.unity`: Menú de inicio y selección de idioma.
*   `Exploration2D.unity`: Segmentos narrativos en vista ortográfica 2D.
*   `CombatArena.unity`: Arena 3D donde opera Cinemachine para las peleas.

### 8. `Scripts/`
Código fuente en C# estructurado por responsabilidades, generado o depurado con asistencia de Claude:
*   **`Combat/`**: Lógica de cálculo de daño, detección de colisiones (Hitboxes/Hurtboxes) y sistemas de recursos (consumo de memoria RAM).
*   **`Controllers/`**: Integración con el nuevo *Input System* (controles por teclado/gamepad) y movimiento físico de personajes.
*   **`Dialogue/`**: Controladores de cuadros de texto clásicos estilo RPG y sincronización con subtítulos.
*   **`Managers/`**: Scripts globales (`GameManager`, `AudioManager`, gestores de cambio de escena).

### 9. `Sprites/`
Archivos de imagen 2D (`.png`) creados en Krita, Pixelorama o generadores 2D:
*   **`Dialogue/`**: Retratos y expresiones de personajes para los cuadros de conversación.
*   **`Environment/`**: Fondos 2D y elementos visuales para las escenas ortográficas narrativas.
*   **`UI/`**: Iconos alegóricos (marcos de cristal, engranajes modulares), botones, fuentes tipográficas y marcos de pantalla.

---

## Convenciones de Nomenclatura para el Equipo
1. **Carpetas**: Siempre en inglés y usando `PascalCase` (ejemplo: `CombatArena`, `StringTables`).
2. **Scripts**: Deben coincidir exactamente con el nombre de la clase de C# en `PascalCase` (ejemplo: `PlayerCombatController.cs`).
3. **Prefabs**: Usar sufijo o prefijo claro (ejemplo: `PF_Fighter_Modular` o `Fighter_Modular_PF`).
4. **Animaciones**: Usar el formato `Anim_[Personaje]_[Accion]` (ejemplo: `Anim_TuxLike_HeavyPunch`).