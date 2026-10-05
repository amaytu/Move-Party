using System.Collections.Generic;
using Imitacoes.Body;
using UnityEngine;
using UnityEngine.UI;

namespace Imitacoes.Visual
{
    /// <summary>
    /// Desenha o jogador como um boneco "cartoon" dentro da UI (vista frontal):
    /// membros em cápsulas grossas, tronco arredondado, cabeça grande com olhos,
    /// contorno escuro contínuo (todas as formas de contorno ficam atrás de todos os preenchimentos,
    /// então o boneco parece uma peça só) e sombra no chão.
    ///
    /// Tudo é criado em tempo de execução a partir de um sprite de círculo fatiado
    /// (cápsula = círculo esticado com as bordas preservadas).
    ///
    /// Posicionamento (px, relativo ao centro deste RectTransform):
    /// - Horizontal: segue o X do jogador, ou fica centralizado (<see cref="CenterHorizontally"/>).
    /// - Vertical: quadril centralizado com referência suavizada (pulos e agachamentos continuam visíveis).
    /// </summary>
    [AddComponentMenu("Imitações/Skeleton View")]
    [RequireComponent(typeof(RectTransform))]
    public sealed class SkeletonView : BodyView
    {
        [Header("Aparência")]
        [SerializeField, Tooltip("Círculo branco fatiado (Art/Generated/Circulo).")]
        private Sprite circleSprite;
        [SerializeField, Tooltip("Círculo suave para a sombra no chão (Art/Generated/CirculoSuave).")]
        private Sprite softSprite;
        [SerializeField, Min(10f)] private float pixelsPerMeter = 300f;
        [SerializeField, Min(4f), Tooltip("Espessura dos braços e pernas (px, na escala de 400 px/m).")]
        private float limbThickness = 60f;
        [SerializeField, Min(10f)] private float torsoWidth = 150f;
        [SerializeField, Min(10f)] private float headSize = 175f;
        [SerializeField, Min(4f)] private float handSize = 74f;
        [SerializeField, Min(0f)] private float outlineWidth = 8f;
        [SerializeField, Range(0f, 1f), Tooltip("Contorno = cor do jogador escurecida por este fator.")]
        private float outlineDarken = 0.7f;
        [SerializeField, Range(0f, 1f), Tooltip("Aura suave na cor do jogador atrás do boneco (destaca do fundo escuro).")]
        private float auraOpacity = 0.35f;

        [Header("Posição")]
        [SerializeField, Tooltip("Posição vertical do quadril na tela (px, relativa ao centro).")]
        private float hipScreenY = -175f;
        [SerializeField] private bool centerHorizontally;
        [SerializeField, Min(0f)] private float verticalFollow = 1.5f;

        [Header("Cor")]
        [SerializeField] private Color color = Color.white;
        [SerializeField, Range(0f, 1f), Tooltip("< 1 = fantasma. As cores são misturadas com a cor do fundo " +
            "(opaco), para as partes sobrepostas não formarem manchas como aconteceria com transparência.")]
        private float alpha = 1f;
        [SerializeField, Tooltip("Cor do fundo usada para \"apagar\" o fantasma.")]
        private Color fadeTarget = new Color(0.055f, 0.06f, 0.11f, 1f);

        // ------------------------------------------------------------------ Definição das formas

        private enum Tone { Limb, Torso, Head }

        /// <summary>Uma parte do boneco: cápsula entre dois joints ou círculo num joint.</summary>
        private readonly struct Part
        {
            public readonly JointId A;
            public readonly JointId B;      // igual a A = círculo
            public readonly float Size;     // espessura (cápsula) ou diâmetro (círculo), relativo
            public readonly Tone Tone;

            public Part(JointId a, JointId b, float size, Tone tone)
            {
                A = a; B = b; Size = size; Tone = tone;
            }

            public bool IsCircle => A == B;
        }

        // Ordem = ordem de desenho (pernas atrás, braços na frente do tronco, cabeça por cima).
        private Part[] parts;

        private Part[] BuildParts()
        {
            float limb = limbThickness, foot = limbThickness * 0.85f;
            return new[]
            {
                new Part(JointId.HipLeft, JointId.KneeLeft, limb, Tone.Limb),
                new Part(JointId.KneeLeft, JointId.AnkleLeft, limb, Tone.Limb),
                new Part(JointId.AnkleLeft, JointId.FootLeft, foot, Tone.Limb),
                new Part(JointId.HipRight, JointId.KneeRight, limb, Tone.Limb),
                new Part(JointId.KneeRight, JointId.AnkleRight, limb, Tone.Limb),
                new Part(JointId.AnkleRight, JointId.FootRight, foot, Tone.Limb),

                new Part(JointId.SpineShoulder, JointId.SpineBase, torsoWidth, Tone.Torso),

                new Part(JointId.ShoulderLeft, JointId.ElbowLeft, limb, Tone.Limb),
                new Part(JointId.ElbowLeft, JointId.WristLeft, limb, Tone.Limb),
                new Part(JointId.HandLeft, JointId.HandLeft, handSize, Tone.Limb),
                new Part(JointId.ShoulderRight, JointId.ElbowRight, limb, Tone.Limb),
                new Part(JointId.ElbowRight, JointId.WristRight, limb, Tone.Limb),
                new Part(JointId.HandRight, JointId.HandRight, handSize, Tone.Limb),

                new Part(JointId.Head, JointId.Head, headSize, Tone.Head),
            };
        }

