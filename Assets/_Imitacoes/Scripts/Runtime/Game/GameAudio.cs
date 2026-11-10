using System;
using Imitacoes.Motion;
using UnityEngine;

namespace Imitacoes.Game
{
    /// <summary>
    /// Música e efeitos sonoros do jogo.
    ///
    /// Músicas (dois AudioSource em loop, com crossfade):
    /// - Menu: telas de menu, sorteio, posicionamento, replay e placares.
    /// - Imitação: contagem e gravação. Durante a gravação ela ACELERA (pitch sobe) conforme o
    ///   tempo acaba; nos últimos segundos toca um tique de urgência; no fim, para com um
    ///   "arranhão" e toca o alarme de tempo esgotado.
    ///
    /// Os clipes são carregados de <c>Resources/Audio</c> pelo nome (veja <see cref="ClipNames"/>),
    /// então nada precisa ser ligado na cena. Um clipe arrastado no Inspector tem prioridade.
    /// Todos os áudios incluídos são CC0 (créditos em Assets/_Imitacoes/Resources/Audio/CREDITOS.md).
    /// </summary>
    [AddComponentMenu("Imitações/Game Audio")]
    public sealed class GameAudio : MonoBehaviour
    {
        /// <summary>Nome de cada som em Resources/Audio (sem extensão).</summary>
        private static readonly string[] ClipNames =
        {
            "SFX_Roleta",        // Tick
            "SFX_Revelacao",     // Reveal
            "SFX_Contagem",      // Countdown
            "SFX_Gravando",      // RecordStart
            "SFX_FimGravacao",   // RecordEnd
            "SFX_Nota",          // Score
            "SFX_Vitoria",       // Victory
            "SFX_TempoEsgotado", // TimeUp
            "SFX_Urgencia",      // Urgency
            "SFX_ContadorNota",  // ScoreCount
            "SFX_NotaOtima",     // ScoreGreat
            "SFX_NotaBoa",       // ScoreGood
            "SFX_NotaRuim",      // ScoreBad
            "SFX_Selecionar",    // Select
            "SFX_Comecar",       // Start
            "SFX_Arranhao",      // RecordScratch
            "SFX_BotaoHover",    // ButtonHover
            "Plateia_Aplausos",  // CrowdApplause
            "Plateia_Vibracao",  // CrowdCheer
            "Plateia_Vaia",      // CrowdBoo
            "Plateia_Risada1",   // CrowdLaugh (sorteia entre Risada1..3)
            "Plateia_Grilos",    // CrowdCrickets
            "SFX_Confete",       // Confetti
            "SFX_Coroa",         // Crown
            "SFX_RegraSurpresa", // SurpriseRule
        };

        private AudioClip[] laughs = Array.Empty<AudioClip>();
        private static readonly System.Random Rng = new System.Random();

        [Header("Músicas (vazio = carrega de Resources/Audio)")]
        [SerializeField] private AudioClip menuMusic;
        [SerializeField] private AudioClip imitationMusic;

        [Header("Volumes")]
        [SerializeField, Range(0f, 1f)] private float musicVolume = 0.45f;
        [SerializeField, Range(0f, 1f), Tooltip("Volume da música de menu durante o replay do Mestre.")]
        private float replayMusicVolume = 0.2f;
        [SerializeField, Range(0f, 1f)] private float sfxVolume = 0.9f;
        [SerializeField, Min(0.05f)] private float crossfadeSeconds = 0.6f;

        [Header("Aceleração da música na gravação")]
        [SerializeField, Range(1f, 2f), Tooltip("Velocidade da música quando o tempo acaba (1 = normal).")]
        private float finalPitch = 1.5f;
        [SerializeField, Range(0.5f, 4f), Tooltip("Curva da aceleração: >1 acelera mais perto do fim.")]
        private float accelerationCurve = 1.6f;
        [SerializeField, Min(0f), Tooltip("Segundos finais com tique de urgência.")]
        private float urgencySeconds = 3f;

        private AudioSource menuSource;
        private AudioSource imitationSource;
        private AudioSource sfxSource;
        private readonly AudioClip[] sfx = new AudioClip[Enum.GetValues(typeof(GameSound)).Length];

        private GameManager game;
        private float menuTarget;
        private float imitationTarget;
        private bool imitationStopped;
        private int lastUrgencySecond = -1;

        // ------------------------------------------------------------------ API

        public void Bind(GameManager manager) => game = manager;

        public void Play(GameSound sound, float volumeScale = 1f, float pitch = 1f)
        {
            AudioClip clip = sfx[(int)sound];
            if (sound == GameSound.CrowdLaugh && laughs.Length > 0) clip = laughs[Rng.Next(laughs.Length)];
            if (clip == null || sfxSource == null) return;

            if (Mathf.Approximately(pitch, 1f))
            {
                sfxSource.PlayOneShot(clip, sfxVolume * volumeScale);
                return;
            }

            // PlayOneShot ignora pitch por som: usa uma fonte temporária.
            var temp = gameObject.AddComponent<AudioSource>();
            temp.playOnAwake = false;
            temp.clip = clip;
            temp.pitch = pitch;
            temp.volume = sfxVolume * volumeScale;
            temp.Play();
            Destroy(temp, clip.length / Mathf.Max(0.1f, pitch) + 0.1f);
        }

