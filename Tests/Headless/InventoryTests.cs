using System.Collections.Generic;
using MadVoxel.Content;
using MadVoxel.Inventory;
using UnityEngine;
using Inv = MadVoxel.Inventory.Inventory;

namespace MadVoxel.Headless
{
    /// <summary>Stacking, transfer, durability and the crafting rules, exercised for real.</summary>
    public static class InventoryTests
    {
        public static void Run(ContentDatabase db)
        {
            Stacking(db);
            Removal(db);
            Durability(db);
            Transfer(db);
            Crafting(db);
        }

        static void Stacking(ContentDatabase db)
        {
            Harness.Section("inventory: stacking");

            var wood = db.Item(ItemIds.WoodLog);
            var inv = new Inv(4);

            Harness.Equal(inv.Add(wood, 10), 0, "adding 10 logs to an empty bag leaves no remainder");
            Harness.Equal(inv.CountOf(wood), 10, "the bag reports 10 logs");

            // A second add must top up the existing stack rather than open a new slot.
            inv.Add(wood, 5);
            Harness.Equal(inv.CountOf(wood), 15, "a second add tops up the same stack");
            Harness.Equal(inv[1].IsEmpty, true, "and does not open a second slot");

            // Overflow past maxStack must spill into the next slot, not vanish.
            var small = new Inv(2);
            int leftover = small.Add(wood, wood.maxStack * 2);
            Harness.Equal(leftover, 0, "two full stacks fit in two slots");
            Harness.Equal(small.CountOf(wood), wood.maxStack * 2, "nothing was lost in the overflow");

            int overflow = small.Add(wood, 5);
            Harness.Equal(overflow, 5, "a full bag returns the whole remainder");
            Harness.Equal(small.CountOf(wood), wood.maxStack * 2, "a rejected add changes nothing");

            Harness.Equal(small.CanFit(wood, 1), false, "CanFit agrees the bag is full");
            Harness.Equal(new Inv(1).CanFit(wood, wood.maxStack), true, "CanFit accepts an exact fit");
        }

        static void Removal(ContentDatabase db)
        {
            Harness.Section("inventory: removal");

            var stone = db.Item(ItemIds.Stone);
            var inv = new Inv(4);
            inv.Add(stone, stone.maxStack + 7); // deliberately spanning two slots

            Harness.Equal(inv.Remove(stone, 5), 5, "removing 5 takes 5");
            Harness.Equal(inv.CountOf(stone), stone.maxStack + 2, "the count drops by exactly 5");

            // Removing across a slot boundary must drain both slots.
            int taken = inv.Remove(stone, stone.maxStack + 2);
            Harness.Equal(taken, stone.maxStack + 2, "a removal spanning two slots takes everything asked");
            Harness.Equal(inv.CountOf(stone), 0, "the bag is empty");
            Harness.Equal(inv.IsEmpty, true, "and reports itself empty");

            Harness.Equal(inv.Remove(stone, 10), 0, "removing from an empty bag takes nothing");
        }

        static void Durability(ContentDatabase db)
        {
            Harness.Section("inventory: tool durability");

            var axe = db.Item(ItemIds.StoneAxe);
            var inv = new Inv(4);
            inv.Add(axe, 3);

            Harness.Equal(inv.CountOf(axe), 3, "three axes occupy three slots");
            Harness.Equal(inv[0].Count, 1, "tools never stack");
            Harness.Equal(inv[0].Durability, axe.maxDurability, "a fresh tool starts at full durability");

            bool broke = inv.DamageSlot(0, axe.maxDurability - 1);
            Harness.Equal(broke, false, "a tool one point from death has not broken");
            Harness.Equal(inv[0].Durability, 1, "durability tracks the damage dealt");

            broke = inv.DamageSlot(0, 1);
            Harness.Equal(broke, true, "the last point breaks the tool");
            Harness.Equal(inv[0].IsEmpty, true, "a broken tool leaves the slot empty");

            // Two damaged tools must never merge into one stack.
            var a = new ItemStack(axe, 1, 50);
            var b = new ItemStack(axe, 1, 90);
            Harness.Equal(a.CanMergeWith(b), false, "damaged tools do not merge");
        }

