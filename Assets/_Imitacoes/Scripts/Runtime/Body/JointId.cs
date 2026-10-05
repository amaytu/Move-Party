namespace Imitacoes.Body
{
    /// <summary>
    /// Identificadores das articulações (joints) do esqueleto.
    /// A ordem é IDÊNTICA à de <c>Windows.Kinect.JointType</c> (Kinect v2), o que permite
    /// converter por cast direto: <c>(JointId)(int)kinectJointType</c>.
    /// O Kinect v1 (20 joints) é mapeado na sua própria fonte; os joints que não existem
    /// no v1 (Neck, HandTip, Thumb) ficam como NotTracked ou são derivados.
    /// </summary>
    public enum JointId
    {
        SpineBase = 0,
        SpineMid = 1,
        Neck = 2,
        Head = 3,
        ShoulderLeft = 4,
        ElbowLeft = 5,
        WristLeft = 6,
        HandLeft = 7,
        ShoulderRight = 8,
        ElbowRight = 9,
        WristRight = 10,
        HandRight = 11,
        HipLeft = 12,
        KneeLeft = 13,
        AnkleLeft = 14,
        FootLeft = 15,
        HipRight = 16,
        KneeRight = 17,
        AnkleRight = 18,
        FootRight = 19,
        SpineShoulder = 20,
        HandTipLeft = 21,
        ThumbLeft = 22,
        HandTipRight = 23,
        ThumbRight = 24
    }

    /// <summary>
    /// Confiabilidade de um joint no frame. Mesmos valores de <c>Windows.Kinect.TrackingState</c>.
    /// A ordem numérica importa: quanto maior, mais confiável (usado em comparações e no Lerp).
    /// </summary>
    public enum JointTrackingState : byte
    {
        /// <summary>O sensor não sabe onde o joint está. Não usar.</summary>
        NotTracked = 0,

        /// <summary>Posição estimada (joint oculto ou fora da visão). Usar com peso reduzido.</summary>
        Inferred = 1,

        /// <summary>Joint visto diretamente pelo sensor.</summary>
        Tracked = 2
    }

    /// <summary>
    /// Estado da mão. Mesmos valores de <c>Windows.Kinect.HandState</c>.
    /// Só existe no Kinect v2; no v1 e no mock fica <see cref="Unknown"/>.
    /// </summary>
    public enum HandState : byte
    {
        Unknown = 0,
        NotTracked = 1,
        Open = 2,
        Closed = 3,
        Lasso = 4
    }

    /// <summary>
    /// Bordas do campo de visão que estão "cortando" o corpo.
    /// Mesmos valores de <c>Windows.Kinect.FrameEdges</c>. Útil para avisar
    /// "você está saindo do campo de visão" antes de o rastreamento cair.
    /// </summary>
    [System.Flags]
    public enum ClippedEdges : byte
    {
        None = 0,
        Right = 1,
        Left = 2,
        Top = 4,
        Bottom = 8
    }
}
