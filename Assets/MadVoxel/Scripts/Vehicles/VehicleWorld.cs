using System.Collections.Generic;
using MadVoxel.Building;
using MadVoxel.Content;
using MadVoxel.Core;
using MadVoxel.Core.Player;
using MadVoxel.Farming.Crops;
using MadVoxel.Farming.Plots;
using MadVoxel.UI;
using MadVoxel.World.Fields;
using MadVoxel.World.Terrain;
using UnityEngine;

namespace MadVoxel.Vehicles
{
    /// <summary>
    /// Owns the machines: spawning them from a deployed item, hitching implements to
    /// them, feeding the tractor panel, and moving grain from a hopper into a bin.
    ///
    /// It exists for the same reason <see cref="MadVoxel.Power.PowerWorld"/> does - the
    /// rig drives, the implement works the ground, and neither of them should know what
    /// a grain bin or a HUD is.
    /// </summary>
    public class VehicleWorld : MonoBehaviour
    {
        /// <summary>How far from a bin you can tip. Close enough to be deliberate.</summary>
        public const float TipRange = 8f;

        readonly List<VehicleRig> _vehicles = new List<VehicleRig>();

        ContentDatabase _content;
        StructureWorld _structures;
        TerrainWorld _voxels;
        FieldWorld _fields;
        PlayerRig _player;
        HudView _hud;
        Transform _root;

        public IReadOnlyList<VehicleRig> All { get { return _vehicles; } }

        /// <summary>The machine the player is sitting on, or null.</summary>
        public VehicleRig Driving { get; private set; }

        /// <summary>The outposts, so a harvest can be sold over the counter. Set by the session.</summary>
        public Traders.TraderWorld Traders { get; set; }

        public void Init(ContentDatabase content, StructureWorld structures, TerrainWorld voxels,
                         FieldWorld fields, PlayerRig player, HudView hud)
        {
            _content = content;
            _structures = structures;
            _voxels = voxels;
            _fields = fields;
            _player = player;
            _hud = hud;

            var rootGo = new GameObject("Vehicles");
            rootGo.transform.SetParent(transform, false);
            _root = rootGo.transform;

            VehicleRig.Mounted += OnMounted;
            VehicleRig.Dismounted += OnDismounted;
        }

        void OnDestroy()
        {
            VehicleRig.Mounted -= OnMounted;
            VehicleRig.Dismounted -= OnDismounted;
        }

        // ------------------------------------------------------------------ spawning

        public VehicleRig Spawn(VehicleDefinition definition, Vector3 position, float yaw)
        {
            if (definition == null) return null;

            var go = new GameObject("Vehicle_" + definition.stringId);
            go.transform.SetParent(_root, false);
            go.transform.position = position;
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

            var rig = go.AddComponent<VehicleRig>();
            rig.Init(definition);

            _vehicles.Add(rig);
            return rig;
        }

        public void Remove(VehicleRig rig)
        {
            if (rig == null) return;

            _vehicles.Remove(rig);
            if (Driving == rig) Driving = null;
            Destroy(rig.gameObject);
        }

        public void Clear()
        {
            for (int i = 0; i < _vehicles.Count; i++)
            {
                if (_vehicles[i] != null) Destroy(_vehicles[i].gameObject);
            }
            _vehicles.Clear();
            Driving = null;
        }

        public VehicleDefinition FindDefinition(string stringId)
        {
            if (_content == null || string.IsNullOrEmpty(stringId)) return null;

            for (int i = 0; i < _content.vehicles.Count; i++)
            {
                if (_content.vehicles[i] != null && _content.vehicles[i].stringId == stringId) return _content.vehicles[i];
            }
            return null;
        }

        public ImplementDefinition FindImplement(string stringId)
        {
            if (_content == null || string.IsNullOrEmpty(stringId)) return null;

            for (int i = 0; i < _content.implements.Count; i++)
            {
                if (_content.implements[i] != null && _content.implements[i].stringId == stringId) return _content.implements[i];
            }
            return null;
        }

        /// <summary>Hitches an implement to a machine. Used by placement and by the loader.</summary>
        public ImplementController Hitch(VehicleRig rig, ImplementDefinition definition)
        {
            float dayLength = _content != null && _content.config != null ? _content.config.dayLengthSeconds : 1200f;
            return ImplementController.Hitch(definition, rig, _fields, _voxels, dayLength);
        }

