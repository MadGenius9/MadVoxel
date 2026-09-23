using System.Collections.Generic;
using MadVoxel.Core;
using MadVoxel.Farming.Crops;
using MadVoxel.Perks;
using MadVoxel.World.Fields;
using MadVoxel.World.Terrain;
using UnityEngine;

namespace MadVoxel.Vehicles
{
    /// <summary>
    /// The thing on the back of the tractor, and the only caller in the game that works
    /// field cells in bulk.
    ///
    /// It owns three decisions that are easy to get wrong and hard to see:
    ///
    /// 1. <b>Where it worked.</b> The cells come from <see cref="FieldSwath"/>, swept
    ///    along the segment travelled since the last tick rather than sampled at a
    ///    point, so frame rate cannot stripe a field.
    /// 2. <b>What it could pay for.</b> <see cref="ImplementWork"/> decides how many of
    ///    those cells the hopper covers before a single one is touched, so a seeder
    ///    never leaves ground that looks sown and comes up bare.
    /// 3. <b>When to stop.</b> A harvester that fills mid-swath stops there and says so
    ///    rather than spilling the rest of the row on the floor in silence.
    ///
    /// Everything it does to the ground goes through <see cref="FieldWorld"/>, the same
    /// operations the hoe calls one cell at a time. The tractor is a faster hand, not a
    /// second rule set.
    /// </summary>
    public class ImplementController : MonoBehaviour
    {
        /// <summary>Metres behind the tractor's origin the implement is dragged.</summary>
        public const float HitchOffset = 2.6f;

        /// <summary>Below this the machine has not moved enough to be worth a sweep.</summary>
        const float MinTravelMetres = 0.15f;

        public ImplementDefinition Definition { get; private set; }
        public VehicleRig Tractor { get; private set; }

        /// <summary>Litres in the hopper. Seed for a seeder, produce for a harvester.</summary>
        public float HopperLitres { get { return _hopperLitres; } }

        // Backed by a field rather than an auto-property: ImplementWork adjusts it by
        // reference, which is what keeps the "what fits" arithmetic in one place.
        float _hopperLitres;

        /// <summary>What the hopper holds. A seeder sows it; a harvester fills with it.</summary>
        public CropDefinition Cargo { get; private set; }

        /// <summary>Lowered into the ground. Raised, it is just weight and drag.</summary>
        public bool Engaged { get; private set; }

        FieldWorld _fields;
        TerrainWorld _terrain;
        float _gameHoursPerSecond = 24f / 1200f;

        readonly List<Vector2Int> _cells = new List<Vector2Int>();

        Vector3 _lastWorked;
        bool _hasLastWorked;
        bool _warnedBlocked;
        float _spilled;

        // ------------------------------------------------------------------ readouts

        public float HopperCapacity { get { return Definition != null ? Definition.hopperCapacityLitres : 0f; } }

        public bool IsBlocked { get { return ImplementWork.IsBlocked(Definition, HopperLitres); } }

        /// <summary>What the tractor's top speed is scaled by. A raised plough is free.</summary>
        public float SpeedMultiplier
        {
            get { return Engaged && Definition != null ? Definition.speedMultiplier : 1f; }
        }

        /// <summary>
        /// Extra litres per real second. The definition is written per in-game hour,
        /// because that is the unit the player reasons about a day's work in, and the
        /// clock's day length is what turns it into a burn rate.
        /// </summary>
        public float FuelPerSecond
        {
            get { return Engaged && Definition != null ? Definition.fuelLitresPerHour * _gameHoursPerSecond : 0f; }
        }

        public string Readout { get { return ImplementWork.Describe(Definition, HopperLitres, Engaged); } }

        // ------------------------------------------------------------------- setup

        /// <summary>
        /// Builds an implement already hitched to a tractor. Implements only exist
        /// attached - an unhitched one is an item in a bag, not an object in the world,
        /// which is the same rule every other deployable in the game follows.
        /// </summary>
        public static ImplementController Hitch(ImplementDefinition definition, VehicleRig tractor,
                                                FieldWorld fields, TerrainWorld terrain, float dayLengthSeconds)
        {
            if (definition == null || tractor == null || tractor.Implement != null) return null;

            var go = new GameObject("Implement_" + definition.stringId);
            go.transform.SetParent(tractor.transform, false);
            go.transform.localPosition = new Vector3(0f, 0.25f, -HitchOffset);

            var controller = go.AddComponent<ImplementController>();
            controller.Definition = definition;
            controller.Tractor = tractor;
            controller._fields = fields;
            controller._terrain = terrain;
            if (dayLengthSeconds > 1f) controller._gameHoursPerSecond = 24f / dayLengthSeconds;

            ImplementVisuals.Build(go.transform, definition);

            tractor.Implement = controller;
            return controller;
        }

