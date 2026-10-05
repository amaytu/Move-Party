using Imitacoes.Config;
using Imitacoes.Interaction;
using Imitacoes.Motion;
using Imitacoes.Scoring;
using Imitacoes.UI;
using UnityEngine;

namespace Imitacoes.Game
{
    /// <summary>
    /// Posicionamento: "Jogador X, posicione-se na frente do sensor".
    /// Avança sozinho quando o corpo fica estável por ~1 s.
    /// </summary>
    public sealed class PositioningState : GameStateBase
    {
        public PositioningState(GameManager game) : base(game) { }

        public override bool RequiresPlayer => true;

        public override void Enter()
        {
            MatchState match = Game.Match;
            PlayerConfig performer = match.CurrentPerformer;

            Game.SetPlayerAccent(performer);
            Game.UI.HideAll();
            Game.UI.SetBanner(match.IsMasterTurn ? $"Vez do {performer.DisplayName}  -  Mestre" : $"Vez do {performer.DisplayName}");
            Game.UI.ShowTitle($"{performer.DisplayName}, posicione-se na frente do sensor", "Fique de corpo inteiro, a uns 2 metros");

            // Imitador também vê só a si mesmo: a imitação é de memória (viu o replay antes).
            Game.UI.SetSkeletonLayout(SkeletonLayout.LiveOnly);
        }

        public override void Tick(float deltaTime)
        {
            float needed = Game.Settings.StableTrackingSeconds;
            float stable = Game.Body.StableTrackingTime;

            if (!Game.Body.HasActiveBody)
                Game.UI.SetSubtitle("Fique de corpo inteiro, a uns 2 metros");
            else if (stable > 0f)
                Game.UI.SetSubtitle($"Isso! Fique assim... {Mathf.RoundToInt(Mathf.Clamp01(stable / needed) * 100f)}%");
            else
                Game.UI.SetSubtitle("Quase lá: deixe o corpo inteiro aparecer");

            if (stable >= needed)
                Game.GoTo(Game.Match.IsMasterTurn ? GameState.ContagemMestre : GameState.ContagemImitador);
        }
    }

    /// <summary>ContagemMestre / ContagemImitador: 3, 2, 1...</summary>
    public sealed class CountdownState : GameStateBase
    {
        private readonly bool master;
        private float remaining;
        private int lastShown;

        public CountdownState(GameManager game, bool isMaster) : base(game)
        {
            master = isMaster;
        }

        public override bool RequiresPlayer => true;

        public override void Enter()
        {
            string theme = Game.Match.CurrentTheme.displayText;
            remaining = Game.Settings.CountdownSeconds;
            lastShown = -1;

            Game.UI.HideAll();
            Game.UI.ShowTitle(theme, master ? "Prepare-se para criar o movimento!" : "De memória: repita o movimento do Mestre!");
            Game.UI.SetSkeletonLayout(SkeletonLayout.LiveOnly);
        }

        public override void Tick(float deltaTime)
        {
            // Saiu da frente do sensor durante a contagem: volta para o posicionamento.
            if (!Game.Body.HasActiveBody)
            {
                Game.GoTo(GameState.Posicionamento);
                return;
            }

            remaining -= deltaTime;
            int shown = Mathf.CeilToInt(remaining);
            if (shown != lastShown && shown > 0)
            {
                lastShown = shown;
                Game.UI.ShowCountdown(shown);
                Game.PlaySound(GameSound.Countdown);
            }

            if (remaining <= 0f) Game.GoTo(master ? GameState.GravandoMestre : GameState.GravandoImitador);
        }

        public override void Exit() => Game.UI.HideCountdown();
    }

    /// <summary>
    /// GravandoMestre / GravandoImitador: grava pelo tempo configurado, mostrando o progresso.
    /// O imitador grava de memória: o movimento do Mestre NÃO aparece na tela durante a gravação.
    /// No fim do imitador calcula a nota e mostra por alguns segundos.
    /// </summary>
    public sealed class RecordingState : GameStateBase
    {
        private const float FeedbackSeconds = 2.5f;
        private const float MinimumTrackedFraction = 0.3f;

        private readonly bool master;
        private bool finished;
        private bool retryMaster;
        private float feedbackTimer;

