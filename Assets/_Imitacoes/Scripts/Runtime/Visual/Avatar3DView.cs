using System.Collections.Generic;
using Imitacoes.Body;
using UnityEngine;
using UnityEngine.Rendering;

namespace Imitacoes.Visual
{
    /// <summary>
    /// Boneco 3D "cartoon" montado com primitivas (cilindros e esferas), iluminado e com sombra,
    /// sobre uma plataforma redonda na cor do jogador.
    ///
    /// Usa as posições 3D reais do sensor (inclusive a profundidade): braço esticado para a frente
    /// vem na direção da câmera, girar o corpo gira o boneco e a cabeça, pular e agachar aparecem.
    ///
    /// Modo espelho: os dados usam X = direita do jogador; aqui X é invertido, então a câmera
    /// (que olha o boneco de frente) mostra a mão direita do jogador do lado direito da tela.
    ///
    /// Cores por <see cref="MaterialPropertyBlock"/> (um material compartilhado para todos).
    /// </summary>
    [AddComponentMenu("Imitações/Avatar 3D View")]
    public sealed class Avatar3DView : BodyView
    {
        [Header("Materiais (criados pelo menu Imitações)")]
        [SerializeField, Tooltip("URP Lit branco: corpo (a cor vem por MaterialPropertyBlock).")]
        private Material bodyMaterial;
        [SerializeField, Tooltip("URP Lit escuro e brilhante: olhos.")]
        private Material eyeMaterial;
        [SerializeField, Tooltip("URP Lit: plataforma sob os pés.")]
        private Material platformMaterial;
        [SerializeField, Tooltip("URP Unlit: anel brilhante da plataforma.")]
        private Material glowMaterial;

        [Header("Proporções (metros)")]
        [SerializeField, Min(0.02f)] private float limbThickness = 0.15f;
        [SerializeField, Min(0.05f)] private float torsoWidth = 0.42f;
        [SerializeField, Min(0.05f)] private float torsoDepth = 0.28f;
        [SerializeField, Min(0.05f)] private float headSize = 0.4f;
        [SerializeField, Min(0.02f)] private float handSize = 0.17f;
        [SerializeField] private Vector3 footSize = new Vector3(0.15f, 0.11f, 0.26f);
        [SerializeField, Min(0.1f)] private float platformRadius = 0.6f;

        [Header("Posição")]
        [SerializeField, Tooltip("Distância lateral (m) dos lugares Esquerda/Direita.")]
        private float slotOffset = 1.35f;
        [SerializeField, Tooltip("Altura do quadril acima da plataforma (m).")]
        private float hipHeight = 0.95f;
        [SerializeField, Min(0f), Tooltip("Velocidade com que a referência (altura/profundidade) acompanha o quadril.")]
        private float referenceFollow = 1.5f;
        [SerializeField] private bool followPlayerX;

        [Header("Cor")]
        [SerializeField] private Color color = Color.white;
        [SerializeField, Range(0f, 1f)] private float alpha = 1f;
        [SerializeField, Tooltip("Cor do fundo usada para \"apagar\" o fantasma.")]
        private Color fadeTarget = new Color(0.055f, 0.06f, 0.11f, 1f);

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static Mesh sphereMesh;
        private static Mesh cylinderMesh;

        // Segmentos (cilindros) e juntas (esferas) do corpo.
        private static readonly (JointId a, JointId b)[] Segments =
        {
            (JointId.ShoulderLeft, JointId.ElbowLeft), (JointId.ElbowLeft, JointId.WristLeft),
            (JointId.ShoulderRight, JointId.ElbowRight), (JointId.ElbowRight, JointId.WristRight),
            (JointId.HipLeft, JointId.KneeLeft), (JointId.KneeLeft, JointId.AnkleLeft),
            (JointId.HipRight, JointId.KneeRight), (JointId.KneeRight, JointId.AnkleRight),
            (JointId.SpineShoulder, JointId.Neck),
        };

