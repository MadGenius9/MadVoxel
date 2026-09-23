using System.Collections.Generic;
using MadVoxel.AI;
using MadVoxel.Building;
using MadVoxel.Claim;
using MadVoxel.Content;
using MadVoxel.Core;
using MadVoxel.Farming.Crops;
using MadVoxel.Farming.Plots;
using MadVoxel.Fluid;
using MadVoxel.Inventory;
using MadVoxel.Inventory.Spoil;
using MadVoxel.Power;
using MadVoxel.World.Weather;
using UnityEngine;

namespace MadVoxel.Colony
{
    /// <summary>
    /// The colony itself: who lives here, what they eat, and where they sleep. One per
    /// world, bound to the claim the board was planted in.
    ///
    /// It owns the questions a colonist is too simple to answer - where the nearest
    /// ready plot is, whether there is water, which bed is free - so the people stay a
    /// state machine and the bookkeeping stays in one place.
    /// </summary>
    public class ColonyWorld : MonoBehaviour
    {
        const float SampleInterval = 2f;

        public string ColonyName = "Mad Colony";
        public bool Founded { get; private set; }

        public ColonyRules Rules { get; private set; }

        StructureWorld _structures;
        BuildingWorld _buildings;
        SpawnDirector _spawner;
        FluidWorld _fluid;
        PowerWorld _power;
        WorldClock _clock;
        WeatherDirector _weather;
        ContentDatabase _content;
        ClaimHeatTracker _heat;

        readonly List<Colonist> _colonists = new List<Colonist>();
        readonly List<string> _usedNames = new List<string>();

        double _lastHours;
        float _sinceSample;
        bool _breachedRecently;
        float _breachClearsAt;

        public IReadOnlyList<Colonist> Colonists { get { return _colonists; } }
        public int Population { get { return _colonists.Count; } }
        public bool IsNight { get { return _clock != null && _clock.IsNight; } }

        /// <summary>
        /// Actions colonists must never take. Empty for now, and deliberately so: the
        /// hook exists because one future thing - the dawn marker - must be off limits
        /// to them, and retrofitting that into a finished AI is worse than carrying an
        /// empty list.
        /// </summary>
        public readonly HashSet<string> ForbiddenActions = new HashSet<string>();

        public void Init(StructureWorld structures, BuildingWorld buildings, SpawnDirector spawner,
                         FluidWorld fluid, PowerWorld power, WorldClock clock, WeatherDirector weather,
                         ContentDatabase content, ClaimHeatTracker heat)
        {
            _structures = structures;
            _buildings = buildings;
            _spawner = spawner;
            _fluid = fluid;
            _power = power;
            _clock = clock;
            _weather = weather;
            _content = content;
            _heat = heat;
            _lastHours = clock != null ? clock.TotalHours : 0.0;

            Rules = content != null ? content.colonyRules : null;
            Colonist.Died += OnColonistDied;
        }

        void OnDestroy()
        {
            Colonist.Died -= OnColonistDied;
        }

        // --------------------------------------------------------------- founding

        /// <summary>Everything the board can currently see, for the founding check.</summary>
        public FoundingCheck Survey()
        {
            var check = new FoundingCheck();

            var claim = Claim();
            check.HasCupboard = claim != null;
            if (claim == null) return check;

            var all = _structures.All;
            for (int i = 0; i < all.Count; i++)
            {
                if (!claim.Contains(all[i].transform.position)) continue;

                if (all[i].GetComponent<BedrollStructure>() != null) check.Beds++;
                if (all[i].Definition.kind == StructureKind.Campfire) check.HasCookingFire = true;

                var storage = all[i].GetComponent<StorageStructure>();
                if (storage != null) check.FoodItems += CountFood(storage.Contents);
            }

            check.HasWater = FindWater() != null;
            return check;
        }

        public FoundResult TryFound(string name)
        {
            var verdict = ColonyCharter.Evaluate(Survey(), Rules, Founded);
            if (verdict != FoundResult.Ok) return verdict;

            Founded = true;
            ColonyName = string.IsNullOrEmpty(name) ? "Mad Colony" : name;
            Notifications.PostFormat("{0} founded", ColonyName);
            return FoundResult.Ok;
        }

