using System.Collections.Generic;
using MadVoxel.Content;
using MadVoxel.Core;
using MadVoxel.Inventory;
using UnityEngine;

namespace MadVoxel.Building
{
    /// <summary>
    /// A furnace. Load it with ore and something that burns, walk away, come back to
    /// metal.
    ///
    /// That last part is the whole reason it exists. A campfire is a screen you stand
    /// at holding a button; smelting a stack of ore that way is twenty seconds of
    /// watching a bar. The furnace is the other shape - it costs more to build, it is
    /// slower per item, and it does not need you.
    ///
    /// <b>One inventory, not three.</b> Ore, fuel and ingots share the same slots. An
    /// input tray, a fuel tray and an output tray would be three grids to learn and
    /// three ways to put something in the wrong one; here you tip everything in and the
    /// furnace works out what is what. Fuel is anything that burns, work is anything
    /// the forge has a recipe for.
    ///
    /// <b>Progress is derived.</b> It runs on the world clock, catching up whenever
    /// anyone asks, because the player will be two hundred metres away in an unloaded
    /// chunk for most of it and asleep for the rest. The hour it last worked to is
    /// advanced by exactly the work that completed, so the remainder is kept - a
    /// furnace you open every minute must not have its progress reset each time.
    /// </summary>
    public class FurnaceStructure : MonoBehaviour, IInteractable
    {
        public static event System.Action<FurnaceStructure> OpenRequested;

        /// <summary>Real seconds between catch-ups while someone is nearby.</summary>
        const float TickInterval = 2f;

        public PlacedStructure Structure { get; private set; }
        public MadVoxel.Inventory.Inventory Contents { get; private set; }

        /// <summary>The hour its work is accounted up to. Everything after this is owed.</summary>
        public double WorkedToHours { get; private set; }

        /// <summary>
        /// Burn that is lit and not yet spent. A lump of coal outlasts a batch several
        /// times over, and throwing the remainder away each time made the furnace cost
        /// more fuel per ingot than a campfire.
        /// </summary>
        public float BankedFuelSeconds { get; private set; }

        /// <summary>Set by the session: the furnace needs to know what a forge can make.</summary>
        public ContentDatabase Content { get; set; }

        readonly List<RecipeDefinition> _recipes = new List<RecipeDefinition>();
        float _sinceTick;
        bool _wasLit;
        Light _glow;

        public void Bind(PlacedStructure structure)
        {
            Structure = structure;
            Contents = new MadVoxel.Inventory.Inventory(Mathf.Max(6, structure.Definition.storageSlots));

            var clock = structure.Owner != null ? structure.Owner.Clock : null;
            WorkedToHours = clock != null ? clock.TotalHours : 0.0;

            if (structure.Owner != null) Content = structure.Owner.Content;

            BuildGlow();
        }

        void BuildGlow()
        {
            var go = new GameObject("FurnaceGlow");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0.5f, 0.5f, 0.5f);

            _glow = go.AddComponent<Light>();
            _glow.type = LightType.Point;
            _glow.range = 7f;
            _glow.intensity = 1.8f;
            _glow.color = new Color(1f, 0.52f, 0.18f);
            _glow.shadows = LightShadows.None;
            _glow.enabled = false;
        }

        double NowHours
        {
            get
            {
                var clock = Structure != null && Structure.Owner != null ? Structure.Owner.Clock : null;
                return clock != null ? clock.TotalHours : WorkedToHours;
            }
        }

        void Update()
        {
            _sinceTick += Time.deltaTime;
            if (_sinceTick < TickInterval) return;
            _sinceTick = 0f;

            Catch();
        }

        // ------------------------------------------------------------------ smelting

        /// <summary>
        /// Works through everything owed since the last catch-up. Returns the batches
        /// completed, which is zero on every tick a furnace is idle - the common case,
        /// and the one that has to be cheap.
        /// </summary>
        public int Catch()
        {
            if (Contents == null || Content == null) return 0;

            double now = NowHours;
            float seconds = FurnaceRules.WorkingSeconds(now - WorkedToHours);
            if (seconds <= 0f)
            {
                SetLit(false);
                return 0;
            }

            FurnaceRules.CollectRecipes(Content.recipes, _recipes);
            if (_recipes.Count == 0)
            {
                WorkedToHours = now;
                SetLit(false);
                return 0;
            }

            int total = 0;
            float spent = 0f;

            // Round-robin rather than finishing one recipe before starting the next: a
            // furnace with ore and sand in it should give you ingots and glass, not all
            // the ingots and then all the glass.
            bool progressed = true;
            while (progressed && seconds - spent > 0.001f)
            {
                progressed = false;

                for (int i = 0; i < _recipes.Count && seconds - spent > 0.001f; i++)
                {
                    int done = RunOne(_recipes[i], seconds - spent, ref spent);
                    if (done <= 0) continue;

                    total += done;
                    progressed = true;
                }
            }

            // Why it stopped decides what happens to the unspent time.
            //
            // Out of time, mid-batch: the remainder is genuinely owed, so advance by
            // exactly the work that happened and leave the rest for next time.
            //
            // Out of ore, fuel or room: the furnace has been sitting cold, and banking
            // those hours would mean walking up to an idle furnace, dropping in a stack
            // and watching a week of stored time turn it into metal at once.
            bool stillCould = IsWorking();

            if (stillCould) WorkedToHours += FurnaceRules.HoursForSeconds(spent);
            else WorkedToHours = now;

            SetLit(stillCould);
            return total;
        }

