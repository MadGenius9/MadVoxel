using System;

namespace MadVoxel.Inventory
{
    /// <summary>One inventory slot's worth of item. A null Item means an empty slot.</summary>
    [Serializable]
    public struct ItemStack
    {
        public ItemDefinition Item;
        public int Count;

        /// <summary>
        /// Game hours of shelf life left. Negative means "never asked" - food that has
        /// not been told its clock yet, which the spoil rules treat as fresh. Items
        /// that cannot spoil leave it negative forever.
        /// </summary>
        public float SpoilRemaining;
        /// <summary>Remaining durability for tools. Ignored when the item has none.</summary>
        public int Durability;

        public static readonly ItemStack Empty = new ItemStack();

        public ItemStack(ItemDefinition item, int count)
        {
            SpoilRemaining = item != null && item.spoilHours > 0f ? item.spoilHours : -1f;
            Item = item;
            Count = count;
            Durability = item != null ? item.maxDurability : 0;
        }

        public ItemStack(ItemDefinition item, int count, int durability)
        {
            SpoilRemaining = item != null && item.spoilHours > 0f ? item.spoilHours : -1f;
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

        /// <summary>
        /// Splitting keeps the shelf life. Without this, halving a week-old stack would
        /// hand back two fresh ones, and the fridge would be pointless.
        /// </summary>
        public ItemStack WithCount(int count)
        {
            if (count <= 0) return Empty;

            var copy = new ItemStack(Item, count, Durability);
            copy.SpoilRemaining = SpoilRemaining;
            return copy;
        }

        /// <summary>Same stack, different clock. Used by the spoilage tick.</summary>
        public ItemStack WithSpoil(float remaining)
        {
            var copy = this;
            copy.SpoilRemaining = remaining;
            return copy;
        }

        public override string ToString()
        {
            return IsEmpty ? "(empty)" : string.Format("{0} x{1}", Item.displayName, Count);
        }
    }
}
