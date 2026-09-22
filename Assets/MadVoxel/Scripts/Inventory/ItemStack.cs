using System;

namespace MadVoxel.Inventory
{
    /// <summary>One inventory slot's worth of item. A null Item means an empty slot.</summary>
    [Serializable]
    public struct ItemStack
    {
        public ItemDefinition Item;
        public int Count;
        /// <summary>Remaining durability for tools. Ignored when the item has none.</summary>
        public int Durability;

        public static readonly ItemStack Empty = new ItemStack();

        public ItemStack(ItemDefinition item, int count)
        {
            Item = item;
            Count = count;
            Durability = item != null ? item.maxDurability : 0;
        }

        public ItemStack(ItemDefinition item, int count, int durability)
        {
            Item = item;
            Count = count;
            Durability = durability;
        }

        public bool IsEmpty { get { return Item == null || Count <= 0; } }

        public int MaxStack { get { return Item != null ? Item.maxStack : 0; } }

        public int SpaceLeft { get { return IsEmpty ? 0 : Item.maxStack - Count; } }

        /// <summary>Two stacks merge only when they are the same item and not damaged tools.</summary>
        public bool CanMergeWith(ItemStack other)
        {
            if (IsEmpty || other.IsEmpty) return false;
            if (Item != other.Item) return false;
            if (Item.HasDurability) return false;
            return true;
        }

        public ItemStack WithCount(int count)
        {
            return count <= 0 ? Empty : new ItemStack(Item, count, Durability);
        }

        public override string ToString()
        {
            return IsEmpty ? "(empty)" : string.Format("{0} x{1}", Item.displayName, Count);
        }
    }
}
