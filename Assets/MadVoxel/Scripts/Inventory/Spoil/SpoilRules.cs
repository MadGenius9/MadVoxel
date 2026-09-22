using UnityEngine;

namespace MadVoxel.Inventory.Spoil
{
    /// <summary>
    /// How long food has left, and what it turns into. Two states only - good or
    /// spoiled - because a nutrition spreadsheet would be a different game.
    ///
    /// The clock is carried on the stack rather than derived from a planting hour like
    /// a crop, because a stack moves: harvested into a bag, tipped into a crate, and
    /// eventually into a fridge. What matters is how much life it has left, not when it
    /// was picked.
    /// </summary>
    public static class SpoilRules
    {
        /// <summary>A stack that has never been told its shelf life carries this.</summary>
        public const float Unset = -1f;

        public static bool CanSpoil(ItemDefinition item)
        {
            return item != null && item.spoilHours > 0f;
        }

        /// <summary>
        /// The shelf life a fresh stack starts with. Anything that cannot spoil reports
        /// <see cref="Unset"/> and is never ticked.
        /// </summary>
        public static float FreshLife(ItemDefinition item)
        {
            return CanSpoil(item) ? item.spoilHours : Unset;
        }

        /// <summary>
        /// Normalises a stack that came from an older save, a mod, or a code path that
        /// never set a shelf life. Food with no clock is treated as fresh rather than
        /// as rotten, which is the forgiving direction to be wrong in.
        /// </summary>
        public static float Normalise(ItemDefinition item, float remaining)
        {
            if (!CanSpoil(item)) return Unset;
            return remaining < 0f ? item.spoilHours : remaining;
        }

        /// <summary>
        /// How fast the clock runs where this stack is sitting. A fridge with watts in
        /// it is the only thing that slows it; a fridge without power is a cupboard.
        /// </summary>
        public static float RateIn(bool inFridge, bool fridgePowered, float fridgeSlowdown)
        {
            if (!inFridge || !fridgePowered) return 1f;
            return 1f / Mathf.Max(1f, fridgeSlowdown);
        }

        /// <summary>
        /// Advances a stack's clock. Returns the stack unchanged when it cannot spoil,
        /// with less life when it can, and as <paramref name="rotInto"/> when it has
        /// run out. A null rot item simply loses the stack, which is what a mod that
        /// declares no compost gets.
        /// </summary>
        public static ItemStack Tick(ItemStack stack, float gameHours, float rate, ItemDefinition rotInto)
        {
            if (stack.IsEmpty || !CanSpoil(stack.Item)) return stack;
            if (gameHours <= 0f || rate <= 0f) return stack;

            float remaining = Normalise(stack.Item, stack.SpoilRemaining) - gameHours * rate;

            if (remaining > 0f)
            {
                stack.SpoilRemaining = remaining;
                return stack;
            }

            var rot = rotInto != null ? rotInto : stack.Item.spoiledInto;
            if (rot == null) return ItemStack.Empty;

            // Rot does not stack with the food it came from, and it does not rot again.
            return new ItemStack(rot, stack.Count) { SpoilRemaining = Unset };
        }

        /// <summary>
        /// Merging keeps the older clock. Topping a crate up with fresh corn must not
        /// launder the corn that has been in there a week.
        /// </summary>
        public static float MergedLife(float a, float b)
        {
            if (a < 0f) return b;
            if (b < 0f) return a;
            return Mathf.Min(a, b);
        }

        /// <summary>
        /// The tooltip tail: "2d", "6h", or "SPOILED". Days once it is more than a day
        /// out, because an exact hour count is noise at that range.
        /// </summary>
        public static string Describe(ItemStack stack)
        {
            if (stack.IsEmpty || !CanSpoil(stack.Item)) return "";

            float remaining = Normalise(stack.Item, stack.SpoilRemaining);
            if (remaining <= 0f) return "SPOILED";

            if (remaining >= 24f) return Mathf.FloorToInt(remaining / 24f) + "d";
            return Mathf.CeilToInt(remaining) + "h";
        }

        /// <summary>True once a stack is close enough to worry about.</summary>
        public static bool IsGoingOff(ItemStack stack)
        {
            if (stack.IsEmpty || !CanSpoil(stack.Item)) return false;

            float remaining = Normalise(stack.Item, stack.SpoilRemaining);
            return remaining <= Mathf.Max(2f, stack.Item.spoilHours * 0.2f);
        }
    }
}
