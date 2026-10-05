using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Imitacoes.Body;
using UnityEngine;

namespace Imitacoes.Kinect
{
    /// <summary>
    /// Fonte de esqueletos do Kinect v1 (Xbox 360 / Kinect for Windows v1), via SDK 1.8.
    ///
    /// Não existe plugin oficial para a Unity no v1: chamamos direto a API nativa
    /// <c>Kinect10.dll</c> (instalada no System32 pelo SDK) por P/Invoke.
    /// O frame nativo é lido de um buffer não gerenciado alocado uma vez só — zero alocação por frame.
    ///
    /// Trata: SDK ausente (Error), sensor desconectado/sem energia (SensorUnavailable, com
    /// reconexão automática) e frames sem dado novo (ignorados).
    /// Só compila com o símbolo KINECT_V1 (ver Imitacoes.Kinect.asmdef).
    /// </summary>
    [AddComponentMenu("Imitações/Kinect v1 Body Source")]
    [DefaultExecutionOrder(-100)] // publica o frame antes dos consumidores no mesmo Update
    public sealed class KinectBodySource : BodySourceBehaviour, IAutoFramingSource
    {
        [SerializeField, Min(0.5f), Tooltip("Intervalo entre tentativas de reconexão (s).")]
        private float reconnectIntervalSeconds = 2f;

        [SerializeField, Tooltip("Ajusta a inclinação do motor do Kinect ao iniciar.")]
        private bool setElevationOnStart;

        [SerializeField, Range(-27, 27), Tooltip("Ângulo do motor em graus (limite do hardware: ±27).")]
        private int elevationAngle;

        [SerializeField, Tooltip("Modo sentado (rastreia só os 10 joints de cima). Deixe desligado para o jogo.")]
        private bool seatedMode;

        [Header("Enquadramento automático")]
        [SerializeField, Tooltip("Inclina o motor sozinho quando a cabeça ou os pés do jogador saem do quadro, " +
                                 "para o sensor enxergar o corpo inteiro.")]
        private bool autoFraming; // desligado: o motor se mexendo faz o Kinect perder o esqueleto
        [SerializeField, Range(1, 10), Tooltip("Graus por ajuste.")]
        private int autoFramingStep = 4;
        [SerializeField, Min(2f), Tooltip("Intervalo mínimo entre ajustes (s). O SDK pede no máximo 15 movimentos a cada 20 s.")]
        private float autoFramingCooldown = 3f;

        /// <summary>O GameManager libera o motor só fora das gravações.</summary>
        public bool AutoFramingAllowed { get; set; } = true;

        public override string SourceName => "Kinect v1 (SDK 1.8)";

        private IntPtr frameBuffer = IntPtr.Zero;
        private float nextTiltTime;
        private volatile bool motorBusy;
        private bool initialized;
        private float retryTimer;

        // ------------------------------------------------------------------ Ciclo de vida

        protected override void OnStartSource()
        {
            if (frameBuffer == IntPtr.Zero) frameBuffer = Marshal.AllocHGlobal(NativeLayout.FrameSize);
            retryTimer = 0f;
            TryInitialize();
        }

        protected override void OnStopSource() => Shutdown();

        private void OnDestroy()
        {
            Shutdown();
            if (frameBuffer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(frameBuffer);
                frameBuffer = IntPtr.Zero;
            }
        }

        private void TryInitialize()
        {
            try
            {
                int hr = NativeMethods.NuiInitialize(NativeMethods.NUI_INITIALIZE_FLAG_USES_SKELETON);
                if (hr < 0)
                {
                    SetStatus(BodySourceStatus.SensorUnavailable,
                        $"Kinect não encontrado (0x{hr:X8}). Verifique o cabo USB e a fonte de energia.");
                    return;
                }

                uint flags = seatedMode ? NativeMethods.NUI_SKELETON_TRACKING_FLAG_ENABLE_SEATED_SUPPORT : 0u;
                hr = NativeMethods.NuiSkeletonTrackingEnable(IntPtr.Zero, flags);
                if (hr < 0)
                {
                    NativeMethods.NuiShutdown();
                    SetStatus(BodySourceStatus.SensorUnavailable, $"Falha ao ativar o rastreamento de esqueleto (0x{hr:X8}).");
                    return;
                }

                if (setElevationOnStart) NativeMethods.NuiCameraElevationSetAngle(elevationAngle);

                initialized = true;
                SetStatus(BodySourceStatus.Running, string.Empty);
            }
            catch (DllNotFoundException)
            {
                SetStatus(BodySourceStatus.Error, "Kinect10.dll não encontrada. Instale o Kinect for Windows SDK 1.8.");
            }
            catch (EntryPointNotFoundException e)
            {
                SetStatus(BodySourceStatus.Error, "Versão incompatível da Kinect10.dll: " + e.Message);
            }
        }

