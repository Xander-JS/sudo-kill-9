namespace SudoKill9.Enemies.Espia
{
    /// <summary>
    /// Estadísticas de espia_opt: enemigo a distancia con EXACTAMENTE el mismo balance que el Virus
    /// (el Virus no sobrescribe nada y usa los valores por defecto de EnemyStatsBase).
    /// Su dificultad viene del alcance del láser, no de más vida o daño.
    /// </summary>
    public class EspiaStats : EnemyStatsBase
    {
        // Escala de balance (vida / daño por disparo / cooldown):
        //   Virus   100 / 15 / 1.5 s  (10.0 DPS, cuerpo a cuerpo)
        //   Espia   100 / 15 / 1.5 s  (10.0 DPS, láser hasta 10 m)
        //   Escudo  160 / 20 / 1.8 s
        //   Volador 250 / 30 / 2.4 s

        /// <summary>Aplica los valores del Virus (al añadir el componente y desde Tools/Setup Espia Enemy).</summary>
        public void ApplyBalanceDefaults()
        {
            maxHealth = 100f;
            currentHealth = maxHealth;
            attackDamage = 15f;
            attackCooldown = 1.5f;
            criticalHealthThreshold = 0.3f;
        }

        private void Reset() => ApplyBalanceDefaults();
    }
}
