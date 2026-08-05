using System;
using UnityEngine;
using UnityEngine.UI;

namespace OperatorPhone.UI
{
    /// <summary>
    /// Shared uGUI construction. Everything is code-built (no AssetBundles), so keeping
    /// the boilerplate here is what keeps the app classes readable.
    /// </summary>
    internal static class Ui
    {
        // Palette. Deliberately small — the phone should read as one device.
        public static readonly Color Bg = new Color(0.09f, 0.10f, 0.11f, 1f);
        public static readonly Color Panel = new Color(0.13f, 0.14f, 0.16f, 1f);
        public static readonly Color PanelHi = new Color(0.17f, 0.19f, 0.21f, 1f);
        public static readonly Color Text = new Color(0.92f, 0.93f, 0.94f, 1f);
        public static readonly Color Dim = new Color(0.55f, 0.58f, 0.62f, 1f);
        public static readonly Color Accent = new Color(0.35f, 0.82f, 0.55f, 1f);
        public static readonly Color AccentDark = new Color(0.16f, 0.34f, 0.24f, 1f);
        public static readonly Color Danger = new Color(0.85f, 0.45f, 0.45f, 1f);
        public static readonly Color BubbleMine = new Color(0.18f, 0.36f, 0.26f, 1f);
        public static readonly Color BubbleTheirs = new Color(0.16f, 0.18f, 0.21f, 1f);

        public static Font Font;   // set once by PhoneShell after resolution

        public static GameObject Rect(string name, Transform parent, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();
            var img = go.AddComponent<Image>();
            img.color = color;
            return go;
        }

        public static GameObject Invisible(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();
            return go;
        }

        public static Text Label(string name, Transform parent, string content, int size,
            TextAnchor anchor, Color color, bool wrap = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();

            var t = go.AddComponent<Text>();
            t.font = Font;
            t.fontSize = size;
            t.alignment = anchor;
            t.color = color;
            t.text = content ?? "";
            t.horizontalOverflow = wrap ? HorizontalWrapMode.Wrap : HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        public static Button TextButton(string name, Transform parent, string label,
            Color bg, Color fg, int fontSize, Action onClick)
        {
            var go = Rect(name, parent, bg);
            var txt = Label(name + "_Label", go.transform, label, fontSize,
                TextAnchor.MiddleCenter, fg);
            Fill(txt.GetComponent<RectTransform>());

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = go.GetComponent<Image>();
            var colors = btn.colors;
            colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
            colors.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
            btn.colors = colors;

            if (onClick != null)
                btn.onClick.AddListener((UnityEngine.Events.UnityAction)onClick);
            return btn;
        }

        /// <summary>
        /// Vertical scroll list. Returns the content transform to parent rows under.
        /// Content grows downward; a VerticalLayoutGroup + ContentSizeFitter drive
        /// sizing so rows only need a LayoutElement with preferredHeight.
        /// </summary>
        public static RectTransform ScrollList(string name, Transform parent, out ScrollRect scroll)
        {
            var viewportGo = Rect(name, parent, Bg);
            var viewport = viewportGo.GetComponent<RectTransform>();
            viewportGo.AddComponent<RectMask2D>();

            scroll = viewportGo.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 25f;

            var contentGo = Invisible(name + "_Content", viewportGo.transform);
            var content = contentGo.GetComponent<RectTransform>();
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);

            var layout = contentGo.AddComponent<VerticalLayoutGroup>();
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.spacing = 2f;
            layout.padding = new RectOffset(8, 8, 8, 8);

            var fitter = contentGo.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.content = content;
            scroll.viewport = viewport;
            return content;
        }

        public static InputField TextInput(string name, Transform parent, string placeholder)
        {
            var go = Rect(name, parent, PanelHi);

            var textGo = Invisible(name + "_Text", go.transform);
            var text = textGo.AddComponent<Text>();
            text.font = Font;
            text.fontSize = 14;
            text.color = Text;
            text.alignment = TextAnchor.MiddleLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.supportRichText = false;
            var tr = textGo.GetComponent<RectTransform>();
            Fill(tr);
            tr.offsetMin = new Vector2(10f, 4f);
            tr.offsetMax = new Vector2(-10f, -4f);

            var phGo = Invisible(name + "_Placeholder", go.transform);
            var ph = phGo.AddComponent<Text>();
            ph.font = Font;
            ph.fontSize = 14;
            ph.fontStyle = FontStyle.Italic;
            ph.color = Dim;
            ph.alignment = TextAnchor.MiddleLeft;
            ph.text = placeholder;
            ph.raycastTarget = false;
            var pr = phGo.GetComponent<RectTransform>();
            Fill(pr);
            pr.offsetMin = new Vector2(10f, 4f);
            pr.offsetMax = new Vector2(-10f, -4f);

            var field = go.AddComponent<InputField>();
            field.targetGraphic = go.GetComponent<Image>();
            field.textComponent = text;
            field.placeholder = ph;
            field.lineType = InputField.LineType.SingleLine;
            field.characterLimit = 500;
            return field;
        }

        public static void Fill(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        public static LayoutElement FixedHeight(GameObject go, float h)
        {
            var le = go.AddComponent<LayoutElement>();
            le.preferredHeight = h;
            le.minHeight = h;
            return le;
        }
    }
}
