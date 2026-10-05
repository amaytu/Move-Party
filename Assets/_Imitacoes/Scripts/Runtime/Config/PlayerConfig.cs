using UnityEngine;

namespace Imitacoes.Config
{
    /// <summary>
    /// Identidade visual de um jogador: número, nome de exibição e cor.
    /// É a ÚNICA fonte da cor do jogador — esqueleto, bordas, textos, barras e placar leem daqui.
    ///
    /// Crie 4 assets em Data/Players (Jogador1..Jogador4) via
    /// <i>Create &gt; Imitações &gt; Config &gt; Jogador</i>.
    /// </summary>
    [CreateAssetMenu(fileName = "Jogador", menuName = "Imitações/Config/Jogador", order = 0)]
    public sealed class PlayerConfig : ScriptableObject
    {
        [SerializeField, Range(1, 4), Tooltip("Número do jogador (1 a 4). Define a ordem de Mestre.")]
        private int playerNumber = 1;

        [SerializeField, Tooltip("Texto exibido na tela. Vazio = \"Jogador N\".")]
        private string displayName = "Jogador 1";

        [SerializeField, Tooltip("Cor principal do jogador.")]
        private Color color = new Color(0.90f, 0.15f, 0.15f);

        [SerializeField, Tooltip("Cor do texto escrito SOBRE a cor do jogador (contraste). " +
                                 "Ex.: preto sobre amarelo, branco sobre azul.")]
        private Color contrastTextColor = Color.white;

        [SerializeField, Range(0f, 1f), Tooltip("Opacidade do esqueleto \"fantasma\" desta cor.")]
        private float ghostAlpha = 0.35f;

        public int PlayerNumber => playerNumber;

        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? $"Jogador {playerNumber}" : displayName;

        public Color Color => color;

        public Color ContrastTextColor => contrastTextColor;

        /// <summary>Cor do jogador com a opacidade do fantasma.</summary>
        public Color GhostColor => WithAlpha(ghostAlpha);

        /// <summary>Hex sem "#" (ex.: "E62626"), para rich text do TextMeshPro.</summary>
        public string ColorHex => ColorUtility.ToHtmlStringRGB(color);

        /// <summary>Nome já colorido para TextMeshPro: <c>&lt;color=#E62626&gt;Jogador 1&lt;/color&gt;</c>.</summary>
        public string ColoredName => $"<color=#{ColorHex}>{DisplayName}</color>";

        public Color WithAlpha(float alpha) => new Color(color.r, color.g, color.b, alpha);

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(displayName)) displayName = $"Jogador {playerNumber}";
        }

        public override string ToString() => $"{DisplayName} (#{ColorHex})";
    }
}
