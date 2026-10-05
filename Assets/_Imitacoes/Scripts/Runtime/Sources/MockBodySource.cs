using System;
using System.IO;
using Imitacoes.Body;
using Imitacoes.Motion;
using UnityEngine;
using UnityEngine.InputSystem;
using Random = UnityEngine.Random;

namespace Imitacoes.Sources
{
    public enum MockMode
    {
        /// <summary>Gera um corpo animado por código.</summary>
        Synthetic,

        /// <summary>Reproduz um MotionClip (.json) de StreamingAssets/Recordings.</summary>
        Playback
    }

    public enum SyntheticMotion
    {
        Idle,
        Wave,
        Chicken,
        JumpingJacks,
        Robot
    }

    /// <summary>
    /// Fonte FALSA de esqueletos, para desenvolver e testar sem o Kinect.
    ///
    /// - Synthetic: corpo em pé a ~2,2 m do sensor, com movimentos simples (acenar, galinha, polichinelo, robô).
    /// - Playback: reproduz gravações reais salvas pelo MotionRecorder (opção "Salvar gravações").
    /// - Mouse: move a mão direita (levar o mouse ao topo da tela = mão acima da cabeça).
    /// - Teclado (Input System): Espaço = levantar as duas mãos; H = sair do campo de visão.
    /// </summary>
    [AddComponentMenu("Imitações/Mock Body Source")]
    [DefaultExecutionOrder(-100)]
    public sealed class MockBodySource : BodySourceBehaviour
    {
        [Header("Modo")]
        [SerializeField] private MockMode mode = MockMode.Synthetic;
        [SerializeField] private SyntheticMotion motion = SyntheticMotion.Idle;
        [SerializeField, Range(0.2f, 3f)] private float motionSpeed = 1f;

        [Header("Playback")]
        [SerializeField, Tooltip("Arquivo em StreamingAssets/Recordings (ex.: galinha.json).")]
        private string playbackFile = string.Empty;
        [SerializeField] private bool loopPlayback = true;

        [Header("Simulação do sensor")]
        [SerializeField, Range(10f, 60f)] private float frameRate = 30f;
        [SerializeField, Tooltip("Posição dos PÉS em relação ao sensor (m). Y negativo = sensor acima do chão.")]
        private Vector3 feetPosition = new Vector3(0f, -0.9f, -2.2f);
        [SerializeField, Range(0f, 0.03f), Tooltip("Ruído por joint (m), para imitar o tremor do Kinect.")]
        private float jitter = 0.004f;

        [Header("Controles")]
        [SerializeField, Tooltip("O mouse controla a mão direita (topo da tela = mão acima da cabeça).")]
        private bool mouseControlsRightHand = true;
        [SerializeField, Tooltip("Espaço = levantar as duas mãos; H = esconder o corpo.")]
        private bool keyboardEnabled = true;

        public override string SourceName => mode == MockMode.Playback ? $"Mock: {playbackFile}" : $"Mock: {motion}";

        public SyntheticMotion Motion
        {
            get => motion;
            set => motion = value;
        }

        /// <summary>Simula o jogador fora do campo de visão.</summary>
        public bool Hidden { get; set; }

        private MotionClip playbackClip;
        private readonly BodyFrame playbackSample = new BodyFrame();
        private float time;
        private float frameTimer;

        // Medidas do corpo sintético (m).
        private const float HipHeight = 0.95f;
        private const float UpperArm = 0.28f;
        private const float Forearm = 0.25f;
        private const float HandLength = 0.08f;
        private const float Thigh = 0.43f;
        private const float Shin = 0.42f;

        // ------------------------------------------------------------------ Ciclo de vida

        protected override void OnStartSource()
        {
            time = 0f;
            frameTimer = 0f;

            if (mode == MockMode.Playback)
            {
                string path = Path.Combine(Application.streamingAssetsPath, "Recordings", playbackFile);
                try
                {
                    playbackClip = MotionClip.LoadFromFile(path);
                }
                catch (Exception e)
                {
                    SetStatus(BodySourceStatus.Error, $"Não consegui ler '{path}': {e.Message}");
                    return;
                }
            }

            SetStatus(BodySourceStatus.Running, string.Empty);
        }

        protected override void OnStopSource() { }

        private void Update()
        {
            if (!IsRunning) return;

            ReadKeyboard();

            float dt = Time.deltaTime;
            time += dt * motionSpeed;
            frameTimer += dt;

            float step = 1f / frameRate;
            if (frameTimer < step) return;
            frameTimer %= step;

            BeginFrame();
            if (!Hidden)
            {
                BodyFrame body = AcquireBody();
                bool ok = mode == MockMode.Playback ? FillFromClip(body) : FillSynthetic(body);
                if (!ok) BeginFrame(); // clipe terminou sem loop: nenhum corpo
            }
            PublishFrame(Time.timeAsDouble);
        }

