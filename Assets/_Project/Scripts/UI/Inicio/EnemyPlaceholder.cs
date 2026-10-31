using UnityEngine;

namespace SudoKill9.UI.Inicio
{
    public enum EnemyTypeCategory
    {
        Soldier_Espia,    // Forma: Cápsula ágil (espia-skeleton)
        Tank_Escudo,      // Forma: Bloque pesado blindado (escudo-skeleton)
        Flyer_Volador,    // Forma: Diamante/Esfera flotante aérea (volador-skeleton)
        Virus_Crawler,    // Forma: Esfera baja de primera línea (virus-skeleton)
        FinalBoss_Windows // Forma: Monolito colosal único en el fondo (windows-skeleton)
    }

    /// <summary>
    /// Componente asignado a cada bloque de enemigo en la escena de inicio.
    /// Categoriza la forma del enemigo y permite reemplazarla fácilmente
    /// por el modelo 3D o prefab correspondiente.
    /// </summary>
    public class EnemyPlaceholder : MonoBehaviour
    {
        [Header("Categoría del Enemigo")]
        public EnemyTypeCategory category;

        [Header("Reemplazo Futuro")]
        [Tooltip("Prefab o modelo 3D con el que puedes reemplazar este bloque.")]
        public GameObject replacementModelPrefab;

        [ContextMenu("Reemplazar por Modelo")]
        public GameObject ReplaceWithModel()
        {
            if (replacementModelPrefab == null)
            {
                Debug.LogWarning($"[EnemyPlaceholder] No hay prefab asignado en {gameObject.name}");
                return null;
            }

            GameObject instance = Instantiate(replacementModelPrefab, transform.parent);
            instance.name = gameObject.name.Replace("_Placeholder", "");
            instance.transform.position = transform.position;
            instance.transform.rotation = transform.rotation;
            instance.transform.localScale = transform.localScale;

            // Desactivar o destruir este bloque placeholder
            DestroyImmediate(gameObject);
            return instance;
        }
    }
}
