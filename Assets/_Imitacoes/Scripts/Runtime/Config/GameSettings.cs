using UnityEngine;

namespace Imitacoes.Config
{
    /// <summary>O que fazer quando o jogador sai do campo de visão durante a gravação.</summary>
    public enum OutOfViewPolicy
    {
        /// <summary>Continua gravando; frames sem corpo entram como NotTracked.</summary>
        Ignore,

        /// <summary>Pausa o cronômetro da gravação até o jogador voltar.</summary>
        PauseRecording,

        /// <summary>Continua gravando e desconta pontos por segundo fora.</summary>
        Penalize
    }

    /// <summary>Como o Mestre pontua na rodada.</summary>
    public enum MasterScoringRule
    {
        /// <summary>Mestre não pontua.</summary>
        None,

        /// <summary>Média das notas dos imitadores × multiplicador.</summary>
        AverageOfImitators,

        /// <summary>Melhor nota entre os imitadores × multiplicador.</summary>
        BestImitator,

        /// <summary>Soma das notas de TODOS os imitadores × multiplicador (ex.: 10% de cada um).</summary>
        SumOfImitators
    }

    /// <summary>
    /// Parâmetros ajustáveis da partida (tempos, gravação, gestos, pontuação, debug).
    /// Um único asset em Data/Settings, referenciado pelo GameManager.
    /// Os parâmetros do algoritmo de similaridade ficam no próprio MotionComparer (próxima etapa).
    /// </summary>
    [CreateAssetMenu(fileName = "GameSettings", menuName = "Imitações/Config/Configurações do Jogo", order = 3)]
    public sealed class GameSettings : ScriptableObject
    {
        [Header("Tempos (segundos)")]
        [SerializeField, Min(0f)] private float countdownSeconds = 3f;
        [SerializeField, Min(1f)] private float recordingSeconds = 5f;
        [SerializeField, Min(0.1f), Tooltip("Tempo de rastreamento estável para começar sozinho.")]
        private float stableTrackingSeconds = 1f;
        [SerializeField, Min(0f)] private float themeRouletteSeconds = 3f;
        [SerializeField, Min(0f), Tooltip("Tempo com o tema sorteado na tela antes de avançar.")]
        private float themeRevealSeconds = 2.5f;
        [SerializeField, Min(0f), Tooltip("Pausa após o replay do Mestre, antes dos imitadores.")]
        private float afterReplaySeconds = 1.5f;
        [SerializeField, Min(0f)] private float roundResultSeconds = 6f;
        [SerializeField, Min(0f), Tooltip("Placar final volta ao menu sozinho após este tempo. 0 = nunca.")]
        private float finalScoreIdleSeconds = 60f;

        [Header("Gravação")]
        [SerializeField, Range(10, 60)] private int recordingFrameRate = 30;
        [SerializeField, Range(0f, 1f), Tooltip("Fração mínima de joints rastreados para considerar o corpo \"estável\".")]
        private float minTrackedRatio = 0.7f;
        [SerializeField] private OutOfViewPolicy outOfViewPolicy = OutOfViewPolicy.PauseRecording;
        [SerializeField, Min(0f), Tooltip("Tolerância antes de considerar \"fora do campo\" (evita piscadas do sensor).")]
        private float outOfViewGraceSeconds = 0.3f;
        [SerializeField, Min(0f), Tooltip("Pontos descontados por segundo fora (política Penalize).")]
        private float outOfViewPenaltyPerSecond = 10f;

        [Header("Interação por gestos")]
        [SerializeField, Range(0.5f, 4f)] private float hoverSelectSeconds = 1.5f;
        [SerializeField] private bool shortcutGesturesEnabled = true;
        [SerializeField, Min(0.1f), Tooltip("Tempo segurando o gesto de atalho para disparar.")]
        private float shortcutGestureHoldSeconds = 1f;

        [Header("Pontuação do Mestre")]
        [SerializeField] private MasterScoringRule masterScoringRule = MasterScoringRule.SumOfImitators;
        [SerializeField, Range(0f, 2f), Tooltip("0,1 = o Mestre ganha 10% dos pontos de cada imitador.")]
        private float masterScoreMultiplier = 0.1f;

        [Header("Debug")]
        [SerializeField, Tooltip("Teclas de atalho. Só funcionam no Editor ou em Development Build.")]
        private bool debugKeysEnabled = true;

        public float CountdownSeconds => countdownSeconds;
        public float RecordingSeconds => recordingSeconds;
        public float StableTrackingSeconds => stableTrackingSeconds;
        public float ThemeRouletteSeconds => themeRouletteSeconds;
        public float ThemeRevealSeconds => themeRevealSeconds;
        public float AfterReplaySeconds => afterReplaySeconds;
        public float RoundResultSeconds => roundResultSeconds;
        public float FinalScoreIdleSeconds => finalScoreIdleSeconds;

        public int RecordingFrameRate => recordingFrameRate;
        public float MinTrackedRatio => minTrackedRatio;
        public OutOfViewPolicy OutOfViewPolicy => outOfViewPolicy;
        public float OutOfViewGraceSeconds => outOfViewGraceSeconds;
        public float OutOfViewPenaltyPerSecond => outOfViewPenaltyPerSecond;

        public float HoverSelectSeconds => hoverSelectSeconds;
        public bool ShortcutGesturesEnabled => shortcutGesturesEnabled;
        public float ShortcutGestureHoldSeconds => shortcutGestureHoldSeconds;

        public MasterScoringRule MasterScoringRule => masterScoringRule;
        public float MasterScoreMultiplier => masterScoreMultiplier;

        /// <summary>Teclas de debug ativas: marcadas E rodando no Editor/Development Build.
        /// Nunca ficam ativas em build de release, mesmo se o asset estiver marcado.</summary>
        public bool DebugKeysActive => debugKeysEnabled && (Application.isEditor || Debug.isDebugBuild);

        /// <summary>Quantidade de frames de uma gravação completa (ex.: 5 s × 30 fps = 150).</summary>
        public int RecordingFrameCount => Mathf.RoundToInt(recordingSeconds * recordingFrameRate);
    }
}
