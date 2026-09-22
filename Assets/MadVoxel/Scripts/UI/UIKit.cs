using UnityEngine;
using UnityEngine.UI;

namespace MadVoxel.UI
{
    /// <summary>
    /// Small builders for the runtime uGUI. Everything is code-built so the project
    /// carries no prefab assets that have to be kept in sync with the scripts.
    /// </summary>
    public static class UIKit
    {
        public static readonly Color Panel = new Color(0.07f, 0.07f, 0.075f, 0.93f);
        public static readonly Color PanelSoft = new Color(0.11f, 0.11f, 0.12f, 0.88f);
        public static readonly Color Slot = new Color(0.17f, 0.17f, 0.18f, 0.95f);
        public static readonly Color SlotHot = new Color(0.30f, 0.27f, 0.20f, 0.98f);
        public static readonly Color Accent = new Color(0.88f, 0.55f, 0.20f);
        public static readonly Color TextMain = new Color(0.89f, 0.88f, 0.85f);
        public static readonly Color TextDim = new Color(0.62f, 0.61f, 0.58f);

        static Font _font;

        public static Font Font
        {
            get
            {
                if (_font == null)
                {
                    _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    if (_font == null) _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                if (_font == null) Debug.LogWarning("MadVoxel: no built-in font found; UI text will not render.");
                }
                return _font;
            }
        }

        public static Canvas CreateCanvas(string name, int sortOrder, Transform parent = null)
        {
            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            if (parent != null) go.transform.SetParent(parent, false);

            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortOrder;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            return canvas;
        }

        public static RectTransform Rect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static Image Image(Transform parent, string name, Color colour)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = colour;
            return image;
        }

        public static Text Label(Transform parent, string name, string text, int size, TextAnchor anchor, Color colour)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);

            var label = go.GetComponent<Text>();
            label.font = Font;
            label.text = text;
            label.fontSize = size;
            label.alignment = anchor;
            label.color = colour;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.raycastTarget = false;
            return label;
        }

        public static Button Button(Transform parent, string name, string text, int fontSize = 26)
        {
            var image = Image(parent, name, PanelSoft);
            var button = image.gameObject.AddComponent<Button>();

            var colours = button.colors;
            colours.normalColor = Color.white;
            colours.highlightedColor = new Color(1.25f, 1.15f, 0.95f);
            colours.pressedColor = new Color(0.8f, 0.7f, 0.5f);
            button.colors = colours;

            var label = Label(image.transform, "Label", text, fontSize, TextAnchor.MiddleCenter, TextMain);
            Stretch(label.rectTransform);
            return button;
        }

        public static InputField Input(Transform parent, string name, string placeholder, string value = "")
        {
            var image = Image(parent, name, new Color(0.05f, 0.05f, 0.06f, 0.95f));
            var field = image.gameObject.AddComponent<InputField>();

            var text = Label(image.transform, "Text", value, 24, TextAnchor.MiddleLeft, TextMain);
            Stretch(text.rectTransform, 12f);
            text.supportRichText = false;

            var hint = Label(image.transform, "Placeholder", placeholder, 24, TextAnchor.MiddleLeft, TextDim);
            Stretch(hint.rectTransform, 12f);

            field.textComponent = text;
            field.placeholder = hint;
            field.text = value;
            return field;
        }

        public static void Stretch(RectTransform rect, float padding = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(padding, padding);
            rect.offsetMax = new Vector2(-padding, -padding);
        }

        /// <summary>Anchors a rect to a corner with an explicit pixel size and offset.</summary>
        public static void Place(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 offset, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = offset;
            rect.sizeDelta = size;
        }

        /// <summary>A left-to-right fill bar with a label on top.</summary>
        public class Bar
        {
            public RectTransform Root;
            public Image Fill;
            public Text Label;

            public void Set(float value, float max, string caption)
            {
                float f = max > 0f ? Mathf.Clamp01(value / max) : 0f;
                Fill.rectTransform.anchorMax = new Vector2(f, 1f);
                Label.text = caption;
            }
        }

        public static Bar CreateBar(Transform parent, string name, Color fillColour)
        {
            var back = Image(parent, name, new Color(0.06f, 0.06f, 0.07f, 0.85f));

            var fill = Image(back.transform, "Fill", fillColour);
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = new Vector2(1f, 1f);
            // Inset vertically only: a horizontal inset makes the rect negative-width
            // once the fill anchor collapses to zero.
            fill.rectTransform.offsetMin = new Vector2(0f, 2f);
            fill.rectTransform.offsetMax = new Vector2(0f, -2f);

            var label = Label(back.transform, "Label", "", 16, TextAnchor.MiddleCenter, TextMain);
            Stretch(label.rectTransform);

            return new Bar { Root = back.rectTransform, Fill = fill, Label = label };
        }
    }
}
