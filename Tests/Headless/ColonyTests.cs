using MadVoxel.Colony;
using MadVoxel.Content;
using UnityEngine;

namespace MadVoxel.Headless
{
    /// <summary>
    /// The colony's rules, without any people in a scene. Founding is a checklist the
    /// player will run into over and over, and the slide into a walk-out is the thing
    /// that has to be legible long before it happens.
    /// </summary>
    public static class ColonyTests
    {
        public static void Run(ContentDatabase db)
        {
            Founding(db);
            Supplies(db);
            Morale(db);
            Content(db);
        }

        static FoundingCheck Complete()
        {
            return new FoundingCheck
            {
                HasCupboard = true,
                Beds = 1,
                HasCookingFire = true,
                HasWater = true,
                FoodItems = 4
            };
        }

        // ---------------------------------------------------------------- founding

        static void Founding(ContentDatabase db)
        {
            Harness.Section("colony: founding");

            var rules = db.colonyRules;
            Harness.Check(rules != null, "the colony rules are authored");

            Harness.Equal(ColonyCharter.Evaluate(Complete(), rules, false), FoundResult.Ok,
                "a claim with a bed, a fire, water and food can be chartered");

            // The cupboard is the charter. This is the link to the base you already have.
            var noClaim = Complete();
            noClaim.HasCupboard = false;
            Harness.Equal(ColonyCharter.Evaluate(noClaim, rules, false), FoundResult.NoCupboard,
                "no cupboard, no colony");

            var noBed = Complete();
            noBed.Beds = 0;
            Harness.Equal(ColonyCharter.Evaluate(noBed, rules, false), FoundResult.NoBed,
                "nobody stays without a bed");

            var noFire = Complete();
            noFire.HasCookingFire = false;
            Harness.Equal(ColonyCharter.Evaluate(noFire, rules, false), FoundResult.NoCookingFire,
                "and without something to cook on");

            var noWater = Complete();
            noWater.HasWater = false;
            Harness.Equal(ColonyCharter.Evaluate(noWater, rules, false), FoundResult.NoWater,
                "or water");

            var noFood = Complete();
            noFood.FoodItems = 0;
            Harness.Equal(ColonyCharter.Evaluate(noFood, rules, false), FoundResult.NoFood,
                "or anything in the larder");

            Harness.Equal(ColonyCharter.Evaluate(Complete(), rules, true), FoundResult.AlreadyFounded,
                "and a colony cannot be founded twice");

            // The cupboard check must come first, because it is the one that explains
            // the whole system: everything else is a shopping list.
            var nothing = new FoundingCheck();
            Harness.Equal(ColonyCharter.Evaluate(nothing, rules, false), FoundResult.NoCupboard,
                "an empty claim is told about the cupboard first");

            for (int i = 0; i < 7; i++)
            {
                var verdict = (FoundResult)i;
                string text = ColonyCharter.Describe(verdict);
                Harness.Check(text != null && (verdict == FoundResult.Ok || text.Length > 0),
                    "every founding refusal says what to go and build: " + verdict);
            }
        }

        // ---------------------------------------------------------------- supplies

        static void Supplies(ContentDatabase db)
        {
            Harness.Section("colony: supplies");

            var rules = db.colonyRules;

            // Nobody to feed means the stores last forever, and the board says so with
            // a dash rather than a made-up number.
            Harness.Equal(ColonyCharter.DescribeDays(ColonyCharter.FoodDays(10, 0, rules)), "-",
                "an empty colony reports no countdown at all");

            float days = ColonyCharter.FoodDays(18, 2, rules);
            Harness.Check(Mathf.Abs(days - 3f) < 0.01f, "eighteen items feeds two people for three days");

            float water = ColonyCharter.WaterDays(24f, 2, rules);
            Harness.Check(Mathf.Abs(water - 3f) < 0.01f, "and twenty-four litres waters them for three");

            Harness.Equal(ColonyCharter.DescribeDays(0f), "NONE", "an empty larder says NONE");
            Harness.Equal(ColonyCharter.DescribeDays(4.2f), "4.2d", "and days read to one decimal");
            Harness.Equal(ColonyCharter.DescribeDays(500f), "99+d", "a silly stockpile does not print a silly number");

            Harness.Check(ColonyCharter.IsShort(1.5f), "a day and a half is short");
            Harness.Check(!ColonyCharter.IsShort(3f), "three days is not");

            // More mouths, less time. Obvious, and the thing the board exists to show.
            Harness.Check(ColonyCharter.FoodDays(18, 3, rules) < ColonyCharter.FoodDays(18, 2, rules),
                "the same larder lasts a third colonist less time");
        }

        // ------------------------------------------------------------------ morale

