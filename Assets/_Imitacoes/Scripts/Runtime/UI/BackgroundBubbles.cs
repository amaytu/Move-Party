using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Imitacoes.UI
{
    /// <summary>
    /// Fundo decorativo: bolhas translúcidas nas cores dos jogadores subindo devagar.
    /// Dá movimento à tela sem competir com o conteúdo (alfa bem baixo).
    /// As bolhas são criadas em tempo de execução.
    /// </summary>
    [AddComponentMenu("Imitações/Background Bubbles")]
    [RequireComponent(typeof(RectTransform))]
    public sealed class BackgroundBubbles : MonoBehaviour
    {
        [SerializeField] private Sprite sprite;
        [SerializeField, Range(0, 40)] private int count = 16;
        [SerializeField] private Vector2 sizeRange = new Vector2(60f, 280f);
        [SerializeField] private Vector2 speedRange = new Vector2(12f, 40f);
        [SerializeField, Range(0f, 0.3f)] private float opacity = 0.07f;

        private readonly List<Bubble> bubbles = new List<Bubble>();
        private RectTransform area;

        private sealed class Bubble
        {
            public RectTransform Rect;
            public float Speed;
            public float Phase;
            public float Sway;
        }

        public void SetSprite(Sprite bubbleSprite) => sprite = bubbleSprite;

        private void Start()
        {
            area = (RectTransform)transform;
            for (int i = 0; i < count; i++) bubbles.Add(CreateBubble(i, randomHeight: true));
        }

        private Bubble CreateBubble(int index, bool randomHeight)
        {
            var go = new GameObject("Bolha", typeof(RectTransform), typeof(Image));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(transform, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);

            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;
            Color c = Palette.Players[index % Palette.Players.Length];
            image.color = new Color(c.r, c.g, c.b, opacity);

            var bubble = new Bubble { Rect = rt };
            Respawn(bubble, randomHeight);
            return bubble;
        }

        private void Respawn(Bubble b, bool randomHeight)
        {
            Rect r = area.rect;
            float size = Random.Range(sizeRange.x, sizeRange.y);
            b.Rect.sizeDelta = new Vector2(size, size);
            b.Speed = Random.Range(speedRange.x, speedRange.y);
            b.Phase = Random.Range(0f, 100f);
            b.Sway = Random.Range(10f, 40f);
            float y = randomHeight ? Random.Range(r.yMin, r.yMax) : r.yMin - size;
            b.Rect.anchoredPosition = new Vector2(Random.Range(r.xMin, r.xMax), y);
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            float top = area.rect.yMax;
            foreach (Bubble b in bubbles)
            {
                Vector2 p = b.Rect.anchoredPosition;
                p.y += b.Speed * dt;
                p.x += Mathf.Sin((Time.unscaledTime + b.Phase) * 0.5f) * b.Sway * dt;
                b.Rect.anchoredPosition = p;
                if (p.y - b.Rect.sizeDelta.y > top) Respawn(b, randomHeight: false);
            }
        }
    }
}
