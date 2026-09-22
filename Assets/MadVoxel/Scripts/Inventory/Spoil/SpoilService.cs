using System.Collections.Generic;
using MadVoxel.Building;
using MadVoxel.Content;
using MadVoxel.Core;
using MadVoxel.Core.Player;
using MadVoxel.Power;
using UnityEngine;

namespace MadVoxel.Inventory.Spoil
{
    /// <summary>
    /// Runs the shelf life on every stack in the world: the player's bag, every crate,
    /// and every fridge. A fridge with watts in it is the only thing that slows the
    /// clock, which is the entire reason the grid matters to a farmer.
    ///
    /// It samples rather than ticking per frame - food does not need millisecond
    /// resolution, and walking every crate sixty times a second would be absurd.
    /// </summary>
    public class SpoilService : MonoBehaviour
    {
        const float SampleInterval = 4f;

        StructureWorld _structures;
        PlayerRig _player;
        WorldClock _clock;
        ContentDatabase _content;

        double _lastHours;
        float _sinceSample;

        readonly List<ItemStack> _scratch = new List<ItemStack>();

        public void Init(StructureWorld structures, PlayerRig player, WorldClock clock, ContentDatabase content)
        {
            _structures = structures;
            _player = player;
            _clock = clock;
            _content = content;
            _lastHours = clock != null ? clock.TotalHours : 0.0;
        }

        void Update()
        {
            if (_clock == null) return;

            _sinceSample += Time.deltaTime;
            if (_sinceSample < SampleInterval) return;
            _sinceSample = 0f;

            double now = _clock.TotalHours;
            float elapsed = Mathf.Max(0f, (float)(now - _lastHours));
            if (elapsed <= 0f) return;
            _lastHours = now;

            int spoiled = 0;

            if (_player != null && _player.Inventory != null)
            {
                // A bag is a bag. Carrying stew around does not keep it.
                spoiled += TickContainer(_player.Inventory.Bag, elapsed, 1f);
            }

            if (_structures != null)
            {
                var all = _structures.All;
                for (int i = 0; i < all.Count; i++)
                {
                    var storage = all[i].GetComponent<StorageStructure>();
                    if (storage == null) continue;

                    spoiled += TickContainer(storage.Contents, elapsed, RateOf(all[i]));
                }
            }

            if (spoiled > 0)
            {
                Notifications.PostFormat(spoiled == 1 ? "{0} stack spoiled" : "{0} stacks spoiled", spoiled);
            }
        }

        /// <summary>
        /// How fast the clock runs inside this container. A fridge that has lost its
        /// power is just a cupboard, which is what makes a blackout expensive.
        /// </summary>
        float RateOf(PlacedStructure structure)
        {
            var device = structure.GetComponent<PowerDeviceStructure>();
            if (device == null || device.Device == null || device.Device.spoilSlowdown <= 1f) return 1f;

            var node = device.Node;
            bool powered = node != null && node.IsPowered;
            return SpoilRules.RateIn(true, powered, device.Device.spoilSlowdown);
        }

        int TickContainer(Inventory inventory, float gameHours, float rate)
        {
            if (inventory == null) return 0;

            var rot = _content != null ? _content.Item(ItemIds.Rot) : null;
            int spoiled = 0;
            bool changed = false;

            for (int i = 0; i < inventory.Size; i++)
            {
                var stack = inventory[i];
                if (stack.IsEmpty || !SpoilRules.CanSpoil(stack.Item)) continue;

                var next = SpoilRules.Tick(stack, gameHours, rate, rot);
                if (next.Item == stack.Item && Mathf.Approximately(next.SpoilRemaining, stack.SpoilRemaining)) continue;

                if (next.Item != stack.Item || next.IsEmpty) spoiled++;

                inventory.SetSlotQuiet(i, next);
                changed = true;
            }

            if (changed) inventory.RaiseChanged();
            return spoiled;
        }
    }
}