        /// <summary>Runs a single batch of one recipe, if everything lines up.</summary>
        int RunOne(RecipeDefinition recipe, float secondsLeft, ref float spent)
        {
            float perBatch = FurnaceRules.SecondsPerBatch(recipe);
            if (perBatch > secondsLeft) return 0;

            int byIngredients = FurnaceRules.BatchesFromIngredients(Contents, recipe);
            if (byIngredients <= 0) return 0;

            int byRoom = Contents.CanFit(recipe.output, recipe.outputCount) ? 1 : 0;
            if (byRoom <= 0) return 0;

            float fuel = FurnaceRules.FuelSecondsIn(Contents) + BankedFuelSeconds;
            if (FurnaceRules.BatchesAffordable(secondsLeft, fuel, perBatch, byIngredients, byRoom) <= 0) return 0;

            // Fuel first: if the burn comes up short the batch does not happen, and no
            // ore has been touched.
            float banked = BankedFuelSeconds;
            float got = FurnaceRules.BurnFuel(Contents, perBatch, ref banked);
            BankedFuelSeconds = banked;

            if (got < perBatch - 0.001f) return 0;

            for (int i = 0; i < recipe.ingredients.Count; i++)
            {
                var ingredient = recipe.ingredients[i];
                if (ingredient.item != null && ingredient.count > 0) Contents.Remove(ingredient.item, ingredient.count);
            }

            Contents.Add(recipe.output, recipe.outputCount);

            spent += perBatch;
            return 1;
        }

        /// <summary>
        /// True when a batch could actually run: something to smelt, room for it, and
        /// enough burn to finish it.
        ///
        /// "Enough burn" rather than "any burn" is the whole point. A lump of coal
        /// almost never divides evenly into batches, so ending a burn with a splash of
        /// banked seconds is the normal case - and treating that as still working put
        /// the catch-up back on the branch that keeps unspent hours, which is the cold
        /// banking this was supposed to have fixed.
        /// </summary>
        public bool IsWorking()
        {
            if (Contents == null || Content == null) return false;

            FurnaceRules.CollectRecipes(Content.recipes, _recipes);

            float fuel = FurnaceRules.FuelSecondsIn(Contents) + BankedFuelSeconds;
            if (fuel <= 0f) return false;

            for (int i = 0; i < _recipes.Count; i++)
            {
                var recipe = _recipes[i];
                if (!FurnaceRules.HasBurnFor(fuel, recipe)) continue;
                if (FurnaceRules.BatchesFromIngredients(Contents, recipe) <= 0) continue;
                if (!Contents.CanFit(recipe.output, recipe.outputCount)) continue;

                return true;
            }
            return false;
        }

        /// <summary>The line the look-at readout shows. It has to say why it stopped.</summary>
        public string Readout()
        {
            if (Contents == null || Content == null) return "FURNACE";

            FurnaceRules.CollectRecipes(Content.recipes, _recipes);

            float available = FurnaceRules.FuelSecondsIn(Contents) + BankedFuelSeconds;

            bool hasWork, hasFuel, hasRoom;
            FurnaceRules.Survey(_recipes, available,
                r => FurnaceRules.BatchesFromIngredients(Contents, r) > 0,
                r => Contents.CanFit(r.output, r.outputCount),
                out hasWork, out hasFuel, out hasRoom);

            return "FURNACE  " + FurnaceRules.Describe(hasWork, hasFuel, hasRoom);
        }

        void SetLit(bool lit)
        {
            if (_glow == null || lit == _wasLit) return;

            _wasLit = lit;
            _glow.enabled = lit;
        }

        // ------------------------------------------------------------------- saving

        public void RestoreState(double workedToHours, float bankedFuelSeconds)
        {
            WorkedToHours = workedToHours;
            BankedFuelSeconds = Mathf.Max(0f, bankedFuelSeconds);
        }

        public string InteractPrompt { get { return Readout(); } }

        public void Interact(GameObject interactor)
        {
            // Caught up before it opens, so what you see is what it has actually made.
            Catch();
            if (OpenRequested != null) OpenRequested(this);
        }
    }
}
