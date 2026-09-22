using MadVoxel.Inventory;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MadVoxel.UI
{
    /// <summary>One inventory square: background, tinted item block and a count.</summary>
    public class SlotView : MonoBehaviour, IPointerClickHandler
    {
        public Image Background;
        public Image Icon;
        public Text Count;
        public Text Durability;

        public int Index { get; private set; }
        public System.Action<int, PointerEventData.InputButton> Clicked;

        public static SlotView Create(Transform parent, string name, int index, float size)
        {
            var background = UIKit.Image(parent, name, UIKit.Slot);
            background.rectTransform.sizeDelta = new Vector2(size, size);

            var view = background.gameObject.AddComponent<SlotView>();
            view.Index = index;
            view.Background = background;

            view.Icon = UIKit.Image(background.transform, "Icon", Color.clear);
            UIKit.Stretch(view.Icon.rectTransform, size * 0.16f);
            view.Icon.raycastTarget = false;

            view.Count = UIKit.Label(background.transform, "Count", "", Mathf.RoundToInt(size * 0.26f),
                TextAnchor.LowerRight, UIKit.TextMain);
            UIKit.Stretch(view.Count.rectTransform, 4f);

            view.Durability = UIKit.Label(background.transform, "Durability", "", Mathf.RoundToInt(size * 0.20f),
                TextAnchor.UpperLeft, UIKit.Accent);
            UIKit.Stretch(view.Durability.rectTransform, 4f);

            return view;
        }

        public void Bind(ItemStack stack, bool selected)
        {
            Background.color = selected ? UIKit.SlotHot : UIKit.Slot;

            if (stack.IsEmpty)
            {
                Icon.color = Color.clear;
                Count.text = "";
                Durability.text = "";
                return;
            }

            Icon.color = stack.Item.tint;
            Count.text = stack.Count > 1 ? stack.Count.ToString() : "";
            Durability.text = stack.Item.HasDurability
                ? Mathf.CeilToInt(100f * stack.Durability / Mathf.Max(1, stack.Item.maxDurability)) + "%"
                : "";
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (Clicked != null) Clicked(Index, eventData.button);
        }
    }
}
