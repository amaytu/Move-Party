using System.Collections.Generic;
using Imitacoes.Body;
using Imitacoes.Motion;
using UnityEngine;

namespace Imitacoes.Scoring
{
    /// <summary>Resultado de uma comparação.</summary>
    public readonly struct ComparisonResult
    {
        /// <summary>Nota de 0 a 100.</summary>
        public readonly float Score;

        /// <summary>Distância média por passo do caminho do DTW (0 = idêntico, 1 = oposto).</summary>
        public readonly float AverageDistance;

        /// <summary>A melhor correspondência foi como espelho (lados trocados).</summary>
        public readonly bool Mirrored;

        /// <summary>Fração dos frames da imitação em que o jogador estava rastreado.</summary>
        public readonly float TrackedFraction;

        public ComparisonResult(float score, float averageDistance, bool mirrored, float trackedFraction)
        {
            Score = score;
            AverageDistance = averageDistance;
            Mirrored = mirrored;
            TrackedFraction = trackedFraction;
        }

        public override string ToString()
            => $"nota {Score:F0} (dist {AverageDistance:F3}{(Mirrored ? ", espelhado" : "")}, rastreado {TrackedFraction:P0})";
    }

    /// <summary>
    /// Compara o movimento do Mestre com o de um imitador e devolve uma nota de 0 a 100.
    ///
    /// Pipeline:
    /// 1. Features por frame (<see cref="PoseFeatures"/>): direções dos ossos no referencial do corpo
    ///    + altura do quadril normalizada pelo tronco. Não depende de posição, altura nem rotação.
    /// 2. Distância entre dois frames: média ponderada do ângulo entre os ossos, θ/π (0 = mesma direção,
    ///    1 = oposta), com pesos por grupo, confiança do rastreamento e ênfase nos ossos que o Mestre movimenta.
    /// 3. Dynamic Time Warping com janela de Sakoe-Chiba: alinha as sequências tolerando atraso e
    ///    diferença de ritmo. Resultado = custo médio por passo do caminho ótimo.
    /// 4. Nota = tabela distância → nota (interpolação linear) — ajuste no Inspector.
    /// Opcionalmente testa também a versão espelhada e fica com a melhor.
    /// </summary>
    [AddComponentMenu("Imitações/Motion Comparer")]
    public sealed class MotionComparer : MonoBehaviour
    {
        [Header("Pesos por grupo de ossos")]
        [SerializeField, Min(0f)] private float torsoWeight = 1f;
        [SerializeField, Min(0f)] private float headWeight = 0.3f;
        [SerializeField, Min(0f)] private float armsWeight = 2f;
        [SerializeField, Min(0f)] private float handsWeight = 0.3f;
        [SerializeField, Min(0f)] private float legsWeight = 1.5f;
        [SerializeField, Min(0f)] private float feetWeight = 0.3f;
        [SerializeField, Min(0f), Tooltip("Peso da altura do quadril (agachar/pular).")]
        private float rootHeightWeight = 0.5f;
        [SerializeField, Min(0f), Tooltip("Peso do giro do corpo (virar de lado). Diferença de 90° = distância máxima.")]
        private float bodyTurnWeight = 2f;
        [SerializeField, Range(0f, 10f), Tooltip("Peso extra para os ossos que o Mestre MOVIMENTA. " +
            "0 = todos iguais; 3 = o osso mais movimentado vale 4× mais. Evita que tronco e pernas parados " +
            "\"combinem de graça\" quando o Mestre só mexeu os braços.")]
        private float motionEmphasis = 3f;

        [Header("Rastreamento")]
        [SerializeField, Range(0f, 1f), Tooltip("Peso de joints \"Inferred\" (estimados pelo sensor).")]
        private float inferredJointWeight = 0.4f;
        [SerializeField, Range(0f, 1f), Tooltip("Distância quando o imitador não aparece no frame.")]
        private float missingFrameDistance = 1f;

        [Header("Dynamic Time Warping")]
        [SerializeField, Range(1, 4), Tooltip("Compara 1 a cada N frames (2 = 15 fps): menos ruído e mais rápido.")]
        private int frameStep = 2;
        [SerializeField, Range(0.05f, 1f), Tooltip("Janela de Sakoe-Chiba: atraso máximo tolerado, em fração da duração.")]
        private float bandFraction = 0.35f;
        [SerializeField, Tooltip("Aceita imitação espelhada (esquerda ↔ direita) e fica com a melhor nota.")]
        private bool allowMirroredImitation = true;

