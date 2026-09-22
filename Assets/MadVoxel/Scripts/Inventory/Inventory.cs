using System;
using System.Collections.Generic;
using UnityEngine;

namespace MadVoxel.Inventory
{
    /// <summary>
    /// A fixed-size bag of stacks. Plain C# - the player, storage crates and the death
    /// backpack all use the same class, and nothing here knows about Unity scenes.
    /// </summary>
    public class Inventory
    {
        readonly ItemStack[] _slots;

        public event Action Changed;

        public Inventory(int size)
        {
            _slots = new ItemStack[size];
        }

        public int Size { get { return _slots.Length; } }

        public ItemStack this[int index]
        {
            get { return index >= 0 && index < _slots.Length ? _slots[index] : ItemStack.Empty; }
        }

        public void SetSlot(int index, ItemStack stack)
        {
            if (index < 0 || index >= _slots.Length) return;
            _slots[index] = stack.Count <= 0 ? ItemStack.Empty : stack;
            RaiseChanged();
        }

        public void RaiseChanged()
        {
            if (Changed != null) Changed();
        }

        /// <summary>Adds what fits and returns the remainder.</summary>
        public int Add(ItemDefinition item, int count, int durability = -1)
        {
            if (item == null || count <= 0) return 0;
            int remaining = count;

            if (!item.HasDurability)
            {
                for (int i = 0; i < _slots.Length && remaining > 0; i++)
                {
                    if (_slots[i].Item != item) continue;
                    int space = _slots[i].SpaceLeft;
                    if (space <= 0) continue;
                    int moved = Mathf.Min(space, remaining);
                    _slots[i].Count += moved;
                    remaining -= moved;
                }
            }

            for (int i = 0; i < _slots.Length && remaining > 0; i++)
            {
                if (!_slots[i].IsEmpty) continue;
                int moved = item.HasDurability ? 1 : Mathf.Min(item.maxStack, remaining);
                _slots[i] = new ItemStack(item, moved, durability >= 0 ? durability : item.maxDurability);
                remaining -= moved;
            }

            if (remaining != count) RaiseChanged();
            return remaining;
        }

        public int Add(ItemStack stack)
        {
            return Add(stack.Item, stack.Count, stack.Durability);
        }

        public bool CanFit(ItemDefinition item, int count)
        {
            if (item == null) return true;
            int space = 0;
            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i].IsEmpty) space += item.maxStack;
                else if (_slots[i].Item == item && !item.HasDurability) space += _slots[i].SpaceLeft;
                if (space >= count) return true;
            }
            return space >= count;
        }

        public int CountOf(ItemDefinition item)
        {
            if (item == null) return 0;
            int total = 0;
            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i].Item == item) total += _slots[i].Count;
            }
            return total;
        }

        /// <summary>Removes up to count and returns how many were actually taken.</summary>
        public int Remove(ItemDefinition item, int count)
        {
            if (item == null || count <= 0) return 0;
            int remaining = count;
            for (int i = 0; i < _slots.Length && remaining > 0; i++)
            {
                if (_slots[i].Item != item) continue;
                int taken = Mathf.Min(_slots[i].Count, remaining);
                _slots[i].Count -= taken;
                if (_slots[i].Count <= 0) _slots[i] = ItemStack.Empty;
                remaining -= taken;
            }
            if (remaining != count) RaiseChanged();
            return count - remaining;
        }

        public ItemStack TakeSlot(int index)
        {
            if (index < 0 || index >= _slots.Length) return ItemStack.Empty;
            var stack = _slots[index];
            _slots[index] = ItemStack.Empty;
            if (!stack.IsEmpty) RaiseChanged();
            return stack;
        }

        public void ConsumeFromSlot(int index, int count)
        {
            if (index < 0 || index >= _slots.Length || _slots[index].IsEmpty) return;
            _slots[index].Count -= count;
            if (_slots[index].Count <= 0) _slots[index] = ItemStack.Empty;
            RaiseChanged();
        }

        /// <summary>Applies tool wear. Returns true when the tool broke.</summary>
        public bool DamageSlot(int index, int amount)
        {
            if (index < 0 || index >= _slots.Length) return false;
            var stack = _slots[index];
            if (stack.IsEmpty || !stack.Item.HasDurability) return false;

            stack.Durability -= amount;
            if (stack.Durability <= 0)
            {
                _slots[index] = ItemStack.Empty;
                RaiseChanged();
                return true;
            }
            _slots[index] = stack;
            RaiseChanged();
            return false;
        }

        /// <summary>Moves everything that fits into another inventory. Used by the death backpack.</summary>
        public void MoveAllTo(Inventory target, int fromIndex = 0, int toIndexExclusive = -1)
        {
            if (toIndexExclusive < 0) toIndexExclusive = _slots.Length;
            for (int i = fromIndex; i < toIndexExclusive && i < _slots.Length; i++)
            {
                if (_slots[i].IsEmpty) continue;
                int left = target.Add(_slots[i]);
                _slots[i] = _slots[i].WithCount(left);
            }
            RaiseChanged();
            target.RaiseChanged();
        }

        public void Clear(int fromIndex = 0, int toIndexExclusive = -1)
        {
            if (toIndexExclusive < 0) toIndexExclusive = _slots.Length;
            for (int i = fromIndex; i < toIndexExclusive && i < _slots.Length; i++) _slots[i] = ItemStack.Empty;
            RaiseChanged();
        }

        public bool IsEmpty
        {
            get
            {
                for (int i = 0; i < _slots.Length; i++) if (!_slots[i].IsEmpty) return false;
                return true;
            }
        }

        public IEnumerable<ItemStack> Stacks
        {
            get
            {
                for (int i = 0; i < _slots.Length; i++) yield return _slots[i];
            }
        }
    }
}
