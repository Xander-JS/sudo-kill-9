namespace SudoKill9.Levels
{
    /// <summary>
    /// Claves de la String Table "Gameplay" (Assets/_Project/Localization/Tables) usadas por el HUD
    /// de supervivencia. Las traducciones es/en viven en GameplayLocalizationBuilder (Editor).
    /// Los marcadores {0}/{1} se rellenan con string.Format en SurvivalLevelHUD.
    /// </summary>
    public static class GameplayKeys
    {
        public const string Table = "Gameplay";

        // HUD permanente
        public const string Survive = "gameplay_survive";
        public const string LevelIntro = "gameplay_level_intro";          // {0} nivel, {1} segundos
        public const string ClearCounter = "gameplay_clear_counter";      // {0} enemigos restantes
        public const string Hostiles = "gameplay_hostiles";               // {0} enemigos vivos

        // Avisos
        public const string FinalStretch = "gameplay_final_stretch";      // {0} segundos
        public const string ClearRemaining = "gameplay_clear_remaining";

        // Panel de resultado
        public const string LevelCompleted = "gameplay_level_completed";  // {0} nivel
        public const string DefeatTitle = "gameplay_defeat_title";
        public const string ContinueHint = "gameplay_continue_hint";      // {0} tecla
        public const string RetryHint = "gameplay_retry_hint";            // {0} tecla

        /// <summary>Prefijo del nombre localizado de una tecla: gameplay_key_space, …</summary>
        public const string KeyPrefix = "gameplay_key_";

        // Hordas programadas
        public const string WaveHorde = "gameplay_wave_horde";
        public const string WaveVirusPatrol = "gameplay_wave_virus_patrol";
        public const string WaveMultiplePatrols = "gameplay_wave_multiple_patrols";
        public const string WaveHoldOn = "gameplay_wave_hold_on";
        public const string WaveShieldWall = "gameplay_wave_shield_wall";
        public const string WaveAirRaid = "gameplay_wave_air_raid";
        public const string WaveFlanked = "gameplay_wave_flanked";
        public const string WaveSiege = "gameplay_wave_siege";
        public const string WaveFinal = "gameplay_wave_final";
    }
}