        static void Morale(ContentDatabase db)
        {
            Harness.Section("colony: morale");

            var person = db.colonyRules.colonist;
            Harness.Check(person != null, "the colonist definition is authored");
            Harness.Check(person.names.Count >= 3, "there are enough names for a full colony");

            var settled = new MoraleContext { HasBed = true };
            float rising = ColonyMorale.Step(50f, settled, person, 4f);
            Harness.Check(rising > 50f, "a fed, watered, rested colonist cheers up");

            var hungry = new MoraleContext { Hungry = true, HasBed = true };
            float falling = ColonyMorale.Step(50f, hungry, person, 4f);
            Harness.Check(falling < 50f, "a hungry one does not");

            var homeless = new MoraleContext();
            Harness.Check(ColonyMorale.Step(50f, homeless, person, 4f) < 50f, "nor does one with no bed");

            // Thirst has to bite hardest, because water is the thing a broken pump takes
            // away - that is the whole link between the plumbing and the people.
            Harness.Check(person.MoralePerHour(MoraleReason.Thirsty) < person.MoralePerHour(MoraleReason.Hungry),
                "thirst is worse than hunger");
            Harness.Check(person.MoralePerHour(MoraleReason.Dark) < 0f, "working in the dark is a nag");

            var thirsty = new MoraleContext { Thirsty = true, HasBed = true };
            Harness.Equal(ColonyMorale.Dominant(thirsty, person), MoraleReason.Thirsty,
                "the board names the loudest complaint");

            var both = new MoraleContext { Hungry = true, Thirsty = true, HasBed = true };
            Harness.Equal(ColonyMorale.Dominant(both, person), MoraleReason.Thirsty,
                "and picks thirst over hunger when both bite");

            // Morale is clamped, so a long neglect cannot go negative and take a week to
            // climb back out of.
            float floored = 5f;
            for (int i = 0; i < 40; i++) floored = ColonyMorale.Step(floored, both, person, 4f);
            Harness.Equal(floored, 0f, "misery bottoms out at zero rather than running away");

            float capped = 95f;
            for (int i = 0; i < 40; i++) capped = ColonyMorale.Step(capped, settled, person, 4f);
            Harness.Equal(capped, ColonyMorale.Max, "and contentment tops out at a hundred");

            Harness.Check(ColonyMorale.IsSulking(person.sulkBelow - 1f, person), "below the sulk line they down tools");
            Harness.Check(!ColonyMorale.IsLeaving(person.sulkBelow - 1f, person), "but they are still here");
            Harness.Check(ColonyMorale.IsLeaving(person.leaveBelow - 1f, person), "below the leave line they walk");

            Harness.Check(person.leaveBelow < person.sulkBelow,
                "they always stop working before they walk out, so there is a warning");

            // The slide has to be slow enough to notice and act on. A thirsty colonist
            // starting content must take most of a day to reach the sulk line.
            float morale = person.startingMorale;
            float hours = 0f;
            while (morale > person.sulkBelow && hours < 200f)
            {
                morale = ColonyMorale.Step(morale, thirsty, person, 1f);
                hours += 1f;
            }
            Harness.Check(hours > 6f, string.Format("the slide to downing tools takes {0}h, not minutes", hours));
            Harness.Check(hours < 48f, "but it does happen inside a couple of days");

            Harness.Equal(ColonyMorale.Describe(5f, person), "LEAVING", "the board says LEAVING");
            Harness.Equal(ColonyMorale.Describe(90f, person), "CONTENT", "and CONTENT");
        }

        // ----------------------------------------------------------------- content

        static void Content(ContentDatabase db)
        {
            Harness.Section("colony: content");

            var rules = db.colonyRules;

            Harness.Check(rules.maxColonists >= 1 && rules.maxColonists <= 6,
                string.Format("the cap is {0} - a farm, not a city", rules.maxColonists));
            Harness.Check(rules.foodPerColonistPerDay > 0f && rules.litresPerColonistPerDay > 0f,
                "a colonist eats and drinks something");

            var board = db.Structure(StructureIds.ColonyBoard);
            Harness.Check(board != null, "the colony board is a real deployable");
            Harness.Equal(board.kind, MadVoxel.Building.StructureKind.ColonyBoard, "and carries the board behaviour");
            Harness.Check(!board.blocksMovement, "a plaque on a wall does not block the doorway");

            var item = db.Item(ItemIds.PieceColonyBoard);
            Harness.Check(item != null && item.placeableStructure == board, "and there is an item that places it");

            var recipe = db.Recipe("madvoxel:craft_colony_board");
            Harness.Check(recipe != null, "the board is craftable");
            Harness.Check(recipe.unlockedByDefault,
                "and not perk-gated - founding a colony must not wait on a skill tree");

            // The people have to be feedable from the garden that already exists.
            var person = rules.colonist;
            float mealsPerDay = person.hungerPerHour * 24f / person.mealRestores;
            Harness.Check(mealsPerDay > 0.5f && mealsPerDay < 3f,
                string.Format("a colonist eats {0:0.0} meals a day - a garden can keep up", mealsPerDay));

            float drinksPerDay = person.thirstPerHour * 24f / person.drinkRestores;
            Harness.Check(drinksPerDay > 0.5f && drinksPerDay < 4f,
                string.Format("and drinks {0:0.0} times a day", drinksPerDay));
        }
    }
}
