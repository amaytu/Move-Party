using System;
using System.Collections.Generic;
using UnityEngine;

namespace Imitacoes.Body
{
    /// <summary>
    /// "Foto" de UM corpo em um instante: timestamp + os 25 joints + metadados.
    ///
    /// <para><b>Convenção de coordenadas</b> (todas as fontes — Kinect v2, v1 e mock — entregam assim):</para>
    /// <list type="bullet">
    /// <item>Unidade: metros. Origem: o sensor.</item>
    /// <item>+X = lado DIREITO do jogador, +Y = para cima, +Z = para a FRENTE do jogador (na direção do sensor).
    /// O jogador fica em Z negativo. É o referencial "de personagem" da Unity (mão esquerda, olhando para +Z).</item>
    /// <item>Conversão a partir do Kinect (mão direita, +Z saindo do sensor): <c>unity = (x, y, -z)</c>.</item>
    /// <item>Os joints são ANATÔMICOS: <see cref="JointId.HandRight"/> é sempre a mão direita do jogador.
    /// Espelhar a imagem na tela é responsabilidade de quem desenha, não dos dados.</item>
    /// </list>
    ///
    /// <para><b>Posse:</b> as fontes REAPROVEITAM as instâncias entre frames (sem alocação por frame).
    /// Uma referência obtida em <see cref="IBodySource.Bodies"/> só é válida até o próximo
    /// <see cref="IBodySource.FrameArrived"/>. Para guardar, use <see cref="Clone"/> ou <see cref="CopyFrom"/>.</para>
    ///
    /// Campos públicos de propósito: é um DTO serializado pelo JsonUtility (arquivos do mock).
    /// </summary>
    [Serializable]
    public sealed class BodyFrame
    {
        /// <summary>Quantidade de joints (Kinect v2). Tamanho fixo do array <see cref="Joints"/>.</summary>
        public const int JointCount = 25;

        /// <summary>
        /// Momento do frame, em segundos. Em frames vivos é o relógio da fonte;
        /// em um <c>MotionClip</c> gravado é relativo ao início da gravação (0 = primeiro frame).
        /// </summary>
        public double Timestamp;

        /// <summary>Id do corpo dado pelo sensor. Muda quando a pessoa sai e volta. 0 = inválido.</summary>
        public ulong TrackingId;

        /// <summary>O sensor está rastreando este corpo neste frame.</summary>
        public bool IsTracked;

        public HandState LeftHandState;
        public HandState RightHandState;

        /// <summary>Bordas do campo de visão que estão cortando o corpo.</summary>
        public ClippedEdges ClippedEdges;

        /// <summary>Joints indexados por <c>(int)JointId</c>. Sempre com <see cref="JointCount"/> elementos.</summary>
        public JointData[] Joints;

        public BodyFrame()
        {
            Joints = new JointData[JointCount];
            Reset();
        }

        // ------------------------------------------------------------------ Acesso

        public JointData GetJoint(JointId id) => Joints[(int)id];

        public Vector3 GetPosition(JointId id) => Joints[(int)id].Position;

        public JointTrackingState GetState(JointId id) => Joints[(int)id].State;

        public void SetJoint(JointId id, in JointData data) => Joints[(int)id] = data;

        public void SetJoint(JointId id, Vector3 position, Quaternion orientation, JointTrackingState state)
            => Joints[(int)id] = new JointData(position, orientation, state);

        /// <summary>Base da coluna: referência para normalização e para "onde o jogador está".</summary>
        public Vector3 Root => GetPosition(JointId.SpineBase);

        /// <summary>Distância (m) do jogador ao sensor, medida na base da coluna.</summary>
        public float DistanceFromSensor => -Root.z;

        /// <summary>Algum lado do corpo está cortado pela borda do campo de visão.</summary>
        public bool IsClipped => ClippedEdges != ClippedEdges.None;

        // ------------------------------------------------------------------ Qualidade do rastreamento