        private void Shutdown()
        {
            if (!initialized) return;
            initialized = false;
            try { NativeMethods.NuiShutdown(); }
            catch (Exception e) { Debug.LogWarning("[Kinect] Erro ao desligar: " + e.Message, this); }
        }

        // ------------------------------------------------------------------ Leitura

        private void Update()
        {
            BodySourceStatus status = Status;
            if (status == BodySourceStatus.Error || status == BodySourceStatus.Stopped || status == BodySourceStatus.NotStarted)
                return;

            if (!initialized)
            {
                retryTimer += Time.unscaledDeltaTime;
                if (retryTimer >= reconnectIntervalSeconds)
                {
                    retryTimer = 0f;
                    TryInitialize();
                }
                return;
            }

            int hr = NativeMethods.NuiSkeletonGetNextFrame(0, frameBuffer);
            if (hr == NativeMethods.E_NUI_FRAME_NO_DATA || hr == NativeMethods.S_FALSE) return; // sem frame novo
            if (hr < 0)
            {
                // Sensor desconectado ou sem energia: publica um frame vazio para os consumidores
                // limparem o estado e tenta reconectar periodicamente.
                Shutdown();
                SetStatus(BodySourceStatus.SensorUnavailable, $"Conexão com o Kinect perdida (0x{hr:X8}). Tentando reconectar...");
                BeginFrame();
                PublishFrame(Time.unscaledTimeAsDouble);
                return;
            }

            ReadFrame();
        }

        private void ReadFrame()
        {
            long timestampMs = Marshal.ReadInt64(frameBuffer, NativeLayout.FrameTimestamp);
            double timestamp = timestampMs / 1000.0;

            BeginFrame();
            ClippedEdges centralEdges = ClippedEdges.None;
            float centralX = float.MaxValue;
            for (int s = 0; s < NativeLayout.SkeletonCount; s++)
            {
                int baseOffset = NativeLayout.FrameSkeletons + s * NativeLayout.SkeletonSize;
                int trackingState = Marshal.ReadInt32(frameBuffer, baseOffset + NativeLayout.SkelTrackingState);
                if (trackingState != NativeMethods.NUI_SKELETON_TRACKED) continue;

                BodyFrame body = AcquireBody();
                body.IsTracked = true;
                body.Timestamp = timestamp;
                body.TrackingId = (uint)Marshal.ReadInt32(frameBuffer, baseOffset + NativeLayout.SkelTrackingId);

                // Os bits de "clipped" do v1 têm os mesmos valores do nosso enum.
                int quality = Marshal.ReadInt32(frameBuffer, baseOffset + NativeLayout.SkelQualityFlags);
                body.ClippedEdges = (ClippedEdges)(quality & 0x0F);

                for (int j = 0; j < NativeLayout.JointCount; j++)
                {
                    int posOffset = baseOffset + NativeLayout.SkelPositions + j * 16;
                    float x = ReadFloat(posOffset);
                    float y = ReadFloat(posOffset + 4);
                    float z = ReadFloat(posOffset + 8);
                    var state = (JointTrackingState)Marshal.ReadInt32(frameBuffer,
                        baseOffset + NativeLayout.SkelPositionStates + j * 4);

                    // Kinect (mão direita, +Z saindo do sensor) → nossa convenção: (x, y, -z).
                    body.SetJoint(V1ToJointId[j], new Vector3(x, y, -z), Quaternion.identity, state);
                }

                DeriveNeck(body);

                // Jogador mais central decide o enquadramento.
                float distanceFromCenter = Mathf.Abs(body.Root.x);
                if (distanceFromCenter < centralX)
                {
                    centralX = distanceFromCenter;
                    centralEdges = body.ClippedEdges;
                }
            }

            PublishFrame(timestamp);
            if (centralX < float.MaxValue) TryAutoFrame(centralEdges);
        }

        /// <summary>
        /// Cabeça cortada no topo → inclina para cima; pés cortados embaixo → para baixo.
        /// Os dois cortados = jogador perto demais (a UI já pede para se afastar).
        /// O motor roda em outra thread: a chamada nativa bloqueia enquanto ele se mexe.
        /// </summary>
        private void TryAutoFrame(ClippedEdges edges)
        {
            if (!autoFraming || !AutoFramingAllowed || motorBusy || Time.unscaledTime < nextTiltTime) return;

            bool top = (edges & ClippedEdges.Top) != 0;
            bool bottom = (edges & ClippedEdges.Bottom) != 0;
            if (top == bottom) return;

            if (NativeMethods.NuiCameraElevationGetAngle(out int angle) < 0) return;
            int target = Mathf.Clamp(angle + (top ? autoFramingStep : -autoFramingStep), -27, 27);
            if (target == angle) return;

            nextTiltTime = Time.unscaledTime + autoFramingCooldown;
            motorBusy = true;
            Task.Run(() =>
            {
                try { NativeMethods.NuiCameraElevationSetAngle(target); }
                catch (Exception) { /* motor indisponível: ignora */ }
                finally { motorBusy = false; }
            });
        }

