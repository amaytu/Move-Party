using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Imitacoes.UI
{
    /// <summary>
    /// Chuva de confete na UI: pedaços de papel coloridos com gravidade, giro, "balanço" e fade.
    /// Metade explode de um ponto, metade cai do topo da tela.
    /// </summary>
    public sealed class ConfettiRain : MonoBehaviour
    {
        private sealed class Piece
        {
            public RectTransform rt;
            public Image image;
            public Vector2 velocity;
            public float spin;
            public float life;
            public float maxLife;
        }

        private static readonly Color[] Palette =
        {
            new Color(0.95f, 0.26f, 0.26f), new Color(0.25f, 0.82f, 0.38f), new Color(1f, 0.82f, 0.15f),
            new Color(0.25f, 0.55f, 1f), new Color(0.85f, 0.4f, 1f), Color.white,
        };

        private readonly List<Piece> pieces = new List<Piece>();
        private static readonly System.Random Rng = new System.Random();

        public static ConfettiRain Create(Transform parent)
        {
            RectTransform rt = UIFactory.Rect("Confete", parent).Stretch();
            return rt.gameObject.AddComponent<ConfettiRain>();
        }

        /// <param name="origin">Ponto da explosão (relativo ao centro deste retângulo, em px de referência).</param>
        public void Burst(int count, Vector2 origin)
        {
            Rect area = ((RectTransform)transform).rect;
            for (int i = 0; i < count; i++)
            {
                Image image = UIFactory.Image("Pedaço", transform, Palette[i % Palette.Length]);
                RectTransform rt = image.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(Range(14f, 26f), Range(26f, 44f));

                bool burst = i % 2 == 0;
                rt.anchoredPosition = burst
                    ? origin + new Vector2(Range(-60f, 60f), Range(0f, 80f))
                    : new Vector2(Range(-area.width * 0.5f, area.width * 0.5f), area.height * 0.5f + Range(20f, 300f));
                float angle = Range(0f, Mathf.PI * 2f);
                Vector2 velocity = burst
                    ? new Vector2(Mathf.Cos(angle), Mathf.Abs(Mathf.Sin(angle)) + 0.3f) * Range(700f, 1500f)
                    : new Vector2(Range(-120f, 120f), Range(-300f, -100f));

                pieces.Add(new Piece
                {
                    rt = rt, image = image, velocity = velocity,
                    spin = Range(-540f, 540f), life = 0f, maxLife = Range(2.6f, 3.8f),
                });
            }
        }

        public void Clear()
        {
            foreach (Piece p in pieces) if (p.rt != null) Destroy(p.rt.gameObject);
            pieces.Clear();
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            for (int i = pieces.Count - 1; i >= 0; i--)
            {
                Piece p = pieces[i];
                p.life += dt;
                p.velocity += new Vector2(0f, -1400f) * dt;   // gravidade
                p.velocity *= 1f - 1.6f * dt;                 // resistência do ar
                p.rt.anchoredPosition += p.velocity * dt;
                p.rt.localRotation = Quaternion.Euler(0f, 0f, p.rt.localEulerAngles.z + p.spin * dt);
                p.rt.localScale = new Vector3(Mathf.Cos(p.life * 6f + i), 1f, 1f); // papel balançando

                float fade = Mathf.Clamp01((p.maxLife - p.life) / 0.6f);
                Color c = p.image.color;
                p.image.color = new Color(c.r, c.g, c.b, fade);

                if (p.life >= p.maxLife)
                {
                    Destroy(p.rt.gameObject);
                    pieces.RemoveAt(i);
                }
            }
        }

        private void OnDisable() => Clear();

        private static float Range(float min, float max) => min + (float)Rng.NextDouble() * (max - min);
    }
}
