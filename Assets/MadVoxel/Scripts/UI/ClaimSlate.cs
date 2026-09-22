using UnityEngine;
using UnityEngine.UI;

namespace MadVoxel.UI
{
    /// <summary>
    /// Claim Slate's widget builders. The palette and the pure colour decisions live in
    /// ClaimSlate.Palette.cs, which the headless checks compile.
    ///
    /// A weathered farm-command overlay - scratched visor in the world, grease-pencil
    /// clipboard in the menus.
    ///
    /// Three rules this file exists to enforce:
    ///   * Corners only. The centre of the screen is the world, never a panel.
    ///   * Numbers beat icon soup. A readout says "DAY 12  17:41", not a sun glyph.
    ///   * Colour is never the only signal. Every state that matters also changes shape,
    ///     so the HUD survives a colourblind player and a washed-out monitor.
    ///
    /// Everything here builds from untextured <see cref="Image"/> rects, so the project
    /// still carries no sprite or material assets. Square corners come for free.
    /// </summary>
    public static partial class ClaimSlate
    {
        // ------------------------------------------------------------------- pieces

        /// <summary>A bare rect with no graphic - a layout anchor.</summary>
        public static RectTransform Rect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static Image Fill(Transform parent, string name, Color colour)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = colour;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>A fill that can be clicked - panels that must swallow the cursor.</summary>
        public static Image Surface(Transform parent, string name, Color colour)
        {
            var image = Fill(parent, name, colour);
            image.raycastTarget = true;
            return image;
        }

        /// <summary>
        /// Stamped type: uppercase, with a one-pixel oil-black offset behind it so the
        /// letters keep their edge against bright terrain. The shadow is a child, so the
        /// returned label is still the one to set text on.
        /// </summary>
        public static Text Stencil(Transform parent, string name, string text, int size,
                                   TextAnchor anchor, Color colour, bool shadow = true)
        {
            var root = Rect(parent, name);

            Text shade = null;
            if (shadow)
            {
                shade = RawLabel(root, "Shade", text, size, anchor, Fade(OilBlack, 0.75f));
                Stretch(shade.rectTransform);
                shade.rectTransform.anchoredPosition = new Vector2(1f, -1f);
            }

            var label = RawLabel(root, "Text", text, size, anchor, colour);
            Stretch(label.rectTransform);

            if (shade != null) label.gameObject.AddComponent<StencilShadow>().Shade = shade;
            return label;
        }

        static Text RawLabel(Transform parent, string name, string text, int size, TextAnchor anchor, Color colour)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);

