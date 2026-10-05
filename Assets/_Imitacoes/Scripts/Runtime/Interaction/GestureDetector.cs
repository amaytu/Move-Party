using System;
using Imitacoes.Body;
using Imitacoes.Sources;
using UnityEngine;
using UnityEngine.Events;

namespace Imitacoes.Interaction
{
    public enum GestureType
    {
        LeftHandUp = 0,
        RightHandUp = 1,
        AnyHandUp = 2,
        BothHandsUp = 3
    }

    /// <summary>
    /// Gestos simples por pose (sem aprendizado): mão acima da cabeça, duas mãos levantadas.
    ///
    /// Cada gesto precisa ser SEGURADO por <see cref="HoldSeconds"/> para disparar, e dispara
    /// UMA vez por "levantada": só volta a disparar depois que o jogador abaixar as mãos.
    /// Chame <see cref="ResetAll"/> ao trocar de tela para exigir que as mãos sejam abaixadas antes.
    /// </summary>
    [AddComponentMenu("Imitações/Gesture Detector")]
    public sealed class GestureDetector : MonoBehaviour
    {
        [Serializable] public sealed class GestureEvent : UnityEvent<GestureType> { }

        [SerializeField] private ActiveBodySelector bodyProvider;
        [SerializeField, Min(0f), Tooltip("Quanto a mão precisa passar acima da cabeça (m).")]
        private float aboveHeadMargin = 0.05f;
        [SerializeField, Min(0.05f)] private float holdSeconds = 0.6f;
        [SerializeField, Tooltip("Aceitar joints inferidos (ex.: mão parcialmente escondida).")]
        private bool allowInferredJoints = true;

        public GestureEvent onGesture = new GestureEvent();

        /// <summary>Disparado quando um gesto completa o tempo de segurar.</summary>
        public event Action<GestureType> GestureTriggered;

        private const int GestureCount = 4;
        private readonly float[] heldTime = new float[GestureCount];
        private readonly bool[] fired = new bool[GestureCount];

        public float HoldSeconds
        {
            get => holdSeconds;
            set => holdSeconds = Mathf.Max(0.05f, value);
        }

        public bool IsActive(GestureType gesture) => heldTime[(int)gesture] > 0f;

        /// <summary>Progresso (0..1) de um gesto em andamento — útil para feedback na tela.</summary>
        public float GetProgress(GestureType gesture)
            => fired[(int)gesture] ? 0f : Mathf.Clamp01(heldTime[(int)gesture] / holdSeconds);

        /// <summary>Exige que os gestos atualmente ativos sejam desfeitos antes de disparar de novo.</summary>
        public void ResetAll()
        {
            for (int i = 0; i < GestureCount; i++)
            {
                fired[i] = heldTime[i] > 0f;
                heldTime[i] = fired[i] ? heldTime[i] : 0f;
            }
        }

        private void Update()
        {
            BodyFrame body = bodyProvider != null ? bodyProvider.ActiveBody : null;
            bool left = body != null && IsHandAboveHead(body, JointId.HandLeft);
            bool right = body != null && IsHandAboveHead(body, JointId.HandRight);

            float dt = Time.unscaledDeltaTime;
            Tick(GestureType.LeftHandUp, left, dt);
            Tick(GestureType.RightHandUp, right, dt);
            Tick(GestureType.AnyHandUp, left || right, dt);
            Tick(GestureType.BothHandsUp, left && right, dt);
        }

        private void Tick(GestureType gesture, bool active, float dt)
        {
            int i = (int)gesture;
            if (!active)
            {
                heldTime[i] = 0f;
                fired[i] = false;
                return;
            }

            heldTime[i] += dt;
            if (fired[i] || heldTime[i] < holdSeconds) return;

            fired[i] = true;
            GestureTriggered?.Invoke(gesture);
            onGesture?.Invoke(gesture);
        }

        private bool IsHandAboveHead(BodyFrame body, JointId handId)
        {
            JointData hand = body.GetJoint(handId);
            JointData head = body.GetJoint(JointId.Head);
            if (!Usable(hand) || !Usable(head)) return false;
            return hand.Position.y > head.Position.y + aboveHeadMargin;
        }

        private bool Usable(in JointData joint)
            => allowInferredJoints ? joint.IsUsable : joint.IsTracked;
    }
}
