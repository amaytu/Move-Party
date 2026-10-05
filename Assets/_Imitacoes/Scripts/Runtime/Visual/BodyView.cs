using Imitacoes.Body;
using UnityEngine;

namespace Imitacoes.Visual
{
    /// <summary>Onde o boneco fica na tela.</summary>
    public enum BodySlot
    {
        Center,
        Left,
        Right
    }

    /// <summary>
    /// Base comum dos desenhos do jogador: <see cref="SkeletonView"/> (2D, na UI) e
    /// <see cref="Avatar3DView"/> (3D, no mundo). O resto do jogo (UI, replay, fantasma)
    /// só conhece esta classe, então trocar 2D ↔ 3D é só trocar o componente na cena.
    /// </summary>
    public abstract class BodyView : MonoBehaviour
    {
        /// <summary>Desenha o corpo (null = esconde).</summary>
        public abstract void Render(BodyFrame body);

        /// <summary>Esconde o boneco.</summary>
        public abstract void Clear();

        /// <param name="alpha">1 = normal; &lt; 1 = fantasma (cor "apagada" em direção ao fundo).</param>
        public abstract void SetColor(Color color, float alpha);

        /// <summary>Esquece a referência de posição (trocar de jogador ou de clipe).</summary>
        public abstract void ResetReference();

        /// <summary>
        /// Posição na tela. <paramref name="followPlayerX"/> = o boneco anda para os lados junto com
        /// o jogador; senão fica centralizado no seu lugar (replay e fantasma).
        /// </summary>
        public abstract void SetSlot(BodySlot slot, bool followPlayerX);

        public void SetVisible(bool visible)
        {
            if (gameObject.activeSelf != visible) gameObject.SetActive(visible);
        }
    }
}