        static void Transfer(ContentDatabase db)
        {
            Harness.Section("inventory: transfer between containers");

            var wood = db.Item(ItemIds.WoodLog);
            var stone = db.Item(ItemIds.Stone);

            var bag = new Inv(36);
            bag.Add(wood, 20);   // lands in slot 0 (the "hotbar")
            bag.SetSlot(9, new ItemStack(stone, 30));

            var crate = new Inv(24);
            bag.MoveAllTo(crate, 9, 36); // dump everything but the hotbar

            Harness.Equal(crate.CountOf(stone), 30, "the crate received the bag contents");
            Harness.Equal(bag.CountOf(stone), 0, "the bag no longer holds them");
            Harness.Equal(bag.CountOf(wood), 20, "the hotbar was left untouched");

            // A full destination must leave the overflow behind rather than delete it.
            var tiny = new Inv(1);
            var source = new Inv(4);
            source.Add(stone, stone.maxStack);
            source.Add(wood, 10);
            source.MoveAllTo(tiny);

            int total = tiny.CountOf(stone) + tiny.CountOf(wood) + source.CountOf(stone) + source.CountOf(wood);
            Harness.Equal(total, stone.maxStack + 10, "nothing is lost when the destination fills up");
        }

        static void Crafting(ContentDatabase db)
        {
            Harness.Section("crafting");

            var unlocked = new HashSet<string>();
            var plankRecipe = db.Recipe("madvoxel:craft_plank");
            Harness.Check(plankRecipe != null, "the plank recipe exists");
            if (plankRecipe == null) return;

            var bag = new Inv(36);
            Harness.Equal(CraftingService.CanCraft(bag, plankRecipe, CraftStation.Hand, unlocked), false,
                "cannot craft with an empty bag");
            Harness.Equal(CraftingService.Craft(bag, plankRecipe, CraftStation.Hand, unlocked), false,
                "a failed craft is refused outright");

            bag.Add(db.Item(ItemIds.WoodLog), 1);
            Harness.Equal(CraftingService.CanCraft(bag, plankRecipe, CraftStation.Hand, unlocked), true,
                "one log is enough for planks");

            Harness.Equal(CraftingService.Craft(bag, plankRecipe, CraftStation.Hand, unlocked), true, "the craft succeeds");
            Harness.Equal(bag.CountOf(db.Item(ItemIds.WoodLog)), 0, "the log was consumed");
            Harness.Equal(bag.CountOf(db.Item(ItemIds.Plank)), plankRecipe.outputCount, "the planks arrived");

            // Station gating.
            var smelt = db.Recipe("madvoxel:smelt_iron");
            Harness.Check(smelt != null && smelt.station == CraftStation.Campfire, "iron smelting needs a campfire");
            if (smelt != null)
            {
                var smelter = new Inv(36);
                smelter.Add(db.Item(ItemIds.IronOre), 10);
                smelter.Add(db.Item(ItemIds.Coal), 10);

                Harness.Equal(CraftingService.CanCraft(smelter, smelt, CraftStation.Hand, unlocked), false,
                    "smelting by hand is refused even with the materials");
                Harness.Equal(CraftingService.CanCraft(smelter, smelt, CraftStation.Campfire, unlocked), true,
                    "smelting at the campfire is allowed");
                Harness.Equal(CraftingService.CanCraft(smelter, smelt, CraftStation.Workbench, unlocked), false,
                    "the workbench does not substitute for the campfire");
            }

            // Hand recipes work at every station, so a bench never hides the basics.
            Harness.Equal(CraftingService.StationSatisfies(CraftStation.Workbench, CraftStation.Hand), true,
                "hand recipes remain available at a station");

            // Skill gating.
            var steel = db.Recipe("madvoxel:craft_block_steel");
            Harness.Check(steel != null && !steel.unlockedByDefault, "the steel block recipe starts locked");
            if (steel != null)
            {
                // Stocked from the recipe's own ingredient list rather than from a
                // hard-coded pair. This test is about the skill gate, not about what
                // steel happens to be made of this week - and listing the materials
                // here meant re-costing the recipe broke an unrelated assertion.
                var rich = new Inv(36);
                for (int i = 0; i < steel.ingredients.Count; i++)
                {
                    var ing = steel.ingredients[i];
                    if (ing.item != null) rich.Add(ing.item, ing.count * 4);
                }

                Harness.Equal(CraftingService.CanCraft(rich, steel, CraftStation.Workbench, unlocked), false,
                    "materials alone do not unlock steel");

                unlocked.Add(steel.stringId);
                Harness.Equal(CraftingService.CanCraft(rich, steel, CraftStation.Workbench, unlocked), true,
                    "unlocking the recipe makes it craftable");
            }

            // A craft must never run when the output cannot fit.
            var full = new Inv(1);
            full.Add(db.Item(ItemIds.Stone), db.Item(ItemIds.Stone).maxStack);
            var cobble = db.Recipe("madvoxel:craft_block_cobble");
            if (cobble != null)
            {
                bool crafted = CraftingService.Craft(full, cobble, CraftStation.Hand, unlocked);
                Harness.Equal(crafted, false, "a craft with nowhere to put the output is refused");
                Harness.Equal(full.CountOf(db.Item(ItemIds.Stone)), db.Item(ItemIds.Stone).maxStack,
                    "and the ingredients are not eaten");
            }
        }
    }
}
