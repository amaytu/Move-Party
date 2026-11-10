using System;
using System.Collections.Generic;
using System.Text;
using Imitacoes.Config;
using Imitacoes.Interaction;
using Imitacoes.Visual;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Imitacoes.UI
{
    /// <summary>Uma linha de pontuação (resultado da rodada ou placar final).</summary>
    public readonly struct ScoreRow
    {
        public readonly PlayerConfig Player;
        public readonly float Score;
        public readonly string Tag;

        public ScoreRow(PlayerConfig player, float score, string tag = "")
        {
            Player = player;
            Score = score;
            Tag = tag ?? string.Empty;
        }
    }

    /// <summary>Como os bonecos aparecem na tela.</summary>
    public enum SkeletonLayout
    {
        Hidden,

        /// <summary>Só o jogador ao vivo, seguindo a posição dele.</summary>
        LiveOnly,

        /// <summary>Só o clipe (replay do Mestre), centralizado.</summary>
        ClipOnly,

        /// <summary>Fantasma do Mestre à esquerda, jogador ao vivo à direita.</summary>
        GhostAndLive
    }

    /// <summary>
    /// Todas as telas do jogo num único Canvas (Screen Space - Overlay, referência 1920×1080).
    /// Visual "party game": fundo escuro com brilho e bolhas coloridas, cartões arredondados com
    /// sombra, pílulas, fonte arredondada (Lilita One) e pequenas animações (pop, pulso, flutuação).
    /// Pensado para leitura a 2–3 m: textos grandes e alto contraste.
    ///
    /// Os estados do jogo chamam métodos de alto nível (ShowTitle, ShowCountdown...).
    /// <see cref="BuildLayout"/> cria a hierarquia inteira (menu Imitações ▸ Criar Cena Principal).
    /// </summary>
    [AddComponentMenu("Imitações/Game UI")]
    public sealed class GameUI : MonoBehaviour
    {
        [Header("Fundo e moldura")]
        [SerializeField] private Image accentGlow;
        [SerializeField] private Image frame;

        [Header("Faixa do jogador")]
        [SerializeField] private GameObject bannerRoot;
        [SerializeField] private Image bannerBackground;
        [SerializeField] private TMP_Text bannerText;

        [Header("Textos")]
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text subtitleText;

        [Header("Contagem")]
        [SerializeField] private RectTransform countdownRoot;
        [SerializeField] private Image countdownRing;
        [SerializeField] private TMP_Text countdownText;

        [Header("Tema")]
        [SerializeField] private RectTransform themeRoot;
        [SerializeField] private TMP_Text themeLabel;
        [SerializeField] private TMP_Text themeText;
        [SerializeField] private Image themeAccent;

        [Header("Gravação")]
        [SerializeField] private GameObject recordingRoot;
        [SerializeField] private TMP_Text recordingText;
        [SerializeField] private Image recordingDot;
        [SerializeField] private RectTransform recordingFill;

        [Header("Botões")]
        [SerializeField] private GameObject playerCountRoot;
        [SerializeField] private GestureButton[] playerCountButtons = Array.Empty<GestureButton>();
        [SerializeField] private GameObject finalButtonsRoot;
        [SerializeField] private GestureButton playAgainButton;
        [SerializeField] private GestureButton menuButton;

        [Header("Pontuação")]
        [SerializeField] private GameObject resultsRoot;
        [SerializeField] private ScoreRowView[] resultRows = Array.Empty<ScoreRowView>();

        [Header("Aviso")]
        [SerializeField] private RectTransform warningRoot;
        [SerializeField] private TMP_Text warningText;

        [Header("Bonecos")]
        [SerializeField, Tooltip("Boneco do jogador ao vivo (2D ou 3D).")] private BodyView liveView;
        [SerializeField, Tooltip("Boneco do replay/fantasma (2D ou 3D).")] private BodyView clipView;

        [Header("Debug")]
        [SerializeField] private TMP_Text debugText;

        public BodyView LiveView => liveView;
        public BodyView ClipView => clipView;
        public IReadOnlyList<GestureButton> PlayerCountButtons => playerCountButtons;
        public GestureButton PlayAgainButton => playAgainButton;
        public GestureButton MenuButton => menuButton;

        private readonly float[] barTargets = new float[4];
        private float barAnimationTime;
        private readonly Dictionary<RectTransform, float> pops = new Dictionary<RectTransform, float>();
        private int lastCountdown = -1;
        private bool titleFloats;
        private Vector2 titleBasePosition;

        // ------------------------------------------------------------------ API: estados

        /// <summary>Esconde o conteúdo central (títulos, contagem, tema, botões, placares).</summary>
        public void HideAll()
        {
            SetText(titleText, null);
            SetText(subtitleText, null);
            HideCountdown();
            HideTheme();
            SetActive(recordingRoot, false);
            SetActive(playerCountRoot, false);
            SetActive(finalButtonsRoot, false);
            SetActive(resultsRoot, false);
            if (scoreReaction != null) scoreReaction.Hide();
            if (howToPlay != null) howToPlay.Hide();
            titleFloats = false;
        }

        // ------------------------------------------------------------------ API: telas montadas em tempo de execução

        private HowToPlayPanel howToPlay;
        private PauseMenu pauseMenu;

        /// <summary>Sprite de círculo da skin (o mesmo usado nas formas arredondadas da cena).</summary>
        internal Sprite CircleSprite => bannerBackground != null ? bannerBackground.sprite : null;
        internal TMPro.TMP_FontAsset Font => titleText != null ? titleText.font : null;

        // ---------------------------------------------------------- Modo "Regras surpresa" (seleção de jogadores)

        private GestureButton surpriseToggle;

        /// <summary>O botão "Regras surpresa" foi acionado.</summary>
        public event Action SurpriseToggled;

        public void ShowSurpriseToggle(bool show, bool on)
        {
            if (!show)
            {
                if (surpriseToggle != null) surpriseToggle.gameObject.SetActive(false);
                return;
            }
            if (surpriseToggle == null)
            {
                UIFactory.Font = Font;
                surpriseToggle = CreateButton("Botao Regras Surpresa", transform, new Vector2(0.5f, 0.1f),
                    new Vector2(860f, 120f), 60f, "", 50f, CircleSprite);
                UIFactory.Font = null;
                surpriseToggle.onSelected.AddListener(() => SurpriseToggled?.Invoke());
                BringCursorToFront();
            }
            surpriseToggle.gameObject.SetActive(true);
            SetSurpriseToggle(on);
        }

        public void SetSurpriseToggle(bool on)
        {
            if (surpriseToggle == null) return;
            surpriseToggle.Label = on ? "Regras surpresa: <color=#FFD23F>LIGADAS</color>" : "Regras surpresa: desligadas";
            surpriseToggle.SetAccentColor(on ? new Color(1f, 0.82f, 0.25f) : new Color(0.35f, 0.8f, 1f));
        }

        // ---------------------------------------------------------- Placar final: prêmios e confete

        private AwardsPanel awards;
        private ConfettiRain confetti;

        public void ShowAwards(IReadOnlyList<Imitacoes.Game.MatchState.Award> list)
        {
            if (awards == null) awards = AwardsPanel.Create(transform, Font, CircleSprite);
            awards.Show(list);
        }

        public void HideAwards()
        {
            if (awards != null) awards.Hide();
        }

        /// <summary>Chuva de confete em tela cheia (atrás dos botões e do cursor).</summary>
        public void BurstConfetti(int count, Vector2 origin)
        {
            if (confetti == null)
            {
                confetti = ConfettiRain.Create(transform);
                confetti.transform.SetAsFirstSibling();
            }
            confetti.Burst(count, origin);
        }

        public void ShowHowToPlay(bool show)
        {
            if (!show)
            {
                if (howToPlay != null) howToPlay.Hide();
                return;
            }
            if (howToPlay == null) howToPlay = HowToPlayPanel.Create(transform, Font, CircleSprite);
            howToPlay.Show();
        }

        public PauseMenu PauseMenu
        {
            get
            {
                if (pauseMenu == null) pauseMenu = PauseMenu.Create(transform, Font, CircleSprite);
                return pauseMenu;
            }
        }

        public void OpenPause(Color accent, string skipLabel)
        {
            PauseMenu.Open(accent, skipLabel);
            BringCursorToFront();
        }

        public void ClosePause()
        {
            if (pauseMenu != null) pauseMenu.Close();
        }

        /// <summary>O cursor da mão fica sempre por cima das telas criadas depois dele.</summary>
        private void BringCursorToFront()
        {
            Transform cursor = transform.Find("Cursor");
            if (cursor != null) cursor.SetAsLastSibling();
        }

        // ------------------------------------------------------------------ API: reação da nota

        private ScoreReaction scoreReaction;

        /// <summary>Reação da nota (montada na primeira vez que é usada).</summary>
        public ScoreReaction ScoreReaction
        {
            get
            {
                if (scoreReaction == null)
                    scoreReaction = ScoreReaction.Create(transform, titleText != null ? titleText.font : null);
                return scoreReaction;
            }
        }

        /// <summary>Número subindo até a nota, frase e (nota alta) confete.</summary>
        public void ShowScoreReaction(float score, Color playerColor) => ScoreReaction.Show(score, playerColor);

        /// <summary>Cor de destaque (moldura, faixa, brilho, contagem) — a cor do jogador da vez.</summary>
        public void SetAccent(Color color, Color textOnColor)
        {
            if (frame != null) frame.color = color;
            if (bannerBackground != null) bannerBackground.color = color;
            if (bannerText != null) bannerText.color = textOnColor;
            if (accentGlow != null) accentGlow.color = new Color(color.r, color.g, color.b, 0.22f);
            if (countdownRing != null) countdownRing.color = color;
            if (themeAccent != null) themeAccent.color = color;
            foreach (GestureButton b in playerCountButtons) if (b != null) b.SetAccentColor(color);
            if (playAgainButton != null) playAgainButton.SetAccentColor(color);
            if (menuButton != null) menuButton.SetAccentColor(color);
        }

        public void SetAccent(PlayerConfig player) => SetAccent(player.Color, player.ContrastTextColor);

        /// <summary>Faixa no topo ("Vez do Jogador 2"). Null esconde.</summary>
        public void SetBanner(string text)
        {
            bool show = !string.IsNullOrEmpty(text);
            bool changed = bannerText != null && bannerText.text != text;
            SetActive(bannerRoot, show);
            if (show && bannerText != null) bannerText.text = text;
            if (show && changed && bannerRoot != null) Pop((RectTransform)bannerRoot.transform, 0.12f);
        }

        public void ShowTitle(string title, string subtitle = null)
        {
            SetText(titleText, title);
            SetText(subtitleText, subtitle);
        }

        /// <summary>Título colorido letra a letra (cores dos jogadores) e flutuando.</summary>
        public void ShowLogo(string title, string subtitle)
        {
            ShowTitle(Colorful(title), subtitle);
            titleFloats = true;
        }

        public void SetSubtitle(string subtitle) => SetText(subtitleText, subtitle);

        public void ShowCountdown(int value)
        {
            if (value <= 0)
            {
                HideCountdown();
                return;
            }

            SetActive(countdownRoot != null ? countdownRoot.gameObject : null, true);
            if (countdownText != null) countdownText.text = value.ToString();
            if (value != lastCountdown && countdownRoot != null) Pop(countdownRoot, 0.35f);
            lastCountdown = value;
        }

        public void HideCountdown()
        {
            lastCountdown = -1;
            SetActive(countdownRoot != null ? countdownRoot.gameObject : null, false);
        }

        /// <param name="pop">Anima a entrada (usado na revelação do tema).</param>
        public void ShowTheme(string label, string text, Color color, bool pop = false)
        {
            SetActive(themeRoot != null ? themeRoot.gameObject : null, true);
            if (themeLabel != null) themeLabel.text = label;
            if (themeText != null)
            {
                themeText.text = text;
                themeText.color = color;
            }
            if (pop && themeRoot != null) Pop(themeRoot, 0.3f);
        }

        public void HideTheme() => SetActive(themeRoot != null ? themeRoot.gameObject : null, false);

        public void ShowRecording(float progress01, float secondsLeft, bool paused)
        {
            SetActive(recordingRoot, true);
            if (recordingFill != null) recordingFill.anchorMax = new Vector2(Mathf.Clamp01(progress01), 1f);
            if (recordingText != null)
                recordingText.text = paused ? "PAUSADO - volte para a frente do sensor" : $"GRAVANDO   {secondsLeft:0.0}s";
            if (recordingDot != null) recordingDot.color = paused ? Palette.Warning : Palette.Record;
        }

        public void HideRecording() => SetActive(recordingRoot, false);

        public void ShowPlayerCountButtons(bool show) => SetActive(playerCountRoot, show);

        public void ShowFinalButtons(bool show) => SetActive(finalButtonsRoot, show);

        private Vector2[] finalButtonsDefault; // anchor, size (Jogar novamente), anchor, size (Menu)

        /// <summary>Com o pódio, os botões vão para a coluna da direita (embaixo dos prêmios).</summary>
        public void SetFinalButtonsBesidePodium(bool beside)
        {
            if (playAgainButton == null || menuButton == null) return;
            var again = (RectTransform)playAgainButton.transform;
            var menu = (RectTransform)menuButton.transform;
            if (finalButtonsDefault == null)
                finalButtonsDefault = new[] { again.anchorMin, again.sizeDelta, menu.anchorMin, menu.sizeDelta };

            if (beside)
            {
                again.anchorMin = again.anchorMax = new Vector2(0.8f, 0.2f);
                again.sizeDelta = new Vector2(600f, 120f);
                menu.anchorMin = menu.anchorMax = new Vector2(0.8f, 0.075f);
                menu.sizeDelta = new Vector2(600f, 110f);
            }
            else
            {
                again.anchorMin = again.anchorMax = finalButtonsDefault[0];
                again.sizeDelta = finalButtonsDefault[1];
                menu.anchorMin = menu.anchorMax = finalButtonsDefault[2];
                menu.sizeDelta = finalButtonsDefault[3];
            }
        }

        /// <summary>Barras de pontuação (resultado da rodada ou placar final).</summary>
        /// <param name="maxScore">Valor que enche a barra inteira.</param>
        /// <param name="showPosition">Medalha com a posição (placar final) em vez do número do jogador.</param>
        public void ShowScores(IReadOnlyList<ScoreRow> rows, float maxScore, bool showPosition)
        {
            SetActive(resultsRoot, true);
            barAnimationTime = 0f;
            maxScore = Mathf.Max(1f, maxScore);

            // Centraliza o bloco de linhas na vertical (2, 3 ou 4 jogadores).
            var resultsRect = (RectTransform)resultsRoot.transform;
            int shown = Mathf.Min(rows.Count, resultRows.Length);
            float used = shown * RowHeight + Mathf.Max(0, shown - 1) * RowSpacing;
            float full = resultRows.Length * RowHeight + (resultRows.Length - 1) * RowSpacing;
            resultsRect.anchoredPosition = new Vector2(0f, -(full - used) * 0.5f);

            for (int i = 0; i < resultRows.Length; i++)
            {
                ScoreRowView view = resultRows[i];
                if (view == null || view.root == null) continue;

                bool visible = i < rows.Count;
                view.root.SetActive(visible);
                if (!visible) continue;

                ScoreRow row = rows[i];
                Color c = row.Player.Color;
                view.medal.color = c;
                view.medalText.text = showPosition ? $"{RankOf(rows, i)}º" : row.Player.PlayerNumber.ToString();
                view.medalText.color = row.Player.ContrastTextColor;
                view.nameText.text = row.Player.DisplayName;
                view.nameText.color = Color.Lerp(c, Color.white, 0.25f);
                view.scoreText.text = $"{row.Score:0}";
                view.tagText.text = row.Tag;
                view.fill.color = c;
                view.fill.rectTransform.anchorMax = new Vector2(0f, 1f);
                barTargets[i] = Mathf.Clamp01(row.Score / maxScore);
                Pop((RectTransform)view.root.transform, 0.12f + 0.06f * i);
            }
        }

        public void HideScores() => SetActive(resultsRoot, false);

        /// <summary>Posição com empates: quem tem a mesma pontuação divide a colocação.</summary>
        private static int RankOf(IReadOnlyList<ScoreRow> rows, int index)
        {
            int rank = 1;
            for (int j = 0; j < rows.Count; j++)
                if (rows[j].Score > rows[index].Score) rank++;
            return rank;
        }

        public void ShowWarning(string message)
        {
            bool show = !string.IsNullOrEmpty(message);
            GameObject go = warningRoot != null ? warningRoot.gameObject : null;
            bool wasHidden = go != null && !go.activeSelf;
            SetActive(go, show);
            if (!show || warningText == null) return;
            if (warningText.text != message) warningText.text = message;
            if (wasHidden) Pop(warningRoot, 0.15f);
        }

        public void SetSkeletonLayout(SkeletonLayout layout)
        {
            bool live = layout == SkeletonLayout.LiveOnly || layout == SkeletonLayout.GhostAndLive;
            bool clip = layout == SkeletonLayout.ClipOnly || layout == SkeletonLayout.GhostAndLive;

            bool sideBySide = layout == SkeletonLayout.GhostAndLive;

            if (liveView != null)
            {
                liveView.SetVisible(live);
                liveView.SetSlot(sideBySide ? BodySlot.Right : BodySlot.Center, followPlayerX: !sideBySide);
            }

            if (clipView != null)
            {
                clipView.SetVisible(clip);
                clipView.SetSlot(sideBySide ? BodySlot.Left : BodySlot.Center, followPlayerX: false);
            }
        }

        public void SetDebug(string text)
        {
            if (debugText == null) return;
            debugText.gameObject.SetActive(!string.IsNullOrEmpty(text));
            debugText.text = text;
        }

        /// <summary>Colore cada letra com as cores dos jogadores (rich text do TMP).</summary>
        public static string Colorful(string text)
        {
            var sb = new StringBuilder();
            int i = 0;
            foreach (char ch in text)
            {
                if (char.IsWhiteSpace(ch))
                {
                    sb.Append(ch);
                    continue;
                }
                Color c = Palette.Players[i++ % Palette.Players.Length];
                sb.Append("<color=#").Append(ColorUtility.ToHtmlStringRGB(c)).Append('>').Append(ch).Append("</color>");
            }
            return sb.ToString();
        }

        // ------------------------------------------------------------------ Animações

        /// <summary>Entrada com "pop": começa maior e volta com leve elástico.</summary>
        private void Pop(RectTransform target, float strength)
        {
            if (target == null) return;
            pops[target] = Time.unscaledTime;
            target.localScale = Vector3.one * (1f + strength);
            popStrength[target] = strength;
        }

        private readonly Dictionary<RectTransform, float> popStrength = new Dictionary<RectTransform, float>();
        private readonly List<RectTransform> finishedPops = new List<RectTransform>();

        private void Update()
        {
            float now = Time.unscaledTime;
            float dt = Time.unscaledDeltaTime;

            // Pops (0,35 s, ease-out-back).
            finishedPops.Clear();
            foreach (KeyValuePair<RectTransform, float> kv in pops)
            {
                float t = Mathf.Clamp01((now - kv.Value) / 0.35f);
                float strength = popStrength.TryGetValue(kv.Key, out float s) ? s : 0.2f;
                float overshoot = 1.70158f;
                float u = t - 1f;
                float eased = 1f + (overshoot + 1f) * u * u * u + overshoot * u * u; // easeOutBack 0→1
                kv.Key.localScale = Vector3.one * Mathf.LerpUnclamped(1f + strength, 1f, eased);
                if (t >= 1f) finishedPops.Add(kv.Key);
            }
            foreach (RectTransform rt in finishedPops)
            {
                rt.localScale = Vector3.one;
                pops.Remove(rt);
            }

            // Título flutuando.
            if (titleText != null)
            {
                RectTransform rt = titleText.rectTransform;
                rt.anchoredPosition = titleBasePosition + (titleFloats ? new Vector2(0f, Mathf.Sin(now * 2f) * 10f) : Vector2.zero);
            }

            // Ponto de gravação pulsando.
            if (recordingDot != null && recordingRoot != null && recordingRoot.activeSelf)
            {
                float pulse = 0.85f + 0.25f * Mathf.Abs(Mathf.Sin(now * 4f));
                recordingDot.rectTransform.localScale = Vector3.one * pulse;
            }

            // Barras enchendo (~1 s).
            if (resultsRoot != null && resultsRoot.activeSelf)
            {
                barAnimationTime += dt;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(barAnimationTime / 1f));
                for (int i = 0; i < resultRows.Length; i++)
                {
                    if (resultRows[i]?.fill == null) continue;
                    resultRows[i].fill.rectTransform.anchorMax = new Vector2(Mathf.Max(0.04f, barTargets[i] * k), 1f);
                }
            }
        }

        private void Awake()
        {
            if (titleText != null) titleBasePosition = titleText.rectTransform.anchoredPosition;
        }

        private static void SetText(TMP_Text text, string value)
        {
            if (text == null) return;
            bool show = !string.IsNullOrEmpty(value);
            text.gameObject.SetActive(show);
            if (show) text.text = value;
        }

        private static void SetActive(GameObject go, bool active)
        {
            if (go != null && go.activeSelf != active) go.SetActive(active);
        }

        // ------------------------------------------------------------------ Montagem do layout

        /// <summary>Referências de uma linha de pontuação.</summary>
        [Serializable]
        public sealed class ScoreRowView
        {
            public GameObject root;
            public Image medal;
            public TMP_Text medalText;
            public TMP_Text nameText;
            public Image fill;
            public TMP_Text scoreText;
            public TMP_Text tagText;
        }

        /// <summary>
        /// Cria toda a hierarquia de UI como filhos deste objeto (que deve ter um Canvas).
        /// Chamado no Editor pelo montador de cena. Apaga filhos anteriores.
        /// </summary>
        /// <param name="backgroundRoot">
        /// Onde criar o fundo (brilhos e bolhas). Para bonecos 3D é um Canvas em Screen Space - Camera
        /// atrás deles; null = no próprio canvas desta UI (bonecos 2D).
        /// </param>
        /// <param name="live3D">Boneco 3D ao vivo já criado na cena; null = cria bonecos 2D na UI.</param>
        /// <param name="clip3D">Boneco 3D do replay/fantasma; null = cria bonecos 2D na UI.</param>
        public HandCursor BuildLayout(UISkin skin, Transform backgroundRoot = null, BodyView live3D = null, BodyView clip3D = null)
        {
            for (int i = transform.childCount - 1; i >= 0; i--) DestroyImmediate(transform.GetChild(i).gameObject);
            if (backgroundRoot != null)
                for (int i = backgroundRoot.childCount - 1; i >= 0; i--) DestroyImmediate(backgroundRoot.GetChild(i).gameObject);

            UIFactory.Font = skin.font;
            Transform root = transform;
            Transform bgRoot = backgroundRoot != null ? backgroundRoot : root;
            Sprite circle = skin.circle;

            // ---------------------------------------------------------- Fundo
            UIFactory.Image("Fundo", bgRoot, Palette.Background).rectTransform.Stretch();
            Image glow = UIFactory.Image("Brilho Central", bgRoot, Palette.BackgroundGlow, skin.soft);
            glow.rectTransform.Place(new Vector2(0.5f, 0.45f), new Vector2(2300f, 1500f));
            accentGlow = UIFactory.Image("Brilho do Jogador", bgRoot, new Color(1f, 1f, 1f, 0.2f), skin.soft);
            accentGlow.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(2000f, 800f), new Vector2(0f, -40f));

            RectTransform bubbles = UIFactory.Rect("Bolhas", bgRoot).Stretch();
            bubbles.gameObject.AddComponent<BackgroundBubbles>().SetSprite(circle);

            // ---------------------------------------------------------- Bonecos
            if (live3D != null && clip3D != null)
            {
                liveView = live3D;
                clipView = clip3D;
            }
            else
            {
                RectTransform stage = UIFactory.Rect("Palco dos Bonecos", root).Stretch();
                clipView = CreateSkeletonView("Boneco Clipe (Replay/Fantasma)", stage, skin);
                liveView = CreateSkeletonView("Boneco Ao Vivo", stage, skin);
            }

            // ---------------------------------------------------------- Moldura arredondada
            frame = UIFactory.Image("Moldura", root, Color.white, skin.ring);
            UISkin.MakeRounded(frame, skin.ring, 46f);
            frame.rectTransform.Stretch(12f, 12f, 12f, 12f);

            // ---------------------------------------------------------- Faixa do jogador
            RectTransform banner = UIFactory.Rect("Faixa do Jogador", root).Place(new Vector2(0.5f, 1f), new Vector2(1060f, 112f), new Vector2(0f, -88f));
            bannerRoot = banner.gameObject;
            UIFactory.Rounded("Sombra", banner, Palette.Shadow, circle, 56f).rectTransform.Stretch(-6f, 4f, -6f, -16f);
            bannerBackground = UIFactory.Rounded("Fundo", banner, Color.gray, circle, 56f);
            bannerBackground.rectTransform.Stretch();
            bannerText = UIFactory.Text("Texto", banner, "Vez do Jogador 1", 58f, Color.white, style: FontStyles.Normal);
            bannerText.rectTransform.Stretch(30f, 0f, 30f, 4f);

            // ---------------------------------------------------------- Títulos
            titleText = UIFactory.Text("Titulo", root, "IMITAÇÕES", 116f, Palette.TextMain, style: FontStyles.Normal);
            titleText.rectTransform.Place(new Vector2(0.5f, 0.79f), new Vector2(1760f, 170f));
            AutoSize(titleText, 56f);
            titleBasePosition = titleText.rectTransform.anchoredPosition;
            subtitleText = UIFactory.Text("Subtitulo", root, "Levante a mão para começar", 50f, Palette.TextSoft, style: FontStyles.Normal);
            subtitleText.rectTransform.Place(new Vector2(0.5f, 0.685f), new Vector2(1700f, 100f));
            AutoSize(subtitleText, 32f);

            // ---------------------------------------------------------- Tema (cartão)
            themeRoot = UIFactory.Rect("Tema", root).Place(new Vector2(0.5f, 0.5f), new Vector2(1500f, 400f));
            themeLabel = UIFactory.Text("Rotulo", themeRoot, "O TEMA É", 46f, Palette.TextSoft, style: FontStyles.Normal);
            themeLabel.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(1200f, 70f), new Vector2(0f, -35f));
            RectTransform card = UIFactory.Rect("Cartao", themeRoot).Place(new Vector2(0.5f, 0.42f), new Vector2(1500f, 270f));
            UIFactory.Rounded("Sombra", card, Palette.Shadow, circle, 64f).rectTransform.Stretch(-6f, 4f, -6f, -18f);
            UIFactory.Rounded("Fundo", card, Palette.Card, circle, 64f).rectTransform.Stretch();
            themeAccent = UIFactory.Rounded("Destaque", card, Color.white, circle, 10f);
            themeAccent.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(360f, 20f), new Vector2(0f, 10f));
            themeText = UIFactory.Text("Texto", card, "Imite uma galinha", 104f, Palette.TextMain, style: FontStyles.Normal);
            themeText.rectTransform.Stretch(40f, 10f, 40f, 30f);
            AutoSize(themeText, 56f);

            // ---------------------------------------------------------- Contagem (medalhão)
            countdownRoot = UIFactory.Rect("Contagem", root).Place(new Vector2(0.5f, 0.47f), new Vector2(380f, 380f));
            UIFactory.Circle("Sombra", countdownRoot, Palette.Shadow, circle).rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(390f, 390f), new Vector2(0f, -14f));
            UIFactory.Circle("Fundo", countdownRoot, Palette.Card, circle).rectTransform.Stretch();
            countdownRing = UIFactory.Circle("Anel", countdownRoot, Color.white, skin.ring);
            countdownRing.rectTransform.Stretch();
            countdownText = UIFactory.Text("Numero", countdownRoot, "3", 230f, Palette.TextMain, style: FontStyles.Normal);
            countdownText.rectTransform.Stretch(0f, 0f, 0f, 20f);

            // ---------------------------------------------------------- Gravação
            RectTransform rec = UIFactory.Rect("Gravacao", root).Place(new Vector2(0.5f, 0.1f), new Vector2(1300f, 130f));
            recordingRoot = rec.gameObject;
            recordingDot = UIFactory.Image("Ponto REC", rec, Palette.Record, circle);
            recordingDot.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(34f, 34f), new Vector2(-330f, -34f));
            recordingText = UIFactory.Text("Texto", rec, "GRAVANDO 5.0s", 50f, Palette.TextMain, TextAlignmentOptions.Left, FontStyles.Normal);
            recordingText.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(900f, 70f), new Vector2(150f, -34f));
            Image track = UIFactory.Rounded("Trilho", rec, Palette.Track, circle, 24f);
            track.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(1300f, 48f), new Vector2(0f, 28f));
            Image fill = UIFactory.Rounded("Progresso", track.transform, Palette.Record, circle, 20f);
            recordingFill = fill.rectTransform;
            recordingFill.anchorMin = Vector2.zero;
            recordingFill.anchorMax = new Vector2(0.3f, 1f);
            recordingFill.offsetMin = new Vector2(4f, 4f);
            recordingFill.offsetMax = new Vector2(-4f, -4f);

            // ---------------------------------------------------------- Botões de quantidade
            RectTransform count = UIFactory.Rect("Botoes Quantidade", root).Stretch();
            playerCountRoot = count.gameObject;
            playerCountButtons = new GestureButton[3];
            for (int i = 0; i < 3; i++)
            {
                int players = i + 2;
                GestureButton b = CreateButton($"Botao {players} Jogadores", count, new Vector2(0.27f + 0.23f * i, 0.38f),
                    new Vector2(380f, 330f), 58f, $"<size=170>{players}</size>\n<size=44>jogadores</size>", 170f, skin);
                AddPlayerDots(b.transform, players, circle);
                playerCountButtons[i] = b;
            }

            // ---------------------------------------------------------- Botões do placar final
            RectTransform finals = UIFactory.Rect("Botoes Placar Final", root).Stretch();
            finalButtonsRoot = finals.gameObject;
            playAgainButton = CreateButton("Botao Jogar Novamente", finals, new Vector2(0.3f, 0.1f), new Vector2(620f, 160f), 80f, "Jogar novamente", 64f, skin);
            menuButton = CreateButton("Botao Menu", finals, new Vector2(0.7f, 0.1f), new Vector2(620f, 160f), 80f, "Menu", 64f, skin);

            // ---------------------------------------------------------- Pontuação
            RectTransform results = UIFactory.Rect("Pontuacao", root).Place(new Vector2(0.5f, 0.45f), new Vector2(1500f, 4 * 118f + 3 * 20f));
            resultsRoot = results.gameObject;
            resultRows = new ScoreRowView[4];
            for (int i = 0; i < 4; i++) resultRows[i] = CreateScoreRow(i, results, circle);

            // ---------------------------------------------------------- Aviso
            warningRoot = UIFactory.Rect("Aviso", root).Place(new Vector2(0.5f, 0f), new Vector2(1500f, 96f), new Vector2(0f, 76f));
            UIFactory.Rounded("Sombra", warningRoot, Palette.Shadow, circle, 48f).rectTransform.Stretch(-4f, 2f, -4f, -12f);
            UIFactory.Rounded("Fundo", warningRoot, Palette.Warning, circle, 48f).rectTransform.Stretch();
            Image icon = UIFactory.Image("Icone", warningRoot, Palette.Outline, circle);
            icon.rectTransform.Place(new Vector2(0f, 0.5f), new Vector2(66f, 66f), new Vector2(52f, 0f));
            UIFactory.Text("!", icon.transform, "!", 50f, Palette.Warning, style: FontStyles.Normal).rectTransform.Stretch(0f, 0f, 0f, 4f);
            warningText = UIFactory.Text("Texto", warningRoot, "Aviso", 42f, Palette.Outline, style: FontStyles.Normal);
            warningText.rectTransform.Stretch(100f, 0f, 30f, 2f);
            AutoSize(warningText, 26f);

            // ---------------------------------------------------------- Debug (fonte padrão, discreta)
            UIFactory.Font = null;
            debugText = UIFactory.Text("Debug", root, "", 15f, new Color(1f, 1f, 1f, 0.45f), TextAlignmentOptions.TopLeft, FontStyles.Normal);
            debugText.rectTransform.Place(new Vector2(0f, 1f), new Vector2(390f, 120f), new Vector2(230f, -95f));
            UIFactory.Font = skin.font;

            // ---------------------------------------------------------- Cursor (por cima de tudo)
            RectTransform cursorRoot = UIFactory.Rect("Cursor", root).Place(Vector2.zero, new Vector2(124f, 124f));
            UIFactory.Image("Sombra", cursorRoot, new Color(0f, 0f, 0f, 0.5f), skin.soft).rectTransform.Stretch(-26f, -20f, -26f, -32f);
            UIFactory.Circle("Anel Fundo", cursorRoot, new Color(1f, 1f, 1f, 0.28f), skin.ring).rectTransform.Stretch();
            Image ring = UIFactory.FilledImage("Anel Progresso", cursorRoot, Color.white, skin.ring, UnityEngine.UI.Image.FillMethod.Radial360);
            ring.rectTransform.Stretch();
            UIFactory.Image("Borda do Ponto", cursorRoot, Color.white, circle).rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(58f, 58f));
            Image dot = UIFactory.Image("Ponto", cursorRoot, Color.white, circle);
            dot.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(44f, 44f));

            HandCursor cursor = GetComponent<HandCursor>();
            if (cursor == null) cursor = gameObject.AddComponent<HandCursor>();
            cursor.SetupVisuals(cursorRoot, dot, ring);

            HideAll();
            ShowWarning(null);
            SetBanner(null);
            SetSkeletonLayout(SkeletonLayout.Hidden);
            return cursor;
        }

        /// <summary>Diminui a fonte (até o mínimo) quando o texto não cabe no retângulo.</summary>
        private static void AutoSize(TMP_Text text, float min)
        {
            text.fontSizeMax = text.fontSize;
            text.fontSizeMin = min;
            text.enableAutoSizing = true;
        }

        private static SkeletonView CreateSkeletonView(string name, Transform parent, UISkin skin)
        {
            RectTransform rt = UIFactory.Rect(name, parent).Place(new Vector2(0.5f, 0.5f), new Vector2(10f, 10f));
            var view = rt.gameObject.AddComponent<SkeletonView>();
            view.SetSprites(skin.circle, skin.soft);
            return view;
        }

        private static GestureButton CreateButton(string name, Transform parent, Vector2 anchor, Vector2 size, float radius,
            string text, float fontSize, UISkin skin)
            => CreateButton(name, parent, anchor, size, radius, text, fontSize, skin.circle);

        /// <summary>Botão de hover no estilo do jogo (também usado pelas telas montadas em tempo de execução).</summary>
        public static GestureButton CreateButton(string name, Transform parent, Vector2 anchor, Vector2 size, float radius,
            string text, float fontSize, Sprite circle)
        {
            RectTransform root = UIFactory.Rect(name, parent).Place(anchor, size);

            // Contorno de hover (atrás), sombra, fundo, preenchimento de progresso, texto.
            Image outline = UIFactory.Rounded("Contorno Hover", root, Color.white, circle, radius + 8f);
            outline.rectTransform.Stretch(-8f, -8f, -8f, -8f);
            outline.enabled = false;
            UIFactory.Rounded("Sombra", root, Palette.Shadow, circle, radius).rectTransform.Stretch(-4f, 4f, -4f, -16f);
            Image bg = UIFactory.Rounded("Fundo", root, Palette.Card, circle, radius);
            bg.rectTransform.Stretch();

            var mask = bg.gameObject.AddComponent<RectMask2D>(); // recorta o preenchimento nas bordas
            mask.softness = Vector2Int.zero;
            Image fill = UIFactory.Rounded("Progresso", bg.transform, new Color(1f, 1f, 1f, 0.5f), circle, radius);
            fill.rectTransform.Stretch();
            fill.enabled = false;

            TMP_Text label = UIFactory.Text("Texto", root, text, fontSize, Palette.TextMain, style: FontStyles.Normal);
            label.rectTransform.Stretch(12f, 12f, 12f, 12f);
            label.lineSpacing = -20f;

            var button = root.gameObject.AddComponent<GestureButton>();
            button.SetupVisuals(label, bg, fill, outline);
            return button;
        }

        /// <summary>Bolinhas com as cores dos N jogadores, no pé do botão de quantidade.</summary>
        private static void AddPlayerDots(Transform button, int players, Sprite circle)
        {
            const float size = 30f, gap = 14f;
            float width = players * size + (players - 1) * gap;
            for (int p = 0; p < players; p++)
            {
                Image dot = UIFactory.Image($"Jogador {p + 1}", button, Palette.Players[p], circle);
                dot.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(size, size),
                    new Vector2(-width * 0.5f + size * 0.5f + p * (size + gap), 40f));
            }
        }

        private const float RowHeight = 118f;
        private const float RowSpacing = 20f;

        private static ScoreRowView CreateScoreRow(int index, Transform parent, Sprite circle)
        {
            const float rowHeight = RowHeight, spacing = RowSpacing;
            RectTransform row = UIFactory.Rect($"Linha {index + 1}", parent);
            row.anchorMin = row.anchorMax = new Vector2(0.5f, 1f);
            row.pivot = new Vector2(0.5f, 0.5f);
            row.sizeDelta = new Vector2(1500f, rowHeight);
            row.anchoredPosition = new Vector2(0f, -rowHeight * 0.5f - index * (rowHeight + spacing));

            UIFactory.Rounded("Sombra", row, Palette.Shadow, circle, rowHeight * 0.5f).rectTransform.Stretch(-4f, 4f, -4f, -12f);
            UIFactory.Rounded("Fundo", row, Palette.Card, circle, rowHeight * 0.5f).rectTransform.Stretch();

            Image medal = UIFactory.Image("Medalha", row, Color.white, circle);
            medal.rectTransform.Place(new Vector2(0f, 0.5f), new Vector2(90f, 90f), new Vector2(62f, 0f));
            TMP_Text medalText = UIFactory.Text("Posicao", medal.transform, "1", 44f, Color.white, style: FontStyles.Normal);
            medalText.rectTransform.Stretch(0f, 0f, 0f, 4f);

            TMP_Text nameText = UIFactory.Text("Nome", row, "Jogador 1", 50f, Palette.TextMain, TextAlignmentOptions.Left, FontStyles.Normal);
            nameText.rectTransform.Place(new Vector2(0f, 0.5f), new Vector2(330f, rowHeight), new Vector2(290f, 2f));

            Image track = UIFactory.Rounded("Trilho", row, Palette.Track, circle, 22f);
            track.rectTransform.Place(new Vector2(0f, 0.5f), new Vector2(760f, 44f), new Vector2(470f + 380f, 0f));
            Image fill = UIFactory.Rounded("Barra", track.transform, Color.white, circle, 18f);
            RectTransform fillRect = fill.rectTransform;
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = new Vector2(0.5f, 1f);
            fillRect.offsetMin = new Vector2(4f, 4f);
            fillRect.offsetMax = new Vector2(-4f, -4f);

            TMP_Text scoreText = UIFactory.Text("Pontos", row, "0", 64f, Palette.TextMain, TextAlignmentOptions.Right, FontStyles.Normal);
            scoreText.rectTransform.Place(new Vector2(1f, 0.5f), new Vector2(220f, rowHeight), new Vector2(-150f, 10f));
            TMP_Text tagText = UIFactory.Text("Etiqueta", row, "", 26f, Palette.TextSoft, TextAlignmentOptions.Right, FontStyles.Normal);
            tagText.rectTransform.Place(new Vector2(1f, 0.5f), new Vector2(220f, 36f), new Vector2(-150f, -38f));

            return new ScoreRowView
            {
                root = row.gameObject, medal = medal, medalText = medalText, nameText = nameText,
                fill = fill, scoreText = scoreText, tagText = tagText
            };
        }
    }
}
