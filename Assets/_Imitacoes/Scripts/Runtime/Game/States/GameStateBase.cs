using Imitacoes.Interaction;

namespace Imitacoes.Game
{
    /// <summary>
    /// Base dos estados. Cada estado recebe o <see cref="GameManager"/> como contexto
    /// (UI, sensores, gravação, pontuação, partida) e decide para onde ir com <c>Game.GoTo</c>.
    /// </summary>
    public abstract class GameStateBase
    {
        protected readonly GameManager Game;

        protected GameStateBase(GameManager game)
        {
            Game = game;
        }

        /// <summary>O estado precisa de um jogador na frente do sensor (ativa os avisos de posicionamento).</summary>
        public virtual bool RequiresPlayer => false;

        public virtual void Enter() { }
        public virtual void Tick(float deltaTime) { }
        public virtual void Exit() { }

        /// <summary>Um gesto foi completado (mão levantada, duas mãos...).</summary>
        public virtual void OnGesture(GestureType gesture) { }

        /// <summary>Atalho "confirmar/pular": duas mãos para cima (se ativado) ou Enter no modo debug.</summary>
        public virtual void OnConfirm() { }
    }
}
