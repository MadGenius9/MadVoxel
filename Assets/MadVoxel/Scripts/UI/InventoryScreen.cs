using System.Collections.Generic;
using MadVoxel.Building;
using MadVoxel.Content;
using MadVoxel.Core;
using MadVoxel.Core.Player;
using MadVoxel.Inventory;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MadVoxel.UI
{
    /// <summary>
    /// The clipboard: bag on the left, whatever you opened on the right, and crafting as
    /// a strip of stamped work orders along the bottom. Click a slot to pick the stack
    /// up, click again to drop it; right-click splits or places one.
    ///
    /// Claim Slate deliberately has no paper-doll here. Nothing in MadVoxel is equipped
    /// to a body slot, so a mannequin would be a picture of a system that does not exist.
    /// </summary>
    public class InventoryScreen : MonoBehaviour
    {
        const float SlotSize = 70f;
        const float SlotGap = 6f;
        const int Columns = PlayerInventory.HotbarSize;

        const float TicketWidth = 252f;
        const float TicketHeight = 158f;
        const float TicketGap = 10f;
        const float StripHeight = 240f;

        PlayerRig _player;
        ContentDatabase _content;

        Canvas _canvas;
        RectTransform _bagRoot;
        RectTransform _containerRoot;
        RectTransform _craftRoot;
        RectTransform _craftContent;
        Text _craftTitle, _containerTitle;
        Image _cursorIcon;
        Text _cursorCount;
        RectTransform _cursorRoot;

        readonly List<SlotView> _bagSlots = new List<SlotView>();
        readonly List<SlotView> _containerSlots = new List<SlotView>();
        readonly List<WorkOrder> _orders = new List<WorkOrder>();

        ItemStack _cursor = ItemStack.Empty;
        /// <summary>
        /// Whatever container is open: a crate, a furnace, anything with slots. Held as
        /// the inventory rather than the structure so a new kind of container needs no
        /// change here.
        /// </summary>
        MadVoxel.Inventory.Inventory _openContainer;
        CraftStation _station = CraftStation.Hand;

        public bool IsOpen { get { return _canvas != null && _canvas.enabled; } }

        /// <summary>One recipe as a job ticket: heading, materials, and a stamp.</summary>
        class WorkOrder
        {
            public RecipeDefinition Recipe;
            public Button Button;
            public Image Background;
            public Text Title;
            public Text Lines;
            public Image Stamp;
            public Text StampLabel;
        }

        public void Init(PlayerRig player, ContentDatabase content)
        {
            _player = player;
            _content = content;

            _canvas = UIKit.CreateCanvas("InventoryScreen", 10, transform);

            var backdrop = ClaimSlate.Surface(_canvas.transform, "Backdrop", ClaimSlate.Fade(ClaimSlate.OilBlack, 0.72f));
            ClaimSlate.Stretch(backdrop.rectTransform);

            BuildBag();
            BuildContainer();
            BuildCrafting();
            BuildCursor();

            _player.Inventory.Bag.Changed += Refresh;
            _canvas.enabled = false;
        }

        void OnDestroy()
        {
            if (_player != null && _player.Inventory != null) _player.Inventory.Bag.Changed -= Refresh;
        }

        // ------------------------------------------------------------------- build

        void BuildBag()
        {
            float width = Columns * (SlotSize + SlotGap) + 60f;
            _bagRoot = ClaimSlate.Plate(_canvas.transform, "BagPlate", "BAG",
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(50f, -50f),
                new Vector2(width, 470f));

            var grid = ClaimSlate.Rect(_bagRoot, "Grid");
            ClaimSlate.Place(grid, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -6f),
                new Vector2(Columns * (SlotSize + SlotGap), 4f * (SlotSize + SlotGap)));

            for (int i = 0; i < PlayerInventory.TotalSize; i++)
            {
                var slot = MakeSlot(grid, "Bag" + i, i, OnBagSlotClicked);
                PlaceInGrid(slot.Background.rectTransform, i, 4, true);
                _bagSlots.Add(slot);
            }

            var hint = ClaimSlate.Stencil(_bagRoot, "BeltHint", "BOTTOM ROW IS YOUR TOOLBELT", 16,
                TextAnchor.LowerLeft, ClaimSlate.BoneDim, false);
            ClaimSlate.Place(ClaimSlate.Holder(hint), new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(0f, -4f), new Vector2(460f, 20f));
        }

        SlotView MakeSlot(Transform parent, string name, int index, System.Action<int, PointerEventData.InputButton> clicked)
        {
            var slot = SlotView.Create(parent, name, index, SlotSize);
            slot.Clicked = clicked;
            slot.Background.color = ClaimSlate.Metal;
            ClaimSlate.Frame(slot.Background.rectTransform, ClaimSlate.Dim(ClaimSlate.Bone, 0.16f), 1f);
            return slot;
        }

        /// <summary>Row 0 is the toolbelt and sits at the bottom, matching the visor.</summary>
        void PlaceInGrid(RectTransform rect, int index, int rows, bool beltAtBottom)
        {
            int column = index % Columns;
            int row = index / Columns;
            if (beltAtBottom) row = rows - 1 - row;

            float x = (column - (Columns - 1) * 0.5f) * (SlotSize + SlotGap);
            float y = ((rows - 1) * 0.5f - row) * (SlotSize + SlotGap);
            ClaimSlate.Place(rect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, y),
                new Vector2(SlotSize, SlotSize));
        }

        void BuildContainer()
        {
            float width = Columns * (SlotSize + SlotGap) + 60f;
            _containerRoot = ClaimSlate.Plate(_canvas.transform, "ContainerPlate", "CONTAINER",
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-50f, -50f),
                new Vector2(width, 380f));

            // The plate already says CONTAINER; this names the actual box, right-aligned
            // on the same line so the heading rule stays unbroken.
            _containerTitle = ClaimSlate.Stencil(_containerRoot.parent, "ContainerName", "", 20,
                TextAnchor.UpperRight, ClaimSlate.OxideRust);
            ClaimSlate.Place(ClaimSlate.Holder(_containerTitle), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-30f, -24f), new Vector2(width - 260f, 26f));

            var grid = ClaimSlate.Rect(_containerRoot, "Grid");
            ClaimSlate.Place(grid, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -6f),
                new Vector2(Columns * (SlotSize + SlotGap), 3f * (SlotSize + SlotGap)));

            for (int i = 0; i < 27; i++)
            {
                var slot = MakeSlot(grid, "Container" + i, i, OnContainerSlotClicked);
                PlaceInGrid(slot.Background.rectTransform, i, 3, false);
                _containerSlots.Add(slot);
            }

            ContainerPlate().gameObject.SetActive(false);
        }

        RectTransform ContainerPlate() { return (RectTransform)_containerRoot.parent; }
        RectTransform CraftPlate() { return (RectTransform)_craftRoot.parent; }

        /// <summary>
        /// Crafting as a work-order strip rather than a second wall of panels: a row of
        /// stamped tickets across the bottom, so the bag and a container can both stay
        /// open while you queue something up.
        /// </summary>
        void BuildCrafting()
        {
            _craftRoot = ClaimSlate.Plate(_canvas.transform, "CraftStrip", "",
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 40f),
                new Vector2(1780f, StripHeight));

            _craftTitle = ClaimSlate.Stencil(CraftPlate(), "CraftTitle", "WORK ORDERS", 24,
                TextAnchor.UpperLeft, ClaimSlate.Bone);
            ClaimSlate.Place(ClaimSlate.Holder(_craftTitle), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(30f, -16f), new Vector2(700f, 26f));

            var rule = ClaimSlate.Fill(CraftPlate(), "CraftRule", ClaimSlate.OxideRust);
            ClaimSlate.Place(rule.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(30f, -44f), new Vector2(1720f, 2f));

            var viewport = ClaimSlate.Surface(CraftPlate(), "Viewport", ClaimSlate.Fade(ClaimSlate.OilBlack, 0.35f));
            ClaimSlate.Place(viewport.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 16f), new Vector2(1720f, TicketHeight + 8f));
            viewport.gameObject.AddComponent<RectMask2D>();

            _craftContent = ClaimSlate.Rect(viewport.transform, "Content");
            _craftContent.anchorMin = new Vector2(0f, 0f);
            _craftContent.anchorMax = new Vector2(0f, 1f);
            _craftContent.pivot = new Vector2(0f, 0.5f);
            _craftContent.anchoredPosition = Vector2.zero;
            _craftContent.sizeDelta = Vector2.zero;

            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.content = _craftContent;
            scroll.viewport = viewport.rectTransform;
            scroll.horizontal = true;
            scroll.vertical = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;
        }

        void BuildCursor()
        {
            _cursorRoot = ClaimSlate.Rect(_canvas.transform, "Cursor");
            ClaimSlate.Place(_cursorRoot, new Vector2(0f, 0f), new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(SlotSize, SlotSize));

            _cursorIcon = ClaimSlate.Fill(_cursorRoot, "Icon", Color.clear);
            ClaimSlate.Stretch(_cursorIcon.rectTransform, 10f);

            _cursorCount = ClaimSlate.Stencil(_cursorRoot, "Count", "", 20, TextAnchor.LowerRight, ClaimSlate.Bone, false);
            ClaimSlate.Stretch(ClaimSlate.Holder(_cursorCount), 4f);

            _cursorRoot.gameObject.SetActive(false);
        }

        // -------------------------------------------------------------------- open

        public void Open(CraftStation station)
        {
            _station = station;
            _openContainer = null;
            ContainerPlate().gameObject.SetActive(false);
            _craftTitle.text = station == CraftStation.Hand
                ? "WORK ORDERS"
                : "WORK ORDERS  -  " + station.ToString().ToUpperInvariant();
            Show();
        }

        public void OpenContainer(StorageStructure container)
        {
            OpenContainer(container.Contents, container.Structure.Definition.displayName, CraftStation.Hand);
        }

        /// <summary>
        /// Opens any container beside the bag. The station lets a container that is also
        /// a workplace - a furnace - offer its own work orders in the same screen.
        /// </summary>
        public void OpenContainer(MadVoxel.Inventory.Inventory contents, string title, CraftStation station)
        {
            if (contents == null) return;

            _openContainer = contents;
            _station = station;
            ContainerPlate().gameObject.SetActive(true);
            _containerTitle.text = (title ?? "CONTAINER").ToUpperInvariant();
            _craftTitle.text = station == CraftStation.Hand
                ? "WORK ORDERS"
                : "WORK ORDERS  -  " + station.ToString().ToUpperInvariant();
            contents.Changed += Refresh;
            Show();
        }

        void Show()
        {
            _canvas.enabled = true;
            RebuildWorkOrders();
            Refresh();
        }

        public void Close()
        {
            if (!IsOpen) return;

            // Never eat the stack on the cursor.
            if (!_cursor.IsEmpty)
            {
                int leftover = _player.Inventory.Bag.Add(_cursor);
                if (leftover > 0 && _openContainer != null) _openContainer.Add(_cursor.WithCount(leftover));
                _cursor = ItemStack.Empty;
            }

            if (_openContainer != null)
            {
                _openContainer.Changed -= Refresh;
                _openContainer = null;
            }

            _canvas.enabled = false;
            UpdateCursorVisual();
        }

        // ------------------------------------------------------------------ slots

        void OnBagSlotClicked(int index, PointerEventData.InputButton button)
        {
            var bag = _player.Inventory.Bag;
            HandleSlotClick(bag, index, button);
        }

        void OnContainerSlotClicked(int index, PointerEventData.InputButton button)
        {
            if (_openContainer == null) return;
            HandleSlotClick(_openContainer, index, button);
        }

        void HandleSlotClick(MadVoxel.Inventory.Inventory inventory, int index, PointerEventData.InputButton button)
        {
            var slot = inventory[index];

            if (_cursor.IsEmpty)
            {
                if (slot.IsEmpty) return;
                if (button == PointerEventData.InputButton.Right && slot.Count > 1)
                {
                    int half = slot.Count / 2;
                    _cursor = slot.WithCount(half);
                    inventory.SetSlot(index, slot.WithCount(slot.Count - half));
                }
                else
                {
                    _cursor = inventory.TakeSlot(index);
                }
            }
            else if (slot.IsEmpty)
            {
                if (button == PointerEventData.InputButton.Right)
                {
                    inventory.SetSlot(index, _cursor.WithCount(1));
                    _cursor = _cursor.WithCount(_cursor.Count - 1);
                }
                else
                {
                    inventory.SetSlot(index, _cursor);
                    _cursor = ItemStack.Empty;
                }
            }
            else if (slot.CanMergeWith(_cursor))
            {
                int move = button == PointerEventData.InputButton.Right ? 1 : _cursor.Count;
                move = Mathf.Min(move, slot.SpaceLeft);
                if (move > 0)
                {
                    inventory.SetSlot(index, slot.WithCount(slot.Count + move));
                    _cursor = _cursor.WithCount(_cursor.Count - move);
                }
            }
            else
            {
                var swap = _cursor;
                _cursor = slot;
                inventory.SetSlot(index, swap);
            }

            Refresh();
        }

        // ------------------------------------------------------------- work orders

        void RebuildWorkOrders()
        {
            for (int i = 0; i < _orders.Count; i++)
            {
                if (_orders[i].Background != null) Destroy(_orders[i].Background.gameObject);
            }
            _orders.Clear();

            if (_content == null) return;

            var unlocked = _player.Progression.UnlockedRecipes;
            int column = 0;

            for (int i = 0; i < _content.recipes.Count; i++)
            {
                var recipe = _content.recipes[i];
                if (recipe == null || recipe.output == null) continue;
                if (!CraftingService.StationSatisfies(_station, recipe.station)) continue;
                if (!CraftingService.IsUnlocked(recipe, unlocked)) continue;

                _orders.Add(BuildTicket(recipe, column));
                column++;
            }

            _craftContent.sizeDelta = new Vector2(column * (TicketWidth + TicketGap) + TicketGap, 0f);
        }

        WorkOrder BuildTicket(RecipeDefinition recipe, int column)
        {
            var card = ClaimSlate.Surface(_craftContent, "Order_" + recipe.stringId, ClaimSlate.Metal);
            ClaimSlate.Place(card.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(TicketGap + column * (TicketWidth + TicketGap), 0f),
                new Vector2(TicketWidth, TicketHeight));
            ClaimSlate.Frame(card.rectTransform, ClaimSlate.Dim(ClaimSlate.Bone, 0.20f), 1f);

            var button = card.gameObject.AddComponent<Button>();
            var colours = button.colors;
            colours.normalColor = Color.white;
            colours.highlightedColor = new Color(1.30f, 1.10f, 0.94f);
            colours.pressedColor = new Color(0.82f, 0.60f, 0.42f);
            button.colors = colours;

            var title = ClaimSlate.Stencil(card.transform, "Title", "", 19, TextAnchor.UpperLeft, ClaimSlate.Bone);
            ClaimSlate.Place(ClaimSlate.Holder(title), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(12f, -10f), new Vector2(TicketWidth - 24f, 22f));

            var rule = ClaimSlate.Fill(card.transform, "Rule", ClaimSlate.Dim(ClaimSlate.Bone, 0.22f));
            ClaimSlate.Place(rule.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(12f, -36f), new Vector2(TicketWidth - 24f, 1f));

            var lines = ClaimSlate.Stencil(card.transform, "Lines", "", 15, TextAnchor.UpperLeft, ClaimSlate.BoneDim, false);
            ClaimSlate.Place(ClaimSlate.Holder(lines), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(12f, -44f), new Vector2(TicketWidth - 24f, 72f));
            lines.horizontalOverflow = HorizontalWrapMode.Wrap;
            lines.verticalOverflow = VerticalWrapMode.Truncate;

            // The stamp: the one place a ticket says yes or no.
            var stamp = ClaimSlate.Fill(card.transform, "Stamp", ClaimSlate.OxideRust);
            ClaimSlate.Place(stamp.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(12f, 10f), new Vector2(TicketWidth - 24f, 26f));

            var stampLabel = ClaimSlate.Stencil(stamp.transform, "StampLabel", "CRAFT", 16,
                TextAnchor.MiddleCenter, ClaimSlate.Bone, false);
            ClaimSlate.Stretch(ClaimSlate.Holder(stampLabel));

            var captured = recipe;
            button.onClick.AddListener(() => Craft(captured));

            return new WorkOrder
            {
                Recipe = recipe,
                Button = button,
                Background = card,
                Title = title,
                Lines = lines,
                Stamp = stamp,
                StampLabel = stampLabel
            };
        }

        void Craft(RecipeDefinition recipe)
        {
            var bag = _player.Inventory.Bag;
            if (!CraftingService.Craft(bag, recipe, _station, _player.Progression.UnlockedRecipes))
            {
                Notifications.Post("Missing materials");
                return;
            }

            // Crafting is real work, unlike putting a block back down.
            _player.Progression.AddXp(Mathf.Max(1f, recipe.craftSeconds * 2f), MadVoxel.Perks.XpSource.Craft);
            Notifications.PostFormat("Crafted {0} x{1}", recipe.output.displayName, recipe.outputCount);
            Refresh();
        }

        // ----------------------------------------------------------------- refresh

        public void Refresh()
        {
            if (!IsOpen) return;

            var bag = _player.Inventory.Bag;
            for (int i = 0; i < _bagSlots.Count; i++)
            {
                bool selected = i == _player.Inventory.SelectedIndex;
                _bagSlots[i].Bind(bag[i], selected);
                _bagSlots[i].Background.color = selected ? ClaimSlate.MetalLit : ClaimSlate.Metal;
            }

            if (_openContainer != null)
            {
                var contents = _openContainer;
                for (int i = 0; i < _containerSlots.Count; i++)
                {
                    bool exists = i < contents.Size;
                    _containerSlots[i].gameObject.SetActive(exists);
                    if (!exists) continue;

                    _containerSlots[i].Bind(contents[i], false);
                    _containerSlots[i].Background.color = ClaimSlate.Metal;
                }
            }

            for (int i = 0; i < _orders.Count; i++)
            {
                var order = _orders[i];
                bool can = CraftingService.CanCraft(bag, order.Recipe, _station, _player.Progression.UnlockedRecipes);

                order.Title.text = TicketTitle(order.Recipe);
                order.Lines.text = TicketLines(order.Recipe);
                order.Title.color = can ? ClaimSlate.Bone : ClaimSlate.Pencil;

                // Colour and wording both change, so a short ticket reads as short even
                // in greyscale.
                order.Stamp.color = can ? ClaimSlate.OxideRust : ClaimSlate.Fade(ClaimSlate.OilBlack, 0.6f);
                order.StampLabel.text = can ? "CRAFT" : "SHORT";
                order.StampLabel.color = can ? ClaimSlate.Bone : ClaimSlate.BoneDim;
                order.Background.color = can ? ClaimSlate.Metal : ClaimSlate.Fade(ClaimSlate.OilBlack, 0.55f);
            }

            UpdateCursorVisual();
        }

        string TicketTitle(RecipeDefinition recipe)
        {
            string name = recipe.output.displayName.ToUpperInvariant();
            return recipe.outputCount > 1 ? name + "  x" + recipe.outputCount : name;
        }

        string TicketLines(RecipeDefinition recipe)
        {
            var bag = _player.Inventory.Bag;
            var sb = new System.Text.StringBuilder();

            for (int i = 0; i < recipe.ingredients.Count; i++)
            {
                var ingredient = recipe.ingredients[i];
                if (ingredient.item == null) continue;
                if (sb.Length > 0) sb.Append('\n');

                int have = bag.CountOf(ingredient.item);
                sb.Append(have).Append('/').Append(ingredient.count).Append("  ")
                  .Append(ingredient.item.displayName.ToUpperInvariant());
            }
            return sb.ToString();
        }

        void UpdateCursorVisual()
        {
            bool visible = IsOpen && !_cursor.IsEmpty;
            _cursorRoot.gameObject.SetActive(visible);
            if (!visible) return;

            _cursorIcon.color = _cursor.Item.tint;
            _cursorCount.text = _cursor.Count > 1 ? _cursor.Count.ToString() : "";
        }

        void Update()
        {
            if (!IsOpen || _cursor.IsEmpty) return;

            Vector2 local;
            var canvasRect = (RectTransform)_canvas.transform;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, Input.mousePosition, null, out local))
            {
                _cursorRoot.anchoredPosition = local + canvasRect.rect.size * 0.5f;
            }
        }
    }
}
