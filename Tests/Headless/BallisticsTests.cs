using System.Collections.Generic;
using MadVoxel.Combat;
using MadVoxel.Content;
using MadVoxel.Inventory;
using UnityEngine;

namespace MadVoxel.Headless
{
    /// <summary>
    /// The arrow. Everything a player learns about a bow is learned by watching where
    /// shots land, so the arc has to be consistent - and the aim prediction has to
    /// integrate exactly the way the arrow does, or the game lies about the shot.
    /// </summary>
    public static class BallisticsTests
    {
        public static void Run(ContentDatabase db)
        {
            Draw();
            Arc();
            Prediction();
            if (db != null) Weapons(db);
        }

        // ------------------------------------------------------------------- draw

        static void Draw()
        {
            Harness.Section("ballistics: the draw");

            Harness.Equal(Ballistics.Draw01(0f, 1f), 0f, "an untouched string is no draw");
            Harness.Equal(Ballistics.Draw01(0.5f, 1f), 0.5f, "half the time is half the draw");
            Harness.Equal(Ballistics.Draw01(5f, 1f), 1f, "and holding longer does not overdraw");
            Harness.Equal(Ballistics.Draw01(0.2f, 0f), 1f, "a bow with no draw time is always full");

            Harness.Check(!Ballistics.CanRelease(0f), "you cannot loose an undrawn bow");
            Harness.Check(!Ballistics.CanRelease(Ballistics.MinimumDraw - 0.01f),
                "nor one barely pulled - the arrow is worth more than the shot");
            Harness.Check(Ballistics.CanRelease(Ballistics.MinimumDraw), "at the threshold it goes");
            Harness.Check(Ballistics.CanRelease(1f), "and a full draw certainly does");

            // Speed and damage both rise with draw, and neither reaches zero or
            // overshoots. A snapped shot has to be worth taking under pressure.
            Harness.Equal(Ballistics.LaunchSpeed(14f, 42f, 0f), 14f, "an unheld shot leaves at the floor speed");
            Harness.Equal(Ballistics.LaunchSpeed(14f, 42f, 1f), 42f, "a full draw at the top");
            Harness.Equal(Ballistics.LaunchSpeed(14f, 42f, 0.5f), 28f, "and halfway is halfway");
            Harness.Equal(Ballistics.LaunchSpeed(14f, 42f, 9f), 42f, "overdraw cannot exceed the bow");

            Harness.Equal(Ballistics.Damage(100f, 1f), 100f, "a full draw does the weapon's rating");
            Harness.Equal(Ballistics.Damage(100f, 0f), Ballistics.MinDamageFraction * 100f,
                "and the least draw still does the floor");
            Harness.Check(Ballistics.Damage(100f, 0.5f) > Ballistics.Damage(100f, 0.2f),
                "more draw is always more damage");
            Harness.Equal(Ballistics.Damage(0f, 1f), 0f, "a weapon with no rating does nothing");
            Harness.Check(Ballistics.Damage(-50f, 1f) >= 0f, "and a negative rating cannot heal a zombie");

            Harness.Equal(Ballistics.DrawLine(1f), "FULL DRAW", "full draw says so");
            Harness.Equal(Ballistics.DrawLine(0.5f), "50%", "and part way reads as a percentage");
            Harness.Equal(Ballistics.DrawLine(0f), "DRAWING", "below the threshold it is not a shot yet");
        }

        // -------------------------------------------------------------------- arc