        // -------------------------------------------------------------------- panel

        void OnMounted(VehicleRig rig)
        {
            Driving = rig;
            if (_hud != null && _hud.Tractor != null) _hud.Tractor.Mount();
        }

        void OnDismounted(VehicleRig rig)
        {
            if (Driving == rig) Driving = null;
            if (_hud != null && _hud.Tractor != null) _hud.Tractor.Dismount();
        }

        void Update()
        {
            if (Driving == null) return;

            // A wrecked machine throws you off rather than stranding you on a dead seat,
            // and so does dying on one: the camera is parented to the machine while you
            // are driving, so a respawn that left you mounted would leave it behind.
            if (!Driving.IsAlive || (Driving.Driver != null && !Driving.Driver.Stats.IsAlive))
            {
                Driving.Dismount();
                return;
            }

            if (InputBridge.HitchDown) ToggleHitch(Driving);
            if (InputBridge.HopperDown) WorkHopper(Driving);

            ReportSpillage(Driving.Implement);
            Refresh(Driving);
        }

        void Refresh(VehicleRig rig)
        {
            if (_hud == null || _hud.Tractor == null) return;

            var implement = rig.Implement;

            _hud.Tractor.SetReadout(
                rig.SpeedKph,
                rig.FuelLitres, rig.Definition.fuelCapacity,
                implement != null ? implement.HopperLitres : 0f,
                implement != null ? Mathf.Max(1f, implement.HopperCapacity) : 1f,
                implement != null ? implement.Readout : "NO IMPLEMENT",
                implement != null && implement.Engaged);
        }

        /// <summary>
        /// A harvester that fills mid-row keeps cutting and throws the surplus away. The
        /// player has to be told, once, or the loss is invisible.
        /// </summary>
        void ReportSpillage(ImplementController implement)
        {
            if (implement == null) return;

            float spilled = implement.TakeSpillage();
            if (spilled >= 1f) Notifications.PostFormat("Spilled {0:0} L - the hopper is full", spilled);
        }

        // ------------------------------------------------------------------ hitching

        void ToggleHitch(VehicleRig rig)
        {
            if (rig.Implement != null) { Unhitch(rig); return; }

            var player = rig.Driver;
            if (player == null) return;

            var held = player.Inventory.SelectedItem;
            if (held == null || held.hitchImplement == null)
            {
                Notifications.Post("Nothing to hitch - hold an implement");
                return;
            }

            if (Hitch(rig, held.hitchImplement) == null) return;

            player.Inventory.ConsumeSelected(1);
            Notifications.PostFormat("{0} hitched - F to lower it", held.hitchImplement.displayName);
        }

        void Unhitch(VehicleRig rig)
        {
            var implement = rig.Implement;
            if (implement == null) return;

            // Unhitching a loaded hopper would throw the load away without saying so.
            if (implement.HopperLitres >= 1f)
            {
                Notifications.PostFormat("{0} is loaded - empty it first (V)", implement.Definition.displayName);
                return;
            }

            var item = implement.Definition.item;
            string name = implement.Definition.displayName;

            // It has to have somewhere to go first. Unhitching into a full bag would
            // destroy a crafted implement to make room for nothing.
            if (item != null && rig.Driver != null && rig.Driver.Inventory.Bag.Add(item, 1) > 0)
            {
                Notifications.PostFormat("No room for the {0} - free a slot first", name);
                return;
            }

            CropDefinition cargo;
            implement.Unhitch(out cargo);

            Notifications.PostFormat("{0} unhitched", name);
        }

        // -------------------------------------------------------------------- hopper

        void WorkHopper(VehicleRig rig)
        {
            var implement = rig.Implement;
            if (implement == null)
            {
                Notifications.Post("Nothing hitched");
                return;
            }
            if (!implement.Definition.UsesHopper)
            {
                Notifications.PostFormat("{0} has no hopper", implement.Definition.displayName);
                return;
            }

            if (implement.Definition.FillsHopper) TipIntoBin(rig, implement);
            else LoadSeed(rig, implement);
        }