        public LandClaim Claim()
        {
            if (_structures == null || _structures.Claims == null) return null;

            var claims = _structures.Claims.Claims;
            return claims.Count > 0 ? claims[0] : null;
        }

        // ---------------------------------------------------------- who turns up

        /// <summary>Someone is standing at the fence waiting for an answer.</summary>
        public bool WandererWaiting { get; private set; }

        /// <summary>Their name, so the board can ask about a person rather than a slot.</summary>
        public string WandererName { get; private set; }

        int _lastArrivalDay;
        int _lastCheckedDay;

        /// <summary>
        /// Whether the colony could take someone, and why not. The board shows this
        /// whether or not anyone is at the fence, because it is the thing the player
        /// can actually do something about.
        /// </summary>
        public RecruitReadiness Readiness()
        {
            if (!Founded || Rules == null) return RecruitReadiness.NoColony;

            var check = Survey();

            float food = FoodInStore();
            float water = WaterInStore();

            return ColonyRecruitment.Readiness(Founded, Population, Rules.maxColonists, check.Beds,
                ColonyRecruitment.DaysOfSupply(food, Population, Rules.foodPerColonistPerDay),
                ColonyRecruitment.DaysOfSupply(water, Population, Rules.litresPerColonistPerDay));
        }

        public int DaysSinceArrival
        {
            get { return _clock != null ? Mathf.Max(0, _clock.Day - _lastArrivalDay) : 0; }
        }

        /// <summary>
        /// Checked once a day, at the turn of it. A wanderer walks in overnight or not
        /// at all - a stranger materialising at noon while you watch is a spawn, not an
        /// arrival.
        /// </summary>
        void CheckForWanderer()
        {
            if (_clock == null || WandererWaiting) return;
            if (_clock.Day == _lastCheckedDay) return;

            _lastCheckedDay = _clock.Day;

            float heat = _heat != null ? _heat.Heat : 0f;
            if (!ColonyRecruitment.Arrives(Readiness(), DaysSinceArrival, heat, UnityEngine.Random.value)) return;

            WandererWaiting = true;
            WandererName = NextName();

            Notifications.PostFormat("{0} is at the fence, asking to stay", WandererName);
        }

        /// <summary>Takes them in. Returns the person, or null if the colony cannot after all.</summary>
        public Colonist AcceptWanderer()
        {
            if (!WandererWaiting) return null;

            var person = TryRecruit(WandererName);
            if (person == null) return null;

            WandererWaiting = false;
            WandererName = "";
            _lastArrivalDay = _clock != null ? _clock.Day : 0;

            return person;
        }

        /// <summary>
        /// Sends them away. They do not come back, and the next one is a few days out -
        /// turning someone away at the gate is a decision, not a reroll.
        /// </summary>
        public void TurnAwayWanderer()
        {
            if (!WandererWaiting) return;

            Notifications.PostFormat("{0} moves on", WandererName);

            WandererWaiting = false;
            WandererName = "";
            _lastArrivalDay = _clock != null ? _clock.Day : 0;
        }

        // -------------------------------------------------------------- recruiting

        /// <summary>
        /// Adds a person. Refuses past the cap and when there is no charter, because a
        /// colonist with nowhere to sleep and nothing to eat is a corpse on a timer.
        /// </summary>
        public Colonist TryRecruit()
        {
            return TryRecruit(null);
        }

        public Colonist TryRecruit(string name)
        {
            if (!Founded || Rules == null || Rules.colonist == null) return null;
            if (_colonists.Count >= Rules.maxColonists) return null;

            var claim = Claim();
            if (claim == null) return null;

            var go = new GameObject("Colonist");
            go.transform.SetParent(transform, false);
            go.transform.position = claim.Centre + new Vector3(1.5f, 1.2f, 1.5f);

            var controller = go.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = 0.32f;
            controller.center = new Vector3(0f, 0.9f, 0f);

            ColonistVisuals.Build(go.transform);

            var colonist = go.AddComponent<Colonist>();
            colonist.Init(this, Rules.colonist, string.IsNullOrEmpty(name) ? NextName() : name);
            _colonists.Add(colonist);

            AssignBed(colonist);
            if (_heat != null) _heat.ColonistCount = _colonists.Count;

            Notifications.PostFormat("{0} joined {1}", colonist.Name, ColonyName);
            return colonist;
        }