        [Header("Nota")]
        [SerializeField, Tooltip("Tabela distância → nota (X = distância média do DTW, Y = nota 0..100), " +
            "em ordem crescente de distância. Entre dois pontos a nota é interpolada em linha reta. " +
            "Calibrada com partidas reais: imitações boas ficaram entre 0,03 e 0,08 (→ 85–98) " +
            "e as fracas perto de 0,15 (→ ~45).")]
        private Vector2[] scoreTable =
        {
            new Vector2(0.00f, 100f),
            new Vector2(0.04f, 97f),
            new Vector2(0.07f, 88f),
            new Vector2(0.10f, 78f),
            new Vector2(0.13f, 60f),
            new Vector2(0.16f, 40f),
            new Vector2(0.20f, 22f),
            new Vector2(0.26f, 8f),
            new Vector2(0.32f, 0f),
        };

        private readonly float[] groupWeights = new float[6];
        private float[] boneWeights = new float[0];
        private float turnWeight;

        // ------------------------------------------------------------------ API

        public ComparisonResult Compare(MotionClip reference, MotionClip attempt)
        {
            if (reference == null || attempt == null || reference.IsEmpty || attempt.IsEmpty)
                return new ComparisonResult(0f, 1f, false, 0f);

            CacheGroupWeights();

            List<PoseFeatures> refFeatures = PoseFeatures.FromFrames(reference.Frames, frameStep, inferredJointWeight);
            List<PoseFeatures> attFeatures = PoseFeatures.FromFrames(attempt.Frames, frameStep, inferredJointWeight);

            float trackedFraction = FractionValid(attFeatures);
            if (trackedFraction <= 0f) return new ComparisonResult(0f, 1f, false, 0f);

            ComputeBoneWeights(refFeatures);

            float normal = Dtw(refFeatures, attFeatures, mirrored: false);
            bool mirrored = false;
            float best = normal;

            if (allowMirroredImitation)
            {
                float mirror = Dtw(refFeatures, attFeatures, mirrored: true);
                if (mirror < best)
                {
                    best = mirror;
                    mirrored = true;
                }
            }

            return new ComparisonResult(DistanceToScore(best), best, mirrored, trackedFraction);
        }

        /// <summary>Converte a distância média do DTW em nota 0..100 pela <see cref="scoreTable"/>.</summary>
        public float DistanceToScore(float averageDistance)
        {
            Vector2[] t = scoreTable;
            if (t == null || t.Length == 0) return 0f;
            if (averageDistance <= t[0].x) return Mathf.Round(Mathf.Clamp(t[0].y, 0f, 100f));

            for (int i = 1; i < t.Length; i++)
            {
                if (averageDistance > t[i].x) continue;
                float span = t[i].x - t[i - 1].x;
                float k = span > 0f ? (averageDistance - t[i - 1].x) / span : 1f;
                return Mathf.Round(Mathf.Clamp(Mathf.Lerp(t[i - 1].y, t[i].y, k), 0f, 100f));
            }

            return Mathf.Round(Mathf.Clamp(t[t.Length - 1].y, 0f, 100f));
        }

        // ------------------------------------------------------------------ DTW

        private float Dtw(List<PoseFeatures> a, List<PoseFeatures> b, bool mirrored)
        {
            int n = a.Count, m = b.Count;
            int band = Mathf.Max(Mathf.Abs(n - m), Mathf.CeilToInt(bandFraction * Mathf.Max(n, m)));

            // cost[i,j] = menor custo acumulado alinhando a[0..i) com b[0..j); steps = tamanho do caminho.
            var cost = new float[n + 1, m + 1];
            var steps = new int[n + 1, m + 1];
            for (int i = 0; i <= n; i++)
                for (int j = 0; j <= m; j++)
                    cost[i, j] = float.PositiveInfinity;
            cost[0, 0] = 0f;

            for (int i = 1; i <= n; i++)
            {
                int jStart = Mathf.Max(1, i - band);
                int jEnd = Mathf.Min(m, i + band);
                for (int j = jStart; j <= jEnd; j++)
                {
                    float d = FrameDistance(a[i - 1], b[j - 1], mirrored);

                    // Diagonal primeiro: em empate, preferir avançar nas duas sequências.
                    float best = cost[i - 1, j - 1];
                    int bestSteps = steps[i - 1, j - 1];
                    if (cost[i - 1, j] < best) { best = cost[i - 1, j]; bestSteps = steps[i - 1, j]; }
                    if (cost[i, j - 1] < best) { best = cost[i, j - 1]; bestSteps = steps[i, j - 1]; }

                    cost[i, j] = best + d;
                    steps[i, j] = bestSteps + 1;
                }
            }

            return steps[n, m] > 0 ? cost[n, m] / steps[n, m] : 1f;
        }

