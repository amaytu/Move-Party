using System;

namespace Imitacoes.Body
{
    /// <summary>
    /// Escolhe, entre os corpos de um <see cref="IBodySource"/>, qual é o "jogador ativo"
    /// (o mais próximo/central) e mantém essa escolha estável enquanto ele continuar rastreado,
    /// mesmo que outra pessoa passe na frente por um instante.
    ///
    /// Separado da fonte para que a regra de seleção seja uma só para Kinect v1, v2 e mock.
    /// A implementação (<c>ActiveBodySelector</c>) vem na próxima etapa.
    /// </summary>
    public interface IActiveBodyProvider
    {
        /// <summary>Frame do jogador ativo, já suavizado. Null quando ninguém está sendo rastreado.
        /// Mesma regra de posse do <see cref="BodyFrame"/>: válido até o próximo frame.</summary>
        BodyFrame ActiveBody { get; }

        bool HasActiveBody { get; }

        /// <summary>TrackingId do jogador ativo (0 quando não há).</summary>
        ulong ActiveTrackingId { get; }

        /// <summary>Quantas pessoas o sensor vê. &gt; 1 permite avisar "fique só uma pessoa na frente".</summary>
        int VisibleBodyCount { get; }

        /// <summary>Há quanto tempo (s) o jogador ativo está rastreado de forma contínua e estável.
        /// Usado na tela de posicionamento ("começa quando estável por ~1 s").</summary>
        float StableTrackingTime { get; }

        /// <summary>Disparado quando o jogador ativo muda. O argumento é null quando ele é perdido.</summary>
        event Action<BodyFrame> ActiveBodyChanged;

        /// <summary>Disparado a cada frame novo do jogador ativo (após suavização).</summary>
        event Action<BodyFrame> ActiveBodyUpdated;
    }
}