        /// <summary>
        /// Unhitches and destroys the object. Returns what was in the hopper so the
        /// caller can decide where it goes - dropping a full harvester's load on the
        /// ground silently would be a quiet way to lose an afternoon.
        /// </summary>
        public float Unhitch(out CropDefinition cargo)
        {
            cargo = Cargo;
            float litres = HopperLitres;

            if (Tractor != null && Tractor.Implement == this) Tractor.Implement = null;
            Tractor = null;

            Destroy(gameObject);
            return litres;
        }

        /// <summary>Restores a hopper after a load, so a saved harvest is not lost.</summary>
        public void RestoreHopper(CropDefinition cargo, float litres)
        {
            Cargo = cargo;
            _hopperLitres = Mathf.Clamp(litres, 0f, HopperCapacity);
        }

        // ------------------------------------------------------------------ working

        /// <summary>Lowers or raises the implement. Raised is the safe default.</summary>
        public bool Toggle()
        {
            Engaged = !Engaged;

            // Forget where we were: a machine that is raised, driven across the yard and
            // lowered again must not plough the line between the two.
            _hasLastWorked = false;
            _warnedBlocked = false;

            Notifications.PostFormat("{0} {1}", Definition.displayName, Engaged ? "lowered" : "raised");
            return Engaged;
        }

        void Update()
        {
            if (Tractor == null || _fields == null) return;

            // Nothing works while nobody is driving. An unmanned machine rolling to a
            // stop should not keep sowing.
            if (!Engaged || !Tractor.HasDriver || !Tractor.IsAlive)
            {
                _hasLastWorked = false;
                return;
            }

            Vector3 now = transform.position;
            if (!_hasLastWorked)
            {
                _lastWorked = now;
                _hasLastWorked = true;
                return;
            }

            Vector3 travel = now - _lastWorked;
            travel.y = 0f;
            if (travel.sqrMagnitude < MinTravelMetres * MinTravelMetres) return;

            Work(_lastWorked, now);
            _lastWorked = now;
        }

        void Work(Vector3 from, Vector3 to)
        {
            int wanted = FieldSwath.Collect(from, to, Tractor.transform.eulerAngles.y,
                                            Definition.workingWidth, _cells);
            if (wanted <= 0) return;

            int affordable = ImplementWork.CellsAffordable(Definition, HopperLitres, wanted);
            if (affordable <= 0)
            {
                WarnBlocked();
                return;
            }

            var result = new WorkResult { HopperLimited = affordable < wanted };

            for (int i = 0; i < affordable; i++)
            {
                if (ApplyCell(_cells[i], ref result)) continue;

                // A full hopper stops the pass where it filled, not at the end of the row.
                if (IsBlocked)
                {
                    result.HopperLimited = true;
                    break;
                }
            }

            if (result.CellsWorked <= 0) return;

            Spend(result);
            Reward(result);

            if (result.HopperLimited) WarnBlocked();
            else _warnedBlocked = false;
        }

        /// <summary>
        /// Works one cell. Returns true when the cell took the operation; false covers
        /// both "wrong state for this implement" and "the hopper just filled", which the
        /// caller tells apart by asking the hopper.
        /// </summary>
        bool ApplyCell(Vector2Int cell, ref WorkResult result)
        {
            int y = _terrain != null ? _terrain.GetSurfaceY(cell.x, cell.y) : 0;
            var block = new Vector3Int(cell.x, y, cell.y);

            switch (Definition.kind)
            {
                case ImplementKind.Plow:
                    if (!_fields.TryPlow(block)) return false;
                    result.CellsWorked++;
                    return true;

                case ImplementKind.Cultivator:
                    if (!_fields.TryCultivate(block)) return false;
                    result.CellsWorked++;
                    return true;

                case ImplementKind.Seeder:
                    if (Cargo == null || !_fields.TrySow(block, Cargo)) return false;
                    result.CellsWorked++;
                    result.LitresMoved += Definition.seedLitresPerCell;
                    return true;

                default:
                    return Reap(block, ref result);
            }
        }