        private void ReadKeyboard()
        {
            if (!keyboardEnabled) return;
            Keyboard kb = Keyboard.current;
            if (kb != null && kb.hKey.wasPressedThisFrame) Hidden = !Hidden;
        }

        // ------------------------------------------------------------------ Playback

        private bool FillFromClip(BodyFrame body)
        {
            if (playbackClip == null || playbackClip.IsEmpty) return false;

            float duration = playbackClip.Duration;
            float t = loopPlayback ? Mathf.Repeat(time, duration) : time;
            if (!loopPlayback && t > duration) return false;

            playbackClip.Sample(t, playbackSample);
            if (!playbackSample.IsTracked) return false;

            body.CopyFrom(playbackSample);
            body.TrackingId = 1;
            body.Timestamp = Time.timeAsDouble;
            return true;
        }

        // ------------------------------------------------------------------ Sintético

        private bool FillSynthetic(BodyFrame body)
        {
            body.IsTracked = true;
            body.TrackingId = 1;
            body.Timestamp = Time.timeAsDouble;

            // Ângulos dos braços no plano frontal: abdução (0 = caído, π/2 = horizontal, π = para cima)
            // e dobra do cotovelo (somada à abdução para o antebraço).
            float leftAbduction = 0.15f, leftElbow = 0.2f;
            float rightAbduction = 0.15f, rightElbow = 0.2f;
            float legSpread = 0.05f, rootLift = 0f, kneeBend = 0f;
            float t = time;

            switch (motion)
            {
                case SyntheticMotion.Idle:
                    leftAbduction = rightAbduction = 0.15f + 0.03f * Mathf.Sin(t * 1.5f);
                    break;

                case SyntheticMotion.Wave:
                    rightAbduction = 2.4f;
                    rightElbow = 0.5f * Mathf.Sin(t * Mathf.PI * 3f);
                    break;

                case SyntheticMotion.Chicken:
                    float flap = 0.55f + 0.45f * Mathf.Sin(t * Mathf.PI * 4f);
                    leftAbduction = rightAbduction = flap;
                    leftElbow = rightElbow = -2.3f;
                    kneeBend = 0.08f + 0.04f * Mathf.Abs(Mathf.Sin(t * Mathf.PI * 2f));
                    break;

                case SyntheticMotion.JumpingJacks:
                    float p = 0.5f + 0.5f * Mathf.Sin(t * Mathf.PI * 2f);
                    leftAbduction = rightAbduction = Mathf.Lerp(0.15f, 2.9f, p);
                    leftElbow = rightElbow = 0.1f;
                    legSpread = Mathf.Lerp(0.05f, 0.3f, p);
                    rootLift = 0.06f * Mathf.Sin(p * Mathf.PI);
                    break;

                case SyntheticMotion.Robot:
                    leftAbduction = Mathf.Sin(t * Mathf.PI) > 0f ? 1.57f : 0.15f;
                    rightAbduction = Mathf.Cos(t * Mathf.PI) > 0f ? 1.57f : 0.15f;
                    leftElbow = Mathf.Cos(t * Mathf.PI) > 0f ? 1.57f : 0f;
                    rightElbow = Mathf.Sin(t * Mathf.PI) > 0f ? 1.57f : 0f;
                    break;
            }

            Keyboard kb = keyboardEnabled ? Keyboard.current : null;
            bool bothHandsUp = kb != null && kb.spaceKey.isPressed;
            if (bothHandsUp)
            {
                leftAbduction = rightAbduction = 2.95f;
                leftElbow = rightElbow = 0f;
            }

            // Tronco e cabeça.
            Vector3 root = feetPosition + new Vector3(0f, HipHeight + rootLift - kneeBend, 0f);
            Vector3 spineMid = root + new Vector3(0f, 0.25f, 0f);
            Vector3 spineShoulder = root + new Vector3(0f, 0.5f, 0f);
            Vector3 neck = spineShoulder + new Vector3(0f, 0.07f, 0f);
            Vector3 head = neck + new Vector3(0f, 0.13f, 0f);

            Set(body, JointId.SpineBase, root);
            Set(body, JointId.SpineMid, spineMid);
            Set(body, JointId.SpineShoulder, spineShoulder);
            Set(body, JointId.Neck, neck);
            Set(body, JointId.Head, head);

            BuildArm(body, -1f, spineShoulder, leftAbduction, leftElbow);
            BuildArm(body, +1f, spineShoulder, rightAbduction, rightElbow);
            BuildLeg(body, -1f, root, legSpread, kneeBend);
            BuildLeg(body, +1f, root, legSpread, kneeBend);

            if (mouseControlsRightHand && !bothHandsUp) ApplyMouseToRightArm(body);
            return true;
        }

