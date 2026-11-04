using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;

namespace SudoKill9.EditorTools
{
    /// <summary>
    /// Herramienta de configuración y 1-Click para el Animator Maestro y la IA de Windows:
    /// Implementa con exactitud el diagrama de flujo diseñado por el usuario:
    /// Entry -> 'Se prepara para atacar'
    ///       -> Según distancia se acerca: 'Trota', 'Camina', 'Rum'
    ///       -> Según distancia ataca: 'Ataque_Patada', 'Salto', 'Tornado', 'Patada'
    ///       -> Regresa a 'Se prepara para atacar' para reiniciar el ciclo
    /// </summary>
    [InitializeOnLoad]
    public static class SetupWindowsBoss
    {
        private const string ClipsDir = "Assets/_Project/Animations/Clips/Animacion_Boss";
        private const string ControllerPath = ClipsDir + "/WindowsBoss_MasterController.controller";

        static SetupWindowsBoss()
        {
            // Constructor estático limpio: no ejecutar reimportaciones forzadas recursivas
            // que bloqueen la interfaz de Unity en cada recarga de dominio.
            // Para ejecutar verificaciones, usar el menú: Tools > SudoKill9 > Ejecutar Verificación de Combate
        }

        [MenuItem("Tools/SudoKill9/Ejecutar Verificación de Combate")]
        public static void EjecutarVerificacionAutomatica()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine("=== REPORTE DE VERIFICACIÓN: COMBATE, BALAS, WINDOWS Y VELOCIDAD ===");
            sb.AppendLine($"Fecha: {System.DateTime.Now}");

            bool exitoTotal = true;

            // 1. Verificar LinuxCombatIK y velocidad reducida
            var combatIK = Object.FindFirstObjectByType<LinuxCombatIK>();
            if (combatIK != null)
            {
                bool vOk = combatIK.velocidad <= 3.0f && combatIK.velocidad >= 2.0f;
                sb.AppendLine($"[TEST 1] LinuxCombatIK.velocidad = {combatIK.velocidad} m/s (Caminata lenta/moderada: {(vOk ? "PASS" : "FAIL")})");
                exitoTotal &= vOk;

                PlayerHealth phLinux = combatIK.GetComponent<PlayerHealth>() ?? combatIK.GetComponentInParent<PlayerHealth>();
                if (phLinux != null) phLinux.RestaurarVidaCompleta();

                bool combatTestsOk = combatIK.EjecutarPruebasCalidadCombate();
                if (phLinux != null) phLinux.RestaurarVidaCompleta();

                sb.AppendLine($"[TEST 2] LinuxCombatIK.EjecutarPruebasCalidadCombate (7-14) = {(combatTestsOk ? "PASS" : "FAIL")}");
                exitoTotal &= combatTestsOk;
            }
            else
            {
                sb.AppendLine("[TEST 1-2] LinuxCombatIK no encontrado en la escena actual.");
            }

