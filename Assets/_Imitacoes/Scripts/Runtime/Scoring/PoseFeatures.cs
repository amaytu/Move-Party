using System.Collections.Generic;
using Imitacoes.Body;
using UnityEngine;

namespace Imitacoes.Scoring
{
    /// <summary>
    /// Representação de UM frame para comparação, independente de onde o jogador está,
    /// da altura dele e de para onde está virado:
    ///
    /// 1. Rotação: gira o corpo em torno do eixo vertical para que a linha ombros+quadril fique
    ///    alinhada com +X (jogador "de frente" para o sensor).
    /// 2. Direções dos ossos: vetor unitário de cada osso (ombro→cotovelo, quadril→joelho...).
    ///    Não depende da posição nem do tamanho do corpo.
    /// 3. Altura do quadril: deslocamento vertical da base da coluna em relação ao início do clipe,
    ///    dividido pelo comprimento do tronco — captura agachar e pular.
    /// 4. Giro do corpo (yaw, graus): quanto o jogador está virado para a esquerda/direita em relação
    ///    ao sensor. A etapa 1 remove o giro para comparar a pose; aqui ele volta como um movimento próprio.
    /// 5. Confiança por osso: 1 (rastreado), peso configurável (inferido) ou 0 (ignorado).
    /// </summary>
    internal sealed class PoseFeatures
    {
        public readonly Vector3[] Directions = new Vector3[BoneCount];
        public readonly float[] Confidence = new float[BoneCount];
        public float RootHeight;
        public float Yaw;
        public bool Valid;

        public static int BoneCount => SkeletonDefinition.Bones.Count;

        /// <summary>
        /// Índice do osso espelhado (esquerdo ↔ direito). Usado para aceitar imitações "de espelho":
        /// o jogador que levanta a mão esquerda quando o Mestre levantou a direita.
        /// </summary>
        public static readonly int[] MirrorBone = BuildMirrorTable();

        /// <summary>Extrai as features de uma sequência inteira (com passo de subamostragem).</summary>
        public static List<PoseFeatures> FromFrames(IReadOnlyList<BodyFrame> frames, int step, float inferredWeight)
        {
            var result = new List<PoseFeatures>(frames.Count / Mathf.Max(1, step) + 1);
            float? baselineY = null;

            for (int i = 0; i < frames.Count; i += Mathf.Max(1, step))
            {
                var f = new PoseFeatures();
                f.Extract(frames[i], inferredWeight, ref baselineY);
                result.Add(f);
            }
            return result;
        }

        private void Extract(BodyFrame frame, float inferredWeight, ref float? baselineY)
        {
            Valid = frame != null && frame.IsTracked && frame.GetJoint(JointId.SpineBase).IsUsable;
            if (!Valid) return;

            Quaternion align = FacingAlignment(frame, out Yaw);
            IReadOnlyList<Bone> bones = SkeletonDefinition.Bones;
            int usable = 0;

            for (int b = 0; b < bones.Count; b++)
            {
                JointData from = frame.GetJoint(bones[b].From);
                JointData to = frame.GetJoint(bones[b].To);
                float confidence = Mathf.Min(StateWeight(from.State, inferredWeight), StateWeight(to.State, inferredWeight));

                Vector3 dir = to.Position - from.Position;
                if (confidence <= 0f || dir.sqrMagnitude < 1e-6f)
                {
                    Confidence[b] = 0f;
                    Directions[b] = Vector3.zero;
                    continue;
                }

                Directions[b] = align * dir.normalized;
                Confidence[b] = confidence;
                usable++;
            }

            // Tronco como régua: altura do quadril em "tamanhos de tronco".
            Vector3 root = frame.GetPosition(JointId.SpineBase);
            float trunk = Vector3.Distance(root, frame.GetPosition(JointId.SpineShoulder));
            if (trunk < 0.2f) trunk = 0.5f;
            baselineY ??= root.y;
            RootHeight = (root.y - baselineY.Value) / trunk;

            Valid = usable >= 6; // poucos ossos = frame sem informação útil
        }

        /// <summary>Rotação em torno de Y que deixa ombros+quadril paralelos a +X.</summary>
        private static Quaternion FacingAlignment(BodyFrame frame, out float yawDegrees)
        {
            Vector3 right = Vector3.zero;
            AddIfUsable(frame, JointId.ShoulderLeft, JointId.ShoulderRight, ref right);
            AddIfUsable(frame, JointId.HipLeft, JointId.HipRight, ref right);
            right.y = 0f;
            yawDegrees = 0f;
            if (right.sqrMagnitude < 1e-4f) return Quaternion.identity;

            right.Normalize();
            yawDegrees = Mathf.Atan2(right.z, right.x) * Mathf.Rad2Deg; // 0 = de frente para o sensor
            return Quaternion.FromToRotation(right, Vector3.right);
        }

        private static void AddIfUsable(BodyFrame frame, JointId left, JointId right, ref Vector3 sum)
        {
            JointData l = frame.GetJoint(left);
            JointData r = frame.GetJoint(right);
            if (l.IsUsable && r.IsUsable) sum += r.Position - l.Position;
        }

        private static float StateWeight(JointTrackingState state, float inferredWeight)
        {
            switch (state)
            {
                case JointTrackingState.Tracked: return 1f;
                case JointTrackingState.Inferred: return inferredWeight;
                default: return 0f;
            }
        }

        private static int[] BuildMirrorTable()
        {
            IReadOnlyList<Bone> bones = SkeletonDefinition.Bones;
            var table = new int[bones.Count];
            for (int b = 0; b < bones.Count; b++)
            {
                JointId from = Mirror(bones[b].From);
                JointId to = Mirror(bones[b].To);
                table[b] = b;
                for (int k = 0; k < bones.Count; k++)
                {
                    if (bones[k].From == from && bones[k].To == to)
                    {
                        table[b] = k;
                        break;
                    }
                }
            }
            return table;
        }

        private static JointId Mirror(JointId id)
        {
            string name = id.ToString();
            if (name.EndsWith("Left")) return (JointId)System.Enum.Parse(typeof(JointId), name.Substring(0, name.Length - 4) + "Right");
            if (name.EndsWith("Right")) return (JointId)System.Enum.Parse(typeof(JointId), name.Substring(0, name.Length - 5) + "Left");
            return id;
        }
    }
}
