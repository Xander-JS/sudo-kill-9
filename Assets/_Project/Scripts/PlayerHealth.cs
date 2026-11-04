using System;
using UnityEngine;

/// <summary>
/// Core health component for the player character.
/// Fires OnHealthChanged(currentHealth, maxHealth) whenever health changes
/// so any listener (e.g. HeartHealthUI) can react without a direct reference.
/// </summary>
public class PlayerHealth : MonoBehaviour
{
    [Header("Health Settings")]
    [SerializeField] private int maxHealth = 5;
    [SerializeField] private bool invincibleOnHit = true;
    [Tooltip("Invincibility window in seconds after taking a hit.")]
    [SerializeField] private float invincibilityDuration = 1f;

    public int MaxHealth => maxHealth;
    public int CurrentHealth { get; private set; } = 5;
    public bool IsDead => CurrentHealth <= 0;

    // payload: (currentHealth, maxHealth)
    public event Action<int, int> OnHealthChanged;
    public event Action OnDeath;

    private float _invincibilityTimer;

    private void Awake()
    {
        if (CurrentHealth <= 0)
        {
            CurrentHealth = maxHealth;
        }
    }

    /// <summary>Restaura la salud completa y cancela el temporizador de invencibilidad.</summary>
    public void RestaurarVidaCompleta()
    {
        CurrentHealth = maxHealth;
        _invincibilityTimer = 0f;
        OnHealthChanged?.Invoke(CurrentHealth, maxHealth);
    }

    private void Update()
    {
        if (_invincibilityTimer > 0f)
            _invincibilityTimer -= Time.deltaTime;
    }

    /// <summary>Reduce health by <paramref name="amount"/>. Ignored when dead or invincible.</summary>
    public void TakeDamage(int amount)
    {
        if (IsDead) return;
        if (invincibleOnHit && _invincibilityTimer > 0f) return;

        CurrentHealth = Mathf.Max(0, CurrentHealth - amount);
        OnHealthChanged?.Invoke(CurrentHealth, maxHealth);

        if (invincibleOnHit)
            _invincibilityTimer = invincibilityDuration;

        if (IsDead)
            OnDeath?.Invoke();
    }

    /// <summary>Restore health by <paramref name="amount"/>, clamped to maxHealth.</summary>
    public void Heal(int amount)
    {
        if (IsDead) return;

        CurrentHealth = Mathf.Min(maxHealth, CurrentHealth + amount);
        OnHealthChanged?.Invoke(CurrentHealth, maxHealth);
    }

    /// <summary>Instantly kill the player.</summary>
    public void Kill()
    {
        TakeDamage(CurrentHealth);
    }

    /// <summary>Restore to full health.</summary>
    public void FullHeal()
    {
        Heal(maxHealth);
    }
}