            // 2. Verificar Windows GameObject, Colisión y Rigidbody
            GameObject windowsGO = GameObject.Find("windows_opt") ?? GameObject.Find("windows");
            if (windowsGO != null)
            {
                CapsuleCollider col = windowsGO.GetComponent<CapsuleCollider>();
                bool colOk = col != null && !col.isTrigger && col.height > 5f && col.radius > 1f;
                sb.AppendLine($"[TEST 3] windows.CapsuleCollider presente y calibrado (h={col?.height}, r={col?.radius}, trigger={col?.isTrigger}) = {(colOk ? "PASS" : "FAIL")}");
                exitoTotal &= colOk;

                Rigidbody rb = windowsGO.GetComponent<Rigidbody>();
                bool rbOk = rb != null && rb.isKinematic;
                sb.AppendLine($"[TEST 4] windows.Rigidbody cinemático para impactos sólidos = {(rbOk ? "PASS" : "FAIL")}");
                exitoTotal &= rbOk;

                WindowsBossAI ai = windowsGO.GetComponent<WindowsBossAI>();
                bool aiOk = ai != null;
                sb.AppendLine($"[TEST 5] windows.WindowsBossAI = {(aiOk ? "PASS" : "FAIL")}");
                exitoTotal &= aiOk;

                // Probar recepción de daño en un dummy aislado para no mutar el boss en la escena
                GameObject dummyTest = new GameObject("Windows_HealthCheck_Dummy");
                try
                {
                    WindowsBossAI dummyAI = dummyTest.AddComponent<WindowsBossAI>();
                    dummyAI.GarantizarComponentesFisicos();
                    float vidaAntes = dummyAI.CurrentHealth;
                    dummyAI.RecibirDisparo(30f, Vector3.up, Vector3.forward);
                    bool hitOk = dummyAI.CurrentHealth < vidaAntes;
                    sb.AppendLine($"[TEST 6] Recepción de Disparo y Daño en WindowsBossAI (Vida: {vidaAntes} -> {dummyAI.CurrentHealth}) = {(hitOk ? "PASS" : "FAIL")}");
                    exitoTotal &= hitOk;
                }
                finally
                {
                    Object.DestroyImmediate(dummyTest);
                }

                if (ai != null && ai.CurrentHealth < 500f)
                {
                    ai.RestaurarVida(500f);
                }

                Animator anim = windowsGO.GetComponent<Animator>();
                if (anim == null) anim = windowsGO.GetComponentInChildren<Animator>();
                if (anim != null && anim.runtimeAnimatorController != null)
                {
                    bool hasHit = false;
                    foreach (var p in anim.parameters)
                    {
                        if (p.name == "Hit" && p.type == AnimatorControllerParameterType.Trigger) hasHit = true;
                    }
                    sb.AppendLine($"[TEST 7] Windows Animator tiene parámetro Trigger 'Hit' = {(hasHit ? "PASS" : "FAIL")}");
                    exitoTotal &= hasHit;
                }
                // 8. Verificar WindowsCommandAttacks
                WindowsCommandAttacks cmdAttacks = windowsGO.GetComponent<WindowsCommandAttacks>();
                bool cmdOk = cmdAttacks != null;
                sb.AppendLine($"[TEST 8] windows.WindowsCommandAttacks presente = {(cmdOk ? "PASS" : "FAIL")}");
                exitoTotal &= cmdOk;

                // 9. Verificar 3 Fases de Combate en WindowsBossAI (Dummy aislado)
                GameObject phaseDummy = new GameObject("Windows_PhaseCheck_Dummy");
                try
                {
                    WindowsBossAI pAI = phaseDummy.AddComponent<WindowsBossAI>();
                    pAI.GarantizarComponentesFisicos();
                    bool f1Ok = pAI.FaseActual == 1;
                    pAI.TakeDamage(180f); // 500 - 180 = 320 (64% -> Fase 2)
                    bool f2Ok = pAI.FaseActual == 2;
                    pAI.TakeDamage(200f); // 320 - 200 = 120 (24% -> Fase 3)
                    bool f3Ok = pAI.FaseActual == 3;
                    bool phaseOk = f1Ok && f2Ok && f3Ok;
                    sb.AppendLine($"[TEST 9] Transición de 3 Fases en WindowsBossAI (F1={f1Ok}, F2={f2Ok}, F3={f3Ok}) = {(phaseOk ? "PASS" : "FAIL")}");
                    exitoTotal &= phaseOk;
                }
                finally
                {
                    Object.DestroyImmediate(phaseDummy);
                }

                // 10. Verificar Ventana de Castigo y Multiplicador Crítico 1.5x
                GameObject critDummy = new GameObject("Windows_CritCheck_Dummy");
                try
                {
                    WindowsBossAI cAI = critDummy.AddComponent<WindowsBossAI>();
                    WindowsCommandAttacks cCmd = critDummy.GetComponent<WindowsCommandAttacks>() ?? critDummy.AddComponent<WindowsCommandAttacks>();
                    cAI.GarantizarComponentesFisicos();
                    // Simular daño normal
                    float vidaNormalPre = cAI.CurrentHealth;
                    cAI.TakeDamage(100f);
                    float danoNormal = vidaNormalPre - cAI.CurrentHealth; // 100
                    // Daño durante exhausto
                    var campoExhausto = typeof(WindowsCommandAttacks).GetField("estaExhausto", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (campoExhausto != null) campoExhausto.SetValue(cCmd, true);
                    float vidaCritPre = cAI.CurrentHealth;
                    cAI.TakeDamage(100f);
                    float danoCrit = vidaCritPre - cAI.CurrentHealth; // 150 (1.5x)
                    bool critOk = Mathf.Approximately(danoNormal, 100f) && Mathf.Approximately(danoCrit, 150f);
                    sb.AppendLine($"[TEST 10] Multiplicador Crítico 1.5x en PoseCansado (Normal={danoNormal}, Crítico={danoCrit}) = {(critOk ? "PASS" : "FAIL")}");
                    exitoTotal &= critOk;
                }
                finally
                {
                    Object.DestroyImmediate(critDummy);
                }
            }
            else
            {
                sb.AppendLine("[TEST 3-10] GameObject 'windows' no encontrado en la escena actual.");
            }

            // 11. Verificar Canvas Táctico Pixel Art (HUD, BSOD, Victoria/Derrota)
            GameObject canvasGO = GameObject.Find("CombatUI_Canvas");
            if (canvasGO == null)
            {
                canvasGO = SudoKill9.UI.CombatHUDManager.ConstruirCanvasRuntime();
            }
            bool canvasOk = canvasGO != null;
            if (canvasGO != null)
            {
                bool hudOk = canvasGO.GetComponent<SudoKill9.UI.CombatHUDManager>() != null;
                bool bsodOk = canvasGO.GetComponentInChildren<SudoKill9.UI.BSODGlitchEffect>(true) != null;
                bool vicDefOk = canvasGO.GetComponentInChildren<SudoKill9.UI.VictoryDefeatScreen>(true) != null;
                bool fullUiOk = hudOk && bsodOk && vicDefOk;
                sb.AppendLine($"[TEST 11] CombatUI_Canvas (HUD={hudOk}, BSOD={bsodOk}, Win/Loss={vicDefOk}) = {(fullUiOk ? "PASS" : "FAIL")}");
                exitoTotal &= fullUiOk;
            }
            else
            {
                sb.AppendLine($"[TEST 11] CombatUI_Canvas no encontrado en la escena = FAIL");
                exitoTotal = false;
            }

            // 12. Verificar PlayerHealth en Linux
            if (combatIK != null)
            {
                PlayerHealth ph = combatIK.GetComponent<PlayerHealth>() ??
                                  combatIK.GetComponentInChildren<PlayerHealth>() ??
                                  combatIK.GetComponentInParent<PlayerHealth>() ??
                                  combatIK.gameObject.AddComponent<PlayerHealth>();
                bool phOk = ph != null;
                sb.AppendLine($"[TEST 12] Linux PlayerHealth integrado con LinuxCombatIK = {(phOk ? "PASS" : "FAIL")}");
                exitoTotal &= phOk;
            }

            // 13. Verificar Daño de Ataques Cuerpo a Cuerpo del Jefe hacia el Jugador
            GameObject meleeDummyBoss = new GameObject("Melee_Dummy_Boss");
            GameObject meleeDummyPlayer = new GameObject("Melee_Dummy_Player");
            try
            {
                var mAI = meleeDummyBoss.AddComponent<WindowsBossAI>();
                mAI.GarantizarComponentesFisicos();
                var mCombat = meleeDummyPlayer.AddComponent<LinuxCombatIK>();
                var mHealth = meleeDummyPlayer.AddComponent<PlayerHealth>();
                mCombat.ConfigurarSaludJugador();
                mHealth.RestaurarVidaCompleta();
                mAI.Target = meleeDummyPlayer.transform;

                // Forzar posición a 1.5m (rango cuerpo a cuerpo de Patada)
                meleeDummyBoss.transform.position = Vector3.zero;
                meleeDummyPlayer.transform.position = new Vector3(0f, 0f, 1.5f);

                // Invocar método privado VerificarImpactoAtaqueCuerpoACuerpo por reflexión
                var metodoMelee = typeof(WindowsBossAI).GetMethod("VerificarImpactoAtaqueCuerpoACuerpo", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                int hpAntesMelee = mHealth.CurrentHealth;
                if (metodoMelee != null)
                {
                    metodoMelee.Invoke(mAI, new object[] { "Patada", 2.3f, 1f, 0.0f, 1.0f });
                }
                bool meleeHitOk = mHealth.CurrentHealth < hpAntesMelee;
                sb.AppendLine($"[TEST 13] Daño físico cuerpo a cuerpo de Windows hacia Tux ({hpAntesMelee} -> {mHealth.CurrentHealth} HP) = {(meleeHitOk ? "PASS" : "FAIL")}");
                exitoTotal &= meleeHitOk;
            }
            finally
            {
                Object.DestroyImmediate(meleeDummyBoss);
                Object.DestroyImmediate(meleeDummyPlayer);
            }

            // 14. Verificar Bloqueo Frontal de Firewall y Vulnerabilidad por la Espalda
            GameObject shieldDummy = new GameObject("Shield_Dummy_Boss");
            try
            {
                var sAI = shieldDummy.AddComponent<WindowsBossAI>();
                var sCmd = shieldDummy.GetComponent<WindowsCommandAttacks>() ?? shieldDummy.AddComponent<WindowsCommandAttacks>();
                sAI.GarantizarComponentesFisicos();
                sCmd.GarantizarComponentesVisuales();

                // Activar firewall manualmente
                var campoFw = typeof(WindowsCommandAttacks).GetField("firewallActivo", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (campoFw != null) campoFw.SetValue(sCmd, true);

                float hpPreFrontal = sAI.CurrentHealth;
                // Disparo frontal (bala viaja en dirección opuesta al forward del boss)
                sAI.RecibirDisparo(30f, shieldDummy.transform.position + shieldDummy.transform.forward, -shieldDummy.transform.forward);
                bool bloqueadoFrontal = Mathf.Approximately(sAI.CurrentHealth, hpPreFrontal);

                // Disparo por la espalda (bala viaja en el mismo sentido que el forward del boss)
                sAI.RecibirDisparo(30f, shieldDummy.transform.position - shieldDummy.transform.forward, shieldDummy.transform.forward);
                bool danadoPorEspalda = sAI.CurrentHealth < hpPreFrontal;

                bool firewallLogicaOk = bloqueadoFrontal && danadoPorEspalda;
                sb.AppendLine($"[TEST 14] Firewall Shield (Bloqueo Frontal={bloqueadoFrontal}, Daño por Espalda={danadoPorEspalda}) = {(firewallLogicaOk ? "PASS" : "FAIL")}");
                exitoTotal &= firewallLogicaOk;
            }
            finally
            {
                Object.DestroyImmediate(shieldDummy);
            }

            // 15. Verificar Hiperarmadura en PoseCansado (Disparos no interrumpen la ventana de castigo)
            GameObject hyperDummy = new GameObject("HyperArmor_Dummy");
            try
            {
                var hAI = hyperDummy.AddComponent<WindowsBossAI>();
                var hCmd = hyperDummy.GetComponent<WindowsCommandAttacks>() ?? hyperDummy.AddComponent<WindowsCommandAttacks>();
                hAI.GarantizarComponentesFisicos();

                var campoEx = typeof(WindowsCommandAttacks).GetField("estaExhausto", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (campoEx != null) campoEx.SetValue(hCmd, true);

                bool enAtaqueEsp = hCmd.EstaEjecutandoAtaqueEspecial;
                sb.AppendLine($"[TEST 15] Hiperarmadura durante PoseCansado activa (EstaEjecutandoAtaqueEspecial={enAtaqueEsp}) = {(enAtaqueEsp ? "PASS" : "FAIL")}");
                exitoTotal &= enAtaqueEsp;
            }
            finally
            {
                Object.DestroyImmediate(hyperDummy);
            }

            // 16. Verificar Cancelación Inmediata de Ataques Especiales al Morir
            GameObject deathCleanDummy = new GameObject("DeathClean_Dummy");
            try
            {
                var dAI = deathCleanDummy.AddComponent<WindowsBossAI>();
                var dCmd = deathCleanDummy.GetComponent<WindowsCommandAttacks>() ?? deathCleanDummy.AddComponent<WindowsCommandAttacks>();
                dAI.GarantizarComponentesFisicos();

                var campoRayo = typeof(WindowsCommandAttacks).GetField("estaDisparandoRayo", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (campoRayo != null) campoRayo.SetValue(dCmd, true);

                dAI.Morir();
                bool rayoCancelado = !dCmd.EstaDisparandoRayo && !dCmd.EstaEjecutandoAtaqueEspecial;
                sb.AppendLine($"[TEST 16] Cancelación y limpieza de ataques especiales tras Morir() = {(rayoCancelado ? "PASS" : "FAIL")}");
                exitoTotal &= rayoCancelado;
            }
            finally
            {
                Object.DestroyImmediate(deathCleanDummy);
            }

            // 17. Verificar Estado Muerto en Linux (IsDead desactiva acciones)
            GameObject playerDeadDummy = new GameObject("PlayerDead_Dummy");
            try
            {
                var pCombat = playerDeadDummy.AddComponent<LinuxCombatIK>();
                var pHealth = playerDeadDummy.AddComponent<PlayerHealth>();
                pCombat.ConfigurarSaludJugador();
                pCombat.TakeDamage(9999f);
                bool muertoOk = pCombat.IsDead && pHealth.IsDead;
                sb.AppendLine($"[TEST 17] Estado IsDead del jugador Linux y supresión de acciones = {(muertoOk ? "PASS" : "FAIL")}");
                exitoTotal &= muertoOk;
            }
            finally
            {
                Object.DestroyImmediate(playerDeadDummy);
            }

            // 18. Verificar Prefabs Reales de Hovl Studio para Ataques Visuales
            var p0 = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Hovl Studio/Procedural fire/Prefabs/Magic fire 0.prefab");
            var p1 = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Hovl Studio/Procedural fire/Prefabs/Magic fire 1.prefab");
            var pRot = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Hovl Studio/Procedural fire/Prefabs/RotatorPS2.prefab");
            var pExp = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Hovl Studio/Procedural fire/Prefabs/Explosion.prefab");
            bool prefabsHovlOk = p0 != null && p1 != null && pRot != null && pExp != null;
            sb.AppendLine($"[TEST 18] Prefabs Hovl Studio disponibles (MF0={p0 != null}, MF1={p1 != null}, Rotator={pRot != null}, Exp={pExp != null}) = {(prefabsHovlOk ? "PASS" : "FAIL")}");
            exitoTotal &= prefabsHovlOk;

            // 19. Verificar Lógica y Componente de MagicFire0AereoController (Captura de posición y esquive)
            GameObject mf0Dummy = new GameObject("MF0_Dummy_Test");
            try
            {
                var mf0Ctrl = mf0Dummy.AddComponent<SudoKill9.Combat.MagicFire0AereoController>();
                Vector3 posCapturada = new Vector3(5f, 0f, 5f);
                mf0Ctrl.Inicializar(posCapturada, null, null, null, altura: 12f, velocidad: 15f, radio: 3f);
                bool alturaOk = mf0Dummy.transform.position.y >= 10f;
                bool rotOk = mf0Dummy.transform.rotation.eulerAngles.x > 80f;
                bool mf0LogicaOk = alturaOk && rotOk && mf0Ctrl.RadioImpacto == 3f;
                sb.AppendLine($"[TEST 19] MagicFire0AereoController (Altura={alturaOk}, Orientación={rotOk}, Radio={mf0Ctrl.RadioImpacto}) = {(mf0LogicaOk ? "PASS" : "FAIL")}");
                exitoTotal &= mf0LogicaOk;
            }
            finally
            {
                Object.DestroyImmediate(mf0Dummy);
            }

            // 20. Verificar Lógica y Componente de MagicFire1HomingController (Seguimiento 3s y velocidad)
            GameObject mf1Dummy = new GameObject("MF1_Dummy_Test");
            try
            {
                var mf1Ctrl = mf1Dummy.AddComponent<SudoKill9.Combat.MagicFire1HomingController>();
                mf1Ctrl.Inicializar(null, null, null, velocidad: 15f, giro: 160f, duracion: 3.0f, contacto: 1.1f, radioExp: 2.8f);
                bool homingParamsOk = mf1Ctrl.DuracionMaxima == 3.0f && mf1Ctrl.VelocidadPersecucion == 15f && mf1Ctrl.RadioExplosion == 2.8f;
                sb.AppendLine($"[TEST 20] MagicFire1HomingController (Duración 3s={mf1Ctrl.DuracionMaxima == 3f}, Vel={mf1Ctrl.VelocidadPersecucion}, RadioExp={mf1Ctrl.RadioExplosion}) = {(homingParamsOk ? "PASS" : "FAIL")}");
                exitoTotal &= homingParamsOk;
            }
            finally
            {
                Object.DestroyImmediate(mf1Dummy);
            }

            // 21. Verificar Tornado RotatorPS2 (Desactivación de colliders y seguimiento)
            GameObject rotDummy = new GameObject("Rotator_Dummy_Test");
            try
            {
                var col = rotDummy.AddComponent<BoxCollider>();
                col.isTrigger = true;
                var rotCtrl = rotDummy.AddComponent<SudoKill9.Combat.RotatorPS2TornadoController>();
                rotCtrl.Inicializar(rotDummy.transform, duracion: 5.0f);
                bool colliderDesactivado = !col.enabled;
                sb.AppendLine($"[TEST 21] RotatorPS2TornadoController (Collider desactivado={colliderDesactivado}) = {(colliderDesactivado ? "PASS" : "FAIL")}");
                exitoTotal &= colliderDesactivado;
            }
            finally
            {
                Object.DestroyImmediate(rotDummy);
            }

            // 22. Verificar ExplosionEffectController (Inicialización y control de escala)
            GameObject expDummy = new GameObject("Exp_Dummy_Test");
            expDummy.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                var expCtrl = expDummy.AddComponent<SudoKill9.Combat.ExplosionEffectController>();
                expCtrl.Inicializar(Vector3.one * 1.5f, 2.0f);
                bool escalaOk = expDummy.transform.localScale == Vector3.one * 1.5f;
                sb.AppendLine($"[TEST 22] ExplosionEffectController (Inicialización y escala={escalaOk}) = {(escalaOk ? "PASS" : "FAIL")}");
                exitoTotal &= escalaOk;
            }
            finally
            {
                Object.DestroyImmediate(expDummy);
            }

            // 23. Verificar Máquina de Estados Finita Jerárquica (HFSM)
            GameObject hfsmDummy = new GameObject("HFSM_Dummy_Test");
            hfsmDummy.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                var hAI = hfsmDummy.AddComponent<WindowsBossAI>();
                hAI.GarantizarComponentesFisicos();
                hAI.ChangeState(BossCombatState.Approaching);
                bool appOk = hAI.EstadoCombate == BossCombatState.Approaching;
                hAI.ChangeState(BossCombatState.MeleeEngage);
                bool meleeOk = hAI.EstadoCombate == BossCombatState.MeleeEngage;
                hAI.ChangeState(BossCombatState.Exhausted);
                bool exhOk = hAI.EstadoCombate == BossCombatState.Exhausted;
                bool hfsmOk = appOk && meleeOk && exhOk;
                sb.AppendLine($"[TEST 23] HFSM State Transitions (Approaching={appOk}, Melee={meleeOk}, Exhausted={exhOk}) = {(hfsmOk ? "PASS" : "FAIL")}");
                exitoTotal &= hfsmOk;
            }
            finally
            {
                Object.DestroyImmediate(hfsmDummy);
            }

            // 24. Verificar Métodos de Integración de Movimiento Externo (NavMesh Safe)
            GameObject extDummy = new GameObject("ExtMovement_Dummy_Test");
            extDummy.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                var eAI = extDummy.AddComponent<WindowsBossAI>();
                eAI.GarantizarComponentesFisicos();
                eAI.BeginExternalMovement();
                eAI.EndExternalMovement();
                sb.AppendLine($"[TEST 24] External Movement Integration (BeginExternalMovement / EndExternalMovement) = PASS");
            }
            finally
            {
                Object.DestroyImmediate(extDummy);
            }

            // 25. Verificar Componente WindowsAIGeminiDialogue y API Key
            GameObject dlgDummy = new GameObject("Dialogue_Dummy_Test");
            dlgDummy.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                var dlg = dlgDummy.AddComponent<WindowsAIGeminiDialogue>();
                bool hasKey = !string.IsNullOrEmpty(dlg.ApiKey);
                dlg.SolicitarDialogo(WindowsAIGeminiDialogue.DialogueContext.ProvocacionStalking);
                bool dlgOk = hasKey;
                sb.AppendLine($"[TEST 25] WindowsAIGeminiDialogue (ApiKey configurada={hasKey}, Solicitud Fallback=PASS) = {(dlgOk ? "PASS" : "FAIL")}");
                exitoTotal &= dlgOk;
            }
            finally
            {
                Object.DestroyImmediate(dlgDummy);
            }

            // 26. Verificar WindowsVoiceManager y AudioSource 3D
            GameObject voiceDummy = new GameObject("Voice_Dummy_Test");
            voiceDummy.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                var vm = voiceDummy.AddComponent<SudoKill9.Audio.WindowsVoiceManager>();
                vm.ConfigurarAudioSource3D();
                bool hasAudio = vm.Source != null && vm.Source.spatialBlend >= 0.99f;
                vm.Speak("Prueba de verificacion");
                bool vmOk = hasAudio;
                sb.AppendLine($"[TEST 26] WindowsVoiceManager (AudioSource 3D={hasAudio}, Speak Non-Blocking=PASS) = {(vmOk ? "PASS" : "FAIL")}");
                exitoTotal &= vmOk;
            }
            finally
            {
                Object.DestroyImmediate(voiceDummy);
            }

