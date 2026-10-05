using System;
using System.Collections.Generic;
using UnityEngine;

namespace Imitacoes.Body
{
    /// <summary>
    /// Base comum das fontes (Kinect v2, v1, mock) como MonoBehaviour.
    ///
    /// Por que existe: o Inspector da Unity não serializa campos do tipo interface.
    /// Os outros componentes declaram <c>[SerializeField] BodySourceBehaviour source</c>
    /// e usam só a interface <see cref="IBodySource"/> no código.
    ///
    /// Também centraliza o que toda fonte repete: controle de status, pool de <see cref="BodyFrame"/>
    /// (zero alocação por frame) e disparo seguro de eventos.
    ///
    /// Como uma subclasse publica um frame:
    /// <code>
    /// BeginFrame();
    /// foreach (corpo rastreado) { BodyFrame b = AcquireBody(); /* preencher b */ }
    /// PublishFrame(timestamp);
    /// </code>
    /// </summary>
    [DefaultExecutionOrder(-100)] // roda antes dos consumidores, que assim leem o frame do mesmo Update
    public abstract class BodySourceBehaviour : MonoBehaviour, IBodySource
    {
        [SerializeField, Tooltip("Inicia a fonte automaticamente no OnEnable.")]
        private bool startOnEnable = true;

        private readonly List<BodyFrame> bodies = new List<BodyFrame>(6);
        private readonly Stack<BodyFrame> pool = new Stack<BodyFrame>(6);

        private BodySourceStatus status = BodySourceStatus.NotStarted;
        private string statusMessage = string.Empty;
        private int frameNumber;
        private double timestamp;

        // ------------------------------------------------------------------ IBodySource

        public abstract string SourceName { get; }
        public BodySourceStatus Status => status;
        public string StatusMessage => statusMessage;
        public bool IsRunning => status == BodySourceStatus.Running;
        public IReadOnlyList<BodyFrame> Bodies => bodies;
        public int FrameNumber => frameNumber;
        public double Timestamp => timestamp;

        public event Action<IBodySource> FrameArrived;
        public event Action<IBodySource, BodySourceStatus> StatusChanged;

        public void StartSource()
        {
            if (status == BodySourceStatus.Running || status == BodySourceStatus.Starting) return;

            SetStatus(BodySourceStatus.Starting, "Iniciando...");
            try
            {
                // A subclasse deve chamar SetStatus(Running) quando estiver pronta
                // (pode ser aqui mesmo ou depois, de forma assíncrona).
                OnStartSource();
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
                SetStatus(BodySourceStatus.Error, e.Message);
            }
        }

        public void StopSource()
        {
            if (status == BodySourceStatus.NotStarted || status == BodySourceStatus.Stopped) return;

            try
            {
                OnStopSource();
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
            }

            BeginFrame(); // devolve os corpos ao pool: ninguém deve ler dados de uma fonte parada
            SetStatus(BodySourceStatus.Stopped, string.Empty);
        }

        // ------------------------------------------------------------------ Ciclo de vida Unity

        protected virtual void OnEnable()
        {
            if (startOnEnable) StartSource();
        }

        protected virtual void OnDisable()
        {
            // Importante no Editor: sem isso o Kinect continua aberto depois de sair do Play Mode.
            StopSource();
        }

        // ------------------------------------------------------------------ Para as subclasses

        /// <summary>Abre o sensor/arquivo. Chamar <see cref="SetStatus"/> com Running quando pronto.</summary>
        protected abstract void OnStartSource();

        /// <summary>Fecha o sensor/arquivo e libera recursos nativos.</summary>
        protected abstract void OnStopSource();

        /// <summary>Altera o status e avisa os ouvintes (só dispara se algo mudou).</summary>
        protected void SetStatus(BodySourceStatus newStatus, string message)
        {
            message ??= string.Empty;
            if (status == newStatus && statusMessage == message) return;

            status = newStatus;
            statusMessage = message;
            SafeInvoke(StatusChanged, this, newStatus);
        }

        /// <summary>Começa um frame novo: devolve os corpos do frame anterior ao pool.</summary>
        protected void BeginFrame()
        {
            for (int i = 0; i < bodies.Count; i++) pool.Push(bodies[i]);
            bodies.Clear();
        }

        /// <summary>Pega um <see cref="BodyFrame"/> limpo do pool e o adiciona ao frame atual.
        /// Só chame para corpos que serão publicados (rastreados).</summary>
        protected BodyFrame AcquireBody()
        {
            BodyFrame body = pool.Count > 0 ? pool.Pop() : new BodyFrame();
            body.Reset();
            bodies.Add(body);
            return body;
        }

        /// <summary>Publica o frame montado desde o último <see cref="BeginFrame"/>.</summary>
        protected void PublishFrame(double frameTimestamp)
        {
            timestamp = frameTimestamp;
            frameNumber++;
            SafeInvoke(FrameArrived, this);
        }

        // Um ouvinte com erro não pode derrubar a fonte nem os outros ouvintes.
        private void SafeInvoke(Action<IBodySource> handler, IBodySource arg)
        {
            if (handler == null) return;
            foreach (Delegate d in handler.GetInvocationList())
            {
                try { ((Action<IBodySource>)d).Invoke(arg); }
                catch (Exception e) { Debug.LogException(e, this); }
            }
        }

        private void SafeInvoke(Action<IBodySource, BodySourceStatus> handler, IBodySource arg, BodySourceStatus st)
        {
            if (handler == null) return;
            foreach (Delegate d in handler.GetInvocationList())
            {
                try { ((Action<IBodySource, BodySourceStatus>)d).Invoke(arg, st); }
                catch (Exception e) { Debug.LogException(e, this); }
            }
        }
    }
}
