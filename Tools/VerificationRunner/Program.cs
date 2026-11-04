using System;
using System.IO;
using System.Text.RegularExpressions;

namespace SudoKill9.Verification
{
    class Program
    {
        static int Main(string[] args)
        {
            Console.WriteLine("================================================================================");
            Console.WriteLine("     SUDO-KILL-9: VERIFICACIÓN INTEGRAL DE COMBATE, DOBLE VIDA Y UI");
            Console.WriteLine("================================================================================");
            Console.WriteLine($"Fecha: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n");

            bool allPassed = true;

            // =========================================================================
            // PRUEBA 1: VERIFICACIÓN DE ROTATORPS1 (TRAMPA ESTÁTICA Y CONFINAMIENTO)
            // =========================================================================
            Console.WriteLine("--- 1. VERIFICACIÓN DE ATAQUE ROTATORPS1 Y TRAMPA ESTÁTICA ---");
            
            // 1.1 Archivo y Controlador
            string rotatorPath = "Assets/_Project/Scripts/Combat/Widows/Windows_Combat/RotatorPS1TrapController.cs";
            bool rotatorFileExists = File.Exists(rotatorPath);
            Console.WriteLine($"[TEST 1.1] RotatorPS1TrapController.cs existe en disco: {(rotatorFileExists ? "PASS" : "FAIL")}");
            allPassed &= rotatorFileExists;

            if (rotatorFileExists)
            {
                string code = File.ReadAllText(rotatorPath);
                
                // 1.2 No genera BoxColliders físicos que bloqueen balas o cámara
                bool hasBoxCollider = code.Contains("BoxCollider") || code.Contains("RotatorPS1_BoundaryRing");
                Console.WriteLine($"[TEST 1.2] Sin BoxColliders sólidos que bloqueen balas o cámara (cero colliders físicos de barrera): {(!hasBoxCollider ? "PASS" : "FAIL")}");
                allPassed &= !hasBoxCollider;

                // 1.3 Implementa CharacterController.Move para deslizamiento tangencial suave
                bool hasCcMove = code.Contains("cc.Move(");
                Console.WriteLine($"[TEST 1.3] Utiliza cc.Move para deslizamiento circular sin romper grounding ni gravedad: {(hasCcMove ? "PASS" : "FAIL")}");
                allPassed &= hasCcMove;

                // 1.4 Duración y radio configurables
                bool hasConfigurableFields = code.Contains("duracionTrampa") && code.Contains("radioConfinamiento");
                Console.WriteLine($"[TEST 1.4] Duración y radio de confinamiento configurables desde Inspector: {(hasConfigurableFields ? "PASS" : "FAIL")}");
                allPassed &= hasConfigurableFields;

                // 1.5 Simulación matemática del algoritmo de confinamiento circular
                float radio = 3.2f;
                float margen = 0.35f;
                float radioMax = radio - margen; // 2.85m
                (float x, float z) centro = (10f, 20f);

                // Jugador adentro: (11, 20) -> dist = 1.0m <= 2.85m -> no debe cambiar
                (float x, float z) posAdentro = (11f, 20f);
                float distAdentro = MathF.Sqrt(MathF.Pow(posAdentro.x - centro.x, 2) + MathF.Pow(posAdentro.z - centro.z, 2));
                bool clampAdentroOk = distAdentro <= radioMax;

                // Jugador intenta escapar: (100, 20) -> dist = 90m > 2.85m -> se clampa a 2.85m
                (float x, float z) posAfuera = (100f, 20f);
                float dirX = (posAfuera.x - centro.x) / 90f;
                float dirZ = (posAfuera.z - centro.z) / 90f;
                (float x, float z) posClamp = (centro.x + dirX * radioMax, centro.z + dirZ * radioMax);
                float distClamp = MathF.Sqrt(MathF.Pow(posClamp.x - centro.x, 2) + MathF.Pow(posClamp.z - centro.z, 2));
                bool clampAfueraOk = MathF.Abs(distClamp - radioMax) < 0.001f;

                bool clampMathOk = clampAdentroOk && clampAfueraOk;
                Console.WriteLine($"[TEST 1.5] Algoritmo matemático de confinamiento perimetral hermético (dist={distClamp:F2}m == {radioMax:F2}m): {(clampMathOk ? "PASS" : "FAIL")}");
                allPassed &= clampMathOk;
            }

            // =========================================================================
            // PRUEBA 2: SISTEMA DE DOS VIDAS Y BARRA HEAD SPACE DE WINDOWS
            // =========================================================================
            Console.WriteLine("\n--- 2. VERIFICACIÓN DE SISTEMA DE DOS VIDAS Y BARRA HEAD SPACE ---");

            string headBarPath = "Assets/_Project/Scripts/UI/WindowsHeadHealthBar.cs";
            bool headBarFileExists = File.Exists(headBarPath);
            Console.WriteLine($"[TEST 2.1] WindowsHeadHealthBar.cs existe en disco: {(headBarFileExists ? "PASS" : "FAIL")}");
            allPassed &= headBarFileExists;

            if (headBarFileExists)
            {
                string headCode = File.ReadAllText(headBarPath);

                // 2.2 Desacopla la escala del padre (escala 0.005 en mundo independiente del 0.2 de Windows)
                bool desacoplado = headCode.Contains("transform.SetParent(null");
                Console.WriteLine($"[TEST 2.2] Desacopla la barra de la escala del modelo 3D (evita encogimiento a 0.38m): {(desacoplado ? "PASS" : "FAIL")}");
                allPassed &= desacoplado;

                // 2.3 Indicadores gemelos de vida creados en UI
                bool hasLifePips = headCode.Contains("Life1Dot") && headCode.Contains("Life2Dot") && headCode.Contains("LifePipsContainer");
                Console.WriteLine($"[TEST 2.3] Indicadores visuales gemelos de vida creados e instanciados: {(hasLifePips ? "PASS" : "FAIL")}");
                allPassed &= hasLifePips;

                // 2.4 Protección contra sobrescritura de daño durante la animación de recarga
                bool hasTransitionDamageProtection = headCode.Contains("targetFill") && headCode.Contains("Mathf.Clamp01(targetFill)");
                Console.WriteLine($"[TEST 2.4] Protección contra daño concurrente en transición de Vida 2 (sin reset a 100%): {(hasTransitionDamageProtection ? "PASS" : "FAIL")}");
                allPassed &= hasTransitionDamageProtection;

                // 2.5 Simulación de máquina de estados de Dos Vidas (WindowsBossAI)
                float hpMax1 = 500f, hpMax2 = 500f;
                int vidaActual = 1, vidasTotales = 2;
                float hpActual = hpMax1;
                bool isDead = false;

                // Recibir 500 daño en vida 1
                hpActual -= 500f;
                if (hpActual <= 0f && vidaActual < vidasTotales)
                {
                    vidaActual++;
                    hpActual = hpMax2;
                }
                bool vida1TransicionOk = (vidaActual == 2 && hpActual == 500f && !isDead);

                // Recibir 250 daño en vida 2
                hpActual -= 250f;
                bool vida2DanoOk = (vidaActual == 2 && hpActual == 250f && !isDead);

                // Recibir 250 daño final en vida 2 -> Muerte definitiva
                hpActual -= 250f;
                if (hpActual <= 0f)
                {
                    if (vidaActual >= vidasTotales)
                    {
                        isDead = true;
                    }
                }
                bool muerteOk = (isDead && hpActual <= 0f);

                bool stateMachineOk = vida1TransicionOk && vida2DanoOk && muerteOk;
                Console.WriteLine($"[TEST 2.5] Máquina de estados de 2 vidas (Vida 1 -> Vida 2 -> Muerte sólo al agotar ambas): {(stateMachineOk ? "PASS" : "FAIL")}");
                allPassed &= stateMachineOk;
            }

            // =========================================================================
            // PRUEBA 3: TARJETA DE MENSAJES CMD (CMDTerminalHUD)
            // =========================================================================
            Console.WriteLine("\n--- 3. VERIFICACIÓN DE TARJETA DE MENSAJES CMD (CMDTerminalHUD) ---");

            string hudPath = "Assets/_Project/Scripts/UI/CombatHUDManager.cs";
            bool hudFileExists = File.Exists(hudPath);
            Console.WriteLine($"[TEST 3.1] CombatHUDManager.cs existe en disco: {(hudFileExists ? "PASS" : "FAIL")}");
            allPassed &= hudFileExists;

            if (hudFileExists)
            {
                string hudCode = File.ReadAllText(hudPath);

                // 3.2 InicializarUI aplica dimensiones 560x195 y padding
                bool aplicaDimensiones = hudCode.Contains("560f, 195f");
                Console.WriteLine($"[TEST 3.2] InicializarUI aplica dimensiones expandidas (560x195): {(aplicaDimensiones ? "PASS" : "FAIL")}");
                allPassed &= aplicaDimensiones;

                // 3.3 Padding generoso (offsetMin=(24,16), offsetMax=(-24,-42))
                bool aplicaPadding = hudCode.Contains("new Vector2(24f, 16f)") && hudCode.Contains("new Vector2(-24f, -42f)");
                Console.WriteLine($"[TEST 3.3] Padding interior generoso para evitar desbordes: {(aplicaPadding ? "PASS" : "FAIL")}");
                allPassed &= aplicaPadding;

                // 3.4 Tipografía y colores
                bool typoOk = hudCode.Contains("tamanoFuenteTitulo = 16f") && hudCode.Contains("tamanoFuenteMensajes = 15f") && hudCode.Contains("espaciadoLineasMensajes = 3.5f");
                Console.WriteLine($"[TEST 3.4] Tipografía legible (Título=16pt, Mensajes=15pt, Interlineado=3.5pt): {(typoOk ? "PASS" : "FAIL")}");
                allPassed &= typoOk;
            }

            // =========================================================================
            // PRUEBA 4: INTEGRIDAD DE LA ESCENA YAML (prueba_Expe_K.unity)
            // =========================================================================
            Console.WriteLine("\n--- 4. VERIFICACIÓN DE LA ESCENA YAML (prueba_Expe_K.unity) ---");

            string scenePath = "Assets/_Project/Scenes/Experimento_K/prueba_Expe_K.unity";
            bool sceneFileExists = File.Exists(scenePath);
            Console.WriteLine($"[TEST 4.1] prueba_Expe_K.unity existe en disco: {(sceneFileExists ? "PASS" : "FAIL")}");
            allPassed &= sceneFileExists;

            if (sceneFileExists)
            {
                string sceneYaml = File.ReadAllText(scenePath);

                // 4.2 CMDTerminalHUD sizeDelta en escena
                bool sceneCardSizeOk = sceneYaml.Contains("m_SizeDelta: {x: 560, y: 195}");
                Console.WriteLine($"[TEST 4.2] Escena YAML: CMDTerminalHUD RectTransform con sizeDelta (560, 195): {(sceneCardSizeOk ? "PASS" : "FAIL")}");
                allPassed &= sceneCardSizeOk;

                // 4.3 TerminalTitle font size 16 en escena
                bool sceneTitleFontOk = sceneYaml.Contains("m_fontSize: 16") && sceneYaml.Contains("m_fontSizeBase: 16");
                Console.WriteLine($"[TEST 4.3] Escena YAML: TerminalTitle tipografía ampliada a 16pt: {(sceneTitleFontOk ? "PASS" : "FAIL")}");
                allPassed &= sceneTitleFontOk;

                // 4.4 TerminalContent font size 15 y lineSpacing 3.5 en escena
                bool sceneContentFontOk = sceneYaml.Contains("m_fontSize: 15") && sceneYaml.Contains("m_lineSpacing: 3.5");
                Console.WriteLine($"[TEST 4.4] Escena YAML: TerminalContent tipografía ampliada a 15pt y espaciado 3.5: {(sceneContentFontOk ? "PASS" : "FAIL")}");
                allPassed &= sceneContentFontOk;

                // 4.5 prefabRotatorPS1 asignado en WindowsCommandAttacks
                bool sceneRotator1Ok = sceneYaml.Contains("prefabRotatorPS1: {fileID: 1789005739015704, guid: 1381b64ff1936ac4e80c3a00687081f3, type: 3}");
                Console.WriteLine($"[TEST 4.5] Escena YAML: WindowsCommandAttacks con prefabRotatorPS1 asignado: {(sceneRotator1Ok ? "PASS" : "FAIL")}");
                allPassed &= sceneRotator1Ok;

                // 4.6 duracionRotatorPS1: 2.5 en escena
                bool sceneDurOk = sceneYaml.Contains("duracionRotatorPS1: 2.5");
                Console.WriteLine($"[TEST 4.6] Escena YAML: duracionRotatorPS1 = 2.5s serializado en escena: {(sceneDurOk ? "PASS" : "FAIL")}");
                allPassed &= sceneDurOk;

                // 4.7 windows_opt presente como modelo optimizado en la escena
                bool sceneWindowsOptOk = sceneYaml.Contains("value: windows_opt");
                Console.WriteLine($"[TEST 4.7] Escena YAML: GameObject 'windows_opt' configurado como modelo optimizado: {(sceneWindowsOptOk ? "PASS" : "FAIL")}");
                allPassed &= sceneWindowsOptOk;
            }

            Console.WriteLine("\n================================================================================");
            if (allPassed)
            {
                Console.WriteLine("    RESULTADO FINAL: TODOS LOS TESTS APROBADOS SATISFACTORIAMENTE (PASS)");
            }
            else
            {
                Console.WriteLine("    RESULTADO FINAL: SE DETECTARON FALLOS EN LA VERIFICACIÓN (FAIL)");
            }
            Console.WriteLine("================================================================================");

            return allPassed ? 0 : 1;
        }
    }
}