        static void Arc()
        {
            Harness.Section("ballistics: the arc");

            // Fired level, an arrow must lose height and never gain it.
            var position = Vector3.zero;
            var velocity = new Vector3(0f, 0f, 30f);

            float previousY = position.y;
            bool everRose = false;

            for (int i = 0; i < 60; i++)
            {
                Ballistics.Step(ref position, ref velocity, 1f / 60f);
                if (position.y > previousY + 0.0001f) everRose = true;
                previousY = position.y;
            }

            Harness.Check(!everRose, "a level shot never climbs");
            Harness.Check(position.y < 0f, "it falls");
            Harness.Check(position.z > 25f, string.Format("and still carries {0:0} m in a second", position.z));

            // Faster arrows drop less over the same ground, which is the entire reason
            // to hold the draw.
            Harness.Check(Ballistics.DropOver(30f, 42f) < Ballistics.DropOver(30f, 14f),
                "a fast arrow drops less over thirty metres than a slow one");
            Harness.Equal(Ballistics.DropOver(0f, 42f), 0f, "no distance is no drop");
            Harness.Equal(Ballistics.DropOver(30f, 0f), 0f, "and a stationary arrow is not modelled");

            // Drop is quadratic in distance: doubling the range roughly quadruples it.
            float near = Ballistics.DropOver(20f, 30f);
            float far = Ballistics.DropOver(40f, 30f);
            Harness.Check(Mathf.Abs(far / Mathf.Max(0.0001f, near) - 4f) < 0.01f,
                "twice the distance is four times the drop");

            Harness.Check(Ballistics.LevelRange(42f, 1.6f) > Ballistics.LevelRange(14f, 1.6f),
                "a full draw carries further from the same height");
            Harness.Equal(Ballistics.LevelRange(42f, 0f), 0f, "and a shot from the floor goes nowhere");

            // The integrator must not care about frame rate. A shot at 30 fps and the
            // same shot at 120 have to land in the same place, or the bow is a
            // different weapon on a different machine.
            var coarse = Vector3.zero;
            var coarseV = new Vector3(0f, 0f, 30f);
            for (int i = 0; i < 30; i++) Ballistics.Step(ref coarse, ref coarseV, 1f / 30f);

            var fine = Vector3.zero;
            var fineV = new Vector3(0f, 0f, 30f);
            for (int i = 0; i < 120; i++) Ballistics.Step(ref fine, ref fineV, 1f / 120f);

            Harness.Check(Mathf.Abs(coarse.z - fine.z) < 0.01f,
                "range is the same at 30 fps and 120");

            // THE one that matters for a weapon people aim by feel. Euler would put
            // these a quarter of a metre apart; the exact solution makes them identical.
            Harness.Check(Mathf.Abs(coarse.y - fine.y) < 0.01f,
                string.Format("and so is the drop, to the centimetre ({0:0.000} vs {1:0.000})", coarse.y, fine.y));

            // And the flight must agree with the closed-form drop the readout uses,
            // because they are now the same equation written twice.
            var level = Vector3.zero;
            var levelV = Vector3.forward * 30f;
            for (int i = 0; i < 60; i++) Ballistics.Step(ref level, ref levelV, 1f / 60f);

            Harness.Check(Mathf.Abs(-level.y - Ballistics.DropOver(level.z, 30f)) < 0.02f,
                "the flown drop matches the drop the readout predicts");
        }

        // --------------------------------------------------------------- prediction

        static void Prediction()
        {
            Harness.Section("ballistics: where the dot says it will land");

            // Nothing in the way: the arc just runs out of time.
            var far = Ballistics.PredictImpact(Vector3.zero, Vector3.forward, 30f, 1f, null);
            Harness.Check(far.z > 25f, "with nothing in the way the shot carries");
            Harness.Check(far.y < 0f, "and has dropped");

            // A wall at twenty metres stops it there, not past it.
            System.Func<Vector3, bool> wall = p => p.z >= 20f;
            var stopped = Ballistics.PredictImpact(Vector3.zero, Vector3.forward, 30f, 3f, wall);

            Harness.Check(stopped.z < 20f, string.Format("a wall at 20 m stops the dot at {0:0.00} m", stopped.z));
            Harness.Check(stopped.z > 19.9f,
                "within a few centimetres of it - a dot that is reliably short is worse than none");

            // THE property the aim dot exists for: it has to agree with the arrow.
            // Both walk the same Step, so a prediction and a flight over the same time
            // must land in the same place.
            var flown = Vector3.zero;
            var flownV = Vector3.forward * 30f;
            for (int i = 0; i < 30; i++) Ballistics.Step(ref flown, ref flownV, 1f / 30f);

            var predicted = Ballistics.PredictImpact(Vector3.zero, Vector3.forward, 30f, 1f, null);
            Harness.Check((flown - predicted).magnitude < 0.05f,
                string.Format("the dot and the arrow agree to within {0:0.000} m", (flown - predicted).magnitude));

            // Ground under your feet stops it immediately rather than looping.
            System.Func<Vector3, bool> floor = p => p.y <= -0.5f;
            var short_ = Ballistics.PredictImpact(Vector3.zero, Vector3.forward, 5f, 5f, floor);
            Harness.Check(short_.z < 3f, "a slow shot buries itself close by");

            // An absurd flight time must still terminate.
            var capped = Ballistics.PredictImpact(Vector3.zero, Vector3.forward, 30f, 0f, null);
            Harness.Check(capped.z >= 0f, "a zero-second prediction still returns a point");
        }

