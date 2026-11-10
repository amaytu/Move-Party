using System;
using Imitacoes.Body;
using Imitacoes.Config;
using Imitacoes.Interaction;
using Imitacoes.Motion;
using Imitacoes.Scoring;
using Imitacoes.Sources;
using Imitacoes.UI;
using Imitacoes.Visual;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Imitacoes.Game
{
    /// <summary>Efeitos sonoros do jogo (clipes opcionais no GameManager).</summary>
    public enum GameSound
    {
        Tick,
        Reveal,
        Countdown,
        RecordStart,
        RecordEnd,
        Score,
        Victory,
        TimeUp,
        Urgency,
        ScoreCount,
        ScoreGreat,
        ScoreGood,
        ScoreBad,
        Select,
        Start,
        RecordScratch,
        ButtonHover,
        CrowdApplause,
        CrowdCheer,
        CrowdBoo,
        CrowdLaugh,
        CrowdCrickets,
        Confetti,
        Crown,
        SurpriseRule
    }

    /// <summary>
    /// Ponto central do jogo: liga os sistemas (sensor, gestos, gravação, pontuação, UI),
    /// cria a máquina de estados e expõe esse contexto para os estados.
    ///
    /// Responsabilidades transversais (valem em qualquer estado):
    /// - avisos: sensor desconectado, ninguém na frente, mais de uma pessoa, fora do quadro;
    /// - desenho do esqueleto ao vivo na cor do jogador da vez;
    /// - teclas de debug (só no Editor/Development Build, se ativadas no GameSettings).
    /// </summary>
    [AddComponentMenu("Imitações/Game Manager")]
    public sealed class GameManager : MonoBehaviour
    {
        [Serializable]
        private sealed class SoundBank
        {
            public AudioClip tick;
            public AudioClip reveal;
            public AudioClip countdown;
            public AudioClip recordStart;
            public AudioClip recordEnd;
            public AudioClip score;
            public AudioClip victory;
        }

        [Header("Configuração")]
        [SerializeField] private GameSettings settings;
        [SerializeField] private PlayerRoster roster;
        [SerializeField] private ThemeCatalog themes;

        [Header("Sistemas")]
        [SerializeField] private ActiveBodySelector body;
        [SerializeField] private GestureDetector gestures;
        [SerializeField] private MotionRecorder recorder;
        [SerializeField] private MotionComparer comparer;

        [Header("Apresentação")]
        [SerializeField] private GameUI ui;
        [SerializeField] private HandCursor handCursor;
        [SerializeField] private MotionClipPlayer clipPlayer;
        [SerializeField] private Color neutralAccent = new Color(0.35f, 0.8f, 1f);

        [Header("Sons (opcionais)")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private SoundBank sounds = new SoundBank();

        [Header("Debug")]
        [SerializeField, Tooltip("Somente leitura: estado atual.")]
        private GameState currentState;
        [SerializeField] private bool showDebugOverlay = true;

        private GameAudio gameAudio;
        private GameStateMachine machine;
        private readonly MatchState match = new MatchState();
        private Color liveColor = Color.white;
        private int lastPlayerCount = PlayerRoster.MinPlayers;
        private float fpsSmoothed = 60f;

        // ------------------------------------------------------------------ Contexto para os estados

        public GameSettings Settings => settings;
        public GameUI UI => ui;
        public ActiveBodySelector Body => body;
        public GestureDetector Gestures => gestures;
        public MotionRecorder Recorder => recorder;
        public MotionComparer Comparer => comparer;
        public MotionClipPlayer ClipPlayer => clipPlayer;
        public MatchState Match => match;
        public GameAudio Audio => gameAudio;
        public GameState CurrentState => machine != null ? machine.Current : GameState.TelaInicial;
        public bool ShortcutGesturesEnabled => settings.ShortcutGesturesEnabled;

        public void GoTo(GameState state) => machine.ChangeState(state);

        /// <summary>Começa uma partida com os N primeiros jogadores do elenco.</summary>
        /// <param name="showHowToPlay">Mostra a tela "Como jogar" antes (não no "Jogar novamente").</param>
        public void StartMatch(int playerCount, bool showHowToPlay = true)
        {
            lastPlayerCount = Mathf.Clamp(playerCount, PlayerRoster.MinPlayers, PlayerRoster.MaxPlayers);
            match.SurpriseMode = surpriseMode;
            match.StartMatch(roster.Take(lastPlayerCount), themes.Themes);
            GoTo(showHowToPlay ? GameState.ComoJogar : GameState.SorteioTema);
        }

        // ------------------------------------------------------------------ Modo "Regras surpresa"

        private bool surpriseMode;

        /// <summary>Modo extra escolhido na seleção de jogadores (vale até mudar de novo).</summary>
        public bool SurpriseMode => surpriseMode;

        private void ToggleSurpriseMode()
        {
            surpriseMode = !surpriseMode;
            ui.SetSurpriseToggle(surpriseMode);
            if (surpriseMode) PlaySound(GameSound.SurpriseRule);
        }

        // ------------------------------------------------------------------ Plateia

        /// <summary>A nota terminou de subir na tela: a "plateia" reage.</summary>
        private void OnScoreRevealed(float score)
        {
            if (score >= ScoreReaction.ConfettiThreshold)
            {
                PlaySound(GameSound.ScoreGreat);
                PlaySound(GameSound.Confetti);
                gameAudio.Play(GameSound.CrowdCheer, 0.9f);
                gameAudio.Play(GameSound.CrowdApplause, 0.7f);
            }
            else if (score >= 50f)
            {
                PlaySound(GameSound.ScoreGood);
                gameAudio.Play(GameSound.CrowdApplause, 0.6f);
            }
            else if (score >= 20f)
            {
                PlaySound(GameSound.ScoreBad);
                gameAudio.Play(GameSound.CrowdBoo, 0.7f);
            }
            else if (score > 0f)
            {
                PlaySound(GameSound.ScoreBad);
                gameAudio.Play(GameSound.CrowdLaugh, 0.9f);
            }
            else
            {
                gameAudio.Play(GameSound.CrowdCrickets, 0.9f);
            }
        }

        // ------------------------------------------------------------------ Pódio

        private PodiumStage podium;

        /// <summary>Monta o pódio 3D. False se a cena usa bonecos 2D (aí o placar usa a lista).</summary>
        public bool ShowPodium(System.Collections.Generic.IReadOnlyList<ScoreRow> ranking)
        {
            if (!(ui.LiveView is Avatar3DView template)) return false;
            if (podium == null) podium = PodiumStage.Create(template, (RectTransform)ui.transform, ui.Font);
            podium.Show(ranking);
            return true;
        }

        public void HidePodium()
        {
            if (podium != null) podium.Hide();
        }

        // ------------------------------------------------------------------ Coroa

        /// <summary>Coroa no boneco do líder: no ao vivo durante a vez dele e no replay se o Mestre lidera.</summary>
        private void UpdateCrowns()
        {
            GameState s = CurrentState;
            bool performerOnScreen = s == GameState.Posicionamento || s == GameState.ContagemMestre || s == GameState.GravandoMestre
                                     || s == GameState.ContagemImitador || s == GameState.GravandoImitador;
            if (ui.LiveView != null) ui.LiveView.SetCrown(performerOnScreen && match.IsLeader(match.CurrentPerformer));
            if (ui.ClipView != null) ui.ClipView.SetCrown(s == GameState.Replay && match.IsLeader(match.Master));
        }

        // ------------------------------------------------------------------ Pausa

        [Header("Pausa")]
        [SerializeField, Min(0.3f), Tooltip("Tempo com os braços cruzados no peito para abrir a pausa.")]
        private float pauseGestureSeconds = 1.2f;

        private bool paused;
        private float crossedArmsTime;

        public bool IsPaused => paused;

        /// <summary>
        /// Pausa só fora das gravações e contagens (lá o movimento pode ter braços cruzados de propósito)
        /// e fora das telas que já têm botões.
        /// </summary>
        private bool PauseAllowed()
        {
            GameState s = CurrentState;
            return s == GameState.SorteioTema || s == GameState.Posicionamento || s == GameState.Replay
                   || s == GameState.ResultadoRodada || s == GameState.ComoJogar;
        }

        public void OpenPause()
        {
            if (paused || !PauseAllowed()) return;
            paused = true;
            crossedArmsTime = 0f;
            if (clipPlayer != null) clipPlayer.enabled = false; // congela o replay

            string skipLabel = null;
            if (CurrentState == GameState.Posicionamento && match.CurrentPerformer != null)
                skipLabel = $"Pular {match.CurrentPerformer.DisplayName}";

            ui.OpenPause(liveColor == Color.white ? neutralAccent : liveColor, skipLabel);
            PlaySound(GameSound.Select);
        }

        public void ClosePause()
        {
            if (!paused) return;
            paused = false;
            crossedArmsTime = 0f;
            if (clipPlayer != null) clipPlayer.enabled = true;
            ui.ClosePause();
            gestures.ResetAll(); // a mão que apertou o botão não começa a vez sem querer
        }

        /// <summary>
        /// Pula quem deveria estar na frente do sensor (ex.: a pessoa saiu da sala).
        /// Mestre pulado = rodada pulada; imitador pulado = nota 0 nessa rodada.
        /// </summary>
        private void SkipCurrentPerformer()
        {
            ClosePause();
            if (CurrentState != GameState.Posicionamento) return;

            if (match.IsMasterTurn)
            {
                if (match.HasNextRound)
                {
                    match.NextRound();
                    GoTo(GameState.SorteioTema);
                }
                else GoTo(GameState.PlacarFinal);
                return;
            }

            match.SetImitatorScore(match.CurrentImitator, 0f, skipped: true);
            GoTo(match.AdvanceImitator() ? GameState.Posicionamento : GameState.ResultadoRodada);
        }

        /// <summary>Braços cruzados na frente do peito (mão esquerda do lado direito e vice-versa).</summary>
        private void UpdatePauseGesture(float dt)
        {
            BodyFrame b = body.ActiveBody;
            bool crossed = false;
            if (b != null && PauseAllowed())
            {
                JointData l = b.GetJoint(JointId.HandLeft), r = b.GetJoint(JointId.HandRight);
                JointData mid = b.GetJoint(JointId.SpineMid), head = b.GetJoint(JointId.Head);
                if (l.State == JointTrackingState.Tracked && r.State == JointTrackingState.Tracked && mid.IsUsable && head.IsUsable)
                {
                    float low = mid.Position.y, high = head.Position.y;
                    crossed = l.Position.x > r.Position.x + 0.05f
                              && l.Position.y > low && l.Position.y < high
                              && r.Position.y > low && r.Position.y < high;
                }
            }

            crossedArmsTime = crossed ? crossedArmsTime + dt : 0f;
            if (crossedArmsTime >= pauseGestureSeconds) OpenPause();
        }

        /// <summary>Tudo na cor do jogador: moldura, faixa, cursor e esqueleto ao vivo.</summary>
        public void SetPlayerAccent(PlayerConfig player)
        {
            ui.SetAccent(player);
            if (handCursor != null) handCursor.SetColor(player.Color);
            liveColor = player.Color;
        }

        /// <summary>Cor neutra (telas sem jogador da vez).</summary>
        public void SetNeutralAccent()
        {
            ui.SetAccent(neutralAccent, Color.black);
            if (handCursor != null) handCursor.SetColor(neutralAccent);
            liveColor = Color.white;
        }

        /// <summary>Mostra o fantasma do Mestre (à esquerda) no instante t, com o jogador ao vivo à direita.</summary>
        public void ShowGhost(float time)
        {
            ui.SetSkeletonLayout(SkeletonLayout.GhostAndLive);
            PlayerConfig master = match.Master;
            ui.ClipView.SetColor(master.Color, Mathf.Max(master.GhostColor.a, 0.5f));
            clipPlayer.ShowTime(match.MasterClip, time);
        }

        public void PlaySound(GameSound sound)
        {
            AudioClip clip = sound switch
            {
                GameSound.Tick => sounds.tick,
                GameSound.Reveal => sounds.reveal,
                GameSound.Countdown => sounds.countdown,
                GameSound.RecordStart => sounds.recordStart,
                GameSound.RecordEnd => sounds.recordEnd,
                GameSound.Score => sounds.score,
                GameSound.Victory => sounds.victory,
                _ => null
            };
            // Clipe arrastado no SoundBank tem prioridade; senão usa os sons do GameAudio (Resources/Audio).
            if (clip != null && audioSource != null) audioSource.PlayOneShot(clip);
            else if (gameAudio != null) gameAudio.Play(sound);
        }

        // ------------------------------------------------------------------ Ciclo de vida

        private void Awake()
        {
            if (!ValidateReferences())
            {
                enabled = false;
                return;
            }

            GestureButton.DefaultHoldSeconds = settings.HoverSelectSeconds;
            GestureButton.MouseClickEnabled = settings.DebugKeysActive;
            body.MinTrackedRatio = settings.MinTrackedRatio;

            // Versão final: esconde a seta do mouse (o jogo é todo por gestos).
            if (!Application.isEditor && !Debug.isDebugBuild) Cursor.visible = false;

            gameAudio = GetComponent<GameAudio>();
            if (gameAudio == null) gameAudio = gameObject.AddComponent<GameAudio>();
            gameAudio.Bind(this);

            // Sons dos botões: ao começar a encher e ao confirmar (vale para qualquer GestureButton).
            GestureButton.AnyHoverStarted += OnButtonHover;
            GestureButton.AnySelected += OnButtonSelected;

            // Reação da nota: tique enquanto o número sobe e som conforme a faixa no fim.
            ui.ScoreReaction.Ticked += n => { if (n % 3 == 0) gameAudio.Play(GameSound.ScoreCount, 0.5f, 0.8f + n / 100f); };
            ui.ScoreReaction.Finished += OnScoreRevealed;
            ui.SurpriseToggled += ToggleSurpriseMode;

            machine = new GameStateMachine();
            machine.Register(GameState.TelaInicial, new TitleState(this));
            machine.Register(GameState.SelecaoJogadores, new PlayerSelectState(this));
            machine.Register(GameState.ComoJogar, new HowToPlayState(this));
            machine.Register(GameState.SorteioTema, new ThemeDrawState(this));
            machine.Register(GameState.Posicionamento, new PositioningState(this));
            machine.Register(GameState.ContagemMestre, new CountdownState(this, isMaster: true));
            machine.Register(GameState.GravandoMestre, new RecordingState(this, isMaster: true));
            machine.Register(GameState.Replay, new ReplayState(this));
            machine.Register(GameState.ContagemImitador, new CountdownState(this, isMaster: false));
            machine.Register(GameState.GravandoImitador, new RecordingState(this, isMaster: false));
            machine.Register(GameState.ResultadoRodada, new RoundResultState(this));
            machine.Register(GameState.PlacarFinal, new FinalScoreState(this));

            machine.StateChanged += (_, next) =>
            {
                currentState = next;
                gestures.ResetAll(); // gesto que abriu esta tela não dispara nada na próxima
                gameAudio.OnStateChanged(next);
            };

            gestures.GestureTriggered += OnGesture;

            for (int i = 0; i < ui.PlayerCountButtons.Count; i++)
            {
                int count = i + PlayerRoster.MinPlayers;
                ui.PlayerCountButtons[i].onSelected.AddListener(() => StartMatch(count));
            }
            ui.PlayAgainButton.onSelected.AddListener(() => StartMatch(lastPlayerCount, showHowToPlay: false));

            ui.PauseMenu.ResumeRequested += ClosePause;
            ui.PauseMenu.SkipRequested += SkipCurrentPerformer;
            ui.PauseMenu.MenuRequested += () => { ClosePause(); GoTo(GameState.TelaInicial); };
            ui.MenuButton.onSelected.AddListener(() => GoTo(GameState.TelaInicial));
        }

        private void Start() => GoTo(GameState.TelaInicial);

        private void OnDestroy()
        {
            if (gestures != null) gestures.GestureTriggered -= OnGesture;
            GestureButton.AnyHoverStarted -= OnButtonHover;
            GestureButton.AnySelected -= OnButtonSelected;
        }

        private void Update()
        {
            // Recompilação durante o Play recria o domínio e perde a máquina (não serializada).
            if (machine == null) return;

            HandlePauseKeys();
            if (paused)
            {
                // Estado congelado: só os avisos e o debug continuam.
                UpdateWarnings();
                UpdateDebugOverlay();
                return;
            }

            UpdatePauseGesture(Time.unscaledDeltaTime);
            machine.Tick(Time.deltaTime);
            UpdateAutoFraming();
            UpdateWarnings();
            HandleDebugKeys();
            UpdateDebugOverlay();
        }

        private void LateUpdate()
        {
            if (machine == null) return;
            UpdateCrowns();

            // Esqueleto ao vivo: sempre o jogador ativo, na cor do jogador da vez.
            BodyView live = ui.LiveView;
            if (live != null && live.isActiveAndEnabled)
            {
                live.SetColor(liveColor, 1f);
                live.Render(body.ActiveBody);
            }
        }

        private void OnButtonHover(GestureButton _) => PlaySound(GameSound.ButtonHover);
        private void OnButtonSelected(GestureButton _) => PlaySound(GameSound.Select);

        private void OnGesture(GestureType gesture)
        {
            if (paused) return;
            machine.CurrentHandler?.OnGesture(gesture);
        }

        /// <summary>P (ou Esc na versão final) abre/fecha a pausa pelo teclado.</summary>
        private void HandlePauseKeys()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null) return;
            bool toggle = kb.pKey.wasPressedThisFrame || (!settings.DebugKeysActive && kb.escapeKey.wasPressedThisFrame);
            if (!toggle) return;
            if (paused) ClosePause();
            else OpenPause();
        }

        /// <summary>
        /// O motor do Kinect só pode se mexer fora das gravações (senão o movimento do
        /// sensor apareceria como movimento do jogador).
        /// </summary>
        private void UpdateAutoFraming()
        {
            if (!(body.CurrentSource is IAutoFramingSource framing)) return;
            GameState s = machine.Current;
            framing.AutoFramingAllowed = s == GameState.TelaInicial || s == GameState.SelecaoJogadores
                                         || s == GameState.SorteioTema || s == GameState.Posicionamento;
        }

        // ------------------------------------------------------------------ Avisos

        private void UpdateWarnings()
        {
            ui.ShowWarning(ComputeWarning());
        }

        private string ComputeWarning()
        {
            IBodySource source = body.CurrentSource;
            if (source == null) return "Nenhuma fonte de movimento configurada";
            if (source.Status == BodySourceStatus.Error) return "Kinect indisponível: " + source.StatusMessage;
            if (source.Status == BodySourceStatus.SensorUnavailable)
                return string.IsNullOrEmpty(source.StatusMessage) ? "Kinect desconectado - verifique o cabo USB e a fonte" : source.StatusMessage;

            GameStateBase state = machine.CurrentHandler;
            if (state == null || !state.RequiresPlayer) return null;

            if (recorder.IsPaused) return "Você saiu do campo de visão! Gravação pausada.";
            if (recorder.IsRecording && recorder.IsPlayerOutOfView) return "Você saiu do campo de visão!";
            if (!body.HasActiveBody) return "Nenhum jogador detectado - fique de frente para o sensor";
            if (body.VisibleBodyCount > 1) return "Só o jogador da vez deve ficar na frente do sensor";

            ClippedEdges edges = body.ActiveBody.ClippedEdges;
            if ((edges & (ClippedEdges.Left | ClippedEdges.Right)) != 0) return "Vá um pouco mais para o centro";
            if ((edges & (ClippedEdges.Top | ClippedEdges.Bottom)) != 0) return "Afaste-se um pouco do sensor";
            return null;
        }

        // ------------------------------------------------------------------ Debug

        private void HandleDebugKeys()
        {
            if (!settings.DebugKeysActive) return;
            Keyboard kb = Keyboard.current;
            if (kb == null) return;

            if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) machine.CurrentHandler?.OnConfirm();
            if (kb.escapeKey.wasPressedThisFrame) GoTo(GameState.TelaInicial);
            if (kb.f1Key.wasPressedThisFrame) showDebugOverlay = !showDebugOverlay;
            if (kb.f2Key.wasPressedThisFrame)
            {
                body.Mode = body.Mode == BodySourceMode.Auto ? BodySourceMode.Kinect
                    : body.Mode == BodySourceMode.Kinect ? BodySourceMode.Mock : BodySourceMode.Auto;
            }

            if (CurrentState == GameState.SelecaoJogadores)
            {
                if (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame) StartMatch(2);
                else if (kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame) StartMatch(3);
                else if (kb.digit4Key.wasPressedThisFrame || kb.numpad4Key.wasPressedThisFrame) StartMatch(4);
                else if (kb.sKey.wasPressedThisFrame) ToggleSurpriseMode();
            }
        }

        private void UpdateDebugOverlay()
        {
            if (!settings.DebugKeysActive || !showDebugOverlay)
            {
                ui.SetDebug(null);
                return;
            }

            fpsSmoothed = Mathf.Lerp(fpsSmoothed, 1f / Mathf.Max(0.0001f, Time.unscaledDeltaTime), 0.05f);
            IBodySource source = body.CurrentSource;
            string sourceInfo = source != null ? $"{source.SourceName} ({source.Status})" : "nenhuma";
            ui.SetDebug(
                $"Estado: {CurrentState}\n" +
                $"Fonte: {sourceInfo}  |  modo {body.Mode}\n" +
                $"Corpos: {body.VisibleBodyCount}  |  estável {body.StableTrackingTime:0.0}s  |  {fpsSmoothed:0} fps\n" +
                "Enter = confirmar  Esc = menu  P = pausa  F1 = info  F2 = fonte  (mock: Espaço = mãos pra cima, H = sair)");
        }

        private bool ValidateReferences()
        {
            bool ok = true;
            void Check(UnityEngine.Object o, string name)
            {
                if (o != null) return;
                Debug.LogError($"[Imitações] GameManager: referência '{name}' não configurada. Use Imitações ▸ Criar Cena Principal.", this);
                ok = false;
            }

            Check(settings, nameof(settings));
            Check(roster, nameof(roster));
            Check(themes, nameof(themes));
            Check(body, nameof(body));
            Check(gestures, nameof(gestures));
            Check(recorder, nameof(recorder));
            Check(comparer, nameof(comparer));
            Check(ui, nameof(ui));
            Check(clipPlayer, nameof(clipPlayer));
            return ok;
        }
    }
}