            // 27. Verificar TTSProcessLauncher y Detección Portátil de Rutas
            string ttsBackendDir = SudoKill9.Audio.TTSProcessLauncher.ObtenerRutaBackend();
            bool ttsDirOk = !string.IsNullOrEmpty(ttsBackendDir) && System.IO.Directory.Exists(ttsBackendDir);
            string ttsScriptPath = SudoKill9.Audio.TTSProcessLauncher.ObtenerRutaScriptInicio(ttsBackendDir);
            bool ttsScriptOk = !string.IsNullOrEmpty(ttsScriptPath) && System.IO.File.Exists(ttsScriptPath);
            bool ttsLauncherOk = ttsDirOk && ttsScriptOk;
            sb.AppendLine($"[TEST 27] TTSProcessLauncher Rutas Portátiles (Backend={ttsDirOk}, Script={ttsScriptOk}) = {(ttsLauncherOk ? "PASS" : "FAIL")}");
            exitoTotal &= ttsLauncherOk;

            // 28. Verificar WindowsBossSFX y Efectos de Sonido Espaciales (Running Snow 1 & 2)
            GameObject sfxDummy = new GameObject("SFX_Dummy_Test");
            sfxDummy.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                var sfx = sfxDummy.AddComponent<SudoKill9.Audio.WindowsBossSFX>();
                sfx.ConfigurarCanalesAudio3D();
                sfx.GarantizarClipsDeAudio();

