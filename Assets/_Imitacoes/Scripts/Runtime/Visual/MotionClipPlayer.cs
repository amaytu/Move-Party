using System;
using Imitacoes.Body;
using Imitacoes.Motion;
using UnityEngine;

namespace Imitacoes.Visual
{
    /// <summary>
    /// Reproduz um <see cref="MotionClip"/> num <see cref="BodyView"/> (2D ou 3D): o replay do Mestre
    /// e o "fantasma" durante a imitação. Pode tocar sozinho (<see cref="Play"/>) ou ser guiado
    /// de fora (<see cref="ShowTime"/>), por exemplo pelo cronômetro da gravação.
    /// </summary>
    [AddComponentMenu("Imitações/Motion Clip Player")]
    public sealed class MotionClipPlayer : MonoBehaviour
    {
        [SerializeField] private BodyView view;

        private readonly BodyFrame sample = new BodyFrame();
        private MotionClip clip;
        private bool loop;

        public bool IsPlaying { get; private set; }
        public float Time { get; private set; }
        public float Duration => clip != null ? clip.Duration : 0f;
        public BodyView View => view;

        public void SetView(BodyView bodyView) => view = bodyView;

        /// <summary>Disparado quando um clipe sem loop chega ao fim.</summary>
        public event Action Finished;

        public void Play(MotionClip motionClip, bool loopPlayback = false, float startTime = 0f)
        {
            clip = motionClip;
            loop = loopPlayback;
            Time = startTime;
            IsPlaying = clip != null && !clip.IsEmpty;
            if (view != null) view.ResetReference();
            Render();
        }

        /// <summary>Mostra o clipe parado no instante <paramref name="time"/> (s).</summary>
        public void ShowTime(MotionClip motionClip, float time)
        {
            if (!ReferenceEquals(clip, motionClip) && view != null) view.ResetReference();
            clip = motionClip;
            IsPlaying = false;
            Time = time;
            Render();
        }

        public void Stop()
        {
            IsPlaying = false;
            clip = null;
            if (view != null) view.Clear();
        }

        private void Update()
        {
            if (!IsPlaying) return;

            Time += UnityEngine.Time.deltaTime;
            if (Time >= Duration)
            {
                if (loop)
                {
                    Time %= Mathf.Max(0.001f, Duration);
                }
                else
                {
                    Time = Duration;
                    IsPlaying = false;
                    Render();
                    Finished?.Invoke();
                    return;
                }
            }

            Render();
        }

        private void Render()
        {
            if (view == null) return;
            if (clip == null || !clip.Sample(Time, sample))
            {
                view.Clear();
                return;
            }
            view.Render(sample);
        }
    }
}
