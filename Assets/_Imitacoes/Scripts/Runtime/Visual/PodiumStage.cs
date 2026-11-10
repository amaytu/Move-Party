using System.Collections.Generic;
using Imitacoes.Body;
using Imitacoes.Config;
using Imitacoes.UI;
using TMPro;
using UnityEngine;

namespace Imitacoes.Visual
{
    /// <summary>
    /// Pódio 3D do placar final: blocos de 1º, 2º e 3º nas cores dos jogadores, com um boneco
    /// dançando em cima de cada um (o campeão de coroa) e o nome + pontos na frente do bloco.
    ///
    /// Os bonecos são cópias do boneco ao vivo (<see cref="Avatar3DView.CreateCopy"/>) movidos
    /// por poses sintéticas (<see cref="PoseBuilder.Dance"/>).
    /// </summary>
    public sealed class PodiumStage : MonoBehaviour
    {
        // Lugar no pódio → posição X (no espaço do palco; +X = esquerda da tela) e altura do bloco.
        private static readonly float[] PlaceX = { 0.85f, 2.05f, -0.35f };
        private static readonly float[] PlaceTop = { 1.05f, 0.8f, 0.65f };
        private const float BlockBottom = -1.2f;
        private const float AvatarScale = 0.58f;

        private Avatar3DView template;
        private Camera cam;
        private RectTransform canvas;
        private TMP_FontAsset font;
        private Material blockMaterialSource;

        private readonly List<GameObject> spawned = new List<GameObject>();
        private readonly List<Avatar3DView> avatars = new List<Avatar3DView>();
        private readonly List<BodyFrame> poses = new List<BodyFrame>();
        private readonly List<(RectTransform label, Vector3 anchor)> labels = new List<(RectTransform, Vector3)>();
        private float startTime;

        public static PodiumStage Create(Avatar3DView template, RectTransform canvas, TMP_FontAsset font)
        {
            var go = new GameObject("Pódio");
            go.transform.SetParent(template.transform.parent, false);
            var stage = go.AddComponent<PodiumStage>();
            stage.template = template;
            stage.canvas = canvas;
            stage.font = font;
            return stage;
        }

        /// <param name="ranking">Jogadores do 1º ao último (só os 3 primeiros sobem no pódio).</param>
        public void Show(IReadOnlyList<ScoreRow> ranking)
        {
            Hide();
            cam = Camera.main;
            startTime = Time.unscaledTime;
            gameObject.SetActive(true);

            int count = Mathf.Min(3, ranking.Count);
            for (int place = 0; place < count; place++)
                BuildPlace(place, ranking[place].Player, ranking[place].Score);
        }

        public void Hide()
        {
            foreach (GameObject go in spawned) if (go != null) Destroy(go);
            spawned.Clear();
            avatars.Clear();
            poses.Clear();
            labels.Clear();
            gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ Montagem

        private void BuildPlace(int place, PlayerConfig player, float score)
        {
            float x = PlaceX[place], top = PlaceTop[place];

            // Bloco (desce até abaixo da tela) e uma tampa mais clara.
            GameObject block = Box($"Bloco {place + 1}", new Vector3(x, (top + BlockBottom) * 0.5f, 0f),
                new Vector3(1.05f, top - BlockBottom, 0.9f), Color.Lerp(player.Color, Color.black, 0.3f));
            GameObject cap = Box($"Tampa {place + 1}", new Vector3(x, top - 0.03f, 0f),
                new Vector3(1.12f, 0.07f, 0.96f), Color.Lerp(player.Color, Color.white, 0.15f));
            spawned.Add(block);
            spawned.Add(cap);

            // Boneco dançando em cima.
            Avatar3DView avatar = template.CreateCopy($"Boneco {place + 1}º", transform);
            avatar.transform.localPosition = new Vector3(x, top, 0f);
            avatar.transform.localScale = Vector3.one * AvatarScale;
            avatar.SetColor(player.Color, 1f);
            avatar.SetCrown(place == 0);
            avatar.ResetReference();
            spawned.Add(avatar.gameObject);
            avatars.Add(avatar);
            poses.Add(new BodyFrame());

            // Número do lugar na frente do bloco + nome e pontos logo abaixo da tampa.
            AddLabel($"<size=110>{place + 1}º</size>", Color.white, new Vector3(x, top - 0.28f, 0.46f), 110f);
            AddLabel($"{player.DisplayName}\n<size=40>{score:0} pts</size>", Color.white, new Vector3(x, top - 0.68f, 0.46f), 46f);
        }

        private GameObject Box(string boxName, Vector3 localPosition, Vector3 size, Color color)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = boxName;
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(transform, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = size;

            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = BlockMaterial();
            var block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", color);
            block.SetColor("_Color", color);
            renderer.SetPropertyBlock(block);
            return go;
        }

        private Material BlockMaterial()
        {
            if (blockMaterialSource != null) return blockMaterialSource;
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            blockMaterialSource = new Material(shader) { name = "Pódio (runtime)" };
            if (blockMaterialSource.HasProperty("_Smoothness")) blockMaterialSource.SetFloat("_Smoothness", 0.35f);
            return blockMaterialSource;
        }

        private void AddLabel(string text, Color color, Vector3 localAnchor, float size)
        {
            TMP_FontAsset previous = UIFactory.Font;
            if (font != null) UIFactory.Font = font;
            TMP_Text t = UIFactory.Text("Pódio - Rótulo", canvas, text, size, color, style: FontStyles.Normal);
            UIFactory.Font = previous;

            t.outlineWidth = 0.2f;
            t.outlineColor = new Color32(10, 10, 25, 255);
            t.lineSpacing = -25f;
            RectTransform rt = t.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(420f, 200f);
            rt.SetSiblingIndex(0); // atrás do resto da UI (botões, cursor)
            rt.SetAsFirstSibling();
            spawned.Add(rt.gameObject);
            labels.Add((rt, localAnchor));
        }

        // ------------------------------------------------------------------ Animação

        private void LateUpdate()
        {
            float t = Time.unscaledTime - startTime;
            for (int i = 0; i < avatars.Count; i++)
            {
                PoseBuilder.Dance(poses[i], i, t + i * 0.37f);
                avatars[i].Render(poses[i]);
            }

            if (cam == null || canvas == null) return;
            foreach ((RectTransform label, Vector3 anchor) in labels)
            {
                Vector3 screen = cam.WorldToScreenPoint(transform.TransformPoint(anchor));
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas, screen, null, out Vector2 local))
                    label.anchoredPosition = local;
            }
        }
    }
}