        // ------------------------------------------------------------------ Estado

        private readonly List<Image> outlines = new List<Image>();
        private readonly List<Image> fills = new List<Image>();
        private RectTransform container;
        private Image floorShadow;
        private Image aura;
        private RectTransform face;
        private Image[] eyes;
        private Image shine;
        private bool built;
        private bool hasReference;
        private Vector2 reference;

        public bool CenterHorizontally
        {
            get => centerHorizontally;
            set => centerHorizontally = value;
        }

        public override void SetColor(Color newColor, float newAlpha)
        {
            color = newColor;
            alpha = newAlpha;
        }

        /// <summary>Sprites usados para montar o boneco (definidos ao montar a cena).</summary>
        public void SetSprites(Sprite circle, Sprite soft)
        {
            circleSprite = circle;
            softSprite = soft;
        }

        public override void ResetReference() => hasReference = false;

        public override void SetSlot(BodySlot slot, bool followPlayerX)
        {
            float x = slot == BodySlot.Left ? -430f : slot == BodySlot.Right ? 430f : 0f;
            ((RectTransform)transform).anchoredPosition = new Vector2(x, 0f);
            centerHorizontally = !followPlayerX;
        }

        public override void Clear()
        {
            if (built) container.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ Desenho

        public override void Render(BodyFrame body)
        {
            if (body == null || !body.IsTracked)
            {
                Clear();
                return;
            }

            EnsureBuilt();
            container.gameObject.SetActive(true);
            UpdateReference(body);

            float scale = pixelsPerMeter / 400f;
            float lowestY = float.MaxValue;

            for (int i = 0; i < parts.Length; i++)
            {
                Part part = parts[i];
                JointData a = body.GetJoint(part.A);
                JointData b = body.GetJoint(part.B);
                bool visible = a.IsUsable && b.IsUsable;

                outlines[i].enabled = visible;
                fills[i].enabled = visible;
                if (!visible) continue;

                Vector2 pa = ToScreen(a.Position);
                Vector2 pb = ToScreen(b.Position);
                lowestY = Mathf.Min(lowestY, Mathf.Min(pa.y, pb.y));

                float size = part.Size * scale;
                bool tracked = a.IsTracked && b.IsTracked;
                Place(fills[i], pa, pb, size, part.IsCircle);
                Place(outlines[i], pa, pb, size + outlineWidth * 2f * scale, part.IsCircle);

                fills[i].color = ToneColor(part.Tone, tracked);
                outlines[i].color = Faded(OutlineColor, tracked);
            }

            UpdateFace(body, scale);
            UpdateFloorShadow(body, lowestY, scale);
            UpdateAura(body, scale);
        }

        private Color OutlineColor => Color.Lerp(color, new Color(0.02f, 0.02f, 0.05f), outlineDarken);

        private void UpdateAura(BodyFrame body, float scale)
        {
            if (aura == null) return;
            JointData mid = body.GetJoint(JointId.SpineMid);
            aura.enabled = mid.IsUsable && auraOpacity > 0f;
            if (!aura.enabled) return;

            aura.rectTransform.anchoredPosition = ToScreen(mid.Position) + new Vector2(0f, 60f * scale);
            aura.rectTransform.sizeDelta = new Vector2(900f, 1300f) * scale;
            aura.color = new Color(color.r, color.g, color.b, auraOpacity * alpha);
        }

        private void Place(Image image, Vector2 a, Vector2 b, float thickness, bool circle)
        {
            RectTransform rt = image.rectTransform;
            if (circle)
            {
                rt.anchoredPosition = a;
                rt.sizeDelta = new Vector2(thickness, thickness);
                rt.localRotation = Quaternion.identity;
                image.pixelsPerUnitMultiplier = 256f / Mathf.Max(1f, thickness);
                return;
            }

            // Cápsula: comprimento + espessura, para as pontas arredondadas ficarem centradas nos joints.
            Vector2 delta = b - a;
            rt.anchoredPosition = (a + b) * 0.5f;
            rt.sizeDelta = new Vector2(delta.magnitude + thickness, thickness);
            rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            image.pixelsPerUnitMultiplier = 256f / Mathf.Max(1f, thickness);
        }

        private Color ToneColor(Tone tone, bool tracked)
        {
            Color c = color;
            switch (tone)
            {
                case Tone.Torso: c = Color.Lerp(color, Color.black, 0.12f); break;
                case Tone.Head: c = Color.Lerp(color, Color.white, 0.12f); break;
            }
            return Faded(c, tracked);
        }

        /// <summary>Mistura com o fundo conforme o alfa (fantasma) e a confiança (inferido = mais apagado). Sempre opaco.</summary>
        private Color Faded(Color c, bool tracked)
        {
            float visibility = alpha * (tracked ? 1f : 0.55f);
            Color result = Color.Lerp(fadeTarget, c, visibility);
            result.a = 1f;
            return result;
        }

        /// <summary>Olhos e brilho na cabeça (inclinam junto com o pescoço).</summary>
        private void UpdateFace(BodyFrame body, float scale)
        {
            JointData head = body.GetJoint(JointId.Head);
            if (!head.IsUsable)
            {
                face.gameObject.SetActive(false);
                return;
            }

            face.gameObject.SetActive(true);
            Vector2 headPos = ToScreen(head.Position);
            face.anchoredPosition = headPos;
            face.localScale = Vector3.one * scale;

            JointData neck = body.GetJoint(JointId.SpineShoulder);
            float tilt = 0f;
            if (neck.IsUsable)
            {
                Vector2 up = headPos - ToScreen(neck.Position);
                tilt = Mathf.Atan2(up.x, up.y) * -Mathf.Rad2Deg;
            }
            face.localRotation = Quaternion.Euler(0f, 0f, Mathf.Clamp(tilt, -35f, 35f));

            Color eyeColor = Faded(Color.Lerp(OutlineColor, Color.black, 0.5f), true);
            foreach (Image eye in eyes) eye.color = eyeColor;
            shine.color = new Color(1f, 1f, 1f, 0.35f * alpha);
        }

        private void UpdateFloorShadow(BodyFrame body, float lowestY, float scale)
        {
            if (floorShadow == null) return;
            bool show = lowestY < float.MaxValue;
            floorShadow.enabled = show;
            if (!show) return;

            Vector2 root = ToScreen(body.Root);
            floorShadow.rectTransform.anchoredPosition = new Vector2(root.x, lowestY - 6f * scale);
            floorShadow.rectTransform.sizeDelta = new Vector2(300f, 60f) * scale;
            floorShadow.color = new Color(0f, 0f, 0f, 0.55f * alpha);
        }

        // ------------------------------------------------------------------ Coordenadas

        private void UpdateReference(BodyFrame body)
        {
            Vector3 root = body.Root;
            if (!hasReference)
            {
                reference = new Vector2(root.x, root.y);
                hasReference = true;
                return;
            }

            float t = 1f - Mathf.Exp(-verticalFollow * Time.unscaledDeltaTime);
            reference.y = Mathf.Lerp(reference.y, root.y, t);
            reference.x = root.x;
        }

        private Vector2 ToScreen(Vector3 p)
        {
            float x = centerHorizontally ? p.x - reference.x : p.x;
            return new Vector2(x * pixelsPerMeter, (p.y - reference.y) * pixelsPerMeter + hipScreenY);
        }

        // ------------------------------------------------------------------ Construção

        private void EnsureBuilt()
        {
            if (built) return;
            built = true;
            parts = BuildParts();

            container = NewRect("Boneco", transform);

            if (softSprite != null)
            {
                aura = NewImage("Aura", container, softSprite, false);
                floorShadow = NewImage("Sombra no chao", container, softSprite, false);
            }

            // Contornos primeiro (todos atrás), depois os preenchimentos.
            RectTransform outlineLayer = NewRect("Contorno", container);
            RectTransform fillLayer = NewRect("Cores", container);
            foreach (Part part in parts)
            {
                outlines.Add(NewImage($"Contorno {part.A}-{part.B}", outlineLayer, circleSprite, true));
                fills.Add(NewImage($"{part.A}-{part.B}", fillLayer, circleSprite, true));
            }

            // Rosto (desenhado sobre a cabeça, em coordenadas de 400 px/m).
            face = NewRect("Rosto", container);
            eyes = new[]
            {
                NewImage("Olho E", face, circleSprite, false),
                NewImage("Olho D", face, circleSprite, false),
            };
            eyes[0].rectTransform.anchoredPosition = new Vector2(-30f, 14f);
            eyes[1].rectTransform.anchoredPosition = new Vector2(30f, 14f);
            foreach (Image eye in eyes) eye.rectTransform.sizeDelta = new Vector2(24f, 34f);

            shine = NewImage("Brilho", face, softSprite != null ? softSprite : circleSprite, false);
            shine.rectTransform.anchoredPosition = new Vector2(-40f, 46f);
            shine.rectTransform.sizeDelta = new Vector2(56f, 40f);
        }

        private static RectTransform NewRect(string name, Transform parent)
        {
            var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = Vector2.zero;
            return rt;
        }

        private static Image NewImage(string name, Transform parent, Sprite sprite, bool sliced)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);

            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
            image.raycastTarget = false;
            return image;
        }
    }
}
