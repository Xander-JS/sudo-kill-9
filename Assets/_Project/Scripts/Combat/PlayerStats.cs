using System;
using UnityEngine;

namespace SudoKill9.Combat
{
    /// <summary>
    /// Maneja los puntos de vida del jugador para interactuar con enemigos y el sistema de combate.
    /// </summary>
    public class PlayerStats : MonoBehaviour, IDamageable
    {
        [Header("Salud")]
        [SerializeField] private float maxHealth = 100f;
        [SerializeField] private float currentHealth;

        public event Action<float> OnTakeDamage;
        public event Action OnDeath;

        public float MaxHealth => maxHealth;
        public float CurrentHealth => currentHealth;
        public bool IsDead => currentHealth <= 0f;

        private void Awake()
        {
            currentHealth = maxHealth;
        }

        public void TakeDamage(float amount)
        {
            if (IsDead) return;

            currentHealth = Mathf.Max(0f, currentHealth - amount);
            Debug.Log($"<color=orange>[PlayerStats]</color> Jugador recibió {amount} de daño. Salud restante: {currentHealth}/{maxHealth}");
            OnTakeDamage?.Invoke(amount);

            if (currentHealth <= 0f)
            {
                Die();
            }
        }

        private void Die()
        {
            Debug.Log("<color=red>[PlayerStats]</color> ¡El jugador ha sido derrotado!");
            OnDeath?.Invoke();
        }

        public void Heal(float amount)
        {
            if (IsDead) return;
            currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        }
    }
}
