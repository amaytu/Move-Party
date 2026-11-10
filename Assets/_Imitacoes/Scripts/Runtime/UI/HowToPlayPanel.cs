using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Imitacoes.UI
{
    /// <summary>
    /// Tela "Como jogar": 4 cartões ilustrados com bonequinhos (feitos só com formas arredondadas,
    /// nas cores dos jogadores). É montada em tempo de execução por cima do Canvas do
    /// <see cref="GameUI"/>, então não precisa recriar a cena.
    /// </summary>
    public sealed class HowToPlayPanel : MonoBehaviour
    {
        private const float CardWidth = 400f;
        private const float CardHeight = 520f;
        private const float CardGap = 34f;

        private RectTransform[] cards;
        private float shownAt;

        private enum Pose { HandUp, ArmsWide, Copy, PointButton }

        private struct Step
        {
            public string title;
            public string text;
            public Pose pose;
        }

        private static readonly Step[] Steps =
        {
            new Step { title = "Levante a mão", pose = Pose.HandUp,
                text = "Na sua vez, fique de corpo inteiro na frente do sensor e levante a mão acima da cabeça." },
            new Step { title = "O Mestre cria", pose = Pose.ArmsWide,
                text = "Um tema é sorteado e o Mestre inventa um movimento de 5 segundos." },
            new Step { title = "Imite de memória", pose = Pose.Copy,
                text = "Todos assistem uma vez. Depois, cada um repete SEM ver o Mestre. Mais parecido = mais pontos!" },
            new Step { title = "Dicas", pose = Pose.PointButton,
                text = "Para apertar um botão, pare a mão em cima até o anel encher. Braços cruzados no peito = pausa." },
        };

        // ------------------------------------------------------------------ Criação

        public static HowToPlayPanel Create(Transform canvas, TMP_FontAsset font, Sprite circle)
        {
            RectTransform root = UIFactory.Rect("Como Jogar", canvas).Stretch();
            var panel = root.gameObject.AddComponent<HowToPlayPanel>();
            panel.Build(font, circle);
            root.gameObject.SetActive(false);
            return panel;
        }

        private void Build(TMP_FontAsset font, Sprite circle)
        {
            TMP_FontAsset previous = UIFactory.Font;
            if (font != null) UIFactory.Font = font;

            RectTransform row = UIFactory.Rect("Cartões", transform)
                .Place(new Vector2(0.5f, 0.36f), new Vector2(4 * CardWidth + 3 * CardGap, CardHeight));

            cards = new RectTransform[Steps.Length];
            for (int i = 0; i < Steps.Length; i++)
            {
                float x = -((Steps.Length - 1) * (CardWidth + CardGap)) * 0.5f + i * (CardWidth + CardGap);
                cards[i] = BuildCard(row, i, Steps[i], new Vector2(x, 0f), circle);
            }

            UIFactory.Font = previous;
        }

        private static RectTransform BuildCard(Transform parent, int index, Step step, Vector2 position, Sprite circle)
        {
            Color color = Palette.Players[index % Palette.Players.Length];
            RectTransform card = UIFactory.Rect($"Passo {index + 1}", parent).Place(new Vector2(0.5f, 0.5f), new Vector2(CardWidth, CardHeight), position);

            UIFactory.Rounded("Sombra", card, Palette.Shadow, circle, 48f).rectTransform.Stretch(-6f, 4f, -6f, -18f);
            UIFactory.Rounded("Fundo", card, Palette.Card, circle, 48f).rectTransform.Stretch();
            Image stripe = UIFactory.Rounded("Faixa", card, color, circle, 8f);
            stripe.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(CardWidth - 120f, 16f), new Vector2(0f, -8f));

            // Número do passo.
            Image medal = UIFactory.Circle("Número", card, color, circle);
            medal.rectTransform.Place(new Vector2(0f, 1f), new Vector2(78f, 78f), new Vector2(58f, -58f));
            UIFactory.Text("Texto", medal.transform, (index + 1).ToString(), 48f, Palette.Outline, style: FontStyles.Normal)
                .rectTransform.Stretch(0f, 0f, 0f, 4f);

            // Ilustração.
            RectTransform art = UIFactory.Rect("Ilustração", card).Place(new Vector2(0.5f, 1f), new Vector2(CardWidth, 230f), new Vector2(0f, -150f));
            Illustrate(art, step.pose, color, circle);

            TMP_Text title = UIFactory.Text("Título", card, step.title, 50f, Palette.TextMain, style: FontStyles.Normal);
            title.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(CardWidth - 40f, 70f), new Vector2(0f, -305f));
            title.textWrappingMode = TextWrappingModes.NoWrap;
            title.enableAutoSizing = true;
            title.fontSizeMax = 50f;
            title.fontSizeMin = 30f;

            TMP_Text text = UIFactory.Text("Descrição", card, step.text, 30f, Palette.TextSoft, style: FontStyles.Normal);
            text.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(CardWidth - 56f, 160f), new Vector2(0f, 100f));
            text.enableAutoSizing = true;
            text.fontSizeMax = 30f;
            text.fontSizeMin = 20f;
            return card;
        }

        // ------------------------------------------------------------------ Ilustrações

        private static void Illustrate(RectTransform art, Pose pose, Color color, Sprite circle)
        {
            switch (pose)
            {
                case Pose.HandUp:
                    Figure(art, new Vector2(0f, -10f), color, 1f, leftArm: 15f, rightArm: 165f, circle);
                    Sparkle(art, new Vector2(70f, 95f), color, circle);
                    break;

                case Pose.ArmsWide:
                    Figure(art, new Vector2(0f, -10f), color, 1f, leftArm: 100f, rightArm: 125f, circle);
                    Sparkle(art, new Vector2(-105f, 70f), color, circle);
                    Sparkle(art, new Vector2(110f, 85f), color, circle);
                    break;

                case Pose.Copy:
                    // O Mestre "na memória" (apagado, atrás) e o imitador copiando na frente.
                    Figure(art, new Vector2(-62f, 0f), Color.Lerp(Palette.Card, Palette.TextSoft, 0.35f), 0.8f, 130f, 40f, circle);
                    Figure(art, new Vector2(62f, -12f), color, 1f, 130f, 40f, circle);
                    break;

                case Pose.PointButton:
                    Figure(art, new Vector2(-60f, -10f), color, 1f, 20f, 95f, circle);
                    // Mini botão com o anel do cursor.
                    Image button = UIFactory.Rounded("Botão", art, Palette.CardHover, circle, 20f);
                    button.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(110f, 80f), new Vector2(95f, 20f));
                    Image ring = UIFactory.Circle("Cursor", art, Color.white, circle);
                    ring.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(34f, 34f), new Vector2(95f, 20f));
                    UIFactory.Circle("Ponto", ring.transform, color, circle).rectTransform.Stretch(7f, 7f, 7f, 7f);
                    break;
            }
        }

        /// <summary>
        /// Bonequinho de frente. Ângulos dos braços em graus a partir de "caído" (0) até "para cima" (180),
        /// medidos para fora do corpo.
        /// </summary>
        private static void Figure(RectTransform parent, Vector2 center, Color color, float scale,
            float leftArm, float rightArm, Sprite circle)
        {
            RectTransform f = UIFactory.Rect("Boneco", parent).Place(new Vector2(0.5f, 0.5f), new Vector2(10f, 10f), center);
            f.localScale = Vector3.one * scale;

            const float limb = 22f;
            Limb(f, new Vector2(-16f, -40f), new Vector2(limb, 78f), 4f, color, circle);   // perna esquerda
            Limb(f, new Vector2(16f, -40f), new Vector2(limb, 78f), -4f, color, circle);   // perna direita
            Limb(f, new Vector2(-30f, 42f), new Vector2(limb, 72f), -leftArm, color, circle);  // braço esquerdo (tela)
            Limb(f, new Vector2(30f, 42f), new Vector2(limb, 72f), rightArm, color, circle);   // braço direito (tela)

            Image body = UIFactory.Rounded("Tronco", f, color, circle, 30f);
            body.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(66f, 98f), new Vector2(0f, 4f));

            Image head = UIFactory.Circle("Cabeça", f, color, circle);
            head.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(62f, 62f), new Vector2(0f, 86f));
            // Olhos.
            UIFactory.Circle("Olho", head.transform, Palette.Outline, circle).rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(10f, 14f), new Vector2(-11f, 4f));
            UIFactory.Circle("Olho", head.transform, Palette.Outline, circle).rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(10f, 14f), new Vector2(11f, 4f));
        }

        /// <summary>Cápsula presa pelo topo (ombro/quadril), girada pelo ângulo dado.</summary>
        private static void Limb(RectTransform parent, Vector2 joint, Vector2 size, float angle, Color color, Sprite circle)
        {
            Image limb = UIFactory.Rounded("Membro", parent, color, circle, size.x * 0.5f);
            RectTransform rt = limb.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 1f - size.x * 0.5f / size.y); // gira em volta do centro da ponta de cima
            rt.sizeDelta = size;
            rt.anchoredPosition = joint;
            rt.localRotation = Quaternion.Euler(0f, 0f, angle);
        }

        private static void Sparkle(RectTransform parent, Vector2 position, Color color, Sprite circle)
        {
            Color c = Color.Lerp(color, Color.white, 0.5f);
            for (int i = 0; i < 2; i++)
            {
                Image ray = UIFactory.Rounded("Brilho", parent, c, circle, 4f);
                ray.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(8f, 34f), position);
                ray.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f + 90f * i);
            }
        }

        // ------------------------------------------------------------------ API

        public void Show()
        {
            gameObject.SetActive(true);
            shownAt = Time.unscaledTime;
        }

        public void Hide()
        {
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }

        private void Update()
        {
            // Entrada em sequência: cada cartão sobe e aparece um pouco depois do anterior.
            for (int i = 0; i < cards.Length; i++)
            {
                float t = Mathf.Clamp01((Time.unscaledTime - shownAt - 0.12f * i) / 0.35f);
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                cards[i].localScale = Vector3.one * Mathf.Lerp(0.85f, 1f, eased);
                float x = cards[i].anchoredPosition.x;
                cards[i].anchoredPosition = new Vector2(x, Mathf.Lerp(-60f, 0f, eased) + Mathf.Sin(Time.unscaledTime * 1.6f + i) * 4f * t);
            }
        }
    }
}
