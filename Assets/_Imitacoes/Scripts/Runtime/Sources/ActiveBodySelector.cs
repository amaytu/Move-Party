using System;
using System.Collections.Generic;
using Imitacoes.Body;
using Imitacoes.Motion.Filters;
using UnityEngine;

namespace Imitacoes.Sources
{
    /// <summary>Qual fonte de corpos o jogo usa.</summary>
    public enum BodySourceMode
    {
        /// <summary>Kinect se ele estiver funcionando (ou já tiver funcionado nesta sessão); senão, mock.</summary>
        Auto,
        Kinect,
        Mock
    }

    /// <summary>
    /// Escolhe o "jogador ativo" entre os corpos da fonte, mantém a escolha estável
    /// e entrega o esqueleto já suavizado (filtro One Euro por joint).
    ///
    /// Regras:
    /// - O jogador atual é mantido enquanto continuar rastreado (mesmo que outro passe na frente).
    /// - Se ele sumir por mais de <see cref="lostGraceSeconds"/>, escolhe o corpo mais central e próximo
    ///   da distância ideal.
    /// - Corpos além de <see cref="maxDistance"/> são ignorados (plateia no fundo).
    /// </summary>
    [DefaultExecutionOrder(-50)] // depois das fontes (-100), antes dos consumidores
    public sealed class ActiveBodySelector : MonoBehaviour, IActiveBodyProvider
    {
        [Header("Fontes")]
        [SerializeField] private BodySourceBehaviour kinectSource;
        [SerializeField] private BodySourceBehaviour mockSource;
        [SerializeField] private BodySourceMode mode = BodySourceMode.Auto;

        [Header("Seleção do jogador")]
        [SerializeField, Min(0f), Tooltip("Tempo que o jogador pode \"piscar\" no sensor antes de ser trocado.")]
        private float lostGraceSeconds = 1f;
        [SerializeField, Min(0.5f), Tooltip("Corpos mais longe que isso (m) são ignorados.")]
        private float maxDistance = 4.2f;
        [SerializeField, Min(0.5f), Tooltip("Distância ideal do sensor (m). Usada para desempatar.")]
        private float idealDistance = 2.3f;
        [SerializeField, Range(0f, 1f), Tooltip("Fração mínima de joints rastreados para contar como estável.")]
        private float minTrackedRatio = 0.7f;

        [Header("Suavização")]
        [SerializeField] private bool smoothingEnabled = true;
        [SerializeField] private OneEuroParams smoothing = OneEuroParams.Default;

        private readonly BodyFrame activeBody = new BodyFrame();
        private readonly OneEuroFilterVector3[] filters = CreateFilters();

        private bool hasActive;
        private bool activeMissing;
        private ulong activeId;
        private float lostTimer;
        private float stableTime;
        private int visibleCount;
        private int lastFrameNumber = -1;
        private double lastBodyTimestamp;
        private bool kinectEverRan;

        // ------------------------------------------------------------------ IActiveBodyProvider

        public BodyFrame ActiveBody => hasActive ? activeBody : null;
        public bool HasActiveBody => hasActive;
        public ulong ActiveTrackingId => hasActive ? activeId : 0;
        public int VisibleBodyCount => visibleCount;
        public float StableTrackingTime => stableTime;

        public event Action<BodyFrame> ActiveBodyChanged;
        public event Action<BodyFrame> ActiveBodyUpdated;

        // ------------------------------------------------------------------ Extras

        /// <summary>Fonte em uso neste momento (pode trocar no modo Auto).</summary>
        public IBodySource CurrentSource { get; private set; }

        /// <summary>O jogador ativo não aparece no frame atual, mas ainda está no período de tolerância.</summary>
        public bool IsActiveBodyMissing => hasActive && activeMissing;

        /// <summary>A fonte em uso é o mock (o cursor pode seguir o mouse).</summary>
        public bool IsUsingMock => CurrentSource != null && ReferenceEquals(CurrentSource, mockSource);

        public BodySourceMode Mode
        {
            get => mode;
            set => mode = value;
        }

        public float MinTrackedRatio
        {
            get => minTrackedRatio;
            set => minTrackedRatio = Mathf.Clamp01(value);
        }

        // ------------------------------------------------------------------ Loop

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;

            IBodySource source = ResolveSource();
            if (!ReferenceEquals(source, CurrentSource))
            {
                CurrentSource = source;
                lastFrameNumber = -1;
                LoseActive();
            }