        // ---------------------------------------------------------------- weapons

        static void Weapons(ContentDatabase db)
        {
            Harness.Section("ballistics: the bow as shipped");

            var bow = db.Item(ItemIds.WoodBow);
            Harness.Check(bow != null, "there is a bow");
            if (bow == null) return;

            Harness.Check(bow.IsRanged, "which counts as a ranged weapon");
            Harness.Check(bow.ammoItem != null, "and knows what it shoots");
            Harness.Check(bow.maxLaunchSpeed > bow.minLaunchSpeed,
                "a full draw is faster than a snapped one");
            Harness.Check(bow.drawSeconds > 0f, "and takes time to draw");
            Harness.Check(bow.meleeDamage < bow.rangedDamage,
                "hitting someone with the bow is much worse than shooting them");

            var stone = db.Item(ItemIds.ArrowStone);
            var iron = db.Item(ItemIds.ArrowIron);

            Harness.Check(stone != null && iron != null, "there are two kinds of arrow");
            if (stone == null || iron == null) return;

            Harness.Check(iron.rangedDamage > stone.rangedDamage, "iron hits harder than stone");

            // The bow picks the best arrow in the bag at the string rather than firing
            // whatever its definition names. Both heads must therefore be findable the
            // same way, or the better one is craftable and unfireable - which is what
            // the iron arrow was.
            Harness.Equal((int)stone.category, (int)ItemCategory.Ammo, "stone arrows read as ammunition");
            Harness.Equal((int)iron.category, (int)ItemCategory.Ammo, "and so do iron ones");
            Harness.Check(bow.ammoItem.category == ItemCategory.Ammo,
                "and the bow's fallback is ammunition too");
            Harness.Check(!stone.IsRanged && !iron.IsRanged,
                "and an arrow is not itself a bow, whatever damage it carries");

            // The numbers that decide whether a bow is worth carrying. A shambler has
            // to take more than one full-draw arrow, or the horde stops being a threat.
            float full = Ballistics.Damage(bow.rangedDamage + stone.rangedDamage, 1f);
            float snap = Ballistics.Damage(bow.rangedDamage + stone.rangedDamage, Ballistics.MinimumDraw);

            var shambler = db.Zombie(ZombieIds.Shambler);
            if (shambler != null)
            {
                Harness.Check(full < shambler.maxHealth,
                    string.Format("a full draw does {0:0} of a shambler's {1:0} health - not a one-shot",
                        full, shambler.maxHealth));
                Harness.Check(full * 3f > shambler.maxHealth,
                    "but three arrows do put one down");
                Harness.Check(snap * 8f < shambler.maxHealth * 2f,
                    "while panicked snap shots are a poor way to fight");
            }

            // Craftable from what you find in the first hour, or a blood moon is a wall.
            RecipeDefinition bowRecipe = null, arrowRecipe = null;
            for (int i = 0; i < db.recipes.Count; i++)
            {
                if (db.recipes[i].output == bow) bowRecipe = db.recipes[i];
                if (db.recipes[i].output == stone) arrowRecipe = db.recipes[i];
            }

            Harness.Check(bowRecipe != null && bowRecipe.unlockedByDefault,
                "the bow is craftable from the start");
            Harness.Check(arrowRecipe != null && arrowRecipe.unlockedByDefault,
                "and so are arrows");
            Harness.Check(arrowRecipe != null && arrowRecipe.outputCount > 1,
                "arrows come more than one at a time - one per craft would be misery");
            Harness.Check(bowRecipe != null && bowRecipe.station == CraftStation.Hand,
                "and neither needs a workbench you have not built yet");
        }
    }
}
