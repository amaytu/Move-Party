using Imitacoes.Body;
using Imitacoes.Motion;
using Imitacoes.Scoring;
using NUnit.Framework;
using UnityEngine;

namespace Imitacoes.Tests
{
    /// <summary>
    /// Testes do algoritmo de similaridade com movimentos sintéticos (sem Kinect).
    /// Rode em Window ▸ General ▸ Test Runner ▸ EditMode ▸ Run All.
    /// </summary>
    public sealed class MotionComparerTests
    {
        private const float Fps = 30f;
        private const float Duration = 5f;

        private GameObject go;
        private MotionComparer comparer;

        [SetUp]
        public void SetUp()
        {
            go = new GameObject("Comparer (teste)");
            comparer = go.AddComponent<MotionComparer>();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(go);

        /// <summary>Compara e escreve o resultado no log do teste (útil para calibrar a tabela de notas).</summary>
        private ComparisonResult Cmp(MotionClip master, MotionClip attempt,
            [System.Runtime.CompilerServices.CallerMemberName] string caller = "")
        {
            ComparisonResult r = comparer.Compare(master, attempt);
            TestContext.WriteLine($"{caller}: {r}");
            Debug.Log($"[Calibração] {caller}: {r}");
            return r;
        }

        // ------------------------------------------------------------------ Casos

        [Test]
        public void TabelaDeNota_Calibrada()
        {
            // Imitação muito boa beira 100; boa fica perto de 80; ruim cai rápido.
            Assert.AreEqual(100f, comparer.DistanceToScore(0f));
            Assert.GreaterOrEqual(comparer.DistanceToScore(0.04f), 95f);
            Assert.AreEqual(78f, comparer.DistanceToScore(0.10f));
            Assert.LessOrEqual(comparer.DistanceToScore(0.16f), 40f);
            Assert.AreEqual(0f, comparer.DistanceToScore(0.32f));
            Assert.AreEqual(0f, comparer.DistanceToScore(1f));
        }

        [Test]
        public void MovimentoIdentico_NotaMaxima()
        {
            MotionClip master = Clip(t => Wave(t));
            float score = Cmp(master, Clip(t => Wave(t))).Score;
            Assert.GreaterOrEqual(score, 95f);
        }

        [Test]
        public void MesmoMovimentoComAtraso_NotaAlta()
        {
            MotionClip master = Clip(t => Wave(t));
            MotionClip late = Clip(t => Wave(t - 0.5f)); // começou meio segundo depois
            float score = Cmp(master, late).Score;
            // Curva exigente: atraso de 0,5 s ainda perde pontos, mas o DTW evita que a nota despenque.
            Assert.GreaterOrEqual(score, 55f, "O DTW deveria tolerar o atraso.");
        }

        [Test]
        public void JogadorMaisAltoEmOutraPosicao_NotaMaxima()
        {
            MotionClip master = Clip(t => Wave(t));
            MotionClip other = Clip(t => Wave(t), scale: 1.2f, offset: new Vector3(0.6f, 0.1f, -0.5f));
            float score = Cmp(master, other).Score;
            Assert.GreaterOrEqual(score, 95f, "A normalização deveria ignorar altura e posição.");
        }

        [Test]
        public void JogadorParadoImitandoMovimento_NotaBaixa()
        {
            MotionClip master = Clip(t => Wave(t));
            MotionClip idle = Clip(t => (0.15f, 0.15f));
            float score = Cmp(master, idle).Score;
            Assert.LessOrEqual(score, 40f);
        }

        [Test]
        public void MovimentoOposto_NotaMenorQueParecido()
        {
            MotionClip master = Clip(t => (2.8f, 2.8f));      // braços para cima
            MotionClip similar = Clip(t => (2.5f, 2.5f));     // quase igual
            MotionClip opposite = Clip(t => (0.1f, 0.1f));    // braços para baixo
            float good = Cmp(master, similar).Score;
            float bad = Cmp(master, opposite).Score;
            Assert.Greater(good, bad + 30f);
        }

        [Test]
        public void ImitacaoEspelhada_Aceita()
        {
            MotionClip master = Clip(t => (0.15f, 2.8f));   // só o braço direito levantado
            MotionClip mirror = Clip(t => (2.8f, 0.15f));   // só o esquerdo (como num espelho)
            ComparisonResult result = Cmp(master, mirror);
            Assert.GreaterOrEqual(result.Score, 90f);
            Assert.IsTrue(result.Mirrored);
        }

        [Test]
        public void GiroDoCorpo_ContaNaNota()
        {
            // Mestre gira o corpo de 0° a 70° e volta; braços parados.
            MotionClip master = Clip(t => (0.3f, 0.3f), yaw: t => 35f - 35f * Mathf.Cos(t * Mathf.PI * 0.8f));
            float same = Cmp(master, Clip(t => (0.3f, 0.3f), yaw: t => 35f - 35f * Mathf.Cos(t * Mathf.PI * 0.8f))).Score;
            float noTurn = Cmp(master, Clip(t => (0.3f, 0.3f))).Score;
            Assert.GreaterOrEqual(same, 95f);
            Assert.LessOrEqual(noTurn, 92f, "Quem não girou deveria perder pontos.");
            Assert.Greater(same, noTurn + 5f);
        }

        [Test]
        public void ImitadorFora_DoQuadro_NotaZero()
        {
            MotionClip master = Clip(t => Wave(t));
            var empty = new MotionClip(Fps);
            for (int i = 0; i < Fps * Duration; i++) empty.AddFrameCopy(new BodyFrame());
            Assert.AreEqual(0f, Cmp(master, empty).Score);
        }

        // ------------------------------------------------------------------ Gerador de movimento

        /// <summary>Acenar com os dois braços (abdução oscilando entre 0,2 e 2,8 rad).</summary>
        private static (float left, float right) Wave(float t)
        {
            float a = 1.5f + 1.3f * Mathf.Sin(t * Mathf.PI * 2f * 0.8f);
            return (a, a);
        }

        private static MotionClip Clip(System.Func<float, (float left, float right)> arms,
            float scale = 1f, Vector3 offset = default, System.Func<float, float> yaw = null)
        {
            var clip = new MotionClip(Fps);
            int frames = Mathf.RoundToInt(Fps * Duration);
            for (int i = 0; i < frames; i++)
            {
                float t = i / Fps;
                (float left, float right) = arms(t);
                BodyFrame pose = Pose(left, right, scale, offset + new Vector3(0f, 0f, -2.2f));
                if (yaw != null) Rotate(pose, yaw(t));
                clip.AddFrameCopy(pose);
            }
            return clip;
        }

        /// <summary>Gira o corpo inteiro em torno do eixo vertical que passa pela base da coluna.</summary>
        private static void Rotate(BodyFrame body, float degrees)
        {
            Vector3 pivot = body.Root;
            Quaternion q = Quaternion.Euler(0f, degrees, 0f);
            for (int i = 0; i < BodyFrame.JointCount; i++)
            {
                JointData j = body.Joints[i];
                j.Position = pivot + q * (j.Position - pivot);
                body.Joints[i] = j;
            }
        }

        /// <summary>Corpo em pé; só os braços mudam (ângulo no plano frontal: 0 = caído, π = para cima).</summary>
        private static BodyFrame Pose(float leftAbduction, float rightAbduction, float s, Vector3 origin)
        {
            var body = new BodyFrame { IsTracked = true, TrackingId = 1 };
            void Set(JointId id, Vector3 p) => body.SetJoint(id, origin + p * s, Quaternion.identity, JointTrackingState.Tracked);

            Vector3 root = new Vector3(0f, 0.95f, 0f);
            Vector3 spineShoulder = root + new Vector3(0f, 0.5f, 0f);
            Set(JointId.SpineBase, root);
            Set(JointId.SpineMid, root + new Vector3(0f, 0.25f, 0f));
            Set(JointId.SpineShoulder, spineShoulder);
            Set(JointId.Neck, spineShoulder + new Vector3(0f, 0.07f, 0f));
            Set(JointId.Head, spineShoulder + new Vector3(0f, 0.2f, 0f));

            foreach (float side in new[] { -1f, 1f })
            {
                bool left = side < 0f;
                float a = left ? leftAbduction : rightAbduction;
                Vector3 dir = new Vector3(side * Mathf.Sin(a), -Mathf.Cos(a), 0f);
                Vector3 shoulder = spineShoulder + new Vector3(side * 0.18f, 0f, 0f);
                Vector3 elbow = shoulder + dir * 0.28f;
                Vector3 wrist = elbow + dir * 0.25f;
                Vector3 hand = wrist + dir * 0.08f;
                Set(left ? JointId.ShoulderLeft : JointId.ShoulderRight, shoulder);
                Set(left ? JointId.ElbowLeft : JointId.ElbowRight, elbow);
                Set(left ? JointId.WristLeft : JointId.WristRight, wrist);
                Set(left ? JointId.HandLeft : JointId.HandRight, hand);
                Set(left ? JointId.HandTipLeft : JointId.HandTipRight, hand + dir * 0.07f);
                Set(left ? JointId.ThumbLeft : JointId.ThumbRight, wrist + dir * 0.04f);

                Vector3 hip = root + new Vector3(side * 0.09f, -0.05f, 0f);
                Vector3 knee = hip + Vector3.down * 0.43f;
                Vector3 ankle = knee + Vector3.down * 0.42f;
                Set(left ? JointId.HipLeft : JointId.HipRight, hip);
                Set(left ? JointId.KneeLeft : JointId.KneeRight, knee);
                Set(left ? JointId.AnkleLeft : JointId.AnkleRight, ankle);
                Set(left ? JointId.FootLeft : JointId.FootRight, ankle + new Vector3(0f, -0.05f, 0.12f));
            }

            return body;
        }
    }
}
