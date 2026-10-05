using System;
using System.IO;
using Imitacoes.Body;
using Imitacoes.Config;
using Imitacoes.Sources;
using UnityEngine;

namespace Imitacoes.Motion
{
    /// <summary>Parâmetros de uma gravação.</summary>
    public struct RecordingOptions
    {
        public float DurationSeconds;
        public int FrameRate;
        public int PlayerNumber;
        public string ThemeId;
        public OutOfViewPolicy OutOfViewPolicy;
        public float OutOfViewGraceSeconds;
        public float MinTrackedRatio;

        public static RecordingOptions FromSettings(GameSettings s, int playerNumber, string themeId) => new RecordingOptions
        {
            DurationSeconds = s.RecordingSeconds,
            FrameRate = s.RecordingFrameRate,
            PlayerNumber = playerNumber,
            ThemeId = themeId,
            OutOfViewPolicy = s.OutOfViewPolicy,
            OutOfViewGraceSeconds = s.OutOfViewGraceSeconds,
            MinTrackedRatio = s.MinTrackedRatio
        };
    }

    /// <summary>
    /// Grava o jogador ativo a uma taxa FIXA (ex.: 30 fps) num <see cref="MotionClip"/>.
    /// A amostragem é por tempo, então o clipe tem sempre o mesmo número de frames
    /// (duração × fps), independente do FPS da Unity ou do sensor.
    ///
    /// Jogador fora do campo de visão (sem corpo ou poucos joints rastreados):
    /// - Ignore: continua gravando (frames vazios).
    /// - PauseRecording: o cronômetro para até ele voltar.
    /// - Penalize: continua gravando e soma os segundos em <see cref="MotionClip.OutOfViewSeconds"/>.
    /// </summary>
    [AddComponentMenu("Imitações/Motion Recorder")]
    public sealed class MotionRecorder : MonoBehaviour
    {
        [SerializeField] private ActiveBodySelector bodyProvider;

        [Header("Arquivos")]
        [SerializeField, Tooltip("Salva cada gravação em .json (para testar com o MockBodySource depois).")]
        private bool saveRecordingsToDisk;

        public bool IsRecording { get; private set; }
        public bool IsPlayerOutOfView { get; private set; }
        public MotionClip CurrentClip { get; private set; }

        /// <summary>Segundos já gravados (não conta o tempo pausado).</summary>
        public float Elapsed => CurrentClip != null ? CurrentClip.FrameCount / (float)options.FrameRate : 0f;
        public float Duration => options.DurationSeconds;
        public float Remaining => Mathf.Max(0f, Duration - Elapsed);
        public float Progress01 => Duration > 0f ? Mathf.Clamp01(Elapsed / Duration) : 0f;
        public bool IsPaused => IsRecording && IsPlayerOutOfView && options.OutOfViewPolicy == OutOfViewPolicy.PauseRecording;

        public bool SaveRecordingsToDisk
        {
            get => saveRecordingsToDisk;
            set => saveRecordingsToDisk = value;
        }

        /// <summary>Pasta das gravações: StreamingAssets/Recordings no Editor; persistentDataPath no build.</summary>
        public static string RecordingsFolder => Application.isEditor
            ? Path.Combine(Application.streamingAssetsPath, "Recordings")
            : Path.Combine(Application.persistentDataPath, "Recordings");

        public event Action<bool> OutOfViewChanged;
        public event Action<MotionClip> RecordingFinished;

        private RecordingOptions options;
        private int targetFrames;
        private float sampleTimer;
        private float badTrackingTimer;
        private readonly BodyFrame emptyFrame = new BodyFrame();

        // ------------------------------------------------------------------ API

        public void StartRecording(RecordingOptions recordingOptions)
        {
            if (recordingOptions.FrameRate <= 0) throw new ArgumentException("FrameRate deve ser > 0.");
            if (recordingOptions.DurationSeconds <= 0f) throw new ArgumentException("DurationSeconds deve ser > 0.");

            options = recordingOptions;
            targetFrames = Mathf.Max(1, Mathf.RoundToInt(options.DurationSeconds * options.FrameRate));
            CurrentClip = new MotionClip(options.FrameRate, options.PlayerNumber, options.ThemeId,
                bodyProvider != null && bodyProvider.CurrentSource != null ? bodyProvider.CurrentSource.SourceName : string.Empty);

            sampleTimer = 0f;
            badTrackingTimer = 0f;
            SetOutOfView(false);
            IsRecording = true;

            AddSample(); // frame 0 no instante exato do início
        }

        /// <summary>Interrompe sem disparar <see cref="RecordingFinished"/>.</summary>
        public void Cancel()
        {
            IsRecording = false;
            SetOutOfView(false);
        }

        // ------------------------------------------------------------------ Loop

        private void Update()
        {
            if (!IsRecording) return;

            float dt = Time.deltaTime;
            UpdateOutOfView(dt);

            if (IsPlayerOutOfView)
            {
                if (options.OutOfViewPolicy == OutOfViewPolicy.PauseRecording) return; // cronômetro parado
                if (options.OutOfViewPolicy == OutOfViewPolicy.Penalize) CurrentClip.OutOfViewSeconds += dt;
            }

            float step = 1f / options.FrameRate;
            sampleTimer += dt;
            while (sampleTimer >= step && CurrentClip.FrameCount < targetFrames)
            {
                sampleTimer -= step;
                AddSample();
            }

            if (CurrentClip.FrameCount >= targetFrames) Finish();
        }

        private void AddSample()
        {
            BodyFrame body = bodyProvider != null ? bodyProvider.ActiveBody : null;
            CurrentClip.AddFrameCopy(body != null ? body : emptyFrame);
        }

        private void UpdateOutOfView(float dt)
        {
            BodyFrame body = bodyProvider != null ? bodyProvider.ActiveBody : null;
            bool bad = body == null
                       || bodyProvider.IsActiveBodyMissing
                       || body.GetTrackedRatio(JointTrackingState.Inferred) < options.MinTrackedRatio;

            badTrackingTimer = bad ? badTrackingTimer + dt : 0f;
            SetOutOfView(badTrackingTimer > options.OutOfViewGraceSeconds);
        }

        private void SetOutOfView(bool value)
        {
            if (IsPlayerOutOfView == value) return;
            IsPlayerOutOfView = value;
            OutOfViewChanged?.Invoke(value);
        }

        private void Finish()
        {
            IsRecording = false;
            SetOutOfView(false);

            if (saveRecordingsToDisk) TrySave(CurrentClip);
            RecordingFinished?.Invoke(CurrentClip);
        }

        private void TrySave(MotionClip clip)
        {
            try
            {
                string theme = string.IsNullOrEmpty(clip.ThemeId) ? "livre" : clip.ThemeId;
                string file = $"{DateTime.Now:yyyyMMdd_HHmmss}_J{clip.PlayerNumber}_{theme}.json";
                string path = Path.Combine(RecordingsFolder, file);
                clip.SaveToFile(path);
                Debug.Log($"[Imitações] Gravação salva: {path}", this);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Imitações] Não consegui salvar a gravação: " + e.Message, this);
            }
        }
    }
}