        public RecordingState(GameManager game, bool isMaster) : base(game)
        {
            master = isMaster;
        }

        public override bool RequiresPlayer => true;

        public override void Enter()
        {
            MatchState match = Game.Match;
            PlayerConfig performer = match.CurrentPerformer;

            finished = false;
            retryMaster = false;
            feedbackTimer = 0f;

            Game.UI.HideAll();
            Game.UI.ShowTitle(master ? "Crie o movimento!" : "Imite!", match.CurrentTheme.displayText);
            Game.UI.SetSkeletonLayout(SkeletonLayout.LiveOnly); // sem o movimento do Mestre na tela

            Game.Recorder.RecordingFinished += OnRecordingFinished;
            Game.Recorder.StartRecording(RecordingOptions.FromSettings(Game.Settings, performer.PlayerNumber, match.CurrentTheme.id));
            Game.PlaySound(GameSound.RecordStart);
        }

        public override void Tick(float deltaTime)
        {
            if (!finished)
            {
                MotionRecorder r = Game.Recorder;
                Game.UI.ShowRecording(r.Progress01, r.Remaining, r.IsPaused);
                return;
            }

            feedbackTimer += deltaTime;
            if (feedbackTimer >= FeedbackSeconds) Proceed();
        }

        public override void Exit()
        {
            Game.Recorder.RecordingFinished -= OnRecordingFinished;
            if (Game.Recorder.IsRecording) Game.Recorder.Cancel();
            Game.UI.HideRecording();
        }

        private void OnRecordingFinished(MotionClip clip)
        {
            finished = true;
            Game.UI.HideRecording();
            Game.PlaySound(GameSound.RecordEnd);

            MatchState match = Game.Match;
            PlayerConfig performer = match.CurrentPerformer;

            if (master)
            {
                if (TrackedFraction(clip) < MinimumTrackedFraction)
                {
                    retryMaster = true;
                    Game.UI.ShowTitle("Não consegui ver o Mestre direito", "Vamos gravar de novo");
                    return;
                }

                match.MasterClip = clip;
                Game.UI.ShowTitle("Gravado!", "Agora todos vão ver o movimento do Mestre");
                return;
            }

            ComparisonResult result = Game.Comparer.Compare(match.MasterClip, clip);
            float score = result.Score;
            if (Game.Settings.OutOfViewPolicy == OutOfViewPolicy.Penalize)
                score = Mathf.Max(0f, score - Game.Settings.OutOfViewPenaltyPerSecond * clip.OutOfViewSeconds);
            score = Mathf.Round(score);

            match.SetImitatorScore(performer, score);
            Debug.Log($"[Imitações] {performer.DisplayName} imitando '{match.CurrentTheme.id}': {result}");

            Game.UI.ShowTitle($"{performer.DisplayName}: {score:0} pontos", Feedback(score));
            Game.PlaySound(GameSound.Score);
        }

        private void Proceed()
        {
            MatchState match = Game.Match;
            if (master)
            {
                Game.GoTo(retryMaster ? GameState.Posicionamento : GameState.Replay);
                return;
            }

            Game.GoTo(match.AdvanceImitator() ? GameState.Posicionamento : GameState.ResultadoRodada);
        }

        private static float TrackedFraction(MotionClip clip)
        {
            if (clip == null || clip.IsEmpty) return 0f;
            int tracked = 0;
            foreach (var f in clip.Frames) if (f.IsTracked) tracked++;
            return (float)tracked / clip.FrameCount;
        }

        private static string Feedback(float score)
        {
            if (score >= 85f) return "Perfeito!";
            if (score >= 65f) return "Muito bom!";
            if (score >= 40f) return "Quase lá!";
            return "Boa tentativa!";
        }
    }

    /// <summary>Replay: o movimento gravado do Mestre, na cor dele, para todos verem.</summary>
    public sealed class ReplayState : GameStateBase
    {
        private float waitTimer;
        private bool done;

        public ReplayState(GameManager game) : base(game) { }

