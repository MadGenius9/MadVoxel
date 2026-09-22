using System;
using MadVoxel.Inventory;
using UnityEngine;

namespace MadVoxel.Core.Player
{
    /// <summary>
    /// The player's 9-slot hotbar plus 27-slot bag, stored as one flat inventory so
    /// moving a stack between them is just a slot swap.
    /// </summary>
    public class PlayerInventory : MonoBehaviour
    {
        public const int HotbarSize = 9;
        public const int BagSize = 27;
        public const int TotalSize = HotbarSize + BagSize;

        public MadVoxel.Inventory.Inventory Bag { get; private set; }
        public int SelectedIndex { get; private set; }

        public event Action SelectionChanged;

        public ItemStack SelectedStack { get { return Bag[SelectedIndex]; } }
        public ItemDefinition SelectedItem { get { return Bag[SelectedIndex].Item; } }

        void Awake()
        {
            if (Bag == null) Bag = new MadVoxel.Inventory.Inventory(TotalSize);
        }

        public void Select(int index)
        {
            if (index < 0) index = HotbarSize - 1;
            if (index >= HotbarSize) index = 0;
            if (index == SelectedIndex) return;
            SelectedIndex = index;
            if (SelectionChanged != null) SelectionChanged();
        }

        void Update()
        {
            if (!InputBridge.Enabled) return;

            int digit = InputBridge.HotbarDigitDown();
            if (digit >= 0) Select(digit);

            float scroll = InputBridge.ScrollDelta;
            if (scroll > 0.01f) Select(SelectedIndex - 1);
            else if (scroll < -0.01f) Select(SelectedIndex + 1);
        }

        /// <summary>Adds to the bag and tells the player what they picked up.</summary>
        public int Collect(ItemDefinition item, int count, bool announce = true)
        {
            if (item == null || count <= 0) return 0;
            int leftover = Bag.Add(item, count);
            int taken = count - leftover;
            if (announce && taken > 0) Notifications.PostFormat("+{0} {1}", taken, item.displayName);
            if (leftover > 0) Notifications.Post("Inventory full");
            return leftover;
        }

        public void ConsumeSelected(int count)
        {
            Bag.ConsumeFromSlot(SelectedIndex, count);
        }

        /// <summary>Returns true when the held tool broke.</summary>
        public bool WearSelected(int amount)
        {
            return Bag.DamageSlot(SelectedIndex, amount);
        }

        /// <summary>Moves the bag (not the hotbar) into a container. Used on death.</summary>
        public void DumpBagInto(MadVoxel.Inventory.Inventory target)
        {
            Bag.MoveAllTo(target, HotbarSize, TotalSize);
        }

        public static bool IsHotbarSlot(int index)
        {
            return index >= 0 && index < HotbarSize;
        }
    }
}