        /// <summary>Fim da gravação: a música da imitação para com um arranhão e toca o alarme.</summary>
        public void TimeUp()
        {
            if (imitationSource != null && imitationSource.isPlaying)
            {
                imitationSource.Stop();
                Play(GameSound.RecordScratch, 0.7f);
            }
            imitationStopped = true;
            imitationTarget = 0f;
            Play(GameSound.TimeUp);
        }

        /// <summary>Escolhe a música da tela.</summary>
        public void OnStateChanged(GameState state)
        {
            switch (state)
            {
                case GameState.ContagemMestre:
                case GameState.ContagemImitador:
                    StartImitationMusic();
                    break;

                case GameState.GravandoMestre:
                case GameState.GravandoImitador:
                    if (imitationStopped || !imitationSource.isPlaying) StartImitationMusic();
                    break;

                case GameState.Replay:
                    SetMenuMusic(replayMusicVolume);
                    break;

                default:
                    SetMenuMusic(musicVolume);
                    break;
            }
        }

        // ------------------------------------------------------------------ Ciclo de vida

        private void Awake()
        {
            menuSource = CreateSource("Música Menu", loop: true);
            imitationSource = CreateSource("Música Imitação", loop: true);
            sfxSource = CreateSource("Efeitos", loop: false);

            if (menuMusic == null) menuMusic = Resources.Load<AudioClip>("Audio/Musica_Menu");
            if (imitationMusic == null) imitationMusic = Resources.Load<AudioClip>("Audio/Musica_Imitacao");
            menuSource.clip = menuMusic;
            imitationSource.clip = imitationMusic;

            for (int i = 0; i < sfx.Length && i < ClipNames.Length; i++)
                sfx[i] = Resources.Load<AudioClip>("Audio/" + ClipNames[i]);

            var laughList = new System.Collections.Generic.List<AudioClip>();
            for (int i = 1; i <= 3; i++)
            {
                AudioClip c = Resources.Load<AudioClip>($"Audio/Plateia_Risada{i}");
                if (c != null) laughList.Add(c);
            }
            laughs = laughList.ToArray();
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            float step = dt / crossfadeSeconds;
            Fade(menuSource, menuTarget, step * musicVolume);
            Fade(imitationSource, imitationTarget, step * musicVolume);

            UpdateRecordingTempo();
        }

        /// <summary>Durante a gravação: acelera a música e toca o tique nos segundos finais.</summary>
        private void UpdateRecordingTempo()
        {
            if (game == null || imitationSource == null) return;
            GameState state = game.CurrentState;
            bool recordingState = state == GameState.GravandoMestre || state == GameState.GravandoImitador;
            MotionRecorder recorder = game.Recorder;

            if (!recordingState || recorder == null || !recorder.IsRecording)
            {
                if (state == GameState.ContagemMestre || state == GameState.ContagemImitador) imitationSource.pitch = 1f;
                lastUrgencySecond = -1;
                return;
            }

            if (recorder.IsPaused) return; // jogador fora do quadro: segura o ritmo

            float k = Mathf.Pow(recorder.Progress01, accelerationCurve);
            imitationSource.pitch = Mathf.Lerp(1f, finalPitch, k);

            int second = Mathf.CeilToInt(recorder.Remaining);
            if (recorder.Remaining <= urgencySeconds && second > 0 && second != lastUrgencySecond)
            {
                lastUrgencySecond = second;
                // Tique cada vez mais agudo: 3… 2… 1…
                Play(GameSound.Urgency, 0.9f, 1f + 0.15f * (urgencySeconds - second));
            }
        }

        // ------------------------------------------------------------------ Músicas

        private void SetMenuMusic(float volume)
        {
            menuTarget = volume;
            imitationTarget = 0f;
            if (menuSource.clip != null && !menuSource.isPlaying) menuSource.Play();
        }

        private void StartImitationMusic()
        {
            menuTarget = 0f;
            imitationTarget = musicVolume;
            imitationStopped = false;
            if (imitationSource.clip == null) return;
            imitationSource.pitch = 1f;
            imitationSource.time = 0f;
            imitationSource.Play();
        }

        private static void Fade(AudioSource source, float target, float maxDelta)
        {
            if (source == null) return;
            source.volume = Mathf.MoveTowards(source.volume, target, Mathf.Max(maxDelta, 0.001f));
            if (target <= 0f && source.volume <= 0f && source.isPlaying && source.loop && source.clip != null)
            {
                // Música de menu só pausa (volta de onde parou); a da imitação para.
                if (source.name == "Música Menu") source.Pause();
                else source.Stop();
            }
            else if (target > 0f && !source.isPlaying && source.clip != null)
            {
                if (source.time > 0f) source.UnPause();
                else source.Play();
            }
        }

        private AudioSource CreateSource(string sourceName, bool loop)
        {
            var go = new GameObject(sourceName);
            go.transform.SetParent(transform, false);
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.volume = 0f;
            source.spatialBlend = 0f;
            if (!loop) source.volume = 1f;
            return source;
        }
    }
}
