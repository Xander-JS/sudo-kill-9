namespace SudoKill9.Enemies.Volador
{
    /// <summary>
    /// Estadísticas del Volador: máquina más pesada que el Virus (más vida y daño, ataques más lentos).
    /// Usa la lógica de EnemyStatsBase; Reset() fija los valores al añadir el componente.
    /// </summary>
    public class VoladorStats : EnemyStatsBase
    {
        // Referencia Virus (valores base): 100 vida, 15 daño, 1.5 s de cooldown.
        private void Reset()
        {
            maxHealth = 250f;
            attackDamage = 30f;
            attackCooldown = 2.4f;
            criticalHealthThreshold = 0.3f;
        }
    }
}