        /// <summary>
        /// Fração (0..1) dos joints "centrais" que estão no estado mínimo pedido.
        /// Usa <see cref="SkeletonDefinition.CoreJoints"/> (os 20 comuns ao v1 e ao v2),
        /// para que o valor seja comparável entre sensores.
        /// </summary>
        public float GetTrackedRatio(JointTrackingState minimumState = JointTrackingState.Tracked)
        {
            if (!IsTracked) return 0f;

            IReadOnlyList<JointId> core = SkeletonDefinition.CoreJoints;
            int count = 0;
            for (int i = 0; i < core.Count; i++)
            {
                if (Joints[(int)core[i]].State >= minimumState) count++;
            }
            return (float)count / core.Count;
        }

        // ------------------------------------------------------------------ Cópia / reset

        /// <summary>Volta ao estado "sem corpo". Não aloca.</summary>
        public void Reset()
        {
            Timestamp = 0;
            TrackingId = 0;
            IsTracked = false;
            LeftHandState = HandState.Unknown;
            RightHandState = HandState.Unknown;
            ClippedEdges = ClippedEdges.None;

            EnsureJointArray();
            for (int i = 0; i < JointCount; i++) Joints[i] = JointData.NotTracked;
        }

        /// <summary>Copia todos os dados de <paramref name="source"/> para esta instância. Não aloca.</summary>
        public void CopyFrom(BodyFrame source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (ReferenceEquals(source, this)) return;

            Timestamp = source.Timestamp;
            TrackingId = source.TrackingId;
            IsTracked = source.IsTracked;
            LeftHandState = source.LeftHandState;
            RightHandState = source.RightHandState;
            ClippedEdges = source.ClippedEdges;

            EnsureJointArray();
            source.EnsureJointArray();
            Array.Copy(source.Joints, Joints, JointCount);
        }

        /// <summary>Cópia independente (aloca). Use para guardar um frame além do frame atual.</summary>
        public BodyFrame Clone()
        {
            var copy = new BodyFrame();
            copy.CopyFrom(this);
            return copy;
        }

        /// <summary>
        /// Garante que <see cref="Joints"/> existe e tem <see cref="JointCount"/> elementos.
        /// Necessário depois de desserializar JSON incompleto ou de versões antigas.
        /// </summary>
        public void EnsureJointArray()
        {
            if (Joints != null && Joints.Length == JointCount) return;

            JointData[] old = Joints;
            Joints = new JointData[JointCount];
            for (int i = 0; i < JointCount; i++)
            {
                Joints[i] = old != null && i < old.Length ? old[i] : JointData.NotTracked;
            }
        }

        /// <summary>
        /// Interpola dois frames em <paramref name="result"/> (sem alocação).
        /// Metadados discretos (id, mãos, bordas) vêm do frame mais próximo.
        /// </summary>
        public static void Lerp(BodyFrame a, BodyFrame b, float t, BodyFrame result)
        {
            if (a == null) throw new ArgumentNullException(nameof(a));
            if (b == null) throw new ArgumentNullException(nameof(b));
            if (result == null) throw new ArgumentNullException(nameof(result));

            t = Mathf.Clamp01(t);
            BodyFrame nearest = t < 0.5f ? a : b;

            // Guarda os joints antes de copiar metadados: result pode ser o próprio a ou b.
            result.EnsureJointArray();
            for (int i = 0; i < JointCount; i++)
            {
                result.Joints[i] = JointData.Lerp(a.Joints[i], b.Joints[i], t);
            }

            result.Timestamp = a.Timestamp + (b.Timestamp - a.Timestamp) * t;
            result.TrackingId = nearest.TrackingId;
            result.IsTracked = a.IsTracked && b.IsTracked;
            result.LeftHandState = nearest.LeftHandState;
            result.RightHandState = nearest.RightHandState;
            result.ClippedEdges = nearest.ClippedEdges;
        }

        public override string ToString()
            => $"BodyFrame(id={TrackingId}, t={Timestamp:F3}s, tracked={IsTracked}, ratio={GetTrackedRatio():P0})";
    }
}
