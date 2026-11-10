using Imitacoes.Body;
using Imitacoes.Motion.Filters;
using Imitacoes.Sources;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Imitacoes.Interaction
{
    public enum CursorInputMode
    {
        /// <summary>Mouse quando a fonte é o mock; mão quando é o Kinect.</summary>
        Auto,
        Hand,
        Mouse
    }

    public enum CursorHand
    {
        None,
        Left,
        Right
    }

    /// <summary>
    /// Cursor na tela controlado pela mão do jogador ativo.
    ///
    /// Estabilidade (o Kinect v1 treme ~1–2 cm na mão, o que vira dezenas de pixels na tela):
    /// - A mão que controla é a que está LEVANTADA (acima da cintura). Ela só troca quando for
    ///   abaixada por um tempo e a outra estiver levantada — nada de o cursor pular de lado sozinho.
    /// - Ponto de controle = média entre pulso e mão (o joint da mão sozinho é o mais ruidoso).
    /// - Se a mão estiver só "inferida" (oculta), o cursor segura a última posição em vez de pular.
    /// - A zona de interação acompanha o ombro com filtro lento: balançar o corpo não mexe o cursor.
    /// - Filtro One Euro na posição da tela + zona morta: parado fica parado, rápido continua rápido.
    ///
    /// Mapeia uma "zona de interação" física (retângulo em metros ao lado do ombro) para a tela inteira.
    /// Imagem espelhada: mão para a direita do jogador = cursor para a direita da tela.
    /// Requer um Canvas em Screen Space - Overlay (a posição do cursor é em pixels de tela).
    /// </summary>
    [AddComponentMenu("Imitações/Hand Cursor")]
    [DefaultExecutionOrder(-10)]
    public sealed class HandCursor : MonoBehaviour
    {
        /// <summary>Cursor ativo na cena (os <see cref="GestureButton"/> consultam este).</summary>
        public static HandCursor Current { get; private set; }

        [SerializeField] private ActiveBodySelector bodyProvider;

        [Header("Visual")]
        [SerializeField] private RectTransform cursorVisual;
        [SerializeField] private Graphic cursorDot;
        [SerializeField, Tooltip("Image do tipo Filled/Radial360: o anel de progresso do hover.")]
        private Image progressRing;

        [Header("Entrada")]
        [SerializeField] private CursorInputMode inputMode = CursorInputMode.Auto;

        [Header("Zona de interação (metros)")]
        [SerializeField, Tooltip("Largura × altura da zona física mapeada para a tela.")]
        private Vector2 zoneSize = new Vector2(0.55f, 0.42f);
        [SerializeField, Tooltip("Centro da zona em relação ao ombro: X para fora do corpo, Y para cima.")]
        private Vector2 zoneOffset = new Vector2(0.15f, 0.05f);

        [Header("Escolha da mão")]
        [SerializeField, Tooltip("Altura (m) acima da base da coluna para a mão contar como \"levantada\".")]
        private float raisedHandHeight = 0.15f;
        [SerializeField, Min(0f), Tooltip("Tempo (s) com a mão abaixada antes de passar o controle para a outra.")]
        private float handSwitchDelay = 0.4f;

        [Header("Suavização")]
        [SerializeField, Tooltip("Filtro One Euro na posição da tela (unidades: fração da tela). " +
                                 "minCutoff menor = mais estável parado; beta maior = menos atraso rápido.")]
        private OneEuroParams cursorFilter = new OneEuroParams(0.7f, 2.5f, 1f);
        [SerializeField, Range(0f, 0.03f), Tooltip("Zona morta (fração da tela): tremores menores que isso são ignorados.")]
        private float deadZone = 0.008f;
        [SerializeField, Range(0.1f, 10f), Tooltip("Rapidez (Hz) com que a zona segue o ombro. Baixo = ignora o balanço do corpo.")]
        private float anchorFollowRate = 1.2f;

        public Vector2 ScreenPosition { get; private set; }
        public bool IsActive { get; private set; }
        public CursorHand Hand { get; private set; } = CursorHand.Right;

        public CursorInputMode InputMode
        {
            get => inputMode;
            set => inputMode = value;
        }

        private readonly OneEuroFilterVector3 screenFilter = new OneEuroFilterVector3();
        private float reportedProgress;
        private bool wasActive;

        private Vector2 normalizedTarget;   // último alvo válido (0..1)
        private Vector2 normalizedCursor;   // posição filtrada (0..1)
        private Vector3 anchor;             // ombro suavizado
        private bool anchorValid;
        private CursorHand anchorHand;
        private float handLoweredTime;
        private ulong lastBodyId;

        // ------------------------------------------------------------------ API

        /// <summary>Cor do cursor (a cor do jogador da vez).</summary>
        public void SetColor(Color color)
        {
            if (cursorDot != null) cursorDot.color = color;
            if (progressRing != null) progressRing.color = color;
        }

        /// <summary>Um botão sob o cursor informa o progresso (0..1). Vale o maior do frame.</summary>
        public void ReportHoverProgress(float progress01)
        {
            if (progress01 > reportedProgress) reportedProgress = progress01;
        }

        /// <summary>Liga as referências visuais (usado pelo GameUI ao montar o layout).</summary>
        internal void SetupVisuals(RectTransform visual, Graphic dot, Image ring)
        {
            cursorVisual = visual;
            cursorDot = dot;
            progressRing = ring;
        }

        // ------------------------------------------------------------------ Loop

        private void OnEnable() => Current = this;

        private void OnDisable()
        {
            if (Current == this) Current = null;
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            bool active;

            if (UseMouse())
            {
                // Mouse: sem filtro (já é preciso).
                active = TryGetMouse(out Vector2 mouse);
                if (active) ScreenPosition = mouse;
                screenFilter.Reset();
            }
            else
            {
                active = TryGetHand(dt, out Vector2 target);
                if (active)
                {
                    if (!wasActive)
                    {
                        screenFilter.Reset();
                        normalizedCursor = target;
                    }

                    // Zona morta: ignora tremor pequeno em volta da posição atual.
                    Vector2 delta = target - normalizedCursor;
                    Vector2 input = delta.magnitude < deadZone ? normalizedCursor : target;

                    Vector3 filtered = screenFilter.Filter(new Vector3(input.x, input.y, 0f), dt, cursorFilter);
                    normalizedCursor = new Vector2(filtered.x, filtered.y);
                    ScreenPosition = new Vector2(normalizedCursor.x * Screen.width, normalizedCursor.y * Screen.height);
                }
            }

            // Só aparece quando há botão na tela: durante as imitações o cursor some.
            if (GestureButton.VisibleCount <= 0) active = false;

            IsActive = active;
            wasActive = active;

            if (cursorVisual != null)
            {
                if (cursorVisual.gameObject.activeSelf != active) cursorVisual.gameObject.SetActive(active);
                if (active) cursorVisual.position = ScreenPosition;
            }
        }

        private void LateUpdate()
        {
            // Os botões reportam no Update deles; aqui aplicamos e zeramos para o próximo frame.
            if (progressRing != null) progressRing.fillAmount = reportedProgress;
            reportedProgress = 0f;
        }

        private bool UseMouse()
        {
            switch (inputMode)
            {
                case CursorInputMode.Mouse: return true;
                case CursorInputMode.Hand: return false;
                default: return bodyProvider != null && bodyProvider.IsUsingMock;
            }
        }

        private static bool TryGetMouse(out Vector2 position)
        {
            Mouse mouse = Mouse.current;
            position = mouse != null ? mouse.position.ReadValue() : Vector2.zero;
            return mouse != null;
        }

        // ------------------------------------------------------------------ Mão

        /// <summary>Calcula o alvo normalizado (0..1). False = sem jogador (cursor some).</summary>
        private bool TryGetHand(float dt, out Vector2 normalized)
        {
            normalized = normalizedTarget;
            BodyFrame body = bodyProvider != null ? bodyProvider.ActiveBody : null;
            if (body == null)
            {
                anchorValid = false;
                return false;
            }

            if (bodyProvider.ActiveTrackingId != lastBodyId)
            {
                lastBodyId = bodyProvider.ActiveTrackingId;
                anchorValid = false;
                handLoweredTime = 0f;
            }

            JointData spineShoulder = body.GetJoint(JointId.SpineShoulder);
            JointData spineBase = body.GetJoint(JointId.SpineBase);
            if (!spineShoulder.IsUsable) return wasActive; // segura a última posição

            float waist = spineBase.IsUsable ? spineBase.Position.y : spineShoulder.Position.y - 0.5f;
            bool rightRaised = IsRaised(body, JointId.HandRight, waist);
            bool leftRaised = IsRaised(body, JointId.HandLeft, waist);

            UpdateHandChoice(rightRaised, leftRaised, dt);

            bool useRight = Hand == CursorHand.Right;
            if (!TryGetControlPoint(body, useRight, out Vector3 hand))
                return wasActive; // mão oculta: não pula, segura onde estava

            // Âncora = ombro da mão em uso, seguida devagar.
            JointData shoulder = body.GetJoint(useRight ? JointId.ShoulderRight : JointId.ShoulderLeft);
            Vector3 shoulderPos = shoulder.IsUsable ? shoulder.Position : spineShoulder.Position;
            if (!anchorValid || anchorHand != Hand)
            {
                anchor = shoulderPos;
                anchorValid = true;
                anchorHand = Hand;
            }
            else
            {
                anchor = Vector3.Lerp(anchor, shoulderPos, 1f - Mathf.Exp(-anchorFollowRate * dt));
            }

            float side = useRight ? 1f : -1f;
            Vector2 center = new Vector2(anchor.x + side * zoneOffset.x, anchor.y + zoneOffset.y);
            float u = Mathf.Clamp01((hand.x - (center.x - zoneSize.x * 0.5f)) / zoneSize.x);
            float v = Mathf.Clamp01((hand.y - (center.y - zoneSize.y * 0.5f)) / zoneSize.y);

            normalizedTarget = normalized = new Vector2(u, v);
            return true;
        }

        /// <summary>
        /// Mantém a mão atual enquanto ela estiver levantada. Troca só se ela ficar abaixada por
        /// <see cref="handSwitchDelay"/> e a outra estiver levantada.
        /// </summary>
        private void UpdateHandChoice(bool rightRaised, bool leftRaised, float dt)
        {
            bool currentRaised = Hand == CursorHand.Left ? leftRaised : rightRaised;
            bool otherRaised = Hand == CursorHand.Left ? rightRaised : leftRaised;

            if (currentRaised || !otherRaised)
            {
                handLoweredTime = 0f;
                return;
            }

            handLoweredTime += dt;
            if (handLoweredTime >= handSwitchDelay)
            {
                Hand = Hand == CursorHand.Left ? CursorHand.Right : CursorHand.Left;
                handLoweredTime = 0f;
            }
        }

        private bool IsRaised(BodyFrame body, JointId handId, float waistY)
        {
            JointData hand = body.GetJoint(handId);
            return hand.State == JointTrackingState.Tracked && hand.Position.y > waistY + raisedHandHeight;
        }

        /// <summary>
        /// Média entre pulso e mão (bem mais estável que a mão sozinha no v1).
        /// Só aceita pontos vistos de verdade pelo sensor (Tracked).
        /// </summary>
        private static bool TryGetControlPoint(BodyFrame body, bool right, out Vector3 point)
        {
            JointData hand = body.GetJoint(right ? JointId.HandRight : JointId.HandLeft);
            JointData wrist = body.GetJoint(right ? JointId.WristRight : JointId.WristLeft);
            bool handOk = hand.State == JointTrackingState.Tracked;
            bool wristOk = wrist.State == JointTrackingState.Tracked;

            if (handOk && wristOk) point = (hand.Position + wrist.Position) * 0.5f;
            else if (handOk) point = hand.Position;
            else if (wristOk) point = wrist.Position;
            else
            {
                point = default;
                return false;
            }
            return true;
        }
    }
}
