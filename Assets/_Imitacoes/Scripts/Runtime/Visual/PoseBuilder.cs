using Imitacoes.Body;
using UnityEngine;

namespace Imitacoes.Visual
{
    /// <summary>
    /// Monta um corpo sintético em pé (mesma geometria do sensor simulado) a partir de ângulos simples.
    /// Usado para animar os bonecos do pódio, que não são de ninguém na frente do sensor.
    /// </summary>
    public static class PoseBuilder
    {
        private const float HipHeight = 0.95f;
        private const float UpperArm = 0.28f;
        private const float Forearm = 0.25f;
        private const float HandLength = 0.08f;
        private const float Thigh = 0.43f;
        private const float Shin = 0.42f;
        private static readonly Vector3 Feet = new Vector3(0f, -0.9f, -2.2f);

        /// <summary>Uma dança diferente por posição no pódio (0 = campeão), no instante t.</summary>
        public static void Dance(BodyFrame body, int place, float t)
        {
            switch (place)
            {
                case 0: // campeão: pula socando o ar
                    float pump = Mathf.Abs(Mathf.Sin(t * 5.5f));
                    Fill(body, 2.5f + 0.4f * pump, 0.3f * pump, 2.5f + 0.4f * (1f - pump), 0.3f * (1f - pump),
                        0.16f, 0.09f * pump, 0f);
                    break;
                case 1: // segundo: acena feliz
                    Fill(body, 0.3f, 0.2f, 2.45f, 0.55f * Mathf.Sin(t * 9f), 0.08f, 0f, 0.04f + 0.04f * Mathf.Abs(Mathf.Sin(t * 4.5f)));
                    break;
                default: // terceiro: dança alternando os braços
                    float s = Mathf.Sin(t * 4.5f);
                    Fill(body, 1.3f + 0.9f * s, 0.6f, 1.3f - 0.9f * s, 0.6f, 0.1f, 0f, 0.06f + 0.05f * Mathf.Abs(s));
                    break;
            }
        }

        /// <summary>
        /// Abdução dos braços no plano frontal (0 = caído, π/2 = horizontal, π = para cima) e dobra
        /// do cotovelo; abertura das pernas, elevação do quadril e dobra dos joelhos.
        /// </summary>
        public static void Fill(BodyFrame body, float leftAbduction, float leftElbow, float rightAbduction, float rightElbow,
            float legSpread, float rootLift, float kneeBend)
        {
            body.IsTracked = true;
            body.TrackingId = 1;
            body.Timestamp = Time.timeAsDouble;

            Vector3 root = Feet + new Vector3(0f, HipHeight + rootLift - kneeBend, 0f);
            Vector3 spineShoulder = root + new Vector3(0f, 0.5f, 0f);
            Vector3 neck = spineShoulder + new Vector3(0f, 0.07f, 0f);

            Set(body, JointId.SpineBase, root);
            Set(body, JointId.SpineMid, root + new Vector3(0f, 0.25f, 0f));
            Set(body, JointId.SpineShoulder, spineShoulder);
            Set(body, JointId.Neck, neck);
            Set(body, JointId.Head, neck + new Vector3(0f, 0.13f, 0f));

            Arm(body, -1f, spineShoulder, leftAbduction, leftElbow);
            Arm(body, +1f, spineShoulder, rightAbduction, rightElbow);
            Leg(body, -1f, root, legSpread, kneeBend);
            Leg(body, +1f, root, legSpread, kneeBend);
        }

        private static void Arm(BodyFrame body, float side, Vector3 spineShoulder, float abduction, float elbowBend)
        {
            Vector3 shoulder = spineShoulder + new Vector3(side * 0.18f, -0.03f, 0f);
            Vector3 upperDir = new Vector3(side * Mathf.Sin(abduction), -Mathf.Cos(abduction), 0.15f).normalized;
            Vector3 elbow = shoulder + upperDir * UpperArm;
            float foreAngle = abduction + elbowBend;
            Vector3 foreDir = new Vector3(side * Mathf.Sin(foreAngle), -Mathf.Cos(foreAngle), 0.25f).normalized;
            Vector3 wrist = elbow + foreDir * Forearm;
            Vector3 hand = wrist + foreDir * HandLength;

            bool left = side < 0f;
            Set(body, left ? JointId.ShoulderLeft : JointId.ShoulderRight, shoulder);
            Set(body, left ? JointId.ElbowLeft : JointId.ElbowRight, elbow);
            Set(body, left ? JointId.WristLeft : JointId.WristRight, wrist);
            Set(body, left ? JointId.HandLeft : JointId.HandRight, hand);
            Set(body, left ? JointId.HandTipLeft : JointId.HandTipRight, hand + foreDir * 0.07f);
            Set(body, left ? JointId.ThumbLeft : JointId.ThumbRight, wrist + foreDir * 0.04f);
        }

        private static void Leg(BodyFrame body, float side, Vector3 root, float spread, float kneeBend)
        {
            Vector3 hip = root + new Vector3(side * 0.09f, -0.05f, 0f);
            Vector3 dir = new Vector3(side * Mathf.Sin(spread), -Mathf.Cos(spread), 0f);
            Vector3 knee = hip + dir * Thigh + new Vector3(0f, 0f, kneeBend * 1.5f);
            Vector3 ankle = knee + dir * Shin - new Vector3(0f, 0f, kneeBend * 1.5f);

            bool left = side < 0f;
            Set(body, left ? JointId.HipLeft : JointId.HipRight, hip);
            Set(body, left ? JointId.KneeLeft : JointId.KneeRight, knee);
            Set(body, left ? JointId.AnkleLeft : JointId.AnkleRight, ankle);
            Set(body, left ? JointId.FootLeft : JointId.FootRight, ankle + new Vector3(0f, -0.05f, 0.12f));
        }

        private static void Set(BodyFrame body, JointId id, Vector3 position)
            => body.SetJoint(id, position, Quaternion.identity, JointTrackingState.Tracked);
    }
}
