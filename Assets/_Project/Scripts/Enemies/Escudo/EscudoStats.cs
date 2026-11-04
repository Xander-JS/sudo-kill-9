namespace SudoKill9.Enemies.Escudo
{
    /// <summary>
    /// Estadísticas de escudo_opt: tanque intermedio de la jerarquía Virus &lt; Escudo &lt; Volador.
    /// Usa la lógica de EnemyStatsBase; solo fija sus valores de balance.
    /// </summary>
    public class EscudoStats : EnemyStatsBase
    {
        // Escala de balance (vida / daño por golpe / cooldown):
        //   Virus   100 / 15 / 1.5 s  (10.0 DPS)
        //   Escudo  160 / 20 / 1.8 s  (11.1 DPS)
        //   Volador debe quedar por encima de 160 / 20.

        /// <summary>Aplica los valores de balance (al añadir el componente y desde Tools/Setup Escudo Enemy).</summary>
        public void ApplyBalanceDefaults()
        {
            maxHealth = 160f;
            currentHealth = maxHealth;
            attackDamage = 20f;
            attackCooldown = 1.8f;
            criticalHealthThreshold = 0.25f;
        }

        private void Reset() => ApplyBalanceDefaults();
    }
}
