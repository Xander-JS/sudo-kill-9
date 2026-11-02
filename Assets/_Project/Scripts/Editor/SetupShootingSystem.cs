using UnityEngine;
using UnityEditor;

/// <summary>
/// Tools > Setup Shooting System
/// Añade PlayerCursorAim (si falta) y PlayerShooter al jugador, y crea el MuzzlePoint
/// en la punta del cañón del arma detectada por WeaponIKHandler.
/// </summary>
public static class SetupShootingSystem
{
    private const string MENU = "Tools/Setup Shooting System";

    [MenuItem(MENU)]
    public static void Run()
    {
        // ── Buscar el jugador ────────────────────────────────────────────
        LinuxSkeletonController player = Object.FindFirstObjectByType<LinuxSkeletonController>();
        if (player == null)
        {
            EditorUtility.DisplayDialog("Setup Shooting System",
                "No se encontró LinuxSkeletonController en la escena.\n" +
                "Abre la escena TerrenoJuego y vuelve a ejecutar este menú.", "OK");
            return;
        }

        GameObject playerGO = player.gameObject;
        Undo.RegisterFullObjectHierarchyUndo(playerGO, "Setup Shooting System");

        // ── PlayerCursorAim ──────────────────────────────────────────────
        PlayerCursorAim cursorAim = playerGO.GetComponent<PlayerCursorAim>();
        if (cursorAim == null)
        {
            cursorAim = Undo.AddComponent<PlayerCursorAim>(playerGO);
            Debug.Log("[SetupShootingSystem] PlayerCursorAim añadido al jugador.");
        }

        // ── PlayerShooter ────────────────────────────────────────────────
        PlayerShooter shooter = playerGO.GetComponent<PlayerShooter>();
        if (shooter == null)
        {
            shooter = Undo.AddComponent<PlayerShooter>(playerGO);
            Debug.Log("[SetupShootingSystem] PlayerShooter añadido al jugador.");
        }

        // ── Buscar el arma vía WeaponIKHandler ───────────────────────────
        WeaponIKHandler ikHandler = playerGO.GetComponentInChildren<WeaponIKHandler>();
        Transform weaponRoot = ikHandler != null ? ikHandler.Weapon : null;

        if (weaponRoot == null)
        {
            Debug.LogWarning("[SetupShootingSystem] No se encontró WeaponIKHandler o arma en la jerarquía. " +
                             "Crea manualmente un GameObject llamado 'MuzzlePoint' como hijo del arma y asígnalo " +
                             "al campo MuzzlePoint del PlayerShooter.");
        }
        else
        {
            // Buscar o crear MuzzlePoint
            Transform muzzle = weaponRoot.Find("MuzzlePoint");
            if (muzzle == null)
            {
                GameObject muzzleGO = new GameObject("MuzzlePoint");
                Undo.RegisterCreatedObjectUndo(muzzleGO, "Create MuzzlePoint");
                muzzleGO.transform.SetParent(weaponRoot, false);
                // Posicionar al frente del arma (ajustar en Scene view según el modelo real).
                muzzleGO.transform.localPosition = new Vector3(0f, 0f, 0.45f);
                muzzleGO.transform.localRotation = Quaternion.identity;
                muzzle = muzzleGO.transform;
                Debug.Log("[SetupShootingSystem] MuzzlePoint creado como hijo de: " + weaponRoot.name +
                          ". Ajusta su posición en la Scene view para que quede en la punta del cañón.");
            }
            else
            {
                Debug.Log("[SetupShootingSystem] MuzzlePoint existente encontrado en: " + weaponRoot.name);
            }

            // Asignar al shooter via SerializedObject para que quede registrado en el undo.
            SerializedObject so = new SerializedObject(shooter);
            so.FindProperty("muzzlePoint").objectReferenceValue = muzzle;
            so.ApplyModifiedProperties();
            Debug.Log("[SetupShootingSystem] MuzzlePoint asignado a PlayerShooter.");
        }

        // ── Verificar que PlayerAim y PlayerShooter no coexistan en conflicto ───
        PlayerAim legacyAim = playerGO.GetComponent<PlayerAim>();
        if (legacyAim != null)
        {
            Debug.LogWarning("[SetupShootingSystem] Se detectó PlayerAim (mira con cursor bloqueado) junto a " +
                             "PlayerCursorAim (mira con cursor libre). Ambos pueden estar activos, pero solo " +
                             "uno debe controlar la orientación del jugador. Considera desactivar uno de ellos.");
        }

        EditorUtility.SetDirty(playerGO);
        Selection.activeGameObject = playerGO;

        EditorUtility.DisplayDialog("Setup Shooting System",
            "Configuración completada.\n\n" +
            "Próximos pasos:\n" +
            "1. Ajusta la posición de MuzzlePoint en la Scene view (debe estar en la punta del cañón).\n" +
            "2. En PlayerCursorAim: confirma que Display Index = 2 (Display 3).\n" +
            "3. En PlayerShooter > Hit Mask: desmarca la capa del jugador para evitar auto-impactos.\n" +
            "4. (Opcional) Añade un componente Light como hijo de MuzzlePoint y asígnalo a Muzzle Light.\n" +
            "5. Entra en Play mode y prueba con clic derecho.", "OK");
    }

    [MenuItem(MENU, true)]
    private static bool Validate()
    {
        return !EditorApplication.isPlaying;
    }
}
