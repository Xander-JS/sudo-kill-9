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

        public event Action<float> OnTakeDamage;
        public event Action OnDeath;
        public event Action<bool> OnCriticalHealthChanged;

        public float MaxHealth => maxHealth;
        public float CurrentHealth => currentHealth;
        public float AttackDamage => attackDamage;
        public float AttackCooldown => attackCooldown;
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
            OnTakeDamage?.Invoke(amount);
            CheckCriticalChange();

            if (IsDead) Die();
        }

        public virtual void Heal(float amount)
        {
            if (IsDead) return;
            currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
            CheckCriticalChange();
        }

        // Nada del jugador daña aún a los enemigos: esto permite probar los SFX de daño/muerte desde el inspector.
        [ContextMenu("Debug/Recibir 25 de daño")]
        private void DebugTakeDamage() => TakeDamage(25f);

        protected virtual void Die()
        {
            OnDeath?.Invoke();
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
