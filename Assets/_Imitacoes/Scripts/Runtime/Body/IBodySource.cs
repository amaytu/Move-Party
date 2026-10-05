using System;
using System.Collections.Generic;

namespace Imitacoes.Body
{
    /// <summary>Estado de uma fonte de corpos. Alimenta os avisos da UI ("Conecte o Kinect" etc.).</summary>
    public enum BodySourceStatus
    {
        /// <summary>Ainda não foi iniciada.</summary>
        NotStarted,

        /// <summary>Abrindo o sensor / carregando o arquivo.</summary>
        Starting,

        /// <summary>Funcionando e entregando frames (pode haver zero corpos na frente).</summary>
        Running,

        /// <summary>Sensor desconectado, sem energia ou em uso. Pode voltar sozinho para Running.</summary>
        SensorUnavailable,

        /// <summary>Falha que não se resolve sozinha (DLL ausente, arquivo inválido...). Ver StatusMessage.</summary>
        Error,

        /// <summary>Parada explicitamente.</summary>
        Stopped
    }

    /// <summary>
    /// Contrato de qualquer fonte de esqueletos: Kinect v2, Kinect v1 ou mock (arquivo/sintético).
    /// O resto do jogo depende SÓ desta interface, então é possível desenvolver sem o sensor.
    ///
    /// <para>Regras do contrato:</para>
    /// <list type="bullet">
    /// <item>Eventos são disparados na thread principal da Unity (dentro do Update da fonte).</item>
    /// <item><see cref="FrameArrived"/> só dispara quando há dado NOVO (o Kinect roda a 30 fps,
    /// a Unity pode rodar a 60+). Quem faz polling pode comparar <see cref="FrameNumber"/>.</item>
    /// <item><see cref="Bodies"/> contém apenas corpos rastreados, na ordem que a fonte quiser.
    /// Escolher o "jogador ativo" é papel de <see cref="IActiveBodyProvider"/>.</item>
    /// <item>As instâncias de <see cref="BodyFrame"/> são reaproveitadas: valem até o próximo frame.</item>
    /// </list>
    /// </summary>
    public interface IBodySource
    {
        /// <summary>Nome para logs e para a UI de debug (ex.: "Kinect v2", "Mock: galinha.json").</summary>
        string SourceName { get; }

        BodySourceStatus Status { get; }

        /// <summary>Mensagem legível sobre o status atual (vazia quando está tudo bem).</summary>
        string StatusMessage { get; }

        /// <summary>Atalho para <c>Status == Running</c>.</summary>
        bool IsRunning { get; }

        /// <summary>Corpos rastreados no frame mais recente. Nunca é null; pode ser vazio.</summary>
        IReadOnlyList<BodyFrame> Bodies { get; }

        /// <summary>Incrementa a cada frame novo publicado.</summary>
        int FrameNumber { get; }

        /// <summary>Timestamp (s) do frame mais recente, no relógio da fonte.</summary>
        double Timestamp { get; }

        /// <summary>Disparado quando um frame novo foi publicado em <see cref="Bodies"/>.</summary>
        event Action<IBodySource> FrameArrived;

        /// <summary>Disparado quando <see cref="Status"/> ou <see cref="StatusMessage"/> muda.</summary>
        event Action<IBodySource, BodySourceStatus> StatusChanged;

        /// <summary>Abre o sensor / começa a reprodução. Idempotente.</summary>
        void StartSource();

        /// <summary>Fecha o sensor e libera recursos. Idempotente.</summary>
        void StopSource();
    }
}