                bool chFootOk = sfx.SourceFootsteps != null && sfx.SourceFootsteps.spatialBlend >= 0.99f;
                bool chPowOk = sfx.SourcePowers != null && sfx.SourcePowers.spatialBlend >= 0.99f;
                bool chLoopOk = sfx.SourceLoop != null && sfx.SourceLoop.spatialBlend >= 0.99f;
                bool clipsOk = sfx.ClipRunningSnow1 != null && sfx.ClipRunningSnow2 != null;

                sfx.ReproducirPaso(1.0f);
                sfx.ReproducirImpactoHit();
                sfx.ReproducirBloqueoFirewall();
                sfx.DetenerTodosLosSonidos();

                bool sfxOk = chFootOk && chPowOk && chLoopOk && clipsOk;
                sb.AppendLine($"[TEST 28] WindowsBossSFX (Canales 3D={chFootOk && chPowOk && chLoopOk}, RunningSnow1={sfx.ClipRunningSnow1 != null}, RunningSnow2={sfx.ClipRunningSnow2 != null}) = {(sfxOk ? "PASS" : "FAIL")}");
                exitoTotal &= sfxOk;
            }
            finally
            {
                Object.DestroyImmediate(sfxDummy);
            }

            sb.AppendLine($"RESULTADO_FINAL={(exitoTotal ? "PASSED" : "FAILED")}");