        private float FrameDistance(PoseFeatures reference, PoseFeatures attempt, bool mirrored)
        {
            if (!reference.Valid) return 0f; // o Mestre saiu do quadro: não culpar o imitador
            if (!attempt.Valid) return missingFrameDistance;

            IReadOnlyList<Bone> bones = SkeletonDefinition.Bones;
            float sum = 0f, weightSum = 0f;

            for (int b = 0; b < bones.Count; b++)
            {
                int ab = mirrored ? PoseFeatures.MirrorBone[b] : b;
                float confidence = Mathf.Min(reference.Confidence[b], attempt.Confidence[ab]);
                if (confidence <= 0f) continue;

                Vector3 attemptDir = attempt.Directions[ab];
                if (mirrored) attemptDir.x = -attemptDir.x; // espelha em relação ao plano do corpo

                float w = boneWeights[b] * confidence;
                // Ângulo linear (θ/π): pune diferenças pequenas mais que (1 − cos θ)/2.
                float dot = Mathf.Clamp(Vector3.Dot(reference.Directions[b], attemptDir), -1f, 1f);
                sum += w * Mathf.Acos(dot) / Mathf.PI;
                weightSum += w;
            }

            if (bodyTurnWeight > 0f)
            {
                // Espelhado: virar para a esquerda equivale a virar para a direita.
                float attemptYaw = mirrored ? -attempt.Yaw : attempt.Yaw;
                float turn = Mathf.Abs(Mathf.DeltaAngle(reference.Yaw, attemptYaw)) / 90f;
                sum += turnWeight * Mathf.Min(1f, turn);
                weightSum += turnWeight;
            }

            if (rootHeightWeight > 0f)
            {
                sum += rootHeightWeight * Mathf.Min(1f, Mathf.Abs(reference.RootHeight - attempt.RootHeight));
                weightSum += rootHeightWeight;
            }

            return weightSum > 0f ? sum / weightSum : missingFrameDistance;
        }

        private void CacheGroupWeights()
        {
            groupWeights[(int)BodyPartGroup.Torso] = torsoWeight;
            groupWeights[(int)BodyPartGroup.Head] = headWeight;
            groupWeights[(int)BodyPartGroup.Arms] = armsWeight;
            groupWeights[(int)BodyPartGroup.Hands] = handsWeight;
            groupWeights[(int)BodyPartGroup.Legs] = legsWeight;
            groupWeights[(int)BodyPartGroup.Feet] = feetWeight;
        }

        /// <summary>
        /// Peso final de cada osso = peso do grupo × (1 + ênfase × atividade relativa no Mestre).
        /// Atividade = 1 − |média das direções| (0 = osso parado; perto de 1 = varreu muitas direções).
        /// </summary>
        private void ComputeBoneWeights(List<PoseFeatures> reference)
        {
            IReadOnlyList<Bone> bones = SkeletonDefinition.Bones;
            if (boneWeights.Length != bones.Count) boneWeights = new float[bones.Count];

            var activity = new float[bones.Count];
            float maxActivity = 0f;
            for (int b = 0; b < bones.Count; b++)
            {
                Vector3 sum = Vector3.zero;
                int count = 0;
                foreach (PoseFeatures f in reference)
                {
                    if (!f.Valid || f.Confidence[b] <= 0f) continue;
                    sum += f.Directions[b];
                    count++;
                }
                activity[b] = count > 0 ? 1f - sum.magnitude / count : 0f;
                maxActivity = Mathf.Max(maxActivity, activity[b]);
            }

            for (int b = 0; b < bones.Count; b++)
            {
                float relative = maxActivity > 0.02f ? activity[b] / maxActivity : 0f;
                boneWeights[b] = groupWeights[(int)bones[b].Group] * (1f + motionEmphasis * relative);
            }

            // Giro: se o Mestre vira o corpo (desvio-padrão do yaw ~30° ou mais), o giro ganha a mesma ênfase.
            float mean = 0f, meanSq = 0f;
            int n = 0;
            foreach (PoseFeatures f in reference)
            {
                if (!f.Valid) continue;
                mean += f.Yaw;
                meanSq += f.Yaw * f.Yaw;
                n++;
            }
            float std = n > 0 ? Mathf.Sqrt(Mathf.Max(0f, meanSq / n - (mean / n) * (mean / n))) : 0f;
            turnWeight = bodyTurnWeight * (1f + motionEmphasis * Mathf.Clamp01(std / 30f));
        }

        private static float FractionValid(List<PoseFeatures> features)
        {
            if (features.Count == 0) return 0f;
            int valid = 0;
            foreach (PoseFeatures f in features) if (f.Valid) valid++;
            return (float)valid / features.Count;
        }
    }
}