        private float ReadFloat(int offset)
            => BitConverter.Int32BitsToSingle(Marshal.ReadInt32(frameBuffer, offset));

        /// <summary>O v1 não tem Neck; estimamos entre SpineShoulder e Head (só para o desenho).</summary>
        private static void DeriveNeck(BodyFrame body)
        {
            JointData shoulder = body.GetJoint(JointId.SpineShoulder);
            JointData head = body.GetJoint(JointId.Head);
            if (!shoulder.IsUsable || !head.IsUsable) return;

            JointTrackingState state = shoulder.State < head.State ? shoulder.State : head.State;
            body.SetJoint(JointId.Neck, Vector3.Lerp(shoulder.Position, head.Position, 0.3f), Quaternion.identity, state);
        }

        /// <summary>NUI_SKELETON_POSITION_INDEX (v1) → JointId. Índices 4..19 são iguais aos do v2.</summary>
        private static readonly JointId[] V1ToJointId =
        {
            JointId.SpineBase,     // HIP_CENTER
            JointId.SpineMid,      // SPINE
            JointId.SpineShoulder, // SHOULDER_CENTER
            JointId.Head,          // HEAD
            JointId.ShoulderLeft, JointId.ElbowLeft, JointId.WristLeft, JointId.HandLeft,
            JointId.ShoulderRight, JointId.ElbowRight, JointId.WristRight, JointId.HandRight,
            JointId.HipLeft, JointId.KneeLeft, JointId.AnkleLeft, JointId.FootLeft,
            JointId.HipRight, JointId.KneeRight, JointId.AnkleRight, JointId.FootRight,
        };

        // ------------------------------------------------------------------ Nativo

        /// <summary>
        /// Layout de NUI_SKELETON_FRAME (NuiSensor.h, SDK 1.8), em bytes:
        /// <code>
        /// LARGE_INTEGER liTimeStamp        0   (ms)
        /// DWORD dwFrameNumber              8
        /// DWORD dwFlags                   12
        /// Vector4 vFloorClipPlane         16
        /// Vector4 vNormalToGravity        32
        /// NUI_SKELETON_DATA[6]            48   (436 bytes cada)
        ///   eTrackingState                +0
        ///   dwTrackingID                  +4
        ///   dwEnrollmentIndex             +8
        ///   dwUserIndex                  +12
        ///   Vector4 Position             +16
        ///   Vector4 SkeletonPositions[20] +32
        ///   eSkeletonPositionTrackingState[20] +352
        ///   dwQualityFlags              +432
        /// </code>
        /// </summary>
        private static class NativeLayout
        {
            public const int SkeletonCount = 6;
            public const int JointCount = 20;

            public const int FrameTimestamp = 0;
            public const int FrameSkeletons = 48;
            public const int SkeletonSize = 436;
            public const int FrameSize = FrameSkeletons + SkeletonCount * SkeletonSize; // 2664

            public const int SkelTrackingState = 0;
            public const int SkelTrackingId = 4;
            public const int SkelPositions = 32;
            public const int SkelPositionStates = 352;
            public const int SkelQualityFlags = 432;
        }

        private static class NativeMethods
        {
            private const string Dll = "Kinect10.dll";

            public const uint NUI_INITIALIZE_FLAG_USES_SKELETON = 0x00000008;
            public const uint NUI_SKELETON_TRACKING_FLAG_ENABLE_SEATED_SUPPORT = 0x00000004;
            public const int NUI_SKELETON_TRACKED = 2;
            public const int S_FALSE = 1;
            public const int E_NUI_FRAME_NO_DATA = unchecked((int)0x83010001);

            [DllImport(Dll)] public static extern int NuiInitialize(uint dwFlags);
            [DllImport(Dll)] public static extern void NuiShutdown();
            [DllImport(Dll)] public static extern int NuiSkeletonTrackingEnable(IntPtr hNextFrameEvent, uint dwFlags);
            [DllImport(Dll)] public static extern int NuiSkeletonGetNextFrame(uint dwMillisecondsToWait, IntPtr pSkeletonFrame);
            [DllImport(Dll)] public static extern int NuiCameraElevationSetAngle(int lAngleDegrees);
            [DllImport(Dll)] public static extern int NuiCameraElevationGetAngle(out int plAngleDegrees);
        }
    }
}