        private static readonly JointId[] RoundJoints =
        {
            JointId.ShoulderLeft, JointId.ElbowLeft, JointId.WristLeft,
            JointId.ShoulderRight, JointId.ElbowRight, JointId.WristRight,
            JointId.HipLeft, JointId.KneeLeft, JointId.AnkleLeft,
            JointId.HipRight, JointId.KneeRight, JointId.AnkleRight,
        };

        private Transform figure;
        private readonly List<Transform> segmentParts = new List<Transform>();
        private readonly List<Transform> jointParts = new List<Transform>();
        private Transform torso, head, handL, handR, footL, footR, platform, rim;
        private readonly List<Renderer> bodyRenderers = new List<Renderer>();
        private Renderer torsoRenderer, headRenderer, rimRenderer;
        private MaterialPropertyBlock block;
        private bool built;
        private bool hasReference;
        private Vector3 reference;
        private readonly Vector3[] mapped = new Vector3[BodyFrame.JointCount];
        private readonly bool[] usable = new bool[BodyFrame.JointCount];

        // ------------------------------------------------------------------ BodyView

        public override void SetColor(Color newColor, float newAlpha)
        {
            color = newColor;
            alpha = newAlpha;
            if (built) ApplyColors();
        }

        public override void ResetReference() => hasReference = false;

        public override void SetSlot(BodySlot slot, bool follow)
        {
            float x = slot == BodySlot.Left ? -slotOffset : slot == BodySlot.Right ? slotOffset : 0f;
            // Esquerda da TELA = +X do mundo (a câmera olha para -Z).
            transform.localPosition = new Vector3(-x, 0f, 0f);
            followPlayerX = follow;
        }

        public override void Clear()
        {
            if (built) figure.gameObject.SetActive(false);
        }

        public override void Render(BodyFrame body)
        {
            if (body == null || !body.IsTracked || !body.GetJoint(JointId.SpineBase).IsUsable)
            {
                Clear();
                return;
            }

            EnsureBuilt();
            figure.gameObject.SetActive(true);
            UpdateReference(body);

            for (int i = 0; i < BodyFrame.JointCount; i++)
            {
                JointData j = body.Joints[i];
                usable[i] = j.IsUsable;
                mapped[i] = Map(j.Position);
            }

            Vector3 up = Vector3.up;
            Vector3 right = BodyRight();
            Vector3 forward = Vector3.Cross(up, right).normalized; // de frente para a câmera = +Z

            // Membros: cilindros entre as juntas + esferas nas juntas (formam cápsulas perfeitas).
            for (int s = 0; s < Segments.Length; s++)
            {
                (JointId a, JointId b) = Segments[s];
                PlaceCylinder(segmentParts[s], a, b, limbThickness);
            }
            for (int k = 0; k < RoundJoints.Length; k++)
            {
                PlaceSphere(jointParts[k], RoundJoints[k], Vector3.one * limbThickness);
            }

            PlaceSphere(handL, JointId.HandLeft, Vector3.one * handSize);
            PlaceSphere(handR, JointId.HandRight, Vector3.one * handSize);
            PlaceFoot(footL, JointId.AnkleLeft, JointId.FootLeft, forward);
            PlaceFoot(footR, JointId.AnkleRight, JointId.FootRight, forward);
            PlaceTorso(right);
            PlaceHead(right);

            // Plataforma sob os pés, seguindo o jogador no chão.
            Vector3 root = mapped[(int)JointId.SpineBase];
            platform.localPosition = new Vector3(root.x, -0.03f, root.z);
            rim.localPosition = new Vector3(root.x, -0.045f, root.z);
        }

        // ------------------------------------------------------------------ Mapeamento

        /// <summary>Sensor → espaço local do boneco (espelhado; quadril na altura hipHeight).</summary>
        private Vector3 Map(Vector3 p)
        {
            float x = followPlayerX ? p.x : p.x - reference.x;
            return new Vector3(-x, p.y - reference.y + hipHeight, p.z - reference.z);
        }

        private void UpdateReference(BodyFrame body)
        {
            Vector3 root = body.Root;
            if (!hasReference)
            {
                reference = root;
                hasReference = true;
                return;
            }

            float t = 1f - Mathf.Exp(-referenceFollow * Time.unscaledDeltaTime);
            reference.y = Mathf.Lerp(reference.y, root.y, t);
            reference.z = Mathf.Lerp(reference.z, root.z, t);
            reference.x = root.x;
        }

