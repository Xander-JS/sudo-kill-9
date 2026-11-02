using UnityEngine;

namespace SudoKill9.Enemies.Virus
{
    /// <summary>
    /// IA del Virus: ruge al detectar, persigue, se arrastra en vida crítica y alterna 3 ataques cuerpo a cuerpo.
    /// Toda la máquina de estados vive en EnemyAIBase; aquí solo va lo específico del Virus.
    /// </summary>
    [RequireComponent(typeof(VirusStats), typeof(VirusAudio))]
    public class VirusAIController : EnemyAIBase
    {
        private const int AttackVariants = 3; // Attack_Swipe, Attack_Slash, Attack_Tear en VirusController.controller

        protected override int ChooseAttackVariant() => Random.Range(0, AttackVariants);
    }
}