        /// <param name="side">-1 = esquerdo, +1 = direito (X positivo é a direita do jogador).</param>
        private void BuildArm(BodyFrame body, float side, Vector3 spineShoulder, float abduction, float elbowBend)
        {
            Vector3 shoulder = spineShoulder + new Vector3(side * 0.18f, -0.03f, 0f);
            Vector3 upperDir = new Vector3(side * Mathf.Sin(abduction), -Mathf.Cos(abduction), 0.15f).normalized;
            Vector3 elbow = shoulder + upperDir * UpperArm;
            float foreAngle = abduction + elbowBend;
            Vector3 foreDir = new Vector3(side * Mathf.Sin(foreAngle), -Mathf.Cos(foreAngle), 0.25f).normalized;
            Vector3 wrist = elbow + foreDir * Forearm;
            Vector3 hand = wrist + foreDir * HandLength;

            bool left = side < 0f;
            Set(body, left ? JointId.ShoulderLeft : JointId.ShoulderRight, shoulder);
            Set(body, left ? JointId.ElbowLeft : JointId.ElbowRight, elbow);
            Set(body, left ? JointId.WristLeft : JointId.WristRight, wrist);
            Set(body, left ? JointId.HandLeft : JointId.HandRight, hand);
            Set(body, left ? JointId.HandTipLeft : JointId.HandTipRight, hand + foreDir * 0.07f);
            Set(body, left ? JointId.ThumbLeft : JointId.ThumbRight, wrist + foreDir * 0.04f + new Vector3(-side * 0.03f, 0f, 0.03f));
        }

        private void BuildLeg(BodyFrame body, float side, Vector3 root, float spread, float kneeBend)
        {
            Vector3 hip = root + new Vector3(side * 0.09f, -0.05f, 0f);
            Vector3 dir = new Vector3(side * Mathf.Sin(spread), -Mathf.Cos(spread), 0f);
            Vector3 knee = hip + dir * Thigh + new Vector3(0f, 0f, kneeBend * 1.5f);
            Vector3 ankle = knee + dir * Shin - new Vector3(0f, 0f, kneeBend * 1.5f);
            Vector3 foot = ankle + new Vector3(0f, -0.05f, 0.12f);

            bool left = side < 0f;
            Set(body, left ? JointId.HipLeft : JointId.HipRight, hip);
            Set(body, left ? JointId.KneeLeft : JointId.KneeRight, knee);
            Set(body, left ? JointId.AnkleLeft : JointId.AnkleRight, ankle);
            Set(body, left ? JointId.FootLeft : JointId.FootRight, foot);
        }

        /// <summary>Posiciona a mão direita pelo mouse, numa zona à frente do ombro direito.</summary>
        private void ApplyMouseToRightArm(BodyFrame body)
        {
            Mouse mouse = Mouse.current;
            if (mouse == null || Screen.width <= 0 || Screen.height <= 0) return;

            Vector2 m = mouse.position.ReadValue();
            float u = Mathf.Clamp01(m.x / Screen.width);
            float v = Mathf.Clamp01(m.y / Screen.height);

            Vector3 shoulder = body.GetPosition(JointId.ShoulderRight);
            Vector3 hand = shoulder + new Vector3(Mathf.Lerp(-0.15f, 0.45f, u), Mathf.Lerp(-0.35f, 0.5f, v), 0.35f);
            Vector3 toHand = hand - shoulder;
            Vector3 elbow = shoulder + toHand * 0.5f + new Vector3(0.05f, -0.08f, 0f);
            Vector3 wrist = hand - toHand.normalized * HandLength;

            Set(body, JointId.ElbowRight, elbow);
            Set(body, JointId.WristRight, wrist);
            Set(body, JointId.HandRight, hand);
            Set(body, JointId.HandTipRight, hand + toHand.normalized * 0.07f);
            Set(body, JointId.ThumbRight, wrist + new Vector3(-0.03f, 0.02f, 0.03f));
        }

        private void Set(BodyFrame body, JointId id, Vector3 position)
        {
            if (jitter > 0f) position += Random.insideUnitSphere * jitter;
            body.SetJoint(id, position, Quaternion.identity, JointTrackingState.Tracked);
        }
    }
}
