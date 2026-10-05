using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Imitacoes.UI
{
    /// <summary>
    /// Atalhos para criar elementos de uGUI por código (usados pelo GameUI.BuildLayout,
    /// que o montador de cena chama no Editor). Tudo em referência 1920×1080.
    /// </summary>
    public static class UIFactory
    {
        /// <summary>Fonte usada pelos textos criados a seguir (null = padrão do TMP).</summary>
        public static TMP_FontAsset Font;

        /// <summary>Retângulo arredondado (ou cápsula, se altura = 2 × raio).</summary>
        public static Image Rounded(string name, Transform parent, Color color, Sprite circle, float radius)
        {
            Image image = Image(name, parent, color);
            UISkin.MakeRounded(image, circle, radius);
            return image;
        }

        /// <summary>
        /// Sombra suave atrás de um elemento: cópia arredondada, escura e deslocada para baixo.
        /// Crie ANTES do elemento para ficar atrás dele.
        /// </summary>
        public static Image Shadow(Transform parent, Sprite circle, Vector2 anchor, Vector2 size, Vector2 offset, float radius)
        {
            Image shadow = Rounded("Sombra", parent, Palette.Shadow, circle, radius);
            shadow.rectTransform.Place(anchor, size + new Vector2(8f, 8f), offset + new Vector2(0f, -10f));
            return shadow;
        }

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            return rt;
        }

        /// <summary>Estica para ocupar o pai inteiro, com margens (px).</summary>
        public static RectTransform Stretch(this RectTransform rt, float left = 0f, float top = 0f, float right = 0f, float bottom = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        /// <summary>Âncora num ponto relativo do pai (0..1), com tamanho fixo e deslocamento.</summary>
        public static RectTransform Place(this RectTransform rt, Vector2 anchor, Vector2 size, Vector2 offset = default)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = offset;
            return rt;
        }

        public static Image Image(string name, Transform parent, Color color, Sprite sprite = null)
        {
            RectTransform rt = Rect(name, parent);
            var image = rt.gameObject.AddComponent<Image>();
            image.color = color;
            image.sprite = sprite;
            image.raycastTarget = false;
            if (sprite != null && sprite.border != Vector4.zero) image.type = UnityEngine.UI.Image.Type.Sliced;
            return image;
        }

        /// <summary>Círculo perfeito (Image simples, sem fatiamento), de qualquer tamanho.</summary>
        public static Image Circle(string name, Transform parent, Color color, Sprite sprite)
        {
            Image image = Image(name, parent, color, sprite);
            image.type = UnityEngine.UI.Image.Type.Simple;
            return image;
        }

        /// <summary>Image que "enche" (barra ou anel). Precisa de sprite para o modo Filled funcionar.</summary>
        public static Image FilledImage(string name, Transform parent, Color color, Sprite sprite, Image.FillMethod method)
        {
            Image image = Image(name, parent, color, sprite);
            image.type = UnityEngine.UI.Image.Type.Filled;
            image.fillMethod = method;
            image.fillOrigin = method == UnityEngine.UI.Image.FillMethod.Radial360 ? (int)UnityEngine.UI.Image.Origin360.Top : 0;
            image.fillClockwise = true;
            image.fillAmount = 0f;
            return image;
        }

        public static TextMeshProUGUI Text(string name, Transform parent, string text, float size, Color color,
            TextAlignmentOptions alignment = TextAlignmentOptions.Center, FontStyles style = FontStyles.Bold)
        {
            RectTransform rt = Rect(name, parent);
            var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = alignment;
            tmp.fontStyle = style;
            tmp.raycastTarget = false;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.overflowMode = TextOverflowModes.Overflow;
            TMP_FontAsset font = Font != null ? Font : DefaultFont();
            if (font != null) tmp.font = font;
            return tmp;
        }

        /// <summary>Fonte padrão do TextMeshPro, ou null se o TMP Essentials ainda não foi importado.</summary>
        public static TMP_FontAsset DefaultFont()
        {
            try { return TMP_Settings.defaultFontAsset; }
            catch (System.Exception) { return null; } // TMP Settings.asset ainda não existe
        }
    }
}
