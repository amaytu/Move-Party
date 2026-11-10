using Imitacoes.Config;
using Imitacoes.Interaction;
using Imitacoes.UI;
using UnityEngine;

namespace Imitacoes.Game
{
    /// <summary>TelaInicial: "Levante a mão para começar".</summary>
    public sealed class TitleState : GameStateBase
    {
        private const string Hint = "Levante a mão acima da cabeça para começar";

        public TitleState(GameManager game) : base(game) { }

        public override void Enter()
        {
            Game.SetNeutralAccent();
            Game.UI.HideAll();
            Game.UI.SetBanner(null);
            Game.UI.ShowLogo("IMITAÇÕES", Hint);
            Game.UI.SetSkeletonLayout(SkeletonLayout.LiveOnly);
        }

        public override void Tick(float deltaTime)
        {
            float progress = Game.Gestures.GetProgress(GestureType.AnyHandUp);
            Game.UI.SetSubtitle(progress > 0f ? $"Segure a mão aí... {Mathf.RoundToInt(progress * 100f)}%" : Hint);
        }

        public override void OnGesture(GestureType gesture)
        {
            if (gesture == GestureType.AnyHandUp) Game.GoTo(GameState.SelecaoJogadores);
        }

        public override void OnConfirm() => Game.GoTo(GameState.SelecaoJogadores);
    }

    /// <summary>
    /// ComoJogar: cartões ilustrados com as regras, antes da primeira rodada.
    /// Levantar a mão (o mesmo gesto do resto do jogo) começa; avança sozinho depois de um tempo.
    /// </summary>
    public sealed class HowToPlayState : GameStateBase
    {
        private const string Hint = "Levante a mão acima da cabeça para começar a partida";
        private const float AutoAdvanceSeconds = 40f;
        private float timer;

        public HowToPlayState(GameManager game) : base(game) { }

        public override void Enter()
        {
            timer = 0f;
            Game.SetNeutralAccent();
            Game.UI.HideAll();
            Game.UI.SetBanner(null);
            Game.UI.ShowTitle("Como jogar", Hint);
            Game.UI.SetSkeletonLayout(SkeletonLayout.Hidden);
            Game.UI.ShowHowToPlay(true);
        }

        public override void Tick(float deltaTime)
        {
            timer += deltaTime;
            float progress = Game.Gestures.GetProgress(GestureType.AnyHandUp);
            Game.UI.SetSubtitle(progress > 0f ? $"Segure a mão aí... {Mathf.RoundToInt(progress * 100f)}%" : Hint);
            if (timer >= AutoAdvanceSeconds) Begin();
        }

        public override void OnGesture(GestureType gesture)
        {
            if (gesture == GestureType.AnyHandUp) Begin();
        }

        public override void OnConfirm() => Begin();

        public override void Exit() => Game.UI.ShowHowToPlay(false);

        private void Begin()
        {
            Game.PlaySound(GameSound.Start);
            Game.GoTo(GameState.SorteioTema);
        }
    }

    /// <summary>SelecaoJogadores: botões 2, 3 e 4 por hover.</summary>
    public sealed class PlayerSelectState : GameStateBase
    {
        public PlayerSelectState(GameManager game) : base(game) { }

        public override void Enter()
        {
            Game.SetNeutralAccent();
            Game.UI.HideAll();
            Game.UI.SetBanner(null);
            Game.UI.ShowTitle("Quantos jogadores?", "Pare a mão sobre um botão até o anel completar");
            Game.UI.ShowPlayerCountButtons(true);
            Game.UI.ShowSurpriseToggle(true, Game.SurpriseMode);
            Game.UI.SetSkeletonLayout(SkeletonLayout.Hidden);
        }

        public override void Exit()
        {
            Game.UI.ShowPlayerCountButtons(false);
            Game.UI.ShowSurpriseToggle(false, false);
        }
    }

    /// <summary>
    /// SorteioTema: roleta que desacelera e revela o tema da rodada.
    /// No modo "Regras surpresa", logo depois gira uma segunda roleta com a regra da rodada.
    /// </summary>
    public sealed class ThemeDrawState : GameStateBase
    {
        private const float RuleDelay = 1.0f;      // pausa entre revelar o tema e começar a roleta da regra
        private const float RuleRoulette = 1.8f;   // duração da roleta da regra
        private const float RuleHold = 2.2f;       // tempo com a regra na tela antes de seguir

