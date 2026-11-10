using System.Collections.Generic;
using Imitacoes.Game;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Imitacoes.UI
{
    /// <summary>
    /// Prêmios engraçados do placar final ("Mais parecido", "Mestre mais cruel", "Mais criativo"),
    /// em cartões na lateral direita. Montado em tempo de execução pelo <see cref="GameUI"/>.
    /// </summary>
    public sealed class AwardsPanel : MonoBehaviour
    {
        private static readonly Color Gold = new Color(1f, 0.82f, 0.25f);
        private const float CardWidth = 620f;
        private const float CardHeight = 150f;

        private TMP_FontAsset font;
        private Sprite circle;
        private readonly List<GameObject> cards = new List<GameObject>();
        private float shownAt;

        public static AwardsPanel Create(Transform canvas, TMP_FontAsset font, Sprite circle)
        {
            RectTransform root = UIFactory.Rect("Prêmios", canvas).Place(new Vector2(0.8f, 0.55f), new Vector2(CardWidth, 3 * CardHeight + 2 * 24f));
            var panel = root.gameObject.AddComponent<AwardsPanel>();
            panel.font = font;
            panel.circle = circle;
            root.gameObject.SetActive(false);
            return panel;
        }

        public void Show(IReadOnlyList<MatchState.Award> awards)
        {
            Clear();
            gameObject.SetActive(awards.Count > 0);
            shownAt = Time.unscaledTime;

            TMP_FontAsset previous = UIFactory.Font;
            if (font != null) UIFactory.Font = font;

            for (int i = 0; i < awards.Count && i < 3; i++)
            {
                MatchState.Award a = awards[i];
                RectTransform card = UIFactory.Rect($"Prêmio {i + 1}", transform);
                card.anchorMin = card.anchorMax = new Vector2(0.5f, 1f);
                card.sizeDelta = new Vector2(CardWidth, CardHeight);
                card.anchoredPosition = new Vector2(0f, -CardHeight * 0.5f - i * (CardHeight + 24f));

                UIFactory.Rounded("Sombra", card, Palette.Shadow, circle, 40f).rectTransform.Stretch(-4f, 4f, -4f, -12f);
                UIFactory.Rounded("Fundo", card, Palette.Card, circle, 40f).rectTransform.Stretch();
                Image bar = UIFactory.Rounded("Cor", card, a.Player.Color, circle, 10f);
                bar.rectTransform.Place(new Vector2(0f, 0.5f), new Vector2(20f, CardHeight - 40f), new Vector2(26f, 0f));

                TMP_Text title = UIFactory.Text("Título", card, a.Title.ToUpperInvariant(), 34f, Gold, TextAlignmentOptions.Left, FontStyles.Normal);
                title.rectTransform.Place(new Vector2(0f, 1f), new Vector2(CardWidth - 70f, 46f), new Vector2(CardWidth * 0.5f + 25f, -32f));

                TMP_Text name = UIFactory.Text("Jogador", card, a.Player.ColoredName, 46f, Color.white, TextAlignmentOptions.Left, FontStyles.Normal);
                name.rectTransform.Place(new Vector2(0f, 0.5f), new Vector2(CardWidth - 70f, 56f), new Vector2(CardWidth * 0.5f + 25f, -2f));

                TMP_Text detail = UIFactory.Text("Detalhe", card, a.Detail, 26f, Palette.TextSoft, TextAlignmentOptions.Left, FontStyles.Normal);
                detail.rectTransform.Place(new Vector2(0f, 0f), new Vector2(CardWidth - 70f, 40f), new Vector2(CardWidth * 0.5f + 25f, 28f));
                detail.enableAutoSizing = true;
                detail.fontSizeMax = 26f;
                detail.fontSizeMin = 18f;

                card.localScale = Vector3.zero;
                cards.Add(card.gameObject);
            }

            UIFactory.Font = previous;
        }

        public void Hide()
        {
            Clear();
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }

        private void Clear()
        {
            foreach (GameObject c in cards) if (c != null) Destroy(c);
            cards.Clear();
        }

        private void Update()
        {
            // Cartões entram um depois do outro, com "pop".
            for (int i = 0; i < cards.Count; i++)
            {
                float t = Mathf.Clamp01((Time.unscaledTime - shownAt - 1.2f - 0.45f * i) / 0.4f);
                float u = t - 1f;
                const float s = 1.70158f;
                float eased = t <= 0f ? 0f : 1f + (s + 1f) * u * u * u + s * u * u;
                cards[i].transform.localScale = Vector3.one * eased;
            }
        }
    }
}