            if (source == null || !source.IsRunning)
            {
                visibleCount = 0;
                activeMissing = true;
            }
            else if (source.FrameNumber != lastFrameNumber)
            {
                lastFrameNumber = source.FrameNumber;
                ProcessFrame(source.Bodies);
            }

            // Tolerância: o jogador some por alguns frames (ex.: braço cruzando o corpo) e volta.
            if (hasActive && activeMissing)
            {
                lostTimer += dt;
                if (lostTimer > lostGraceSeconds) LoseActive();
            }

            bool stable = hasActive && !activeMissing && activeBody.GetTrackedRatio() >= minTrackedRatio;
            stableTime = stable ? stableTime + dt : 0f;
        }

        private void ProcessFrame(IReadOnlyList<BodyFrame> bodies)
        {
            visibleCount = 0;
            BodyFrame current = null;
            BodyFrame best = null;
            float bestScore = float.MaxValue;

            for (int i = 0; i < bodies.Count; i++)
            {
                BodyFrame body = bodies[i];
                if (!body.IsTracked) continue;

                float distance = body.DistanceFromSensor;
                if (distance <= 0f || distance > maxDistance) continue;

                visibleCount++;
                if (hasActive && body.TrackingId == activeId) current = body;

                // Menor = melhor: mais central e mais perto da distância ideal.
                float score = Mathf.Abs(body.Root.x) + 0.5f * Mathf.Abs(distance - idealDistance);
                if (body.GetTrackedRatio(JointTrackingState.Inferred) < 0.3f) score += 10f;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = body;
                }
            }

            if (current != null)
            {
                activeMissing = false;
                lostTimer = 0f;
                ApplyBody(current);
                ActiveBodyUpdated?.Invoke(activeBody);
                return;
            }

            activeMissing = true;

            // Sem jogador ativo: adota o melhor candidato imediatamente.
            if (!hasActive && best != null)
            {
                SwitchTo(best);
            }
        }

        private void SwitchTo(BodyFrame body)
        {
            hasActive = true;
            activeMissing = false;
            activeId = body.TrackingId;
            lostTimer = 0f;
            stableTime = 0f;
            foreach (OneEuroFilterVector3 f in filters) f.Reset();

            ApplyBody(body);
            ActiveBodyChanged?.Invoke(activeBody);
            ActiveBodyUpdated?.Invoke(activeBody);
        }

        private void LoseActive()
        {
            if (!hasActive) return;

            hasActive = false;
            activeMissing = false;
            activeId = 0;
            stableTime = 0f;
            lostTimer = 0f;
            activeBody.Reset();
            ActiveBodyChanged?.Invoke(null);
        }

        private void ApplyBody(BodyFrame source)
        {
            float dt = (float)(source.Timestamp - lastBodyTimestamp);
            if (dt <= 0f || dt > 0.25f) dt = 1f / 30f; // primeiro frame ou salto de tempo
            lastBodyTimestamp = source.Timestamp;

            activeBody.CopyFrom(source);
            if (!smoothingEnabled) return;

            for (int i = 0; i < BodyFrame.JointCount; i++)
            {
                JointData joint = activeBody.Joints[i];
                if (!joint.IsUsable) continue;
                joint.Position = filters[i].Filter(joint.Position, dt, smoothing);
                activeBody.Joints[i] = joint;
            }
        }

        private IBodySource ResolveSource()
        {
            if (kinectSource != null && kinectSource.IsRunning) kinectEverRan = true;

            switch (mode)
            {
                case BodySourceMode.Kinect:
                    return kinectSource;
                case BodySourceMode.Mock:
                    return mockSource;
                default:
                    // Se o Kinect já funcionou nesta sessão, continua nele mesmo desconectado
                    // (a UI mostra o aviso) — trocar para o mock no meio da partida seria confuso.
                    if (kinectSource != null && (kinectSource.IsRunning || kinectEverRan)) return kinectSource;
                    return mockSource != null ? mockSource : kinectSource;
            }
        }

        private static OneEuroFilterVector3[] CreateFilters()
        {
            var result = new OneEuroFilterVector3[BodyFrame.JointCount];
            for (int i = 0; i < result.Length; i++) result[i] = new OneEuroFilterVector3();
            return result;
        }
    }
}
