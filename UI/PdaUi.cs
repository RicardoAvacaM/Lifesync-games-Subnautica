using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace LifeSyncGamesSubnautica.UI
{
    /// <summary>
    /// Fábrica de elementos uGUI con estética PDA. Todo se crea por código (sin prefabs ni .asset).
    /// </summary>
    internal static class PdaUi
    {
        internal static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            // Por defecto, estirar a todo el padre. Los llamadores que necesiten otra cosa lo sobreescriben.
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return rt;
        }

        /// <summary>Estira el RectTransform a su padre con márgenes (left, top, right, bottom).</summary>
        internal static void Stretch(RectTransform rt, float left, float top, float right, float bottom)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
        }

        internal static Image CreatePanel(string name, Transform parent, Color color, bool rounded = true)
        {
            var rt = CreateRect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = rounded ? PdaTheme.PanelSprite : PdaTheme.FlatSprite;
            img.type = Image.Type.Sliced;
            img.color = color;
            return img;
        }

        internal static TextMeshProUGUI CreateLabel(
            string name, Transform parent, string text, int fontSize, Color color,
            TextAlignmentOptions align = TextAlignmentOptions.TopLeft, bool wrap = true)
        {
            var rt = CreateRect(name, parent);
            var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = color;
            tmp.alignment = align;
            tmp.enableWordWrapping = wrap;
            tmp.richText = true;
            tmp.overflowMode = TextOverflowModes.Overflow;
            var font = PdaTheme.Font;
            if (font != null)
            {
                tmp.font = font;
            }

            return tmp;
        }

        /// <summary>Crea un botón PDA con su label TMP centrado. Devuelve el Button (el label es el primer hijo TMP).</summary>
        internal static Button CreateButton(string name, Transform parent, string text, UnityAction onClick, int fontSize = 16)
        {
            var rt = CreateRect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = PdaTheme.SoftSprite;
            img.type = Image.Type.Sliced;
            img.color = PdaTheme.ButtonNormal;

            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.25f, 1.25f, 1.25f, 1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.6f);
            colors.fadeDuration = 0.08f;
            btn.colors = colors;
            if (onClick != null)
            {
                btn.onClick.AddListener(onClick);
            }

            var label = CreateLabel("Label", rt, text, fontSize, PdaTheme.TextPrimary, TextAlignmentOptions.Center, false);
            Stretch(label.rectTransform, 8, 2, 8, 2);

            // Línea de acento a la izquierda (toque PDA).
            var accent = CreatePanel("Accent", rt, PdaTheme.Accent, false);
            accent.rectTransform.anchorMin = new Vector2(0f, 0.15f);
            accent.rectTransform.anchorMax = new Vector2(0f, 0.85f);
            accent.rectTransform.pivot = new Vector2(0f, 0.5f);
            accent.rectTransform.sizeDelta = new Vector2(3f, 0f);
            accent.rectTransform.anchoredPosition = new Vector2(3f, 0f);
            accent.raycastTarget = false;

            return btn;
        }

        /// <summary>Cambia el color base de un botón (para estados enabled/disabled/activo).</summary>
        internal static void SetButtonColor(Button btn, Color color)
        {
            if (btn != null && btn.targetGraphic is Image img)
            {
                img.color = color;
            }
        }

        internal static TMP_Text ButtonLabel(Button btn)
        {
            return btn != null ? btn.GetComponentInChildren<TMP_Text>() : null;
        }

        internal static TMP_InputField CreateInput(
            string name, Transform parent, string placeholder, bool password)
        {
            var rt = CreateRect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = PdaTheme.SoftSprite;
            img.type = Image.Type.Sliced;
            img.color = new Color(0.02f, 0.06f, 0.08f, 0.95f);

            var input = rt.gameObject.AddComponent<TMP_InputField>();

            var textArea = CreateRect("TextArea", rt);
            Stretch(textArea, 10, 4, 10, 4);
            var mask = textArea.gameObject.AddComponent<RectMask2D>();

            var ph = CreateLabel("Placeholder", textArea, placeholder, 15, PdaTheme.TextMuted, TextAlignmentOptions.Left, false);
            Stretch(ph.rectTransform, 0, 0, 0, 0);
            ph.fontStyle = FontStyles.Italic;

            var txt = CreateLabel("Text", textArea, string.Empty, 15, PdaTheme.TextPrimary, TextAlignmentOptions.Left, false);
            Stretch(txt.rectTransform, 0, 0, 0, 0);

            input.textViewport = textArea;
            input.textComponent = txt;
            input.placeholder = ph;
            input.fontAsset = PdaTheme.Font;
            input.pointSize = 15;
            input.customCaretColor = true;
            input.caretColor = PdaTheme.Accent;
            input.selectionColor = PdaTheme.AccentDim;
            input.lineType = TMP_InputField.LineType.SingleLine;
            if (password)
            {
                input.contentType = TMP_InputField.ContentType.Password;
            }

            return input;
        }

        /// <summary>
        /// Crea un ScrollRect vertical con su content (VerticalLayoutGroup + ContentSizeFitter).
        /// Devuelve el RectTransform del content donde añadir filas.
        /// </summary>
        internal static RectTransform CreateScroll(string name, Transform parent, out ScrollRect scrollRect)
        {
            var viewportImg = CreatePanel(name, parent, new Color(0.02f, 0.05f, 0.07f, 0.6f), false);
            var viewport = viewportImg.rectTransform;
            viewport.gameObject.AddComponent<RectMask2D>();
            scrollRect = viewport.gameObject.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 24f;

            var content = CreateRect("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = new Vector2(0f, 0f);
            content.offsetMax = new Vector2(0f, 0f);

            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 6f;
            layout.padding = new RectOffset(6, 6, 6, 6);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect.viewport = viewport;
            scrollRect.content = content;
            return content;
        }

        /// <summary>Barra de progreso 0..1 con fondo (track) y relleno con color.</summary>
        internal static Image CreateBar(string name, Transform parent, float value01, Color fillColor)
        {
            var track = CreatePanel(name, parent, PdaTheme.BarTrack);
            Stretch(track.rectTransform, 0, 0, 0, 0);
            track.raycastTarget = false;

            var fill = CreatePanel("Fill", track.rectTransform, fillColor);
            fill.rectTransform.anchorMin = new Vector2(0f, 0f);
            fill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(value01), 1f);
            fill.rectTransform.offsetMin = new Vector2(2f, 2f);
            fill.rectTransform.offsetMax = new Vector2(-2f, -2f);
            fill.raycastTarget = false;
            return fill;
        }

        /// <summary>Añade LayoutElement con altura preferida (para filas dentro de un VerticalLayoutGroup).</summary>
        internal static LayoutElement SetPreferredHeight(GameObject go, float height)
        {
            var le = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
            le.preferredHeight = height;
            le.minHeight = height;
            return le;
        }

        /// <summary>Checkbox con etiqueta a la derecha (estilo PDA). <paramref name="rowRect"/> es la fila para PlaceTop.</summary>
        internal static Toggle CreateToggle(
            string name,
            Transform parent,
            string label,
            bool isOn,
            UnityAction<bool> onValueChanged,
            out RectTransform rowRect)
        {
            rowRect = CreateRect(name, parent);
            rowRect.anchorMin = new Vector2(0f, 1f);
            rowRect.anchorMax = new Vector2(1f, 1f);
            rowRect.pivot = new Vector2(0.5f, 1f);
            rowRect.sizeDelta = new Vector2(0f, 40f);

            var boxRt = CreateRect("Box", rowRect);
            boxRt.anchorMin = new Vector2(0f, 0.5f);
            boxRt.anchorMax = new Vector2(0f, 0.5f);
            boxRt.pivot = new Vector2(0f, 0.5f);
            boxRt.anchoredPosition = new Vector2(10f, 0f);
            boxRt.sizeDelta = new Vector2(28f, 28f);

            var border = CreatePanel("Border", boxRt, isOn ? PdaTheme.Accent : PdaTheme.AccentDim, true);
            Stretch(border.rectTransform, 0f, 0f, 0f, 0f);
            border.raycastTarget = false;

            var inner = CreatePanel("Inner", boxRt, PdaTheme.Panel, true);
            Stretch(inner.rectTransform, 2f, 2f, 2f, 2f);

            var check = CreateLabel("Check", boxRt, "\u2713", 18, PdaTheme.Accent, TextAlignmentOptions.Center, false);
            check.fontStyle = FontStyles.Bold;
            check.raycastTarget = false;
            Stretch(check.rectTransform, 4f, 2f, 4f, 2f);

            var toggle = boxRt.gameObject.AddComponent<Toggle>();
            toggle.targetGraphic = inner;
            toggle.graphic = check;
            toggle.isOn = isOn;
            var toggleColors = toggle.colors;
            toggleColors.normalColor = Color.white;
            toggleColors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
            toggleColors.pressedColor = new Color(0.88f, 0.88f, 0.88f, 1f);
            toggleColors.selectedColor = new Color(1.08f, 1.08f, 1.08f, 1f);
            toggleColors.fadeDuration = 0.06f;
            toggle.colors = toggleColors;

            toggle.onValueChanged.AddListener(v =>
            {
                border.color = v ? PdaTheme.Accent : PdaTheme.AccentDim;
                onValueChanged?.Invoke(v);
            });

            var lbl = CreateLabel("Label", rowRect, label, 14, PdaTheme.TextPrimary, TextAlignmentOptions.Left, true);
            lbl.raycastTarget = false;
            lbl.rectTransform.anchorMin = new Vector2(0f, 0f);
            lbl.rectTransform.anchorMax = new Vector2(1f, 1f);
            lbl.rectTransform.offsetMin = new Vector2(46f, 0f);
            lbl.rectTransform.offsetMax = new Vector2(-8f, 0f);

            var hit = CreatePanel("RowHit", rowRect, new Color(0f, 0f, 0f, 0.001f), false);
            Stretch(hit.rectTransform, 0f, 0f, 0f, 0f);
            var rowBtn = hit.gameObject.AddComponent<Button>();
            rowBtn.targetGraphic = hit;
            var colors = rowBtn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 1f, 1f, 0.06f);
            colors.pressedColor = new Color(1f, 1f, 1f, 0.12f);
            colors.fadeDuration = 0.05f;
            rowBtn.colors = colors;
            rowBtn.onClick.AddListener(() => toggle.isOn = !toggle.isOn);

            return toggle;
        }
    }
}