        private Vector3 BodyRight()
        {
            Vector3 right = Vector3.zero;
            if (usable[(int)JointId.ShoulderLeft] && usable[(int)JointId.ShoulderRight])
                right += mapped[(int)JointId.ShoulderRight] - mapped[(int)JointId.ShoulderLeft];
            if (usable[(int)JointId.HipLeft] && usable[(int)JointId.HipRight])
                right += mapped[(int)JointId.HipRight] - mapped[(int)JointId.HipLeft];
            right.y = 0f;
            return right.sqrMagnitude > 1e-4f ? right.normalized : Vector3.left; // de frente: direita do jogador = -X
        }

        // ------------------------------------------------------------------ Peças

        private void PlaceCylinder(Transform part, JointId a, JointId b, float thickness)
        {
            bool visible = usable[(int)a] && usable[(int)b];
            part.gameObject.SetActive(visible);
            if (!visible) return;

            Vector3 pa = mapped[(int)a], pb = mapped[(int)b];
            Vector3 dir = pb - pa;
            float length = dir.magnitude;
            if (length < 1e-4f)
            {
                part.gameObject.SetActive(false);
                return;
            }

            part.localPosition = (pa + pb) * 0.5f;
            part.localRotation = Quaternion.FromToRotation(Vector3.up, dir / length);
            part.localScale = new Vector3(thickness, length * 0.5f, thickness); // cilindro padrão tem 2 m
        }

        private void PlaceSphere(Transform part, JointId joint, Vector3 size)
        {
            bool visible = usable[(int)joint];
            part.gameObject.SetActive(visible);
            if (!visible) return;
            part.localPosition = mapped[(int)joint];
            part.localScale = size;
        }

        private void PlaceFoot(Transform part, JointId ankle, JointId foot, Vector3 forward)
        {
            bool visible = usable[(int)ankle];
            part.gameObject.SetActive(visible);
            if (!visible) return;

            Vector3 pa = mapped[(int)ankle];
            Vector3 dir = usable[(int)foot] ? mapped[(int)foot] - pa : forward;
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-4f) dir = forward;

            part.localPosition = pa + dir.normalized * footSize.z * 0.3f - new Vector3(0f, footSize.y * 0.3f, 0f);
            part.localRotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
            part.localScale = footSize;
        }

        private void PlaceTorso(Vector3 right)
        {
            int top = (int)JointId.SpineShoulder, bottom = (int)JointId.SpineBase;
            bool visible = usable[top] && usable[bottom];
            torso.gameObject.SetActive(visible);
            if (!visible) return;

            Vector3 a = mapped[top], b = mapped[bottom];
            Vector3 up = (a - b).normalized;
            Vector3 fwd = Vector3.Cross(up, right).normalized;
            if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.forward;

            // Elipsoide "feijão": um pouco além dos ombros e do quadril.
            float length = Vector3.Distance(a, b) + 0.2f;
            torso.localPosition = (a + b) * 0.5f + up * 0.02f;
            torso.localRotation = Quaternion.LookRotation(fwd, up);
            torso.localScale = new Vector3(torsoWidth, length, torsoDepth);
        }

        private void PlaceHead(Vector3 right)
        {
            int h = (int)JointId.Head;
            head.gameObject.SetActive(usable[h]);
            if (!usable[h]) return;

            int neck = usable[(int)JointId.Neck] ? (int)JointId.Neck : (int)JointId.SpineShoulder;
            Vector3 up = usable[neck] ? (mapped[h] - mapped[neck]).normalized : Vector3.up;
            if (up.sqrMagnitude < 1e-4f) up = Vector3.up;
            Vector3 fwd = Vector3.Cross(up, right).normalized;
            if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.forward;

            head.localPosition = mapped[h] + up * 0.03f;
            head.localRotation = Quaternion.LookRotation(fwd, up);
            head.localScale = Vector3.one * headSize;
        }

        // ------------------------------------------------------------------ Cores

