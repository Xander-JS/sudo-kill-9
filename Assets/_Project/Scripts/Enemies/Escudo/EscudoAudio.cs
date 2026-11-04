namespace SudoKill9.Enemies.Escudo
{
    /// <summary>
    /// SFX de escudo_opt. Hereda toda la reproducción 3D (vol. 1.0, rolloff lineal 4 m → 40 m);
    /// los clips se asignan con Tools/Setup Escudo Enemy.
    /// Orden de attackClips: [golpe de escudo, embestida] → AttackIndex 0 = golpe, 1 = embestida.
    /// </summary>
    public class EscudoAudio : EnemyAudioBase
    {
    }
}
