using System;
using UnityEngine;
using SudoKill9.Combat;

namespace SudoKill9.Enemies
{
    /// <summary>
    /// Vida, estado crítico y estadísticas de combate comunes a todos los enemigos.
    /// Cada enemigo hereda y ajusta valores en el inspector o sobrescribe TakeDamage/Die.
    /// </summary>
    public abstract class EnemyStatsBase : MonoBehaviour, IDamageable
    {
        [Header("Salud y Estados")]
        [SerializeField] protected float maxHealth = 100f;
        [SerializeField] protected float currentHealth;
        [Tooltip("Porcentaje de vida (0 a 1) para entrar en estado crítico.")]
        [Range(0.05f, 0.5f)] [SerializeField] protected float criticalHealthThreshold = 0.3f;

        [Header("Estadísticas de Combate")]
        [SerializeField] protected float attackDamage = 15f;
        [Tooltip("Tiempo mínimo entre ataques en segundos.")]
        [SerializeField] protected float attackCooldown = 1.5f;

        [Header("Muerte")]
        [Tooltip("Segundos entre la muerte y Destroy(gameObject): deja ver animación, SFX y VFX de muerte. < 0 = no destruir (el cadáver queda).")]
        [SerializeField] protected float despawnDelay = 1.5f;

        public event Action<float> OnTakeDamage;
        /// <summary>(vida actual, vida máxima) tras cada daño o curación. Lo usa EnemyHealthBar.</summary>
        public event Action<float, float> OnHealthChanged;
        public event Action OnDeath;
        public event Action<bool> OnCriticalHealthChanged;

        public float MaxHealth => maxHealth;
        public float CurrentHealth => currentHealth;
        public float AttackDamage => attackDamage;

        /// <summary>
        /// attackDamage está en escala de 100 HP (tabla de balance), pero PlayerHealth cuenta corazones (10 en Nivel_1/2).
        /// Sin esta conversión un golpe de 15 restaba 15 corazones: muerte instantánea. 20 HP por corazón,
        /// así Virus/Espía/Escudo quitan 1 corazón y Volador 2 (mínimo 1). El jefe Windows ya usa corazones y no pasa por aquí.
        /// </summary>
        public const float HpPerPlayerHeart = 20f;
        public float PlayerDamage => Mathf.Max(1f, Mathf.Round(attackDamage / HpPerPlayerHeart));
        public float AttackCooldown => attackCooldown;
        public float DespawnDelay => despawnDelay;
        public bool IsDead => currentHealth <= 0f;
        public bool IsCriticalHealth => !IsDead && (currentHealth / maxHealth) <= criticalHealthThreshold;

        private bool wasCritical;

        protected virtual void Awake()
        {
            currentHealth = maxHealth;
        }

        public virtual void TakeDamage(float amount)
        {
            if (IsDead) return;

            currentHealth = Mathf.Max(0f, currentHealth - amount);
            OnHealthChanged?.Invoke(currentHealth, maxHealth);
            OnTakeDamage?.Invoke(amount);
            CheckCriticalChange();

            if (IsDead) Die();
        }

        public virtual void Heal(float amount)
        {
            if (IsDead) return;
            currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
            OnHealthChanged?.Invoke(currentHealth, maxHealth);
            CheckCriticalChange();
        }

        // Atajo de depuración: probar barra de vida, SFX y VFX de daño/muerte desde el inspector sin disparar.
        [ContextMenu("Debug/Recibir 25 de daño")]
        private void DebugTakeDamage() => TakeDamage(25f);

        protected virtual void Die()
        {
            OnDeath?.Invoke();
            // Desde el menú contextual en modo edición no se puede destruir; solo en juego.
            if (despawnDelay >= 0f && Application.isPlaying) Destroy(gameObject, despawnDelay);
        }

        private void CheckCriticalChange()
        {
            bool isCriticalNow = IsCriticalHealth;
            if (isCriticalNow == wasCritical) return;
            wasCritical = isCriticalNow;
            OnCriticalHealthChanged?.Invoke(isCriticalNow);
        }
    }
}
