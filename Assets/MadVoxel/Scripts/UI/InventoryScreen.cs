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
    /// Bag, crafting list and, when one is open, a container. Click a slot to pick the
    /// stack up, click again to drop it; right-click splits or places one.
    /// </summary>
    public class InventoryScreen : MonoBehaviour
    {
        const float SlotSize = 74f;
        const float SlotGap = 6f;
        const int Columns = PlayerInventory.HotbarSize;
        const float RowHeight = 46f;

        PlayerRig _player;
        ContentDatabase _content;

        Canvas _canvas;
        RectTransform _bagRoot;
        RectTransform _containerRoot;
        RectTransform _craftRoot;
        RectTransform _craftContent;
        Text _title, _craftTitle, _containerTitle;
        Image _cursorIcon;
        Text _cursorCount;
        RectTransform _cursorRoot;

        readonly List<SlotView> _bagSlots = new List<SlotView>();
        readonly List<SlotView> _containerSlots = new List<SlotView>();
        readonly List<RecipeRow> _recipeRows = new List<RecipeRow>();

        ItemStack _cursor = ItemStack.Empty;
        StorageStructure _openContainer;
        CraftStation _station = CraftStation.Hand;

        public bool IsOpen { get { return _canvas != null && _canvas.enabled; } }

        class RecipeRow
        {
            public RecipeDefinition Recipe;
            public Button Button;
            public Text Label;
            public Image Background;
        }

        public void Init(PlayerRig player, ContentDatabase content)
        {
            _player = player;
            _content = content;

            _canvas = UIKit.CreateCanvas("InventoryScreen", 10, transform);
            var backdrop = UIKit.Image(_canvas.transform, "Backdrop", new Color(0f, 0f, 0f, 0.62f));
            UIKit.Stretch(backdrop.rectTransform);

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
            var panel = UIKit.Image(_canvas.transform, "BagPanel", UIKit.Panel);
            UIKit.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-20f, -40f), new Vector2(760f, 520f));
            _bagRoot = panel.rectTransform;

            _title = UIKit.Label(_bagRoot, "Title", "BAG", 30, TextAnchor.UpperLeft, UIKit.Accent);
            UIKit.Place(_title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -18f), new Vector2(400f, 34f));

            var grid = UIKit.Rect(_bagRoot, "Grid");
            UIKit.Place(grid, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -22f), new Vector2(720f, 400f));

            for (int i = 0; i < PlayerInventory.TotalSize; i++)
            {
                var slot = SlotView.Create(grid, "Bag" + i, i, SlotSize);
                slot.Clicked = OnBagSlotClicked;
                PlaceInGrid(slot.Background.rectTransform, i, 4, true);
                _bagSlots.Add(slot);
            }

            var hotbarHint = UIKit.Label(_bagRoot, "HotbarHint", "Bottom row is your hotbar", 18, TextAnchor.LowerLeft, UIKit.TextDim);
            UIKit.Place(hotbarHint.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(24f, 16f), new Vector2(500f, 24f));
        }

        /// <summary>Row 0 is the hotbar and sits at the bottom, matching the HUD.</summary>
        void PlaceInGrid(RectTransform rect, int index, int rows, bool hotbarAtBottom)
        {
            int column = index % Columns;
            int row = index / Columns;
            if (hotbarAtBottom) row = rows - 1 - row;

            float x = (column - (Columns - 1) * 0.5f) * (SlotSize + SlotGap);
            float y = ((rows - 1) * 0.5f - row) * (SlotSize + SlotGap);
            UIKit.Place(rect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, y), new Vector2(SlotSize, SlotSize));
        }

        void BuildContainer()
        {
            var panel = UIKit.Image(_canvas.transform, "ContainerPanel", UIKit.Panel);
            UIKit.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(20f, -40f), new Vector2(760f, 400f));
            _containerRoot = panel.rectTransform;

            _containerTitle = UIKit.Label(_containerRoot, "Title", "CONTAINER", 30, TextAnchor.UpperLeft, UIKit.Accent);
            UIKit.Place(_containerTitle.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -18f), new Vector2(400f, 34f));

            var grid = UIKit.Rect(_containerRoot, "Grid");
            UIKit.Place(grid, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -18f), new Vector2(720f, 300f));

            for (int i = 0; i < 27; i++)
            {
                var slot = SlotView.Create(grid, "Container" + i, i, SlotSize);
                slot.Clicked = OnContainerSlotClicked;
                PlaceInGrid(slot.Background.rectTransform, i, 3, false);
                _containerSlots.Add(slot);
            }

            _containerRoot.gameObject.SetActive(false);
        }

        void BuildCrafting()
        {
            var panel = UIKit.Image(_canvas.transform, "CraftPanel", UIKit.Panel);
            UIKit.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(20f, -40f), new Vector2(760f, 520f));
            _craftRoot = panel.rectTransform;

            _craftTitle = UIKit.Label(_craftRoot, "Title", "CRAFTING", 30, TextAnchor.UpperLeft, UIKit.Accent);
            UIKit.Place(_craftTitle.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -18f), new Vector2(500f, 34f));

            var viewport = UIKit.Image(_craftRoot, "Viewport", new Color(0f, 0f, 0f, 0.25f));
            UIKit.Place(viewport.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -22f), new Vector2(712f, 430f));
            viewport.gameObject.AddComponent<RectMask2D>();

            _craftContent = UIKit.Rect(viewport.transform, "Content");
            _craftContent.anchorMin = new Vector2(0f, 1f);
            _craftContent.anchorMax = new Vector2(1f, 1f);
            _craftContent.pivot = new Vector2(0.5f, 1f);
            _craftContent.anchoredPosition = Vector2.zero;
            _craftContent.sizeDelta = new Vector2(0f, 0f);

            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.content = _craftContent;
            scroll.viewport = viewport.rectTransform;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 28f;
        }

        void BuildCursor()
        {
            _cursorRoot = UIKit.Rect(_canvas.transform, "Cursor");
            UIKit.Place(_cursorRoot, new Vector2(0f, 0f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(SlotSize, SlotSize));

            _cursorIcon = UIKit.Image(_cursorRoot, "Icon", Color.clear);
            UIKit.Stretch(_cursorIcon.rectTransform, 10f);
            _cursorIcon.raycastTarget = false;

            _cursorCount = UIKit.Label(_cursorRoot, "Count", "", 20, TextAnchor.LowerRight, UIKit.TextMain);
            UIKit.Stretch(_cursorCount.rectTransform, 4f);

            _cursorRoot.gameObject.SetActive(false);
        }

        // -------------------------------------------------------------------- open

        public void Open(CraftStation station)
        {
            _station = station;
            _openContainer = null;
            _containerRoot.gameObject.SetActive(false);
            _craftRoot.gameObject.SetActive(true);
            _craftTitle.text = station == CraftStation.Hand ? "CRAFTING" : "CRAFTING - " + station.ToString().ToUpperInvariant();
            Show();
        }

        public void OpenContainer(StorageStructure container)
        {
            _openContainer = container;
            _station = CraftStation.Hand;
            _craftRoot.gameObject.SetActive(false);
            _containerRoot.gameObject.SetActive(true);
            _containerTitle.text = container.Structure.Definition.displayName.ToUpperInvariant();
            container.Contents.Changed += Refresh;
            Show();
        }

        void Show()
        {
            _canvas.enabled = true;
            RebuildRecipeRows();
            Refresh();
        }

        public void Close()
        {
            if (!IsOpen) return;

            // Never eat the stack on the cursor.
            if (!_cursor.IsEmpty)
            {
                int leftover = _player.Inventory.Bag.Add(_cursor);
                if (leftover > 0 && _openContainer != null) _openContainer.Contents.Add(_cursor.WithCount(leftover));
                _cursor = ItemStack.Empty;
            }

            if (_openContainer != null)
            {
                _openContainer.Contents.Changed -= Refresh;
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
            HandleSlotClick(_openContainer.Contents, index, button);
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

        // ---------------------------------------------------------------- crafting

        void RebuildRecipeRows()
        {
            for (int i = 0; i < _recipeRows.Count; i++)
            {
                if (_recipeRows[i].Button != null) Destroy(_recipeRows[i].Button.gameObject);
            }
            _recipeRows.Clear();

            if (_content == null || !_craftRoot.gameObject.activeSelf) return;

            var unlocked = _player.Progression.UnlockedRecipes;
            int row = 0;

            for (int i = 0; i < _content.recipes.Count; i++)
            {
                var recipe = _content.recipes[i];
                if (recipe == null || recipe.output == null) continue;
                if (!CraftingService.StationSatisfies(_station, recipe.station)) continue;
                if (!CraftingService.IsUnlocked(recipe, unlocked)) continue;

                var button = UIKit.Button(_craftContent, "Recipe" + i, "", 20);
                UIKit.Place(button.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0f, 1f),
                    new Vector2(6f, -(row * (RowHeight + 4f)) - 6f), new Vector2(700f, RowHeight));

                var label = button.GetComponentInChildren<Text>();
                label.alignment = TextAnchor.MiddleLeft;
                label.rectTransform.offsetMin = new Vector2(12f, 0f);

                var captured = recipe;
                button.onClick.AddListener(() => Craft(captured));

                _recipeRows.Add(new RecipeRow
                {
                    Recipe = recipe,
                    Button = button,
                    Label = label,
                    Background = button.GetComponent<Image>()
                });
                row++;
            }

            _craftContent.sizeDelta = new Vector2(0f, row * (RowHeight + 4f) + 12f);
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
                _bagSlots[i].Bind(bag[i], i == _player.Inventory.SelectedIndex);
            }

            if (_openContainer != null)
            {
                var contents = _openContainer.Contents;
                for (int i = 0; i < _containerSlots.Count; i++)
                {
                    bool exists = i < contents.Size;
                    _containerSlots[i].gameObject.SetActive(exists);
                    if (exists) _containerSlots[i].Bind(contents[i], false);
                }
            }

            for (int i = 0; i < _recipeRows.Count; i++)
            {
                var row = _recipeRows[i];
                bool can = CraftingService.CanCraft(bag, row.Recipe, _station, _player.Progression.UnlockedRecipes);
                row.Label.text = Describe(row.Recipe);
                row.Label.color = can ? UIKit.TextMain : UIKit.TextDim;
                row.Background.color = can ? UIKit.PanelSoft : new Color(0.08f, 0.08f, 0.085f, 0.85f);
            }

            UpdateCursorVisual();
        }

        string Describe(RecipeDefinition recipe)
        {
            var bag = _player.Inventory.Bag;
            var sb = new System.Text.StringBuilder();
            sb.Append(recipe.output.displayName);
            if (recipe.outputCount > 1) sb.Append(" x").Append(recipe.outputCount);
            sb.Append("   -   ");

            for (int i = 0; i < recipe.ingredients.Count; i++)
            {
                var ing = recipe.ingredients[i];
                if (ing.item == null) continue;
                if (i > 0) sb.Append(", ");
                sb.Append(ing.item.displayName).Append(' ')
                  .Append(bag.CountOf(ing.item)).Append('/').Append(ing.count);
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
