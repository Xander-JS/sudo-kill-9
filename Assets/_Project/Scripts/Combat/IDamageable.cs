namespace SudoKill9.Combat
{
    /// <summary>
    /// Interfaz para cualquier entidad que pueda recibir daño y ser destruida/derrotada.
    /// </summary>
    public interface IDamageable
    {
        void TakeDamage(float amount);
        bool IsDead { get; }
    }
}