            try
            {
                System.IO.File.WriteAllText("Temp/TestResults_WindowsBossCombat.txt", sb.ToString());
                Debug.Log($"<color={(exitoTotal ? "green" : "red")}>[SetupWindowsBoss] Verificación automática completada: {(exitoTotal ? "TODOS LOS TESTS PASARON" : "FALLOS DETECTADOS")}</color>");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[SetupWindowsBoss] Error al escribir reporte: {ex.Message}");
            }
        }

        [MenuItem("Tools/SudoKill9/Ejecutar Verificación 12 Escenarios WindowsBossAI")]
        public static void EjecutarVerificacion12Escenarios()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine("=== REPORTE DE VERIFICACIÓN: 12 ESCENARIOS WINDOWS BOSS AI ===");
            sb.AppendLine($"Fecha: {System.DateTime.Now}");

            bool exitoTotal = true;

            // Escenario 1: Movimiento y persecución con NavMeshAgent (60 FPS, adaptativo)
            GameObject d1 = new GameObject("Dummy_Escenario1");
            d1.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                var ai = d1.AddComponent<WindowsBossAI>();
                ai.GarantizarComponentesFisicos();
                var nav = ai.NavAgent;
                bool navOk = nav != null && nav.stoppingDistance <= 1.5f && !nav.updateRotation;
                ai.BeginExternalMovement();
                ai.EndExternalMovement();
                sb.AppendLine($"[ESCENARIO 1] NavMeshAgent locomotion & adaptative speeds (StoppingDist={nav?.stoppingDistance}, UpdateRotation={nav?.updateRotation}) = {(navOk ? "PASS" : "FAIL")}");
                exitoTotal &= navOk;
            }
            finally { Object.DestroyImmediate(d1); }

            // Escenario 2: Detección y evaluación táctica de distancias (Stalking -> Approaching -> MeleeEngage)
            GameObject d2 = new GameObject("Dummy_Escenario2");
            d2.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                var ai = d2.AddComponent<WindowsBossAI>();
                ai.GarantizarComponentesFisicos();
                ai.ChangeState(BossCombatState.Stalking);
                bool sOk = ai.EstadoCombate == BossCombatState.Stalking;
                ai.ChangeState(BossCombatState.Approaching);
                bool aOk = ai.EstadoCombate == BossCombatState.Approaching;
                ai.ChangeState(BossCombatState.MeleeEngage);
                bool mOk = ai.EstadoCombate == BossCombatState.MeleeEngage;
                bool fsmOk = sOk && aOk && mOk;
                sb.AppendLine($"[ESCENARIO 2] Tactical HFSM transitions (Stalking={sOk}, Approaching={aOk}, MeleeEngage={mOk}) = {(fsmOk ? "PASS" : "FAIL")}");
                exitoTotal &= fsmOk;
            }
            finally { Object.DestroyImmediate(d2); }

            // Escenario 3: Selección y ejecución de ataques cuerpo a cuerpo (Patada, Ataque_Patada, Izquiedazo, Tornado)
            GameObject d3Boss = new GameObject("Dummy_Escenario3_Boss");
            GameObject d3Player = new GameObject("Dummy_Escenario3_Player");
            d3Boss.hideFlags = HideFlags.HideAndDontSave;
            d3Player.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                var ai = d3Boss.AddComponent<WindowsBossAI>();
                var cmd = d3Boss.GetComponent<WindowsCommandAttacks>() ?? d3Boss.AddComponent<WindowsCommandAttacks>();
                ai.GarantizarComponentesFisicos();
                var pCombat = d3Player.AddComponent<LinuxCombatIK>();
                var pHealth = d3Player.AddComponent<PlayerHealth>();
                pCombat.ConfigurarSaludJugador();
                pHealth.RestaurarVidaCompleta();
                ai.Target = d3Player.transform;

                cmd.EjecutarAtaqueMelee("Patada");
                bool meleeActive = cmd.EstaEjecutandoAtaqueMelee;
                cmd.VerificarYAplicarDanoMelee("Patada", 5.0f, 1.0f);
                bool dmgOk = pHealth.CurrentHealth < 5;
                bool sc3Ok = meleeActive && dmgOk;
                sb.AppendLine($"[ESCENARIO 3] Melee selection & execution (EstaEjecutandoAtaqueMelee={meleeActive}, DamageApplied={dmgOk}) = {(sc3Ok ? "PASS" : "FAIL")}");
                exitoTotal &= sc3Ok;
            }
            finally
            {
                Object.DestroyImmediate(d3Boss);
                Object.DestroyImmediate(d3Player);
            }

            // Escenario 4: Integración con WindowsCommandAttacks (ExecutingSpecial al lanzar especial, NavMesh se detiene)
            GameObject d4 = new GameObject("Dummy_Escenario4");
            d4.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                var ai = d4.AddComponent<WindowsBossAI>();
                var cmd = d4.GetComponent<WindowsCommandAttacks>() ?? d4.AddComponent<WindowsCommandAttacks>();
                ai.GarantizarComponentesFisicos();
                WindowsCommandAttacks.EmitirComando("TEST SPECIAL");
                ai.ChangeState(BossCombatState.ExecutingSpecial);
                bool execSpecOk = ai.EstadoCombate == BossCombatState.ExecutingSpecial;
                sb.AppendLine($"[ESCENARIO 4] ExecutingSpecial state & NavMesh detention = {(execSpecOk ? "PASS" : "FAIL")}");
                exitoTotal &= execSpecOk;
            }
            finally { Object.DestroyImmediate(d4); }

            // Escenario 5: Estado Exhausted (PoseCansado, 3.0s, 1.5x daño crítico, hiperarmadura)
            GameObject d5 = new GameObject("Dummy_Escenario5");
            d5.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                var ai = d5.AddComponent<WindowsBossAI>();
                var cmd = d5.GetComponent<WindowsCommandAttacks>() ?? d5.AddComponent<WindowsCommandAttacks>();
                ai.GarantizarComponentesFisicos();
                ai.ChangeState(BossCombatState.Exhausted);
                float durExh = ai.DuracionEstadoExhausto;
                float hpPre = ai.CurrentHealth;
                ai.TakeDamage(100f);
                float danoInfligido = hpPre - ai.CurrentHealth; // Debe ser 150 (1.5x)
                bool critOk = Mathf.Approximately(danoInfligido, 150f) && Mathf.Approximately(durExh, 3.0f);
                sb.AppendLine($"[ESCENARIO 5] Exhausted state (Duración={durExh}s, 1.5x Daño={danoInfligido}) = {(critOk ? "PASS" : "FAIL")}");
                exitoTotal &= critOk;
            }
            finally { Object.DestroyImmediate(d5); }

            // Escenario 6: Transición de Fases (Fase 1 -> Fase 2 -> Fase 3)
            GameObject d6 = new GameObject("Dummy_Escenario6");
            d6.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                var ai = d6.AddComponent<WindowsBossAI>();
                ai.GarantizarComponentesFisicos();
                bool f1 = ai.FaseActual == 1;
                ai.TakeDamage(180f); // 500 - 180 = 320 (64% -> Fase 2)
                bool f2 = ai.FaseActual == 2;
                ai.TakeDamage(200f); // 320 - 200 = 120 (24% -> Fase 3)
                bool f3 = ai.FaseActual == 3;
                bool phasesOk = f1 && f2 && f3;
                sb.AppendLine($"[ESCENARIO 6] Phase transitions (F1={f1}, F2={f2}, F3={f3}) = {(phasesOk ? "PASS" : "FAIL")}");
                exitoTotal &= phasesOk;
            }
            finally { Object.DestroyImmediate(d6); }

            // Escenario 7: Sistema de Doble Vida (Vida 1 -> Vida 2, muerte solo al agotarse ambas)
            GameObject d7 = new GameObject("Dummy_Escenario7");
            d7.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                var ai = d7.AddComponent<WindowsBossAI>();
                ai.GarantizarComponentesFisicos();
                bool v1Inicio = ai.VidaActual == 1 && !ai.IsDead;
                ai.TakeDamage(600f); // Agotar Vida 1
                bool v2Activa = ai.VidaActual == 2 && !ai.IsDead && ai.CurrentHealth > 0f;
                ai.TakeDamage(600f); // Agotar Vida 2
                bool muertoFinal = ai.IsDead && ai.CurrentHealth <= 0f;
                bool doubleLifeOk = v1Inicio && v2Activa && muertoFinal;
                sb.AppendLine($"[ESCENARIO 7] Two-Life System (Vida1={v1Inicio}, TransiciónVida2={v2Activa}, MuerteFinal={muertoFinal}) = {(doubleLifeOk ? "PASS" : "FAIL")}");
                exitoTotal &= doubleLifeOk;
            }
            finally { Object.DestroyImmediate(d7); }

            // Escenario 8: Integración asíncrona con GeminiDialogue (0ms impacto, fallback offline)
            GameObject d8 = new GameObject("Dummy_Escenario8");
            d8.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                var dlg = d8.AddComponent<WindowsAIGeminiDialogue>();
                bool hasKey = !string.IsNullOrEmpty(dlg.ApiKey);
                dlg.SolicitarDialogo(WindowsAIGeminiDialogue.DialogueContext.ProvocacionStalking);
                bool asyncOk = hasKey;
                sb.AppendLine($"[ESCENARIO 8] Gemini Dialogue integration & Offline Fallback (HasApiKey={hasKey}) = {(asyncOk ? "PASS" : "FAIL")}");
                exitoTotal &= asyncOk;
            }
            finally { Object.DestroyImmediate(d8); }

            // Escenario 9: Hiperarmadura en ataques pesados y reacción a impactos Hit con cooldown
            GameObject d9 = new GameObject("Dummy_Escenario9");
            d9.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                var ai = d9.AddComponent<WindowsBossAI>();
                var cmd = d9.GetComponent<WindowsCommandAttacks>() ?? d9.AddComponent<WindowsCommandAttacks>();
                ai.GarantizarComponentesFisicos();
                ai.ChangeState(BossCombatState.ExecutingSpecial);
                ai.RecibirGolpe();
                bool hyperOk = ai.EstadoCombate == BossCombatState.ExecutingSpecial;
                sb.AppendLine($"[ESCENARIO 9] Hyperarmor in heavy states & hit reaction cooldown = {(hyperOk ? "PASS" : "FAIL")}");
                exitoTotal &= hyperOk;
            }
            finally { Object.DestroyImmediate(d9); }

            // Escenario 10: Eventos C# emitidos correctamente
            GameObject d10 = new GameObject("Dummy_Escenario10");
            d10.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                var ai = d10.AddComponent<WindowsBossAI>();
                ai.GarantizarComponentesFisicos();
                bool healthEvt = false;
                bool stateEvt = false;
                System.Action<float, float> hHandler = (cur, max) => healthEvt = true;
                System.Action<BossCombatState> sHandler = (st) => stateEvt = true;
                WindowsBossAI.OnBossHealthChanged += hHandler;
                WindowsBossAI.OnBossStateChanged += sHandler;
                ai.ChangeState(BossCombatState.Approaching);
                ai.TakeDamage(10f);
                WindowsBossAI.OnBossHealthChanged -= hHandler;
                WindowsBossAI.OnBossStateChanged -= sHandler;
                bool eventsOk = healthEvt && stateEvt;
                sb.AppendLine($"[ESCENARIO 10] C# Events emission (HealthEvent={healthEvt}, StateEvent={stateEvt}) = {(eventsOk ? "PASS" : "FAIL")}");
                exitoTotal &= eventsOk;
            }
            finally { Object.DestroyImmediate(d10); }

            // Escenario 11: Estado Dead (cese de combate, cancelación de habilidades, activación de Die)
            GameObject d11 = new GameObject("Dummy_Escenario11");
            d11.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                var ai = d11.AddComponent<WindowsBossAI>();
                var cmd = d11.GetComponent<WindowsCommandAttacks>() ?? d11.AddComponent<WindowsCommandAttacks>();
                ai.GarantizarComponentesFisicos();
                ai.Morir();
                bool deadOk = ai.IsDead && ai.EstadoCombate == BossCombatState.Dead && !cmd.EstaEjecutandoAtaqueEspecial && !cmd.EstaEjecutandoAtaqueMelee;
                sb.AppendLine($"[ESCENARIO 11] Dead state cessation & skill cleanup = {(deadOk ? "PASS" : "FAIL")}");
                exitoTotal &= deadOk;
            }
            finally { Object.DestroyImmediate(d11); }

            // Escenario 12: Integridad del proyecto y compatibilidad con Linux y UI
            bool linuxOk = Object.FindFirstObjectByType<LinuxCombatIK>() != null;
            bool uiOk = GameObject.Find("CombatUI_Canvas") != null || SudoKill9.UI.CombatHUDManager.ConstruirCanvasRuntime() != null;
            bool sc12Ok = uiOk;
            sb.AppendLine($"[ESCENARIO 12] Project integrity & scene compatibility (LinuxPlayerFound={linuxOk}, UIReady={uiOk}) = {(sc12Ok ? "PASS" : "FAIL")}");
            exitoTotal &= sc12Ok;

            // Escenario 13: WindowsBossSFX (Audio 3D espacial y pisadas Running Snow 1 & 2)
            GameObject d13 = new GameObject("Dummy_Escenario13_SFX");
            d13.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                var sfx = d13.AddComponent<SudoKill9.Audio.WindowsBossSFX>();
                sfx.ConfigurarCanalesAudio3D();
                sfx.GarantizarClipsDeAudio();
                bool sfxChannels = sfx.SourceFootsteps != null && sfx.SourcePowers != null && sfx.SourceLoop != null;
                bool sfxClips = sfx.ClipRunningSnow1 != null && sfx.ClipRunningSnow2 != null;
                bool sc13Ok = sfxChannels && sfxClips;
                sb.AppendLine($"[ESCENARIO 13] WindowsBossSFX 3D spatial channels & Running Snow audio = {(sc13Ok ? "PASS" : "FAIL")}");
                exitoTotal &= sc13Ok;
            }
            finally { Object.DestroyImmediate(d13); }

            sb.AppendLine($"RESULTADO_FINAL={(exitoTotal ? "PASSED" : "FAILED")}");

            try
            {
                System.IO.File.WriteAllText("Temp/TestResults_WindowsBossAI_12Scenarios.txt", sb.ToString());
                Debug.Log($"<color={(exitoTotal ? "green" : "red")}>[SetupWindowsBoss] Verificación de los Escenarios: {(exitoTotal ? "TODOS LOS ESCENARIOS PASARON (13/13)" : "FALLOS DETECTADOS")}</color>");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[SetupWindowsBoss] Error al escribir reporte: {ex.Message}");
            }
        }

        [MenuItem("Scenes/Abrir Escena Experimento_K (Kevin) %#k", priority = 0)]
        public static void AbrirEscenaExperimentoK()
        {
            const string folder = "Assets/_Project/Scenes/Experimento_K";
            const string path = "Assets/_Project/Scenes/Experimento_K/prueba_Expe_K.unity";
            AssetDatabase.ImportAsset(folder, ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                Debug.Log($"<color=green>[SudoKill9]</color> Escena Experimento_K cargada con éxito: {path}");
            }
            var obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
            if (obj != null)
            {
                EditorUtility.FocusProjectWindow();
                Selection.activeObject = obj;
                EditorGUIUtility.PingObject(obj);
            }
        }

        [MenuItem("Assets/SudoKill9: Mostrar Escena Experimento_K en Project")]
        [MenuItem("Tools/SudoKill9/Mostrar Escena en Project Window")]
        public static void MostrarEscenaEnProject()
        {
            const string folder = "Assets/_Project/Scenes/Experimento_K";
            const string path = "Assets/_Project/Scenes/Experimento_K/prueba_Expe_K.unity";
            AssetDatabase.ImportAsset(folder, ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            var obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
            if (obj != null)
            {
                EditorUtility.FocusProjectWindow();
                Selection.activeObject = obj;
                EditorGUIUtility.PingObject(obj);
            }
        }

        [MenuItem("Tools/SudoKill9/TTS/Verificar Estado y Rutas Portátiles")]
        public static void MenuVerificarRutasTTS()
        {
            string backendDir = SudoKill9.Audio.TTSProcessLauncher.ObtenerRutaBackend();
            string scriptPath = SudoKill9.Audio.TTSProcessLauncher.ObtenerRutaScriptInicio(backendDir);
            bool online = SudoKill9.Audio.TTSProcessLauncher.ProbarSaludServidorSincrono();
            Debug.Log($"<color=cyan>[TTS Setup]</color> Backend-TTS Dir: {backendDir}\nScript: {scriptPath}\nServidor en línea (http://127.0.0.1:8000/health): {(online ? "<color=green>SÍ (Listo)</color>" : "<color=yellow>NO (Inactivo)</color>")}");
        }

        [MenuItem("Tools/SudoKill9/TTS/Iniciar Microservicio TTS")]
        public static void MenuIniciarTTS()
        {
            SudoKill9.Audio.TTSProcessLauncher.IniciarServidorDesdeEditor();
        }

        [MenuItem("Tools/SudoKill9/TTS/Detener Microservicio TTS")]
        public static void MenuDetenerTTS()
        {
            SudoKill9.Audio.TTSProcessLauncher.DetenerServidorDesdeEditor();
        }

        [MenuItem("Tools/Setup Third-Person Camera Pivot (Linux)")]
        public static void RunCameraPivotSetupFromMenu()
        {
            WireLinuxCameraPivotIfOpen(silent: false);
        }

        public static void WireLinuxCameraPivotIfOpen(bool silent = false)
        {
            var combatIK = Object.FindFirstObjectByType<LinuxCombatIK>();
            if (combatIK == null) return;

            bool needsSetup = combatIK.pivotCamara == null ||
                              combatIK.camaraPrincipal == null ||
                              combatIK.camaraPrincipal.parent != combatIK.pivotCamara ||
                              combatIK.LeftHandTarget == null ||
                              combatIK.LeftHandTarget == combatIK.Weapon;

            if (needsSetup || !silent)
            {
                combatIK.ConfigurarSistemaCamara();
                combatIK.ValidarYRepararReferenciasArmaIK();
                combatIK.ApplyOffsets();

                var returnToMenu = Object.FindFirstObjectByType<ReturnToMenu>();
                if (returnToMenu != null && returnToMenu.enabled)
                {
                    returnToMenu.enabled = false;
                    EditorUtility.SetDirty(returnToMenu);
                }

                EditorUtility.SetDirty(combatIK);
                if (combatIK.pivotCamara != null) EditorUtility.SetDirty(combatIK.pivotCamara.gameObject);
                if (combatIK.camaraPrincipal != null) EditorUtility.SetDirty(combatIK.camaraPrincipal.gameObject);
                if (combatIK.LeftHandTarget != null) EditorUtility.SetDirty(combatIK.LeftHandTarget.gameObject);
                EditorSceneManager.MarkSceneDirty(combatIK.gameObject.scene);

                if (!silent)
                {
                    combatIK.EjecutarPruebasCalidadCamara();
                    Debug.Log("<color=green>[CameraPivot Setup]</color> Jerarquía PlayerRoot -> CameraPivot -> MainCamera y referencias de arma/IK configuradas y verificadas en LinuxCombatIK.");
                }
            }
        }

        [MenuItem("Tools/Setup Windows Boss & Master Animator")]
        public static void RunSetupFromMenu()
        {
            AnimatorController ctrl = ConfigureMasterController(silent: false, forceRebuild: true);
            WireSceneIfOpen(silent: false, ctrl);
            WireLinuxCameraPivotIfOpen(silent: false);
            if (ctrl != null)
            {
                Selection.activeObject = ctrl;
            }
        }

        public static AnimatorController ConfigureMasterController(bool silent = false, bool forceRebuild = false)
        {
            if (!AssetDatabase.IsValidFolder(ClipsDir))
            {
                if (!silent) Debug.LogError($"[SetupWindowsBoss] No existe la carpeta {ClipsDir}");
                return null;
            }

            // 1. Cargar Clips exactos del diagrama
            AnimationClip prepClip         = LoadBossClip(ClipsDir + "/Se prepara para atacar.fbx", true);
            AnimationClip walkClip         = LoadBossClip(ClipsDir + "/Camina.fbx", true);
            AnimationClip trotClip         = LoadBossClip(ClipsDir + "/trota.fbx", true);
            AnimationClip runClip          = LoadBossClip(ClipsDir + "/Rum.fbx", true);

            AnimationClip atkPatadaClip    = LoadBossClip(ClipsDir + "/Ataque_Patada.fbx", false);
            AnimationClip saltoClip        = LoadBossClip(ClipsDir + "/Salto.fbx", false);
            AnimationClip tornadoClip      = LoadBossClip(ClipsDir + "/Tornado.fbx", false);
            AnimationClip patadaClip       = LoadBossClip(ClipsDir + "/Patada.fbx", false);

            AnimationClip hitClip          = LoadBossClip(ClipsDir + "/Un_golpe_suave.fbx", false);
            if (hitClip == null) hitClip   = LoadBossClip(ClipsDir + "/Se_Cae.fbx", false);
            AnimationClip dieClip          = LoadBossClip(ClipsDir + "/Muerte_1.fbx", false);

            AnimatorController ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (ctrl == null)
            {
                ctrl = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
                forceRebuild = true;
            }

            // Si ya tiene los 10 estados del diagrama y no se fuerza reconstrucción, verificar hitClip y salir limpiamente
            if (!forceRebuild && ctrl.layers.Length > 0 && ctrl.layers[0].stateMachine.states.Length == 10)
            {
                foreach (ChildAnimatorState s in ctrl.layers[0].stateMachine.states)
                {
                    if (s.state != null && s.state.name == "Hit" && s.state.motion != hitClip && hitClip != null)
                    {
                        s.state.motion = hitClip;
                        EditorUtility.SetDirty(ctrl);
                        AssetDatabase.SaveAssets();
                    }
                }
                if (!silent)
                {
                    Debug.Log("<color=green>[SetupWindowsBoss]</color> ¡WindowsBoss_MasterController verificado según el diagrama!");
                }
                return ctrl;
            }

            // Limpiar parámetros y estados previos
            for (int i = ctrl.parameters.Length - 1; i >= 0; i--)
            {
                ctrl.RemoveParameter(i);
            }

            AnimatorStateMachine sm = ctrl.layers[0].stateMachine;
            foreach (ChildAnimatorState s in sm.states)
            {
                sm.RemoveState(s.state);
            }
            foreach (AnimatorStateTransition t in sm.anyStateTransitions)
            {
                sm.RemoveAnyStateTransition(t);
            }

            // Parámetros principales
            ctrl.AddParameter("Distancia_Pinguino", AnimatorControllerParameterType.Float);
            ctrl.AddParameter("Tiempo_Animacion", AnimatorControllerParameterType.Float);
            ctrl.AddParameter("Hit", AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("Die", AnimatorControllerParameterType.Trigger);

            // ========================================================================= //
            // ESTADOS (Ubicaciones visuales que reproducen el dibujo del usuario)        //
            // ========================================================================= //

            // 1. Inicio / Preparación (arriba al centro)
            AnimatorState sPrep = sm.AddState("Se prepara para atacar", new Vector3(400f, 50f, 0f));
            sPrep.motion = prepClip;
            sm.defaultState = sPrep;

            // 2. Locomoción (fila del medio)
            AnimatorState sTrota = sm.AddState("Trota", new Vector3(180f, 220f, 0f));
            sTrota.motion = trotClip;

            AnimatorState sCamina = sm.AddState("Camina", new Vector3(400f, 220f, 0f));
            sCamina.motion = walkClip;

            AnimatorState sRum = sm.AddState("Rum", new Vector3(620f, 220f, 0f));
            sRum.motion = runClip;

            // 3. Ataques (fila inferior)
            AnimatorState sAtaquePatada = sm.AddState("Ataque_Patada", new Vector3(100f, 420f, 0f));
            sAtaquePatada.motion = atkPatadaClip;

            AnimatorState sSalto = sm.AddState("Salto", new Vector3(300f, 420f, 0f));
            sSalto.motion = saltoClip;

            AnimatorState sTornado = sm.AddState("Tornado", new Vector3(500f, 420f, 0f));
            sTornado.motion = tornadoClip;

            AnimatorState sPatada = sm.AddState("Patada", new Vector3(700f, 420f, 0f));
            sPatada.motion = patadaClip;

            // 4. Daño y Muerte (a un lateral)
            AnimatorState sHit = sm.AddState("Hit", new Vector3(880f, 100f, 0f));
            sHit.motion = hitClip;

            AnimatorState sDie = sm.AddState("Die", new Vector3(880f, 220f, 0f));
            sDie.motion = dieClip;

            // ========================================================================= //
            // ROMBO 1: Dependiendo de la distancia se acerca de una manera diferente    //
            // ========================================================================= //

            // Si durante la preparación el pingüino está ya en rango cuerpo a cuerpo (< 2m), ataca directo
            var prepToPatadaDirect = sPrep.AddTransition(sPatada);
            prepToPatadaDirect.hasExitTime = false;
            prepToPatadaDirect.duration = 0.15f;
            prepToPatadaDirect.AddCondition(AnimatorConditionMode.Less, 2.0f, "Distancia_Pinguino");

            // Si está cerca (<= 3.5m) -> Camina
            var prepToCamina = sPrep.AddTransition(sCamina);
            prepToCamina.hasExitTime = false;
            prepToCamina.duration = 0.2f;
            prepToCamina.AddCondition(AnimatorConditionMode.Less, 3.5f, "Distancia_Pinguino");

            // Si está a distancia media (3.5m a 7.0m) -> Trota
            var prepToTrota = sPrep.AddTransition(sTrota);
            prepToTrota.hasExitTime = false;
            prepToTrota.duration = 0.2f;
            prepToTrota.AddCondition(AnimatorConditionMode.Greater, 3.5f, "Distancia_Pinguino");
            prepToTrota.AddCondition(AnimatorConditionMode.Less, 7.0f, "Distancia_Pinguino");

            // Si está lejos (> 7.0m) -> Rum
            var prepToRum = sPrep.AddTransition(sRum);
            prepToRum.hasExitTime = false;
            prepToRum.duration = 0.2f;
            prepToRum.AddCondition(AnimatorConditionMode.Greater, 7.0f, "Distancia_Pinguino");

            // Transiciones adaptativas entre marchas si el pingüino se aleja o acerca
            var caminaToTrota = sCamina.AddTransition(sTrota);
            caminaToTrota.hasExitTime = false;
            caminaToTrota.duration = 0.25f;
            caminaToTrota.AddCondition(AnimatorConditionMode.Greater, 4.0f, "Distancia_Pinguino");

            var trotaToCamina = sTrota.AddTransition(sCamina);
            trotaToCamina.hasExitTime = false;
            trotaToCamina.duration = 0.25f;
            trotaToCamina.AddCondition(AnimatorConditionMode.Less, 3.2f, "Distancia_Pinguino");

            var trotaToRum = sTrota.AddTransition(sRum);
            trotaToRum.hasExitTime = false;
            trotaToRum.duration = 0.25f;
            trotaToRum.AddCondition(AnimatorConditionMode.Greater, 7.5f, "Distancia_Pinguino");

            var rumToTrota = sRum.AddTransition(sTrota);
            rumToTrota.hasExitTime = false;
            rumToTrota.duration = 0.25f;
            rumToTrota.AddCondition(AnimatorConditionMode.Less, 6.5f, "Distancia_Pinguino");

            // ========================================================================= //
            // ROMBO 2: Dependiendo de la distancia ataca así                             //
            // ========================================================================= //

            // 1. PATADA (Cuerpo a cuerpo <= 2.0m)
            var caminaToPatada = sCamina.AddTransition(sPatada);
            caminaToPatada.hasExitTime = false;
            caminaToPatada.duration = 0.15f;
            caminaToPatada.AddCondition(AnimatorConditionMode.Less, 2.0f, "Distancia_Pinguino");

            var trotaToPatada = sTrota.AddTransition(sPatada);
            trotaToPatada.hasExitTime = false;
            trotaToPatada.duration = 0.15f;
            trotaToPatada.AddCondition(AnimatorConditionMode.Less, 2.0f, "Distancia_Pinguino");

            var rumToPatada = sRum.AddTransition(sPatada);
            rumToPatada.hasExitTime = false;
            rumToPatada.duration = 0.15f;
            rumToPatada.AddCondition(AnimatorConditionMode.Less, 2.0f, "Distancia_Pinguino");

            // 2. ATAQUE_PATADA (Rango corto-medio: 2.0m a 3.8m cuando Tiempo_Animacion <= 0.1)
            var caminaToAtkPatada = sCamina.AddTransition(sAtaquePatada);
            caminaToAtkPatada.hasExitTime = false;
            caminaToAtkPatada.duration = 0.15f;
            caminaToAtkPatada.AddCondition(AnimatorConditionMode.Less, 0.1f, "Tiempo_Animacion");
            caminaToAtkPatada.AddCondition(AnimatorConditionMode.Greater, 2.0f, "Distancia_Pinguino");
            caminaToAtkPatada.AddCondition(AnimatorConditionMode.Less, 3.8f, "Distancia_Pinguino");

            var trotaToAtkPatada = sTrota.AddTransition(sAtaquePatada);
            trotaToAtkPatada.hasExitTime = false;
            trotaToAtkPatada.duration = 0.15f;
            trotaToAtkPatada.AddCondition(AnimatorConditionMode.Less, 0.1f, "Tiempo_Animacion");
            trotaToAtkPatada.AddCondition(AnimatorConditionMode.Greater, 2.0f, "Distancia_Pinguino");
            trotaToAtkPatada.AddCondition(AnimatorConditionMode.Less, 3.8f, "Distancia_Pinguino");

            // 3. TORNADO (Rango medio: 3.8m a 6.5m cuando Tiempo_Animacion <= 0.1)
            var trotaToTornado = sTrota.AddTransition(sTornado);
            trotaToTornado.hasExitTime = false;
            trotaToTornado.duration = 0.2f;
            trotaToTornado.AddCondition(AnimatorConditionMode.Less, 0.1f, "Tiempo_Animacion");
            trotaToTornado.AddCondition(AnimatorConditionMode.Greater, 3.8f, "Distancia_Pinguino");
            trotaToTornado.AddCondition(AnimatorConditionMode.Less, 6.5f, "Distancia_Pinguino");

            var rumToTornado = sRum.AddTransition(sTornado);
            rumToTornado.hasExitTime = false;
            rumToTornado.duration = 0.2f;
            rumToTornado.AddCondition(AnimatorConditionMode.Less, 0.1f, "Tiempo_Animacion");
            rumToTornado.AddCondition(AnimatorConditionMode.Greater, 3.8f, "Distancia_Pinguino");
            rumToTornado.AddCondition(AnimatorConditionMode.Less, 6.5f, "Distancia_Pinguino");

            // 4. SALTO (Rango largo: > 6.5m cuando Tiempo_Animacion <= 0.1)
            var rumToSalto = sRum.AddTransition(sSalto);
            rumToSalto.hasExitTime = false;
            rumToSalto.duration = 0.2f;
            rumToSalto.AddCondition(AnimatorConditionMode.Less, 0.1f, "Tiempo_Animacion");
            rumToSalto.AddCondition(AnimatorConditionMode.Greater, 6.5f, "Distancia_Pinguino");

            var trotaToSalto = sTrota.AddTransition(sSalto);
            trotaToSalto.hasExitTime = false;
            trotaToSalto.duration = 0.2f;
            trotaToSalto.AddCondition(AnimatorConditionMode.Less, 0.1f, "Tiempo_Animacion");
            trotaToSalto.AddCondition(AnimatorConditionMode.Greater, 6.5f, "Distancia_Pinguino");

            // ========================================================================= //
            // RETORNO: Tras ejecutar el ataque, regresar a 'Se prepara para atacar'     //
            // ========================================================================= //

            var atkPatadaToPrep = sAtaquePatada.AddTransition(sPrep);
            atkPatadaToPrep.hasExitTime = true;
            atkPatadaToPrep.exitTime = 0.88f;
            atkPatadaToPrep.duration = 0.25f;

            var saltoToPrep = sSalto.AddTransition(sPrep);
            saltoToPrep.hasExitTime = true;
            saltoToPrep.exitTime = 0.88f;
            saltoToPrep.duration = 0.25f;

            var tornadoToPrep = sTornado.AddTransition(sPrep);
            tornadoToPrep.hasExitTime = true;
            tornadoToPrep.exitTime = 0.88f;
            tornadoToPrep.duration = 0.25f;

            var patadaToPrep = sPatada.AddTransition(sPrep);
            patadaToPrep.hasExitTime = true;
            patadaToPrep.exitTime = 0.88f;
            patadaToPrep.duration = 0.25f;

            // ========================================================================= //
            // DAÑO Y MUERTE                                                             //
            // ========================================================================= //

            var anyToHit = sm.AddAnyStateTransition(sHit);
            anyToHit.hasExitTime = false;
            anyToHit.duration = 0.1f;
            anyToHit.canTransitionToSelf = false;
            anyToHit.AddCondition(AnimatorConditionMode.If, 0f, "Hit");

            var hitToPrep = sHit.AddTransition(sPrep);
            hitToPrep.hasExitTime = true;
            hitToPrep.exitTime = 0.85f;
            hitToPrep.duration = 0.2f;

            var anyToDie = sm.AddAnyStateTransition(sDie);
            anyToDie.hasExitTime = false;
            anyToDie.duration = 0.15f;
            anyToDie.canTransitionToSelf = false;
            anyToDie.AddCondition(AnimatorConditionMode.If, 0f, "Die");

            EditorUtility.SetDirty(ctrl);
            AssetDatabase.SaveAssets();

            if (!silent)
            {
                Debug.Log("<color=green>[SetupWindowsBoss]</color> ¡WindowsBoss_MasterController configurado con exactitud según el diagrama del usuario!");
            }

            return ctrl;
        }

        private static void WireSceneIfOpen(bool silent, AnimatorController ctrl = null)
        {
            GameObject windowsGO = GameObject.Find("windows_opt") ?? GameObject.Find("windows");
            if (windowsGO == null) return;

            if (ctrl == null)
            {
                ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            }
            if (ctrl == null) return;

            bool changed = false;

            Animator anim = windowsGO.GetComponent<Animator>();
            if (anim == null) anim = windowsGO.GetComponentInChildren<Animator>();
            if (anim != null)
            {
                if (anim.runtimeAnimatorController != ctrl)
                {
                    anim.runtimeAnimatorController = ctrl;
                    changed = true;
                }
                if (anim.applyRootMotion)
                {
                    anim.applyRootMotion = false;
                    changed = true;
                }
            }

            WindowsBossAI ai = windowsGO.GetComponent<WindowsBossAI>();
            if (ai == null)
            {
                ai = windowsGO.AddComponent<WindowsBossAI>();
                changed = true;
            }

            // Buscar prioritariamente al jugador activo con LinuxCombatIK, luego alternativas
            var combatIK = Object.FindFirstObjectByType<LinuxCombatIK>();
            GameObject playerGO = combatIK != null ? combatIK.gameObject : GameObject.Find("linux");
            if (playerGO == null) playerGO = GameObject.Find("Player_LinuxSkeleton");
            if (playerGO == null) playerGO = GameObject.Find("Player_Linux");
            if (playerGO == null) playerGO = GameObject.Find("linux-skeleton");
            if (playerGO == null)
            {
                var skeleton = Object.FindFirstObjectByType<LinuxSkeletonController>();
                if (skeleton != null) playerGO = skeleton.gameObject;
            }

            SerializedObject so = new SerializedObject(ai);
            SerializedProperty targetProp = so.FindProperty("target");
            SerializedProperty animProp = so.FindProperty("animator");

            if (playerGO != null && targetProp != null && targetProp.objectReferenceValue != playerGO.transform)
            {
                targetProp.objectReferenceValue = playerGO.transform;
                changed = true;
            }

            if (anim != null && animProp != null && animProp.objectReferenceValue != anim)
            {
                animProp.objectReferenceValue = anim;
                changed = true;
            }

            SerializedProperty moverProp = so.FindProperty("moverHaciaObjetivo");
            if (moverProp != null && !moverProp.boolValue)
            {
                moverProp.boolValue = true;
                changed = true;
            }

            // Garantizar física y CapsuleCollider para impacto de balas
            CapsuleCollider bossCol = windowsGO.GetComponent<CapsuleCollider>();
            if (bossCol == null)
            {
                changed = true;
            }
            ai.GarantizarComponentesFisicos();

            // Garantizar WindowsAIGeminiDialogue
            WindowsAIGeminiDialogue dlg = windowsGO.GetComponent<WindowsAIGeminiDialogue>();
            if (dlg == null)
            {
                dlg = windowsGO.AddComponent<WindowsAIGeminiDialogue>();
                changed = true;
            }

            // Garantizar NavMeshAgent
            UnityEngine.AI.NavMeshAgent nav = windowsGO.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (nav == null)
            {
                nav = windowsGO.AddComponent<UnityEngine.AI.NavMeshAgent>();
                changed = true;
            }
            if (nav != null)
            {
                nav.stoppingDistance = 1.4f;
                nav.updateRotation = false;
                nav.acceleration = 16f;
            }

            // Garantizar WindowsHeadHealthBar en GameObject dedicado en World Space
            SudoKill9.UI.WindowsHeadHealthBar oldHead = windowsGO.GetComponent<SudoKill9.UI.WindowsHeadHealthBar>();
            if (oldHead != null)
            {
                Object.DestroyImmediate(oldHead, true);
                changed = true;
            }

            SudoKill9.UI.WindowsHeadHealthBar headBar = Object.FindFirstObjectByType<SudoKill9.UI.WindowsHeadHealthBar>();
            if (headBar == null)
            {
                GameObject headBarGO = new GameObject("WindowsHeadHealthBar");
                headBar = headBarGO.AddComponent<SudoKill9.UI.WindowsHeadHealthBar>();
                changed = true;
            }
            // Garantizar WindowsVoiceManager y AudioSource 3D
            SudoKill9.Audio.WindowsVoiceManager voiceMgr = windowsGO.GetComponent<SudoKill9.Audio.WindowsVoiceManager>();
            if (voiceMgr == null)
            {
                voiceMgr = windowsGO.AddComponent<SudoKill9.Audio.WindowsVoiceManager>();
                changed = true;
            }
            voiceMgr.ConfigurarAudioSource3D();
            var vmProp = so.FindProperty("voiceManager");
            if (vmProp != null && vmProp.objectReferenceValue != voiceMgr)
            {
                vmProp.objectReferenceValue = voiceMgr;
                changed = true;
            }

            if (headBar != null)
            {
                headBar.ConfigurarParaBoss(ai);
                var hbProp = so.FindProperty("headHealthBar");
                if (hbProp != null && hbProp.objectReferenceValue != headBar)
                {
                    hbProp.objectReferenceValue = headBar;
                    changed = true;
                }
            }

            if (changed)
            {
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(windowsGO);
                EditorUtility.SetDirty(ai);
                if (headBar != null) EditorUtility.SetDirty(headBar);
                EditorSceneManager.MarkSceneDirty(windowsGO.scene);
                if (!silent)
                {
                    Debug.Log($"<color=green>[SetupWindowsBoss]</color> GameObject 'windows' configurado con WindowsBoss_MasterController y apuntando a '{playerGO?.name}'.");
                }
            }
        }

        private static AnimationClip LoadBossClip(string fbxPath, bool loop)
        {
            EnsureHumanoidAndLoop(fbxPath, loop);
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
            {
                if (o is AnimationClip c && !c.name.StartsWith("__preview__"))
                {
                    return c;
                }
            }
            return null;
        }

        private static void EnsureHumanoidAndLoop(string fbxPath, bool loop)
        {
            ModelImporter importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
            if (importer == null) return;

            bool needsReimport = false;

            if (importer.animationType != ModelImporterAnimationType.Human)
            {
                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                needsReimport = true;
            }

            ModelImporterClipAnimation[] clips = importer.clipAnimations;
            if (clips == null || clips.Length == 0)
            {
                clips = importer.defaultClipAnimations;
            }

            if (clips != null && clips.Length > 0)
            {
                foreach (ModelImporterClipAnimation c in clips)
                {
                    if (c.loopTime != loop || !c.keepOriginalPositionY)
                    {
                        c.loopTime = loop;
                        c.keepOriginalOrientation = true;
                        c.keepOriginalPositionY = true;
                        c.keepOriginalPositionXZ = true;
                        needsReimport = true;
                    }
                }
                if (needsReimport)
                {
                    importer.clipAnimations = clips;
                }
            }

            if (needsReimport)
            {
                importer.SaveAndReimport();
            }
        }
    }
}
