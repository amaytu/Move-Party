using System.Collections.Generic;

namespace Imitacoes.Body
{
    /// <summary>
    /// Grupos de partes do corpo. Usados para dar pesos diferentes na pontuação
    /// (braços e pernas pesam mais; cabeça e mãos, menos) e para colorir/destacar no esqueleto.
    /// </summary>
    public enum BodyPartGroup
    {
        Torso,
        Head,
        Arms,
        Hands,
        Legs,
        Feet
    }

    /// <summary>
    /// Um osso = segmento entre dois joints. A direção normalizada (To - From) é a base
    /// do algoritmo de similaridade, pois não depende da altura nem da posição do jogador.
    /// </summary>
    public readonly struct Bone
    {
        /// <summary>Joint "pai" (mais próximo da coluna).</summary>
        public readonly JointId From;

        /// <summary>Joint "filho" (mais próximo da extremidade).</summary>
        public readonly JointId To;

        public readonly BodyPartGroup Group;

        public Bone(JointId from, JointId to, BodyPartGroup group)
        {
            From = from;
            To = to;
            Group = group;
        }

        public override string ToString() => $"{From}→{To} ({Group})";
    }

    /// <summary>
    /// Topologia fixa do esqueleto: lista de ossos e de joints comuns aos dois sensores.
    /// Dados estáticos e somente leitura, compartilhados por renderização, gravação e pontuação.
    /// </summary>
    public static class SkeletonDefinition
    {
        /// <summary>Os 24 ossos do esqueleto do Kinect v2, da coluna para as extremidades.</summary>
        public static readonly IReadOnlyList<Bone> Bones = new[]
        {
            // Tronco e cabeça
            new Bone(JointId.SpineBase,     JointId.SpineMid,      BodyPartGroup.Torso),
            new Bone(JointId.SpineMid,      JointId.SpineShoulder, BodyPartGroup.Torso),
            new Bone(JointId.SpineShoulder, JointId.Neck,          BodyPartGroup.Head),
            new Bone(JointId.Neck,          JointId.Head,          BodyPartGroup.Head),

            // Braço esquerdo
            new Bone(JointId.SpineShoulder, JointId.ShoulderLeft,  BodyPartGroup.Torso),
            new Bone(JointId.ShoulderLeft,  JointId.ElbowLeft,     BodyPartGroup.Arms),
            new Bone(JointId.ElbowLeft,     JointId.WristLeft,     BodyPartGroup.Arms),
            new Bone(JointId.WristLeft,     JointId.HandLeft,      BodyPartGroup.Hands),
            new Bone(JointId.HandLeft,      JointId.HandTipLeft,   BodyPartGroup.Hands),
            new Bone(JointId.WristLeft,     JointId.ThumbLeft,     BodyPartGroup.Hands),

            // Braço direito
            new Bone(JointId.SpineShoulder, JointId.ShoulderRight, BodyPartGroup.Torso),
            new Bone(JointId.ShoulderRight, JointId.ElbowRight,    BodyPartGroup.Arms),
            new Bone(JointId.ElbowRight,    JointId.WristRight,    BodyPartGroup.Arms),
            new Bone(JointId.WristRight,    JointId.HandRight,     BodyPartGroup.Hands),
            new Bone(JointId.HandRight,     JointId.HandTipRight,  BodyPartGroup.Hands),
            new Bone(JointId.WristRight,    JointId.ThumbRight,    BodyPartGroup.Hands),

            // Perna esquerda
            new Bone(JointId.SpineBase,     JointId.HipLeft,       BodyPartGroup.Torso),
            new Bone(JointId.HipLeft,       JointId.KneeLeft,      BodyPartGroup.Legs),
            new Bone(JointId.KneeLeft,      JointId.AnkleLeft,     BodyPartGroup.Legs),
            new Bone(JointId.AnkleLeft,     JointId.FootLeft,      BodyPartGroup.Feet),

            // Perna direita
            new Bone(JointId.SpineBase,     JointId.HipRight,      BodyPartGroup.Torso),
            new Bone(JointId.HipRight,      JointId.KneeRight,     BodyPartGroup.Legs),
            new Bone(JointId.KneeRight,     JointId.AnkleRight,    BodyPartGroup.Legs),
            new Bone(JointId.AnkleRight,    JointId.FootRight,     BodyPartGroup.Feet),
        };

        /// <summary>
        /// Os 20 joints que existem tanto no Kinect v1 quanto no v2.
        /// (O v1 não tem Neck, HandTip e Thumb.) Use nas métricas de qualidade de rastreamento.
        /// </summary>
        public static readonly IReadOnlyList<JointId> CoreJoints = new[]
        {
            JointId.SpineBase, JointId.SpineMid, JointId.SpineShoulder, JointId.Head,
            JointId.ShoulderLeft, JointId.ElbowLeft, JointId.WristLeft, JointId.HandLeft,
            JointId.ShoulderRight, JointId.ElbowRight, JointId.WristRight, JointId.HandRight,
            JointId.HipLeft, JointId.KneeLeft, JointId.AnkleLeft, JointId.FootLeft,
            JointId.HipRight, JointId.KneeRight, JointId.AnkleRight, JointId.FootRight,
        };

        /// <summary>O joint existe no Kinect v1 (SDK 1.8)?</summary>
        public static bool ExistsInKinectV1(JointId id)
            => id != JointId.Neck
            && id != JointId.HandTipLeft && id != JointId.ThumbLeft
            && id != JointId.HandTipRight && id != JointId.ThumbRight;
    }
}
