using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Imitacoes.UI
{
    /// <summary>
    /// Assets visuais compartilhados (gerados pelo Editor em Art/Generated):
    /// um círculo fatiado vira cápsula ou retângulo arredondado de qualquer raio.
    /// </summary>
    [Serializable]
    public sealed class UISkin
    {
        /// <summary>Círculo branco 256 px com borda de fatiamento 128 px.</summary>
        public Sprite circle;

        /// <summary>Anel branco 256 px (molduras, cursor).</summary>
        public Sprite ring;

        /// <summary>Círculo de borda suave (sombras e brilhos).</summary>
        public Sprite soft;

        /// <summary>Fonte principal (Lilita One). Null = fonte padrão do TMP.</summary>
        public TMP_FontAsset font;

        /// <summary>Metade do tamanho do sprite do círculo = borda de fatiamento (px).</summary>
        public const float CircleBorder = 128f;

        /// <summary>
        /// Configura uma Image fatiada com cantos de raio <paramref name="radius"/> (px).
        /// Se a altura for 2×raio, vira uma cápsula (pílula).
        /// </summary>
        public static void MakeRounded(Image image, Sprite sprite, float radius)
        {
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = CircleBorder / Mathf.Max(1f, radius);
        }
    }

    /// <summary>Paleta da interface (fundo escuro, alto contraste, leitura a distância).</summary>
    public static class Palette
    {
        public static readonly Color Background = new Color(0.055f, 0.06f, 0.11f, 1f);
        public static readonly Color BackgroundGlow = new Color(0.28f, 0.3f, 0.62f, 0.55f);
        public static readonly Color Card = new Color(0.13f, 0.14f, 0.24f, 0.96f);
        public static readonly Color CardHover = new Color(0.19f, 0.21f, 0.34f, 1f);
        public static readonly Color Track = new Color(1f, 1f, 1f, 0.09f);
        public static readonly Color Shadow = new Color(0f, 0f, 0.02f, 0.55f);
        public static readonly Color TextMain = Color.white;
        public static readonly Color TextSoft = new Color(0.75f, 0.78f, 0.9f, 1f);
        public static readonly Color Warning = new Color(1f, 0.74f, 0.2f, 1f);
        public static readonly Color Outline = new Color(0.03f, 0.035f, 0.07f, 1f);
        public static readonly Color Record = new Color(1f, 0.25f, 0.3f, 1f);

        /// <summary>As 4 cores dos jogadores (para o título e as bolhas do fundo).</summary>
        public static readonly Color[] Players =
        {
            new Color(0.93f, 0.2f, 0.24f), new Color(0.2f, 0.78f, 0.35f),
            new Color(1f, 0.82f, 0.15f), new Color(0.2f, 0.5f, 1f)
        };
    }
}
