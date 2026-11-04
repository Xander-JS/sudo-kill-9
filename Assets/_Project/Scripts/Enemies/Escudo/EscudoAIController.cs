using UnityEngine;

namespace SudoKill9.Enemies.Escudo
{
    /// <summary>
    /// IA de escudo_opt: robot-escudo pesado y defensivo. Se planta con una provocación al detectar,
    /// avanza más lento que el Virus, cojea en vida crítica (estado "Crawl" del Animator = InjuredRun)
    /// y alterna 2 ataques: golpe de escudo descendente y embestida lateral.
    /// Toda la máquina de estados vive en EnemyAIBase.
    /// </summary>
    [RequireComponent(typeof(EscudoStats), typeof(EscudoAudio))]
    public class EscudoAIController : EnemyAIBase
    {
        public const int AttackVariants = 2; // Attack_ShieldSlam, Attack_ShieldBash en escudo.controller

        public float RunSpeed => runSpeed;
        public float AttackRange => attackRange;

        /// <summary>Más pesado que el Virus (run 5.5, crawl 2.2, alerta 1.4 s, stagger 0.45 s).</summary>
        public void ApplyBalanceDefaults()
        {
            alertRange = 12f;
            attackRange = 2.4f;
            runSpeed = 4.2f;
            crawlSpeed = 2.4f;
            rotationSpeed = 7f;
            alertDuration = 1.2f;
            hitStaggerDuration = 0.3f; // Blindado: se recupera antes del impacto.
        }

        private void Reset() => ApplyBalanceDefaults();

        protected override int ChooseAttackVariant() => Random.Range(0, AttackVariants);
    }
}