        private void ApplyColors()
        {
            Color limb = Faded(color);
            Color torsoColor = Faded(Color.Lerp(color, Color.black, 0.1f));
            Color headColor = Faded(Color.Lerp(color, Color.white, 0.1f));

            foreach (Renderer r in bodyRenderers) SetRendererColor(r, limb);
            SetRendererColor(torsoRenderer, torsoColor);
            SetRendererColor(headRenderer, headColor);
            SetRendererColor(rimRenderer, Faded(color));
        }

        private Color Faded(Color c)
        {
            Color result = Color.Lerp(fadeTarget, c, alpha);
            result.a = 1f;
            return result;
        }

        private void SetRendererColor(Renderer r, Color c)
        {
            if (r == null) return;
            block ??= new MaterialPropertyBlock();
            r.GetPropertyBlock(block);
            block.SetColor(BaseColorId, c);
            block.SetColor(ColorId, c);
            r.SetPropertyBlock(block);
        }

        // ------------------------------------------------------------------ Construção

        private void EnsureBuilt()
        {
            if (built) return;
            built = true;
            LoadMeshes();

            figure = new GameObject("Boneco 3D").transform;
            figure.SetParent(transform, false);

            foreach ((JointId a, JointId b) in Segments)
                segmentParts.Add(CreatePart($"{a}-{b}", cylinderMesh, bodyMaterial, true));
            foreach (JointId j in RoundJoints)
                jointParts.Add(CreatePart(j.ToString(), sphereMesh, bodyMaterial, true));

            handL = CreatePart("Mao E", sphereMesh, bodyMaterial, true);
            handR = CreatePart("Mao D", sphereMesh, bodyMaterial, true);
            footL = CreatePart("Pe E", sphereMesh, bodyMaterial, true);
            footR = CreatePart("Pe D", sphereMesh, bodyMaterial, true);

            torso = CreatePart("Tronco", sphereMesh, bodyMaterial, false);
            torsoRenderer = torso.GetComponent<Renderer>();
            head = CreatePart("Cabeca", sphereMesh, bodyMaterial, false);
            headRenderer = head.GetComponent<Renderer>();

            // Olhos: filhos da cabeça (coordenadas na esfera de raio 0,5).
            foreach (float side in new[] { -1f, 1f })
            {
                Transform eye = CreatePart(side < 0 ? "Olho E" : "Olho D", sphereMesh, eyeMaterial, false, castShadows: false);
                eye.SetParent(head, false);
                eye.localPosition = new Vector3(side * 0.17f, 0.06f, 0.43f);
                eye.localScale = new Vector3(0.14f, 0.2f, 0.1f);
            }

            platform = CreatePart("Plataforma", cylinderMesh, platformMaterial, false, castShadows: false);
            platform.localScale = new Vector3(platformRadius * 2f, 0.03f, platformRadius * 2f);
            rim = CreatePart("Anel", cylinderMesh, glowMaterial, false, castShadows: false);
            rim.localScale = new Vector3(platformRadius * 2.12f, 0.02f, platformRadius * 2.12f);
            rimRenderer = rim.GetComponent<Renderer>();

            ApplyColors();
        }

        private Transform CreatePart(string partName, Mesh mesh, Material material, bool isLimb, bool castShadows = true)
        {
            var go = new GameObject(partName, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(figure, false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;

            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            renderer.receiveShadows = true;
            if (isLimb) bodyRenderers.Add(renderer);
            return go.transform;
        }

        /// <summary>Pega as malhas de esfera e cilindro da Unity (uma vez, sem colliders).</summary>
        private static void LoadMeshes()
        {
            if (sphereMesh != null && cylinderMesh != null) return;
            sphereMesh = GrabMesh(PrimitiveType.Sphere);
            cylinderMesh = GrabMesh(PrimitiveType.Cylinder);
        }

        private static Mesh GrabMesh(PrimitiveType type)
        {
            GameObject temp = GameObject.CreatePrimitive(type);
            Mesh mesh = temp.GetComponent<MeshFilter>().sharedMesh;
            if (Application.isPlaying) Destroy(temp); else DestroyImmediate(temp);
            return mesh;
        }
    }
}
