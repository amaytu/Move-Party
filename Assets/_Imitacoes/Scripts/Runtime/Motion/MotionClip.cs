using System;
using System.Collections.Generic;
using System.IO;
using Imitacoes.Body;
using UnityEngine;

namespace Imitacoes.Motion
{
    /// <summary>
    /// Sequência de frames gravada a uma taxa FIXA (ex.: 30 fps) — o movimento do Mestre
    /// ou de um imitador. É o que o <c>MotionComparer</c> compara, o que o replay reproduz
    /// e o que o <c>MockBodySource</c> lê de arquivo.
    ///
    /// Taxa fixa significa: <c>Frames[i]</c> corresponde ao instante <c>i / FrameRate</c>.
    /// O <c>MotionRecorder</c> faz a reamostragem; assim o DTW compara frames equivalentes
    /// mesmo que o sensor tenha oscilado entre 25 e 30 fps.
    ///
    /// Serializado com JsonUtility (campos públicos de propósito).
    /// </summary>
    [Serializable]
    public sealed class MotionClip
    {
        /// <summary>Versão do formato do arquivo. Incrementar se a estrutura mudar.</summary>
        public const int CurrentFormatVersion = 1;

        public int FormatVersion = CurrentFormatVersion;

        /// <summary>Frames por segundo da gravação.</summary>
        public float FrameRate = 30f;

        /// <summary>Número do jogador que gravou (1..4). 0 = desconhecido (ex.: gravação de teste).</summary>
        public int PlayerNumber;

        /// <summary>Id do tema imitado (ver ThemeCatalog). Vazio se não se aplica.</summary>
        public string ThemeId = string.Empty;

        /// <summary>Fonte que gerou os dados (ex.: "Kinect v2").</summary>
        public string SourceName = string.Empty;

        /// <summary>Data/hora da gravação em ISO 8601 UTC (string porque o JsonUtility não serializa DateTime).</summary>
        public string RecordedAtUtc = string.Empty;

        /// <summary>
        /// Segundos em que o jogador ficou fora do campo de visão durante a gravação.
        /// Usado pela regra de penalidade configurável.
        /// </summary>
        public float OutOfViewSeconds;

        /// <summary>Frames na ordem, com Timestamp relativo ao início (0, 1/fps, 2/fps...).</summary>
        public List<BodyFrame> Frames = new List<BodyFrame>();

        public int FrameCount => Frames?.Count ?? 0;

        public bool IsEmpty => FrameCount == 0;

        /// <summary>Duração em segundos (150 frames a 30 fps = 5 s).</summary>
        public float Duration => FrameRate > 0f ? FrameCount / FrameRate : 0f;

        // ------------------------------------------------------------------ Construção

        public MotionClip() { }

        public MotionClip(float frameRate, int playerNumber = 0, string themeId = "", string sourceName = "")
        {
            if (frameRate <= 0f) throw new ArgumentOutOfRangeException(nameof(frameRate), "A taxa deve ser > 0.");

            FrameRate = frameRate;
            PlayerNumber = playerNumber;
            ThemeId = themeId ?? string.Empty;
            SourceName = sourceName ?? string.Empty;
            RecordedAtUtc = DateTime.UtcNow.ToString("o");
        }

        /// <summary>Adiciona uma CÓPIA do frame (o original pertence à fonte e será reaproveitado).
        /// O Timestamp é reescrito para o instante do índice na taxa fixa.</summary>
        public void AddFrameCopy(BodyFrame frame)
        {
            if (frame == null) throw new ArgumentNullException(nameof(frame));

            BodyFrame copy = frame.Clone();
            copy.Timestamp = FrameCount / (double)FrameRate;
            Frames.Add(copy);
        }

        public void Clear()
        {
            Frames.Clear();
            OutOfViewSeconds = 0f;
        }

        // ------------------------------------------------------------------ Leitura

        /// <summary>
        /// Amostra o clipe no instante <paramref name="time"/> (s), interpolando entre os dois
        /// frames vizinhos. Usado no replay e no "fantasma". Não aloca.
        /// </summary>
        /// <returns>False se o clipe está vazio (o <paramref name="result"/> é resetado).</returns>
        public bool Sample(float time, BodyFrame result)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));

            if (IsEmpty)
            {
                result.Reset();
                return false;
            }

            float position = Mathf.Clamp(time * FrameRate, 0f, FrameCount - 1);
            int index0 = Mathf.FloorToInt(position);
            int index1 = Mathf.Min(index0 + 1, FrameCount - 1);

            BodyFrame.Lerp(Frames[index0], Frames[index1], position - index0, result);
            return true;
        }

        // ------------------------------------------------------------------ Persistência (JSON)

        public string ToJson(bool prettyPrint = false) => JsonUtility.ToJson(this, prettyPrint);

        /// <summary>Lê e valida um clipe. Lança <see cref="FormatException"/> se o conteúdo for inválido.</summary>
        public static MotionClip FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new FormatException("JSON do clipe está vazio.");

            MotionClip clip;
            try
            {
                clip = JsonUtility.FromJson<MotionClip>(json);
            }
            catch (ArgumentException e)
            {
                throw new FormatException("JSON do clipe está malformado: " + e.Message, e);
            }

            if (clip == null) throw new FormatException("JSON do clipe não contém dados.");
            if (clip.FormatVersion > CurrentFormatVersion)
                throw new FormatException($"Clipe na versão {clip.FormatVersion}; este jogo lê até a {CurrentFormatVersion}.");
            if (clip.FrameRate <= 0f)
                throw new FormatException("FrameRate inválido no clipe.");

            clip.Frames ??= new List<BodyFrame>();
            clip.Frames.RemoveAll(f => f == null);
            foreach (BodyFrame frame in clip.Frames) frame.EnsureJointArray();

            return clip;
        }

        /// <summary>Salva em arquivo (cria a pasta se necessário).</summary>
        public void SaveToFile(string path, bool prettyPrint = false)
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            File.WriteAllText(path, ToJson(prettyPrint));
        }

        public static MotionClip LoadFromFile(string path) => FromJson(File.ReadAllText(path));

        public override string ToString()
            => $"MotionClip(jogador {PlayerNumber}, tema '{ThemeId}', {FrameCount} frames, {Duration:F2}s @ {FrameRate}fps)";
    }
}