        void LoadSeed(VehicleRig rig, ImplementController implement)
        {
            var player = rig.Driver;
            if (player == null) return;

            var held = player.Inventory.SelectedStack;
            var crop = CropForSeed(held.Item);
            if (crop == null)
            {
                Notifications.Post("Hold a field seed to load the seeder");
                return;
            }

            float perItem = Mathf.Max(0.1f, implement.Definition.litresPerSeedItem);
            int loaded = 0;

            // One sack at a time. LoadSeed refuses a part-fill outright, so a sack is
            // either in the hopper or still in the bag - there is no state where the
            // player has paid for litres they did not get, or got litres they did not
            // pay for.
            for (int i = 0; i < held.Count; i++)
            {
                if (implement.LoadSeed(crop, perItem) <= 0f) break;
                loaded++;
            }

            if (loaded <= 0)
            {
                bool full = !ImplementWork.Accepts(implement.Definition, implement.HopperLitres, perItem);
                Notifications.PostFormat(full
                    ? "The hopper is as full as it will go"
                    : "The hopper will not take {0}", crop.displayName);
                return;
            }

            player.Inventory.ConsumeSelected(loaded);
            Notifications.PostFormat("Loaded {0} - {1:0} L", crop.displayName, implement.HopperLitres);
        }

        void TipIntoBin(VehicleRig rig, ImplementController implement)
        {
            if (implement.HopperLitres <= 0f)
            {
                Notifications.Post("The hopper is empty");
                return;
            }

            // A counter beats a bin. If you drove a full harvester all the way to an
            // outpost, you came to sell it, not to look for somewhere to put it.
            if (SellAtCounter(rig, implement)) return;

            var bin = NearestBin(rig.transform.position);
            if (bin == null)
            {
                Notifications.PostFormat("No grain bin within {0:0} m", TipRange);
                return;
            }

            CropDefinition cargo;
            float moved = implement.Tip(bin.Deposit, out cargo);

            if (moved <= 0f)
            {
                Notifications.Post("The grain bin is full");
                return;
            }

            Notifications.PostFormat("Tipped {0:0} L of {1}", moved,
                cargo != null ? cargo.displayName : "produce");
        }

        /// <summary>
        /// Tips the load over a trader's counter instead of into a bin. Returns true
        /// when a counter was in range, whether or not the sale went through - a failed
        /// sale at an outpost must not silently fall back to looking for a grain bin
        /// four hundred metres away.
        /// </summary>
        bool SellAtCounter(VehicleRig rig, ImplementController implement)
        {
            if (Traders == null) return false;

            var post = Traders.NearestPost(rig.transform.position, MadVoxel.Traders.TraderWorld.TipRange);
            if (post == null) return false;

            var player = rig.Driver;
            if (player == null) return false;

            var cargo = implement.Cargo;
            int paid;
            float sold = Traders.SellHarvest(post, player.Inventory.Bag, cargo,
                                             implement.HopperLitres,
                                             implement.Definition.FillsHopper, out paid);
            if (sold <= 0f) return true;

            // Only a sale over a counter counts toward a delivery contract. Tipping
            // into your own bin is storage, not delivery.
            if (_content != null && cargo != null)
            {
                player.Quests.ReportLitres(_content.Quest, cargo.stringId, Mathf.RoundToInt(sold));
            }

            implement.Empty();
            return true;
        }

        SiloStructure NearestBin(Vector3 from)
        {
            if (_structures == null) return null;

            SiloStructure best = null;
            float bestSqr = TipRange * TipRange;

            var all = _structures.All;
            for (int i = 0; i < all.Count; i++)
            {
                var bin = all[i] != null ? all[i].GetComponent<SiloStructure>() : null;
                if (bin == null) continue;

                float sqr = (bin.transform.position - from).sqrMagnitude;
                if (sqr > bestSqr) continue;

                bestSqr = sqr;
                best = bin;
            }
            return best;
        }

        CropDefinition CropForSeed(Inventory.ItemDefinition item)
        {
            if (item == null || _content == null) return null;

            for (int i = 0; i < _content.crops.Count; i++)
            {
                var crop = _content.crops[i];
                if (crop != null && crop.growsOnField && crop.seedItem == item) return crop;
            }
            return null;
        }
    }
}
