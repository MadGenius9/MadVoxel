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

        MadVoxel.Inventory.Inventory _bag;

        /// <summary>Created on first access so subscribers never race component Awake.</summary>
        public MadVoxel.Inventory.Inventory Bag
        {
            get
            {
                if (_bag == null) _bag = new MadVoxel.Inventory.Inventory(TotalSize);
                return _bag;
            }
        }

        public int SelectedIndex { get; private set; }

        public event Action SelectionChanged;

        public ItemStack SelectedStack { get { return Bag[SelectedIndex]; } }
        public ItemDefinition SelectedItem { get { return Bag[SelectedIndex].Item; } }

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
        /// <summary>Keeps a multi-stack harvest from firing the pickup sound repeatedly.</summary>
        float _nextPickupSound;

        public int Collect(ItemDefinition item, int count, bool announce = true)
        {
            if (item == null || count <= 0) return 0;
            int leftover = Bag.Add(item, count);
            int taken = count - leftover;

            if (taken > 0)
            {
                // The one place everything the player picks up passes through, so the
                // sound only has to be wired here. Rate limited: a harvest that yields
                // three stacks at once is one pickup to the ear.
                if (Time.time >= _nextPickupSound)
                {
                    _nextPickupSound = Time.time + 0.08f;
                    MadVoxel.Audio.GameAudio.Play(MadVoxel.Audio.Sound.Pickup, 0.1f, 0.55f);
                }

                if (announce) Notifications.PostFormat("+{0} {1}", taken, item.displayName);
            }

            if (leftover > 0)
            {
                Notifications.Post("Inventory full");
                MadVoxel.Audio.GameAudio.Play(MadVoxel.Audio.Sound.Denied);
            }

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
