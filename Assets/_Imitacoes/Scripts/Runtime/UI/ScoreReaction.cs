using System;
using TMPro;
using UnityEngine;

namespace Imitacoes.UI
{
    /// <summary>
    /// Reação à nota do imitador:
    /// 1. o número sobe de 0 até a nota (com tique a cada passo);
    /// 2. ao chegar, dá um "pop" e mostra uma frase conforme a faixa (engraçada nas notas baixas);
    /// 3. nota ≥ <see cref="ConfettiThreshold"/>: chuva de confete (<see cref="ConfettiRain"/>).
    ///
    /// É montada em tempo de execução por cima do Canvas do <see cref="GameUI"/>, então não
    /// precisa recriar a cena.
    /// </summary>
    public sealed class ScoreReaction : MonoBehaviour
    {
        public const float ConfettiThreshold = 80f;
        private const float CountSeconds = 1.3f;
        private const int ConfettiCount = 110;
        private ConfettiRain confetti;

        /// <summary>Disparado a cada número novo durante a contagem (para o som de tique).</summary>
        public event Action<int> Ticked;

        /// <summary>Disparado quando a contagem chega na nota final.</summary>
        public event Action<float> Finished;

        private TMP_Text numberText;
        private TMP_Text pointsLabel;
        private TMP_Text phraseText;
        private RectTransform numberRoot;

        private float target;
        private float elapsed;
        private int lastShown = -1;
        private bool counting;
        private float popTime = -1f;

        private static readonly System.Random Rng = new System.Random();

        // ------------------------------------------------------------------ Frases

        private static readonly string[] Perfect =
        {
            "PERFEITO! Clone detectado!", "Isso é bruxaria?!", "Copiou até a alma!", "O Mestre foi superado!",
        };
        private static readonly string[] Great =
        {
            "Mandou muito bem!", "Quase um espelho!", "O Mestre tá suando frio!", "Show de bola!",
        };
        private static readonly string[] Ok =
        {
            "Boa! Dá pra melhorar", "Tá no caminho certo!", "Meio parecido... meio não", "O espírito estava lá!",
        };
        private static readonly string[] Weak =
        {
            "Hmm... criativo", "Você viu o mesmo replay?", "Interpretação livre!", "Foi quase... quase nada",
        };
        private static readonly string[] Awful =
        {
            "Isso foi uma imitação?!", "O sensor chorou", "Você imitou outro jogo?",
            "Nota artística: 10. Imitação: 0", "Da próxima vez, de olhos abertos!", "Ninguém viu nada. Próximo!",
        };

        public static string PhraseFor(float score)
        {
            string[] pool = score >= 95f ? Perfect : score >= 80f ? Great : score >= 50f ? Ok : score >= 30f ? Weak : Awful;
            return pool[Rng.Next(pool.Length)];
        }

        // ------------------------------------------------------------------ Criação

        /// <summary>Monta a reação como filho do Canvas, por cima de tudo.</summary>
        public static ScoreReaction Create(Transform canvas, TMP_FontAsset font)
        {
            RectTransform root = UIFactory.Rect("Reação da Nota", canvas).Stretch();
            root.SetAsLastSibling();
            var reaction = root.gameObject.AddComponent<ScoreReaction>();
            reaction.Build(font);
            root.gameObject.SetActive(false);
            return reaction;
        }

        private void Build(TMP_FontAsset font)
        {
            TMP_FontAsset previous = UIFactory.Font;
            if (font != null) UIFactory.Font = font;

            confetti = ConfettiRain.Create(transform);

            numberRoot = UIFactory.Rect("Número", transform).Place(new Vector2(0.5f, 0.5f), new Vector2(900f, 340f), new Vector2(0f, 40f));
            numberText = UIFactory.Text("Nota", numberRoot, "0", 300f, Color.white);
            numberText.rectTransform.Stretch();
            numberText.outlineWidth = 0.18f;
            numberText.outlineColor = new Color32(20, 18, 40, 255);

            pointsLabel = UIFactory.Text("Pontos", transform, "pontos", 64f, new Color(1f, 1f, 1f, 0.85f));
            pointsLabel.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(600f, 90f), new Vector2(0f, -150f));

            phraseText = UIFactory.Text("Frase", transform, string.Empty, 78f, Color.white);
            phraseText.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(1600f, 140f), new Vector2(0f, -260f));
            phraseText.outlineWidth = 0.15f;
            phraseText.outlineColor = new Color32(20, 18, 40, 255);

            UIFactory.Font = previous;
        }

        // ------------------------------------------------------------------ API

        public void Show(float score, Color playerColor)
        {
            gameObject.SetActive(true);
            transform.SetAsLastSibling();

            target = Mathf.Clamp(score, 0f, 100f);
            elapsed = 0f;
            lastShown = -1;
            counting = true;
            popTime = -1f;

            numberText.color = playerColor;
            numberText.text = "0";
            numberRoot.localScale = Vector3.one;
            phraseText.text = string.Empty;
            confetti.Clear();
        }

        public void Hide()
        {
            counting = false;
            confetti.Clear();
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ Animação

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;

            if (counting)
            {
                elapsed += dt;
                float t = Mathf.Clamp01(elapsed / CountSeconds);
                float eased = 1f - Mathf.Pow(1f - t, 3f); // desacelera no fim
                int shown = Mathf.RoundToInt(target * eased);
                if (shown != lastShown)
                {
                    lastShown = shown;
                    numberText.text = shown.ToString();
                    Ticked?.Invoke(shown);
                }
                // Número "pulsa" um pouco enquanto sobe.
                numberRoot.localScale = Vector3.one * (1f + 0.04f * Mathf.Sin(elapsed * 40f) * (1f - t));

                if (t >= 1f)
                {
                    counting = false;
                    numberText.text = Mathf.RoundToInt(target).ToString();
                    phraseText.text = PhraseFor(target);
                    popTime = Time.unscaledTime;
                    if (target >= ConfettiThreshold) confetti.Burst(ConfettiCount, new Vector2(0f, 40f));
                    Finished?.Invoke(target);
                }
            }

            // Pop final (ease-out-back).
            if (popTime >= 0f)
            {
                float p = Mathf.Clamp01((Time.unscaledTime - popTime) / 0.4f);
                float u = p - 1f;
                const float s = 1.70158f;
                float eased = 1f + (s + 1f) * u * u * u + s * u * u;
                float from = target >= ConfettiThreshold ? 1.45f : 1.2f;
                numberRoot.localScale = Vector3.one * Mathf.LerpUnclamped(from, 1f, eased);
                phraseText.rectTransform.localScale = Vector3.one * Mathf.LerpUnclamped(0.3f, 1f, eased);
                if (p >= 1f) popTime = -1f;
            }
        }

        private static float Range(float min, float max) => min + (float)Rng.NextDouble() * (max - min);
    }
}