        private float elapsed;
        private float swapTimer;
        private float swapInterval;
        private bool revealed;
        private bool ruleRevealed;
        private float ruleSwapTimer;
        private float ruleSwapInterval;
        private int lastIndex = -1;
        private int lastRule = -1;

        public ThemeDrawState(GameManager game) : base(game) { }

        private bool HasRule => Game.Match.CurrentRule != null;
        private float RevealAt => Game.Settings.ThemeRouletteSeconds;
        private float RuleStartAt => RevealAt + RuleDelay;
        private float RuleRevealAt => RuleStartAt + RuleRoulette;
        private float EndAt => HasRule ? RuleRevealAt + RuleHold : RevealAt + Game.Settings.ThemeRevealSeconds;

        public override void Enter()
        {
            MatchState match = Game.Match;
            match.BeginRound();

            Game.SetPlayerAccent(match.Master);
            Game.UI.HideAll();
            Game.UI.SetSkeletonLayout(SkeletonLayout.Hidden);
            Game.UI.SetBanner($"Rodada {match.RoundIndex + 1} de {match.RoundCount}  -  Mestre: {match.Master.DisplayName}");

            elapsed = 0f;
            swapTimer = 0f;
            swapInterval = 0.06f;
            ruleSwapTimer = 0f;
            ruleSwapInterval = 0.07f;
            revealed = false;
            ruleRevealed = false;
            ShowRandomTheme();
        }

        public override void Tick(float deltaTime)
        {
            elapsed += deltaTime;

            if (!revealed)
            {
                if (elapsed >= RevealAt)
                {
                    Reveal();
                    return;
                }

                swapTimer -= deltaTime;
                if (swapTimer <= 0f)
                {
                    ShowRandomTheme();
                    swapInterval *= 1.15f; // desacelera como uma roleta
                    swapTimer = swapInterval;
                }
                return;
            }

            if (HasRule && !ruleRevealed && elapsed >= RuleStartAt)
            {
                if (elapsed >= RuleRevealAt)
                {
                    RevealRule();
                    return;
                }

                ruleSwapTimer -= deltaTime;
                if (ruleSwapTimer <= 0f)
                {
                    ShowRandomRule();
                    ruleSwapInterval *= 1.18f;
                    ruleSwapTimer = ruleSwapInterval;
                }
                return;
            }

            if (elapsed >= EndAt) Game.GoTo(GameState.Posicionamento);
        }

        public override void OnConfirm()
        {
            if (!revealed) elapsed = RevealAt;
            else if (HasRule && !ruleRevealed) elapsed = RuleRevealAt;
            else Game.GoTo(GameState.Posicionamento);
        }

        public override void OnGesture(GestureType gesture)
        {
            if (gesture == GestureType.BothHandsUp && Game.ShortcutGesturesEnabled) OnConfirm();
        }

        private void Reveal()
        {
            revealed = true;
            elapsed = RevealAt;
            Game.UI.ShowTheme("O TEMA É", Game.Match.CurrentTheme.displayText, Game.Match.Master.Color, pop: true);
            Game.PlaySound(GameSound.Reveal);
        }

        private void RevealRule()
        {
            ruleRevealed = true;
            elapsed = RuleRevealAt;
            Game.UI.ShowTitle($"Regra surpresa: <color=#FFD23F>{Game.Match.CurrentRule}</color>");
            Game.PlaySound(GameSound.SurpriseRule);
        }

        private void ShowRandomTheme()
        {
            var all = Game.Match.AllThemes;
            if (all.Count == 0) return;

            int index = Random.Range(0, all.Count);
            if (all.Count > 1 && index == lastIndex) index = (index + 1) % all.Count;
            lastIndex = index;
            Game.UI.ShowTheme("SORTEANDO O TEMA...", all[index].displayText, Color.white);
            Game.PlaySound(GameSound.Tick);
        }

        private void ShowRandomRule()
        {
            var all = Game.Match.AllRules;
            int index = Random.Range(0, all.Count);
            if (all.Count > 1 && index == lastRule) index = (index + 1) % all.Count;
            lastRule = index;
            Game.UI.ShowTitle($"Regra surpresa: {all[index]}");
            Game.PlaySound(GameSound.Tick);
        }
    }
}
