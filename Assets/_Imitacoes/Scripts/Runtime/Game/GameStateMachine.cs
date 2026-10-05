using System;
using System.Collections.Generic;
using UnityEngine;

namespace Imitacoes.Game
{
    /// <summary>Estados do jogo (nomes do documento de design).</summary>
    public enum GameState
    {
        TelaInicial,
        SelecaoJogadores,
        SorteioTema,
        Posicionamento,
        ContagemMestre,
        GravandoMestre,
        Replay,
        ContagemImitador,
        GravandoImitador,
        ResultadoRodada,
        PlacarFinal
    }

    /// <summary>
    /// Máquina de estados finita simples: um objeto por estado, com Enter/Tick/Exit.
    ///
    /// Trocas pedidas DENTRO de Enter/Exit são enfileiradas e aplicadas em seguida (sem recursão),
    /// então um estado pode decidir imediatamente pular para outro.
    /// </summary>
    public sealed class GameStateMachine
    {
        private readonly Dictionary<GameState, GameStateBase> states = new Dictionary<GameState, GameStateBase>();
        private bool transitioning;
        private GameState? pending;

        public GameState Current { get; private set; }
        public GameStateBase CurrentHandler { get; private set; }
        public bool HasState => CurrentHandler != null;

        /// <summary>Segundos desde a entrada no estado atual.</summary>
        public float TimeInState { get; private set; }

        /// <summary>(anterior, novo).</summary>
        public event Action<GameState, GameState> StateChanged;

        public void Register(GameState id, GameStateBase handler)
        {
            states[id] = handler ?? throw new ArgumentNullException(nameof(handler));
        }

        public void ChangeState(GameState next)
        {
            if (!states.ContainsKey(next))
            {
                Debug.LogError($"[Imitações] Estado não registrado: {next}");
                return;
            }

            if (transitioning)
            {
                pending = next;
                return;
            }

            transitioning = true;
            try
            {
                GameState? target = next;
                while (target.HasValue)
                {
                    pending = null;
                    GameState previous = Current;
                    CurrentHandler?.Exit();

                    Current = target.Value;
                    CurrentHandler = states[Current];
                    TimeInState = 0f;
                    CurrentHandler.Enter();
                    StateChanged?.Invoke(previous, Current);

                    target = pending;
                }
            }
            finally
            {
                transitioning = false;
            }
        }

        public void Tick(float deltaTime)
        {
            if (CurrentHandler == null) return;
            TimeInState += deltaTime;
            CurrentHandler.Tick(deltaTime);
        }
    }
}
