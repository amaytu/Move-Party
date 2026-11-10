using System;
using Imitacoes.Interaction;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Imitacoes.UI
{
    /// <summary>
    /// Menu de pausa: escurece a tela e mostra os botões Continuar, Pular jogador e Voltar ao menu
    /// (acionados por hover, como os outros). Montado em tempo de execução pelo <see cref="GameUI"/>.
    /// </summary>
    public sealed class PauseMenu : MonoBehaviour
    {
        public event Action ResumeRequested;
        public event Action SkipRequested;
        public event Action MenuRequested;

        private GestureButton resumeButton;
        private GestureButton skipButton;
        private GestureButton menuButton;
        private TMP_Text subtitle;

        public bool IsOpen => gameObject.activeSelf;

        public static PauseMenu Create(Transform canvas, TMP_FontAsset font, Sprite circle)
        {
            RectTransform root = UIFactory.Rect("Pausa", canvas).Stretch();
            var menu = root.gameObject.AddComponent<PauseMenu>();
            menu.Build(font, circle);
            root.gameObject.SetActive(false);
            return menu;
        }

        private void Build(TMP_FontAsset font, Sprite circle)
        {
            TMP_FontAsset previous = UIFactory.Font;
            if (font != null) UIFactory.Font = font;

            UIFactory.Image("Escurecer", transform, new Color(0.02f, 0.02f, 0.06f, 0.95f)).rectTransform.Stretch();

            TMP_Text title = UIFactory.Text("Título", transform, "Jogo pausado", 110f, Palette.TextMain, style: FontStyles.Normal);
            title.rectTransform.Place(new Vector2(0.5f, 0.8f), new Vector2(1400f, 150f));
            subtitle = UIFactory.Text("Subtítulo", transform, "Pare a mão sobre uma opção", 46f, Palette.TextSoft, style: FontStyles.Normal);
            subtitle.rectTransform.Place(new Vector2(0.5f, 0.69f), new Vector2(1400f, 80f));

            resumeButton = GameUI.CreateButton("Continuar", transform, new Vector2(0.5f, 0.53f), new Vector2(720f, 140f), 70f, "Continuar", 64f, circle);
            skipButton = GameUI.CreateButton("Pular Jogador", transform, new Vector2(0.5f, 0.37f), new Vector2(720f, 140f), 70f, "Pular jogador", 64f, circle);
            menuButton = GameUI.CreateButton("Voltar ao Menu", transform, new Vector2(0.5f, 0.21f), new Vector2(720f, 140f), 70f, "Voltar ao menu", 64f, circle);

            resumeButton.onSelected.AddListener(() => ResumeRequested?.Invoke());
            skipButton.onSelected.AddListener(() => SkipRequested?.Invoke());
            menuButton.onSelected.AddListener(() => MenuRequested?.Invoke());

            UIFactory.Font = previous;
        }

        /// <param name="skipLabel">Texto do botão de pular (ex.: "Pular Jogador 3"); null esconde o botão.</param>
        public void Open(Color accent, string skipLabel)
        {
            gameObject.SetActive(true);
            transform.SetAsLastSibling();

            resumeButton.SetAccentColor(accent);
            skipButton.SetAccentColor(accent);
            menuButton.SetAccentColor(accent);

            bool canSkip = !string.IsNullOrEmpty(skipLabel);
            skipButton.gameObject.SetActive(canSkip);
            if (canSkip) skipButton.Label = skipLabel;

            // Sem o botão de pular, o "Voltar ao menu" sobe para não deixar buraco.
            ((RectTransform)menuButton.transform).anchorMin = ((RectTransform)menuButton.transform).anchorMax =
                new Vector2(0.5f, canSkip ? 0.21f : 0.37f);
        }

        public void Close()
        {
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }
    }
}
