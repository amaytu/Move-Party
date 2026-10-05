using System;
using UnityEngine;

namespace Imitacoes.Body
{
    /// <summary>
    /// Dados de um joint em um frame: posição, orientação e confiabilidade.
    /// É um struct (tipo valor) para que o array de 25 joints fique contíguo na memória
    /// e copiar um frame inteiro seja um único <c>Array.Copy</c>, sem alocação.
    /// Campos públicos de propósito: é um DTO serializado pelo JsonUtility.
    /// </summary>
    [Serializable]
    public struct JointData
    {
        /// <summary>Posição em metros, na convenção de coordenadas descrita em <see cref="BodyFrame"/>.</summary>
        public Vector3 Position;

        /// <summary>
        /// Orientação absoluta do joint (Kinect v2: JointOrientation; Kinect v1: BoneOrientation absoluta).
        /// É <see cref="Quaternion.identity"/> quando a fonte não fornece orientação.
        /// O algoritmo de similaridade NÃO depende dela (usa vetores de ossos), então é opcional.
        /// </summary>
        public Quaternion Orientation;

        /// <summary>Confiabilidade do dado neste frame.</summary>
        public JointTrackingState State;

        /// <summary>Valor padrão para joint sem dado. Use este em vez de <c>default</c>,
        /// porque <c>default(Quaternion)</c> é (0,0,0,0), que não é uma rotação válida.</summary>
        public static readonly JointData NotTracked =
            new JointData(Vector3.zero, Quaternion.identity, JointTrackingState.NotTracked);

        public JointData(Vector3 position, Quaternion orientation, JointTrackingState state)
        {
            Position = position;
            Orientation = orientation;
            State = state;
        }

        /// <summary>Visto diretamente pelo sensor.</summary>
        public bool IsTracked => State == JointTrackingState.Tracked;

        /// <summary>Tem algum dado (Tracked ou Inferred).</summary>
        public bool IsUsable => State != JointTrackingState.NotTracked;

        /// <summary>
        /// Interpola dois joints (usado no replay e na reamostragem para taxa fixa).
        /// O estado resultante é o PIOR dos dois, para não "promover" um dado inferido a rastreado.
        /// </summary>
        public static JointData Lerp(in JointData a, in JointData b, float t)
        {
            return new JointData(
                Vector3.Lerp(a.Position, b.Position, t),
                Quaternion.Slerp(SafeRotation(a.Orientation), SafeRotation(b.Orientation), t),
                a.State < b.State ? a.State : b.State);
        }

        /// <summary>
        /// Garante um quaternion utilizável: (0,0,0,0) vira identity e o resto é normalizado.
        /// Evita NaN no Slerp quando o dado veio de JSON antigo ou de uma fonte sem orientação.
        /// </summary>
        public static Quaternion SafeRotation(Quaternion q)
        {
            float sqrMagnitude = q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w;
            return sqrMagnitude < 1e-6f ? Quaternion.identity : Quaternion.Normalize(q);
        }

        public override string ToString() => $"{State} {Position:F3}";
    }
}