        public override void Enter()
        {
            MatchState match = Game.Match;
            PlayerConfig master = match.Master;

            waitTimer = 0f;
            done = false;
            Game.SetPlayerAccent(master);
            Game.UI.HideAll();
            Game.UI.SetBanner($"Movimento do Mestre  -  {master.DisplayName}");
            Game.UI.ShowTitle(match.CurrentTheme.displayText, "Observem com atenção!");
            Game.UI.SetSkeletonLayout(SkeletonLayout.ClipOnly);
            Game.UI.ClipView.SetColor(master.Color, 1f);
            Game.ClipPlayer.Play(match.MasterClip);
        }

        public override void Tick(float deltaTime)
        {
            if (Game.ClipPlayer.IsPlaying) return;
            waitTimer += deltaTime;
            if (waitTimer >= Game.Settings.AfterReplaySeconds) Proceed();
        }

        public override void OnConfirm() => Proceed();

        public override void OnGesture(GestureType gesture)
        {
            if (gesture == GestureType.BothHandsUp && Game.ShortcutGesturesEnabled) Proceed();
        }

        public override void Exit() => Game.ClipPlayer.Stop();

        private void Proceed()
        {
            if (done) return;
            done = true;
            Game.Match.BeginImitators();
            Game.GoTo(GameState.Posicionamento);
        }
    }

    /// <summary>ResultadoRodada: barras de pontuação nas cores dos jogadores. Avança sozinho.</summary>
    public sealed class RoundResultState : GameStateBase
    {
        private float timer;

        public RoundResultState(GameManager game) : base(game) { }

        public override void Enter()
        {
            MatchState match = Game.Match;
            match.FinishRound(Game.Settings.MasterScoringRule, Game.Settings.MasterScoreMultiplier);

            timer = 0f;
            Game.SetNeutralAccent();
            Game.UI.HideAll();
            Game.UI.SetSkeletonLayout(SkeletonLayout.Hidden);
            Game.UI.SetBanner($"Resultado da rodada {match.RoundIndex + 1} de {match.RoundCount}");
            Game.UI.ShowTitle(match.CurrentTheme.displayText);
            Game.UI.ShowScores(match.GetRoundRows(), 100f, showPosition: false);
        }

        public override void Tick(float deltaTime)
        {
            timer += deltaTime;
            if (timer >= Game.Settings.RoundResultSeconds) Next();
        }

        public override void OnConfirm() => Next();

        public override void OnGesture(GestureType gesture)
        {
            if (gesture == GestureType.BothHandsUp && Game.ShortcutGesturesEnabled) Next();
        }

        public override void Exit() => Game.UI.HideScores();

        private void Next()
        {
            MatchState match = Game.Match;
            if (match.HasNextRound)
            {
                match.NextRound();
                Game.GoTo(GameState.SorteioTema);
            }
            else
            {
                Game.GoTo(GameState.PlacarFinal);
            }
        }
    }

    /// <summary>PlacarFinal: ranking com as cores e botões "Jogar novamente" / "Menu".</summary>
    public sealed class FinalScoreState : GameStateBase
    {
        private float idleTimer;

        public FinalScoreState(GameManager game) : base(game) { }

        public override void Enter()
        {
            var ranking = Game.Match.GetRanking();
            PlayerConfig winner = ranking[0].Player;
            bool tie = ranking.Count > 1 && Mathf.Approximately(ranking[0].Score, ranking[1].Score);

            idleTimer = 0f;
            Game.SetPlayerAccent(winner);
            Game.UI.HideAll();
            Game.UI.SetSkeletonLayout(SkeletonLayout.Hidden);
            Game.UI.SetBanner(tie ? "Empate!" : $"{winner.DisplayName} venceu!");
            Game.UI.ShowTitle("Placar final");
            Game.UI.ShowScores(ranking, Mathf.Max(1f, ranking[0].Score), showPosition: true);
            Game.UI.ShowFinalButtons(true);
            Game.PlaySound(GameSound.Victory);
        }

        public override void Tick(float deltaTime)
        {
            float limit = Game.Settings.FinalScoreIdleSeconds;
            if (limit <= 0f) return;
            idleTimer += deltaTime;
            if (idleTimer >= limit) Game.GoTo(GameState.TelaInicial);
        }

        public override void Exit()
        {
            Game.UI.ShowFinalButtons(false);
            Game.UI.HideScores();
        }
    }
}
