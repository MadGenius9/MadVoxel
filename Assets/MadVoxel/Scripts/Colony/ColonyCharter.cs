using UnityEngine;

namespace MadVoxel.Colony
{
    /// <summary>What the board can see when someone tries to found a colony.</summary>
    public struct FoundingCheck
    {
        public bool HasCupboard;
        public int Beds;
        public bool HasCookingFire;
        public bool HasWater;
        public int FoodItems;
    }

    /// <summary>Why the charter was refused. The board shows these verbatim.</summary>
    public enum FoundResult
    {
        Ok,
        NoCupboard,
        NoBed,
        NoCookingFire,
        NoWater,
        NoFood,
        AlreadyFounded
    }

    /// <summary>
    /// The founding rules, and the two numbers the board lives on. Pure, because
    /// "can I found here" is a question a player will ask over and over while walking
    /// around a half-built base, and it has to give the same answer every time.
    ///
    /// The requirements are not a tax. Each one is a thing a colonist will need on
    /// their first day: somewhere to sleep, something to cook on, water, food, and a
    /// cupboard to say whose ground it is. A colony founded without them would simply
    /// starve in front of you, which is worse than being told no.
    /// </summary>
    public static class ColonyCharter
    {
        public static FoundResult Evaluate(FoundingCheck check, ColonyRules rules, bool alreadyFounded)
        {
            if (alreadyFounded) return FoundResult.AlreadyFounded;

            // The cupboard is the charter. No claim, no colony - that is the whole
            // link between this system and the base you already built.
            if (!check.HasCupboard) return FoundResult.NoCupboard;

            int bedsNeeded = rules != null ? Mathf.Max(1, rules.bedsRequired) : 1;
            if (check.Beds < bedsNeeded) return FoundResult.NoBed;

            if (!check.HasCookingFire) return FoundResult.NoCookingFire;
            if (!check.HasWater) return FoundResult.NoWater;

            int foodNeeded = rules != null ? Mathf.Max(1, rules.foodRequired) : 1;
            if (check.FoodItems < foodNeeded) return FoundResult.NoFood;

            return FoundResult.Ok;
        }

        public static string Describe(FoundResult result)
        {
            switch (result)
            {
                case FoundResult.Ok: return "";
                case FoundResult.NoCupboard: return "Plant a tool cupboard first - the claim is the charter";
                case FoundResult.NoBed: return "Nobody will stay without a bed";
                case FoundResult.NoCookingFire: return "You need a campfire or a stove";
                case FoundResult.NoWater: return "You need a tap or a water barrel";
                case FoundResult.NoFood: return "Put some food in a box or a fridge first";
                case FoundResult.AlreadyFounded: return "This colony already exists";
                default: return "Cannot found a colony here";
            }
        }

        /// <summary>
        /// Days of food in hand at the current population. Zero colonists means the
        /// stores last forever, which the board says as a dash rather than infinity.
        /// </summary>
        public static float FoodDays(int foodItems, int colonists, ColonyRules rules)
        {
            if (colonists <= 0) return float.MaxValue;

            float perDay = rules != null ? Mathf.Max(0.1f, rules.foodPerColonistPerDay) : 3f;
            return foodItems / (perDay * colonists);
        }

        public static float WaterDays(float litres, int colonists, ColonyRules rules)
        {
            if (colonists <= 0) return float.MaxValue;

            float perDay = rules != null ? Mathf.Max(0.1f, rules.litresPerColonistPerDay) : 4f;
            return litres / (perDay * colonists);
        }

        /// <summary>"4.2d", or a dash when there is nobody to feed.</summary>
        public static string DescribeDays(float days)
        {
            if (days >= float.MaxValue) return "-";
            if (days <= 0f) return "NONE";
            if (days >= 100f) return "99+d";
            return days.ToString("0.0") + "d";
        }

        /// <summary>
        /// True once the stores are short enough that the player should act. Two days
        /// is about one blood-moon cycle of warning.
        /// </summary>
        public static bool IsShort(float days)
        {
            return days < 2f;
        }
    }
}
