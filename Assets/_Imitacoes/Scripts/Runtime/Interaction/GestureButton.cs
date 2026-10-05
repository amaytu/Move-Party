using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Imitacoes.Interaction
{
    /// <summary>
    /// Botão acionado por "hover": o jogador mantém o <see cref="HandCursor"/> sobre ele por
    /// <see cref="HoldSeconds"/> (padrão 1,5 s). O anel do cursor e a barra do botão mostram o progresso.
    ///
    /// Proteções contra acionamento acidental:
    /// - ao aparecer (OnEnable), o botão fica travado até o cursor sair de cima dele;
    /// - depois de acionar, trava de novo até o cursor sair;
    /// - tirar o cursor de cima faz o progresso cair rápido (não zera na hora: tolera tremor na borda).
    ///
    /// Tolerância ao tremor da mão:
    /// - depois de entrar, o botão continua "em cima" numa área maior (margem <see cref="stickyMargin"/>);
    /// - saídas curtas (menos de <see cref="exitGraceSeconds"/>) só pausam o progresso, não contam como saída.
    /// </summary>
    [AddComponentMenu("Imitações/Gesture Button")]
    [RequireComponent(typeof(RectTransform))]
    public sealed class GestureButton : MonoBehaviour
    {
        /// <summary>Tempo padrão de hover (o GameManager copia do GameSettings).</summary>
        public static float DefaultHoldSeconds = 1.5f;

        /// <summary>Permite clicar com o mouse (só no modo debug).</summary>
        public static bool MouseClickEnabled;

        [SerializeField] private TMP_Text label;
        [SerializeField, Tooltip("Fundo/borda que muda de cor ao passar o cursor.")]
        private Graphic background;
        [SerializeField, Tooltip("Image que enche com o progresso. Filled = usa fillAmount; " +
                                 "Sliced/Simple = cresce de baixo para cima (mantém os cantos arredondados).")]
        private Image progressFill;
        [SerializeField, Tooltip("Contorno na cor do jogador que aparece ao passar o cursor.")]
        private Graphic hoverOutline;

        [SerializeField] private bool useDefaultHoldTime = true;
        [SerializeField, Min(0.1f)] private float holdSeconds = 1.5f;
        [SerializeField, Min(0f), Tooltip("Velocidade com que o progresso cai quando o cursor sai (× por segundo).")]
        private float decayPerSecond = 3f;
        [SerializeField, Min(1f)] private float hoverScale = 1.06f;
        [SerializeField, Range(0f, 0.5f), Tooltip("Margem extra (fração do tamanho do botão) que mantém o hover depois de entrar.")]
        private float stickyMargin = 0.18f;
        [SerializeField, Range(0f, 1f), Tooltip("Saídas mais curtas que isso (s) só pausam o progresso.")]
        private float exitGraceSeconds = 0.3f;

        [SerializeField] private Color normalColor = new Color(0.13f, 0.14f, 0.24f, 0.96f);
        [SerializeField] private Color hoverColor = new Color(0.19f, 0.21f, 0.34f, 1f);

        public UnityEvent onSelected = new UnityEvent();

        private RectTransform rect;
        private Vector3 baseScale = Vector3.one;
        private float progress;
        private bool locked;
        private float outsideTime;
        private static GestureButton hoverOwner;

        /// <summary>Cursor dentro do retângulo real (sem a margem de tolerância).</summary>
        public bool StrictInside { get; private set; }

        public float HoldSeconds => useDefaultHoldTime ? DefaultHoldSeconds : holdSeconds;
        public bool IsHovered { get; private set; }
        public float Progress01 => progress;

        public string Label
        {
            get => label != null ? label.text : string.Empty;
            set { if (label != null) label.text = value; }
        }

        /// <summary>Cor de destaque (normalmente a cor do jogador): barra de progresso.</summary>
        public void SetAccentColor(Color color)
        {
            if (progressFill != null) progressFill.color = new Color(color.r, color.g, color.b, 0.5f);
            if (hoverOutline != null) hoverOutline.color = color;
        }

        /// <summary>Liga as referências visuais (usado pelo GameUI ao montar o layout).</summary>
        internal void SetupVisuals(TMP_Text labelText, Graphic backgroundGraphic, Image fill, Graphic outline)
        {
            label = labelText;
            background = backgroundGraphic;
            progressFill = fill;
            hoverOutline = outline;
        }

        /// <summary>Aciona o botão por código (usado pelas teclas de debug).</summary>
        public void Select()
        {
            progress = 0f;
            locked = true;
            onSelected?.Invoke();
        }

        private void Awake()
        {
            rect = (RectTransform)transform;
            baseScale = rect.localScale;
        }

        private void OnEnable()
        {
            progress = 0f;
            locked = true; // precisa sair e entrar de novo: evita acionar ao trocar de tela
            IsHovered = false;
            outsideTime = 0f;
        }

        private void OnDisable()
        {
            if (hoverOwner == this) hoverOwner = null;
            if (rect != null) rect.localScale = baseScale;
        }

        private void Update()
        {
            HandCursor cursor = HandCursor.Current;
            float dt = Time.unscaledDeltaTime;

            // Outro botão tomou o cursor: perde o hover (e a margem ampliada).
            if (hoverOwner != null && hoverOwner != this && hoverOwner.isActiveAndEnabled) IsHovered = false;

            // Dentro de verdade, ou dentro da área ampliada se já estava em cima.
            bool active = cursor != null && cursor.IsActive;
            StrictInside = active && ContainsScreenPoint(cursor.ScreenPosition, 0f);
            bool inside = StrictInside || (active && IsHovered && ContainsScreenPoint(cursor.ScreenPosition, stickyMargin));

            // Só um botão por vez: a margem de outro não bloqueia quem está com o cursor em cima de verdade.
            if (inside && hoverOwner != null && hoverOwner != this && hoverOwner.isActiveAndEnabled && hoverOwner.IsHovered &&
                (!StrictInside || hoverOwner.StrictInside))
                inside = false;
            if (inside) outsideTime = 0f;
            else outsideTime += dt;

            // Saída curta (tremor): continua em cima, mas o progresso só pausa.
            bool inGrace = !inside && IsHovered && outsideTime < exitGraceSeconds && cursor != null && cursor.IsActive;
            IsHovered = inside || inGrace;
            if (IsHovered) hoverOwner = this;
            else if (hoverOwner == this) hoverOwner = null;

            if (locked)
            {
                progress = 0f;
                if (!IsHovered) locked = false;
            }
            else if (inGrace)
            {
                // pausa
            }
            else if (IsHovered)
            {
                progress += dt / HoldSeconds;
                if (progress >= 1f)
                {
                    Select();
                    return;
                }
            }
            else
            {
                progress = Mathf.Max(0f, progress - dt * decayPerSecond);
            }

            if (IsHovered && !locked) cursor.ReportHoverProgress(progress);

            if (MouseClickEnabled && IsHovered && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                Select();
                return;
            }

            UpdateVisuals(dt);
        }

        /// <summary>Ponto de tela dentro do retângulo, ampliado por uma margem (fração do tamanho).</summary>
        private bool ContainsScreenPoint(Vector2 screenPoint, float margin)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, screenPoint, null, out Vector2 local))
                return false;
            Rect r = rect.rect;
            float pad = Mathf.Min(r.width, r.height) * margin;
            return local.x >= r.xMin - pad && local.x <= r.xMax + pad &&
                   local.y >= r.yMin - pad && local.y <= r.yMax + pad;
        }

        private void UpdateVisuals(float dt)
        {
            if (progressFill != null)
            {
                if (progressFill.type == Image.Type.Filled)
                {
                    progressFill.fillAmount = progress;
                }
                else
                {
                    // Cresce de baixo para cima; some quando zerado (evita cantos esmagados).
                    progressFill.enabled = progress > 0.02f;
                    RectTransform fillRect = progressFill.rectTransform;
                    fillRect.anchorMin = Vector2.zero;
                    fillRect.anchorMax = new Vector2(1f, progress);
                    fillRect.offsetMin = fillRect.offsetMax = Vector2.zero;
                }
            }
            if (hoverOutline != null) hoverOutline.enabled = IsHovered;
            if (background != null) background.color = IsHovered ? hoverColor : normalColor;

            Vector3 targetScale = IsHovered ? baseScale * hoverScale : baseScale;
            rect.localScale = Vector3.Lerp(rect.localScale, targetScale, 1f - Mathf.Exp(-15f * dt));
        }
    }
}