        string NextName()
        {
            var pool = Rules.colonist.names;
            for (int i = 0; i < pool.Count; i++)
            {
                if (_usedNames.Contains(pool[i])) continue;

                _usedNames.Add(pool[i]);
                return pool[i];
            }
            return "Survivor " + (_colonists.Count + 1);
        }

        void OnColonistDied(Colonist colonist)
        {
            if (!_colonists.Contains(colonist)) return;

            // The bed frees, the morale hits, and the name is spent. They do not come
            // back, and the board should make that plain.
            _colonists.Remove(colonist);
            if (_heat != null) _heat.ColonistCount = _colonists.Count;

            for (int i = 0; i < _colonists.Count; i++)
            {
                _colonists[i].Morale = Mathf.Max(0f, _colonists[i].Morale - 14f);
            }

            if (colonist != null && colonist.gameObject != null) Destroy(colonist.gameObject, 8f);
        }

        public void AssignBed(Colonist colonist)
        {
            var claim = Claim();
            if (claim == null) return;

            var all = _structures.All;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].GetComponent<BedrollStructure>() == null) continue;
                if (!claim.Contains(all[i].transform.position)) continue;
                if (IsBedTaken(all[i], colonist)) continue;

                colonist.Bed = all[i];
                return;
            }
            colonist.Bed = null;
        }

        bool IsBedTaken(PlacedStructure bed, Colonist except)
        {
            for (int i = 0; i < _colonists.Count; i++)
            {
                if (_colonists[i] != except && _colonists[i].Bed == bed) return true;
            }
            return false;
        }

        // ------------------------------------------------------------------- ticks

        void Update()
        {
            if (!Founded || _clock == null) return;

            _sinceSample += Time.deltaTime;
            if (_sinceSample < SampleInterval) return;
            _sinceSample = 0f;

            CheckForWanderer();

            double now = _clock.TotalHours;
            float elapsed = Mathf.Max(0f, (float)(now - _lastHours));
            _lastHours = now;
            if (elapsed <= 0f) return;

            if (_breachedRecently && Time.time > _breachClearsAt) _breachedRecently = false;

            for (int i = _colonists.Count - 1; i >= 0; i--)
            {
                var colonist = _colonists[i];
                if (colonist == null) { _colonists.RemoveAt(i); continue; }

                if (colonist.Bed == null) AssignBed(colonist);
                colonist.TickNeeds(elapsed, ContextFor(colonist));

                if (colonist.State == ColonyState.Leaving && FarFromClaim(colonist)) Leave(colonist);
            }
        }

        MoraleContext ContextFor(Colonist colonist)
        {
            return new MoraleContext
            {
                Hungry = colonist.IsHungry,
                Thirsty = colonist.IsThirsty,
                HasBed = colonist.Bed != null,
                InTheDark = IsNight && !HasLightNear(colonist.transform.position),
                OutInBadWeather = _weather != null && _weather.OutdoorMoralePerHour < 0f && !IsIndoors(colonist),
                BaseWasBreached = _breachedRecently,
                WeatherPerHour = _weather != null ? _weather.OutdoorMoralePerHour : 0f
            };
        }

        bool FarFromClaim(Colonist colonist)
        {
            var claim = Claim();
            if (claim == null) return true;

            return (colonist.transform.position - claim.Centre).sqrMagnitude > 40f * 40f;
        }

        void Leave(Colonist colonist)
        {
            Notifications.PostFormat("{0} walked out of {1}", colonist.Name, ColonyName);

            _colonists.Remove(colonist);
            if (_heat != null) _heat.ColonistCount = _colonists.Count;
            Destroy(colonist.gameObject);
        }

        /// <summary>Called by the horde when something inside the claim is destroyed.</summary>
        public void ReportBreach()
        {
            _breachedRecently = true;
            _breachClearsAt = Time.time + 240f;
        }

        // ------------------------------------------------------------- the answers

        public bool HasLightNear(Vector3 position)
        {
            if (_power == null) return false;

            var devices = _power.Devices;
            for (int i = 0; i < devices.Count; i++)
            {
                var device = devices[i];
                if (device.Device.lightRange <= 0f) continue;

                var node = device.Node;
                if (node == null || !node.IsPowered) continue;
                if ((device.transform.position - position).sqrMagnitude <= device.Device.lightRange * device.Device.lightRange)
                    return true;
            }

            // A campfire counts. Not every colony has a grid yet.
            var all = _structures.All;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].Definition.kind != StructureKind.Campfire) continue;
                if ((all[i].transform.position - position).sqrMagnitude <= 81f) return true;
            }
            return false;
        }

        static bool IsIndoors(Colonist colonist)
        {
            return Physics.Raycast(colonist.transform.position + Vector3.up * 1.2f, Vector3.up, 8f,
                ~0, QueryTriggerInteraction.Ignore);
        }

        public FarmPlotStructure FindReadyPlot(Vector3 from)
        {
            var claim = Claim();
            if (claim == null) return null;

            FarmPlotStructure best = null;
            float bestSq = float.MaxValue;

            var all = _structures.All;
            for (int i = 0; i < all.Count; i++)
            {
                var plot = all[i].GetComponent<FarmPlotStructure>();
                if (plot == null || plot.IsEmpty || plot.Stage != CropStage.Ready) continue;
                if (!claim.Contains(all[i].transform.position)) continue;

                float distSq = (all[i].transform.position - from).sqrMagnitude;
                if (distSq >= bestSq) continue;

                best = plot;
                bestSq = distSq;
            }
            return best;
        }

        /// <summary>
        /// The Farm job's payoff: a ready plot goes into a crate rather than into the
        /// colonist. They are working the garden, not eating it.
        /// </summary>
        public void HarvestInto(FarmPlotStructure plot, Colonist colonist)
        {
            if (plot == null || plot.IsEmpty || plot.Stage != CropStage.Ready) return;

            var crop = plot.Crop;
            var box = FindStorage(true);
            if (box == null)
            {
                Notifications.PostFormat("{0} has nowhere to put the harvest", colonist.Name);
                return;
            }

            int yield = Random.Range(crop.harvestMin, crop.harvestMax + 1);
            box.Contents.Add(crop.harvestItem, yield);

            if (crop.replants) plot.Plant(crop, _clock != null ? _clock.TotalHours : 0.0);
            else plot.ClearCrop();

            Notifications.PostFormat("{0} harvested {1}", colonist.Name, crop.displayName.ToLowerInvariant());
        }

        public StorageStructure FindStorage(bool wantsRoom)
        {
            var claim = Claim();
            if (claim == null) return null;

            StorageStructure fallback = null;
            var all = _structures.All;

            for (int i = 0; i < all.Count; i++)
            {
                var storage = all[i].GetComponent<StorageStructure>();
                if (storage == null || storage.IsDeathBackpack) continue;
                if (!claim.Contains(all[i].transform.position)) continue;

                // A powered fridge is always the right answer for food.
                var device = all[i].GetComponent<PowerDeviceStructure>();
                if (device != null && device.Device.spoilSlowdown > 1f)
                {
                    var node = device.Node;
                    if (node != null && node.IsPowered) return storage;
                }

                if (fallback == null) fallback = storage;
            }
            return fallback;
        }

        int CountFood(Inventory.Inventory inventory)
        {
            int count = 0;
            for (int i = 0; i < inventory.Size; i++)
            {
                var stack = inventory[i];
                if (stack.IsEmpty || stack.Item.foodRestore <= 0f) continue;
                count += stack.Count;
            }
            return count;
        }

        /// <summary>Total food in the claim, for the board's "days" line.</summary>
        public int FoodInStore()
        {
            var claim = Claim();
            if (claim == null) return 0;

            int total = 0;
            var all = _structures.All;
            for (int i = 0; i < all.Count; i++)
            {
                var storage = all[i].GetComponent<StorageStructure>();
                if (storage == null || storage.IsDeathBackpack) continue;
                if (!claim.Contains(all[i].transform.position)) continue;

                total += CountFood(storage.Contents);
            }
            return total;
        }

        public float WaterInStore()
        {
            var tank = FindWater();
            if (tank == null) return 0f;

            return _fluid != null && _fluid.Graph != null ? _fluid.Graph.StoredLitres : 0f;
        }

        /// <summary>A live tap, or failing that a barrel with something in it.</summary>
        public FluidDeviceStructure FindWater()
        {
            if (_fluid == null) return null;

            var claim = Claim();
            var devices = _fluid.Devices;

            FluidDeviceStructure barrel = null;
            for (int i = 0; i < devices.Count; i++)
            {
                var device = devices[i];
                if (claim != null && !claim.Contains(device.transform.position)) continue;

                var node = device.Node;
                if (node == null) continue;

                if (device.Device.kind == FluidDeviceKind.Tap && node.IsLive && !node.IsFrozen) return device;
                if (device.Device.kind == FluidDeviceKind.Tank && node.Litres > 0f && barrel == null) barrel = device;
            }
            return barrel;
        }

        /// <summary>
        /// Drinking. A tap is pulled through the graph so the litres really leave the
        /// system - unplug the pump and thirst starts rising, which is the point.
        /// </summary>
        public bool TryDrink(Colonist colonist)
        {
            var source = FindWater();
            if (source == null) return false;

            var node = source.Node;
            float served;

            if (source.Device.kind == FluidDeviceKind.Tap)
            {
                served = _fluid.Graph.DrawFromOutlet(source.NodeId, source.Device.servingLitres);
            }
            else
            {
                served = Mathf.Min(node.Litres, 1.5f);
                node.Litres -= served;
            }

            if (served <= 0.01f) return false;

            colonist.Drink(colonist.Definition.drinkRestores);
            return true;
        }

        /// <summary>
        /// Eating. They take the oldest good food first, which is what makes a fridge
        /// worth wiring rather than a nicety.
        /// </summary>
        public bool TryEat(Colonist colonist)
        {
            var claim = Claim();
            if (claim == null) return false;

            StorageStructure bestBox = null;
            int bestSlot = -1;
            float oldest = float.MaxValue;

            var all = _structures.All;
            for (int i = 0; i < all.Count; i++)
            {
                var storage = all[i].GetComponent<StorageStructure>();
                if (storage == null || storage.IsDeathBackpack) continue;
                if (!claim.Contains(all[i].transform.position)) continue;

                for (int slot = 0; slot < storage.Contents.Size; slot++)
                {
                    var stack = storage.Contents[slot];
                    if (stack.IsEmpty || stack.Item.foodRestore <= 0f) continue;

                    float life = SpoilRules.Normalise(stack.Item, stack.SpoilRemaining);
                    if (life < 0f) life = float.MaxValue - 1f;   // never spoils: eat it last
                    if (life >= oldest) continue;

                    oldest = life;
                    bestBox = storage;
                    bestSlot = slot;
                }
            }

            if (bestBox == null) return false;

            var chosen = bestBox.Contents[bestSlot];
            bestBox.Contents.SetSlot(bestSlot, chosen.WithCount(chosen.Count - 1));

            colonist.Eat(colonist.Definition.mealRestores);
            return true;
        }

        public PlacedStructure CookingFire()
        {
            var claim = Claim();
            if (claim == null) return null;

            var all = _structures.All;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].Definition.kind != StructureKind.Campfire) continue;
                if (!claim.Contains(all[i].transform.position)) continue;
                return all[i];
            }
            return null;
        }

        /// <summary>
        /// One pass of the Cook job: the first campfire recipe the stores can pay for.
        /// It uses the same crafting service the player does, so a mod that adds a meal
        /// is cooked by the colony without a line of code here.
        /// </summary>
        public void CookOnce(Colonist colonist)
        {
            var box = FindStorage(false);
            if (box == null || _content == null) return;

            for (int i = 0; i < _content.recipes.Count; i++)
            {
                var recipe = _content.recipes[i];
                if (recipe == null || recipe.output == null) continue;
                if (recipe.station != CraftStation.Campfire) continue;
                if (recipe.output.foodRestore <= 0f) continue;

                if (!CraftingService.CanCraft(box.Contents, recipe, CraftStation.Campfire, null)) continue;
                if (!CraftingService.Craft(box.Contents, recipe, CraftStation.Campfire, null)) continue;

                Notifications.PostFormat("{0} cooked {1}", colonist.Name, recipe.output.displayName.ToLowerInvariant());
                return;
            }
        }

        public BuildPiece FindDamagedPiece(Vector3 from)
        {
            if (_buildings == null) return null;

            var claim = Claim();
            BuildPiece best = null;
            float bestSq = float.MaxValue;

            var all = _buildings.All;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].HealthFraction >= 0.99f) continue;
                if (claim != null && !claim.Contains(all[i].transform.position)) continue;

                float distSq = (all[i].transform.position - from).sqrMagnitude;
                if (distSq >= bestSq) continue;

                best = all[i];
                bestSq = distSq;
            }
            return best;
        }

        public void RepairA(BuildPiece piece, Colonist colonist)
        {
            if (piece == null) return;
            piece.Repair(piece.Definition.maxHealth * colonist.Definition.repairFraction);
        }

        public IDamageable NearestThreat(Vector3 from, float radius)
        {
            if (_spawner == null) return null;

            var alive = _spawner.Alive;
            for (int i = 0; i < alive.Count; i++)
            {
                var zombie = alive[i];
                if (zombie == null || !zombie.IsAlive) continue;
                if ((zombie.transform.position - from).sqrMagnitude > radius * radius) continue;

                return zombie;
            }
            return null;
        }

        /// <summary>Where a guard stands: the claim edge nearest the trouble.</summary>
        public Vector3 GuardPost(Colonist colonist)
        {
            var claim = Claim();
            if (claim == null) return colonist.transform.position;

            var threat = NearestThreat(claim.Centre, claim.Radius * 1.6f);
            if (threat == null) return claim.Centre;

            var mono = threat as MonoBehaviour;
            if (mono == null) return claim.Centre;

            Vector3 toward = mono.transform.position - claim.Centre;
            toward.y = 0f;

            return claim.Centre + toward.normalized * (claim.Radius * 0.7f);
        }

        /// <summary>Their bed if they have one, otherwise the middle of the claim.</summary>
        public Vector3 ShelterPoint(Colonist colonist)
        {
            if (colonist.Bed != null) return colonist.Bed.transform.position;

            var claim = Claim();
            return claim != null ? claim.Centre : colonist.transform.position;
        }

        public Vector3 ExitPoint()
        {
            var claim = Claim();
            return claim != null ? claim.Centre + new Vector3(60f, 0f, 60f) : transform.position;
        }

        /// <summary>The board's shelter order, given before dusk.</summary>
        public void OrderShelter(bool shelter)
        {
            for (int i = 0; i < _colonists.Count; i++) _colonists[i].Sheltering = shelter;

            Notifications.Post(shelter ? "Colonists ordered to shelter" : "Colonists back to work");
        }

        public bool Sheltering
        {
            get { return _colonists.Count > 0 && _colonists[0].Sheltering; }
        }

        // ------------------------------------------------------------------- state

        /// <summary>Used by the save loader to rebuild the colony exactly.</summary>
        public void LoadState(string name, bool founded, List<ColonistState> people)
        {
            ColonyName = string.IsNullOrEmpty(name) ? "Mad Colony" : name;
            Founded = founded;
            if (!founded || people == null) return;

            for (int i = 0; i < people.Count; i++)
            {
                var colonist = TryRecruitForLoad(people[i]);
                if (colonist == null) break;
            }
            if (_heat != null) _heat.ColonistCount = _colonists.Count;
        }

        Colonist TryRecruitForLoad(ColonistState state)
        {
            var colonist = TryRecruit();
            if (colonist == null) return null;

            colonist.Name = state.Name;
            colonist.Job = state.Job;
            colonist.Food = state.Food;
            colonist.Water = state.Water;
            colonist.Morale = state.Morale;

            if (!_usedNames.Contains(state.Name)) _usedNames.Add(state.Name);
            return colonist;
        }
    }

    /// <summary>A colonist, flattened for the save file.</summary>
    public struct ColonistState
    {
        public string Name;
        public ColonyJob Job;
        public float Food;
        public float Water;
        public float Morale;
    }
}