        bool Reap(Vector3Int block, ref WorkResult result)
        {
            CropDefinition crop;
            float litres = _fields.TryHarvest(block, out crop);
            if (litres <= 0f || crop == null) return false;

            // The Agronomist perk lands here: a bigger field harvest, not a bigger hand one.
            var progression = Tractor.Driver != null ? Tractor.Driver.Progression : null;
            if (progression != null) litres *= progression.Effects.Multiplier(PerkEffectType.FieldYieldMultiplier);

            // Mixing crops in one hopper would need a per-crop store on the machine. It
            // tips instead: the bin behind you is the thing that holds more than one crop.
            if (Cargo != null && Cargo != crop && HopperLitres > 0.01f)
            {
                result.HopperLimited = true;
                return false;
            }
            Cargo = crop;

            _spilled += ImplementWork.AddToHopper(Definition, ref _hopperLitres, litres);

            result.CellsWorked++;
            result.LitresMoved += litres;
            return true;
        }

        void Spend(WorkResult result)
        {
            if (Definition.FillsHopper) return;

            _hopperLitres = Mathf.Max(0f, _hopperLitres - ImplementWork.SeedCost(Definition, result.CellsWorked));
        }

        void Reward(WorkResult result)
        {
            var progression = Tractor.Driver != null ? Tractor.Driver.Progression : null;
            if (progression == null) return;

            // Per cell, and small: a field is hundreds of cells, and the tractor should
            // not out-earn every other thing in the game by driving in a straight line.
            progression.AddXp(result.CellsWorked * 0.05f, XpSource.Harvest);
        }

        void WarnBlocked()
        {
            if (_warnedBlocked) return;
            _warnedBlocked = true;

            if (Definition.FillsHopper)
            {
                Notifications.PostFormat("{0} is full - tip it into a grain bin (V)", Definition.displayName);
            }
            else if (Cargo == null)
            {
                Notifications.PostFormat("{0} has no seed - load it with a seed in hand (V)", Definition.displayName);
            }
            else
            {
                Notifications.PostFormat("{0} is out of {1} seed", Definition.displayName, Cargo.displayName);
            }
        }

        // ------------------------------------------------------------------ hopper

        /// <summary>
        /// Loads seed into a seeder. Returns the litres taken, so the caller knows how
        /// many seed items to consume.
        /// </summary>
        public float LoadSeed(CropDefinition crop, float litres)
        {
            if (Definition == null || Definition.FillsHopper || crop == null || litres <= 0f) return 0f;
            if (!crop.growsOnField) return 0f;

            // One crop at a time. Changing seed empties what is in there rather than
            // sowing a silent mixture the player cannot see or undo.
            if (Cargo != crop && HopperLitres > 0.01f) return 0f;

            float before = _hopperLitres;
            ImplementWork.AddToHopper(Definition, ref _hopperLitres, litres);

            float taken = _hopperLitres - before;
            if (taken > 0f) Cargo = crop;
            return taken;
        }

        /// <summary>Empties the hopper into a store. Returns what it handed over.</summary>
        public float Tip(System.Func<string, float, float> deposit, out CropDefinition cargo)
        {
            cargo = Cargo;
            if (deposit == null || HopperLitres <= 0f || Cargo == null) return 0f;

            float rejected = deposit(Cargo.stringId, HopperLitres);
            float moved = HopperLitres - Mathf.Clamp(rejected, 0f, HopperLitres);

            _hopperLitres -= moved;
            if (_hopperLitres <= 0.01f)
            {
                _hopperLitres = 0f;
                Cargo = null;
                _warnedBlocked = false;
            }

            return moved;
        }

        /// <summary>
        /// Empties the hopper outright. Used when the load has already been paid for
        /// over a counter, where there is no store to tip into and nothing to reject.
        /// </summary>
        public void Empty()
        {
            _hopperLitres = 0f;
            Cargo = null;
            _warnedBlocked = false;
        }

        /// <summary>Litres lost to a full hopper since the last time anyone asked.</summary>
        public float TakeSpillage()
        {
            float spilled = _spilled;
            _spilled = 0f;
            return spilled;
        }
    }
}
