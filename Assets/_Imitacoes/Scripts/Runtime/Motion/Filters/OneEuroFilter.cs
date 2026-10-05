using System;
using UnityEngine;

namespace Imitacoes.Motion.Filters
{
    /// <summary>
    /// Parâmetros do filtro One Euro. Ajuste no Inspector:
    /// 1) com o jogador PARADO, baixe <see cref="minCutoff"/> até o tremor sumir;
    /// 2) com o jogador se mexendo RÁPIDO, suba <see cref="beta"/> até o atraso sumir.
    /// </summary>
    [Serializable]
    public struct OneEuroParams
    {
        [Tooltip("Frequência de corte mínima (Hz). Menor = mais suave quando parado.")]
        [Min(0.01f)] public float minCutoff;

        [Tooltip("Quanto a suavização diminui com a velocidade. Maior = menos atraso em movimentos rápidos.")]
        [Min(0f)] public float beta;

        [Tooltip("Frequência de corte da derivada (Hz). Normalmente 1.")]
        [Min(0.01f)] public float derivativeCutoff;

        public OneEuroParams(float minCutoff, float beta, float derivativeCutoff)
        {
            this.minCutoff = minCutoff;
            this.beta = beta;
            this.derivativeCutoff = derivativeCutoff;
        }

        /// <summary>Valores bons para posições do Kinect em metros.</summary>
        public static OneEuroParams Default => new OneEuroParams(1.2f, 1.5f, 1f);
    }

    /// <summary>
    /// Filtro One Euro para <see cref="Vector3"/> (Casiez, Roussel e Vogel, CHI 2012).
    /// Suaviza muito quando o sinal está quase parado (remove o tremor do Kinect)
    /// e pouco quando ele se move rápido (sem atraso perceptível).
    /// </summary>
    public sealed class OneEuroFilterVector3
    {
        private Vector3 previousValue;
        private Vector3 previousDerivative;
        private bool initialized;

        public void Reset() => initialized = false;

        /// <param name="value">Nova amostra.</param>
        /// <param name="deltaTime">Tempo (s) desde a amostra anterior.</param>
        public Vector3 Filter(Vector3 value, float deltaTime, in OneEuroParams p)
        {
            if (!initialized)
            {
                previousValue = value;
                previousDerivative = Vector3.zero;
                initialized = true;
                return value;
            }

            if (deltaTime <= 0f) return previousValue;

            // Derivada suavizada: estima a velocidade sem o ruído.
            Vector3 derivative = (value - previousValue) / deltaTime;
            previousDerivative = Vector3.Lerp(previousDerivative, derivative, Alpha(p.derivativeCutoff, deltaTime));

            // Corte adaptativo: quanto mais rápido, maior o corte (menos suavização).
            float cutoff = p.minCutoff + p.beta * previousDerivative.magnitude;
            previousValue = Vector3.Lerp(previousValue, value, Alpha(cutoff, deltaTime));
            return previousValue;
        }

        private static float Alpha(float cutoff, float deltaTime)
        {
            float tau = 1f / (2f * Mathf.PI * cutoff);
            return 1f / (1f + tau / deltaTime);
        }
    }
}