            var label = go.GetComponent<Text>();
            label.font = UIKit.Font;
            label.text = text;
            label.fontSize = size;
            label.alignment = anchor;
            label.color = colour;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.raycastTarget = false;
            return label;
        }

        /// <summary>Keeps a stencil label's drop shadow reading the same string.</summary>
        public class StencilShadow : MonoBehaviour
        {
            public Text Shade;
            Text _label;
            string _last;

            void LateUpdate()
            {
                if (Shade == null) return;
                if (_label == null) _label = GetComponent<Text>();
                if (_label == null || _label.text == _last) return;

                _last = _label.text;
                Shade.text = _last;
            }
        }

        /// <summary>A one-pixel outline drawn as four thin rects. No rounded corners.</summary>
        public static void Frame(RectTransform target, Color colour, float thickness = 2f)
        {
            Edge(target, "FrameTop", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, thickness), colour);
            Edge(target, "FrameBottom", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, thickness), colour);
            Edge(target, "FrameLeft", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(thickness, 0f), colour);
            Edge(target, "FrameRight", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(thickness, 0f), colour);
        }

        static void Edge(RectTransform parent, string name, Vector2 min, Vector2 max, Vector2 size, Color colour)
        {
            var edge = Fill(parent, name, colour);
            var rect = edge.rectTransform;
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = Vector2.zero;
        }

        /// <summary>Four corner rivets, inset. The detail that makes a rect read as plate.</summary>
        public static void Rivets(RectTransform target, float inset = 13f, float size = 5f)
        {
            Corner(target, "RivetTL", new Vector2(0f, 1f), new Vector2(inset, -inset), size);
            Corner(target, "RivetTR", new Vector2(1f, 1f), new Vector2(-inset, -inset), size);
            Corner(target, "RivetBL", new Vector2(0f, 0f), new Vector2(inset, inset), size);
            Corner(target, "RivetBR", new Vector2(1f, 0f), new Vector2(-inset, inset), size);
        }

        static void Corner(RectTransform parent, string name, Vector2 anchor, Vector2 offset, float size)
        {
            var rivet = Fill(parent, name, Rivet);
            Place(rivet.rectTransform, anchor, new Vector2(0.5f, 0.5f), offset, new Vector2(size, size));
        }

        /// <summary>
        /// The house panel: riveted plate with a stencilled heading. Returns the body
        /// rect - the area inside the frame that content should be parented to.
        /// </summary>
        public static RectTransform Plate(Transform parent, string name, string heading,
                                          Vector2 anchor, Vector2 pivot, Vector2 offset, Vector2 size,
                                          bool deep = false)
        {
            var plate = Surface(parent, name, deep ? SlateDeep : Slate);
            Place(plate.rectTransform, anchor, pivot, offset, size);
            Frame(plate.rectTransform, Dim(Bone, 0.22f));
            Rivets(plate.rectTransform);

            float top = 18f;
            if (!string.IsNullOrEmpty(heading))
            {
                var label = Stencil(plate.transform, "Heading", heading.ToUpperInvariant(), 26,
                    TextAnchor.UpperLeft, Bone);
                Place(label.rectTransform.parent as RectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                    new Vector2(30f, -20f), new Vector2(size.x - 60f, 30f));

                // The rust rule under a heading is the system's signature.
                var rule = Fill(plate.transform, "HeadingRule", OxideRust);
                Place(rule.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                    new Vector2(30f, -54f), new Vector2(size.x - 60f, 2f));
                top = 70f;
            }

            var body = Rect(plate.transform, "Body");
            body.anchorMin = Vector2.zero;
            body.anchorMax = Vector2.one;
            body.offsetMin = new Vector2(30f, 26f);
            body.offsetMax = new Vector2(-30f, -top);
            return body;
        }

        /// <summary>
        /// A short readout bar with a shape tick at its left cap, so the reading survives
        /// without colour. <paramref name="glyph"/> picks the tick: Plus, Bars or Drop.
        /// </summary>
        public class Gauge
        {
            public RectTransform Root;
            public Image Fill;
            public Text Label;
            public Image Track;

            public void Set(float value, float max, string caption, Color colour)
            {
                float f = max > 0f ? Mathf.Clamp01(value / max) : 0f;
                Fill.rectTransform.anchorMax = new Vector2(f, 1f);
                Fill.color = colour;
                Label.text = caption;
            }

            public void SetVisible(bool visible)
            {
                if (Root != null && Root.gameObject.activeSelf != visible) Root.gameObject.SetActive(visible);
            }
        }

        public enum Tick { Plus, Bars, Drop, Grain }

        public static Gauge CreateGauge(Transform parent, string name, Color colour, Tick tick, float width = 176f)
        {
            var root = Rect(parent, name);
            root.sizeDelta = new Vector2(width, 18f);

            DrawTick(root, tick, colour);

            var track = Fill(root, "Track", Fade(OilBlack, 0.72f));
            Place(track.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(24f, 0f),
                new Vector2(width - 24f, 12f));
            Frame(track.rectTransform, Dim(Bone, 0.18f), 1f);

            var fill = Fill(track.transform, "Fill", colour);
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = new Vector2(1f, 1f);
            fill.rectTransform.offsetMin = new Vector2(1f, 1f);
            fill.rectTransform.offsetMax = new Vector2(-1f, -1f);

            var label = Stencil(root, "Value", "", 15, TextAnchor.MiddleRight, BoneDim);
            Place(label.rectTransform.parent as RectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-4f, 0f), new Vector2(width - 30f, 16f));

            return new Gauge { Root = root, Fill = fill, Label = label, Track = track };
        }

        /// <summary>
        /// The colourblind half of the contract: each gauge carries a distinct mark.
        /// Plus for health, stacked bars for stamina, a drop for water, a grain for food.
        /// </summary>
        static void DrawTick(RectTransform root, Tick tick, Color colour)
        {
            var mark = Rect(root, "Tick");
            Place(mark, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0f), new Vector2(16f, 16f));

            switch (tick)
            {
                case Tick.Plus:
                    Bar(mark, "H", Vector2.zero, new Vector2(14f, 3f), colour);
                    Bar(mark, "V", Vector2.zero, new Vector2(3f, 14f), colour);
                    break;
                case Tick.Bars:
                    Bar(mark, "B0", new Vector2(0f, 5f), new Vector2(14f, 3f), colour);
                    Bar(mark, "B1", new Vector2(0f, 0f), new Vector2(11f, 3f), colour);
                    Bar(mark, "B2", new Vector2(0f, -5f), new Vector2(8f, 3f), colour);
                    break;
                case Tick.Drop:
                    Bar(mark, "D0", new Vector2(0f, 4f), new Vector2(4f, 5f), colour);
                    Bar(mark, "D1", new Vector2(0f, -1f), new Vector2(10f, 5f), colour);
                    Bar(mark, "D2", new Vector2(0f, -5f), new Vector2(6f, 3f), colour);
                    break;
                default:
                    Bar(mark, "G0", new Vector2(-3f, 0f), new Vector2(3f, 13f), colour);
                    Bar(mark, "G1", new Vector2(3f, 2f), new Vector2(3f, 9f), colour);
                    break;
            }
        }

        static void Bar(RectTransform parent, string name, Vector2 offset, Vector2 size, Color colour)
        {
            var bar = Fill(parent, name, colour);
            Place(bar.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), offset, size);
        }

        /// <summary>
        /// A toolbelt slot: quiet metal, never a bright square. The selected one is marked
        /// by a rust underline rather than a glow, so a full belt stays readable.
        /// </summary>
        public static Image BeltSlot(Transform parent, string name, float size, out Image underline)
        {
            var slot = Fill(parent, name, Metal);
            slot.rectTransform.sizeDelta = new Vector2(size, size);
            Frame(slot.rectTransform, Dim(Bone, 0.16f), 1f);

            underline = Fill(slot.transform, "Underline", OxideRust);
            Place(underline.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f),
                new Vector2(0f, -3f), new Vector2(size, 3f));
            underline.gameObject.SetActive(false);

            return slot;
        }

        // ------------------------------------------------------------------ layout

        public static void Place(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 offset, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = offset;
            rect.sizeDelta = size;
        }

        public static void Stretch(RectTransform rect, float padding = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(padding, padding);
            rect.offsetMax = new Vector2(-padding, -padding);
        }

        /// <summary>The rect a <see cref="Stencil"/> label sits in, for positioning.</summary>
        public static RectTransform Holder(Text stencil)
        {
            return stencil.rectTransform.parent as RectTransform;
        }
    }
}
