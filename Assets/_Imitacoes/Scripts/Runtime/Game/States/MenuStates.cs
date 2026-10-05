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
            Game.UI.SetSkeletonLayout(SkeletonLayout.Hidden);
        }

        public override void Exit() => Game.UI.ShowPlayerCountButtons(false);
    }

    /// <summary>SorteioTema: roleta que desacelera e revela o tema da rodada.</summary>
    public sealed class ThemeDrawState : GameStateBase
    {
        private float elapsed;
        private float swapTimer;
        private float swapInterval;
        private bool revealed;
        private int lastIndex = -1;

        public ThemeDrawState(GameManager game) : base(game) { }

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
            revealed = false;
            ShowRandomTheme();
        }

        public override void Tick(float deltaTime)
        {
            elapsed += deltaTime;
            GameSettings s = Game.Settings;

            if (!revealed)
            {
                if (elapsed >= s.ThemeRouletteSeconds)
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
            }
            else if (elapsed >= s.ThemeRouletteSeconds + s.ThemeRevealSeconds)
            {
                Game.GoTo(GameState.Posicionamento);
            }
        }

        public override void OnConfirm()
        {
            if (!revealed) elapsed = Game.Settings.ThemeRouletteSeconds;
            else Game.GoTo(GameState.Posicionamento);
        }

        public override void OnGesture(GestureType gesture)
        {
            if (gesture == GestureType.BothHandsUp && Game.ShortcutGesturesEnabled) OnConfirm();
        }

        private void Reveal()
        {
            revealed = true;
            elapsed = Game.Settings.ThemeRouletteSeconds;
            Game.UI.ShowTheme("O TEMA É", Game.Match.CurrentTheme.displayText, Game.Match.Master.Color, pop: true);
            Game.PlaySound(GameSound.Reveal);
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
    }
}
