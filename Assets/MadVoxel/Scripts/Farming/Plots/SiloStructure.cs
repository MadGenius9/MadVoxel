using MadVoxel.Building;
using MadVoxel.Farming.Crops;
using MadVoxel.Core;
using UnityEngine;

namespace MadVoxel.Farming.Plots
{
    /// <summary>
    /// A grain bin. Bulk produce is measured in litres, not stacks, which is the whole
    /// point of the field layer: a garden fills your bag, a field fills this.
    ///
    /// A harvester tips into it with V while parked within
    /// <see cref="MadVoxel.Vehicles.VehicleWorld.TipRange"/> metres; selling what is in
    /// it is the trader's job and is not built yet.
    /// </summary>
    public class SiloStructure : MonoBehaviour, IInteractable
    {
        /// <summary>Raised when the player opens it. The UI layer listens.</summary>
        public static event System.Action<SiloStructure> OpenRequested;

        public PlacedStructure Structure { get; private set; }

        /// <summary>Litres currently stored, keyed by crop string id.</summary>
        readonly System.Collections.Generic.Dictionary<string, float> _contents =
            new System.Collections.Generic.Dictionary<string, float>();

        public float Capacity
        {
            get { return Structure != null ? Structure.Definition.siloCapacityLitres : 0f; }
        }

        public float StoredLitres
        {
            get
            {
                float total = 0f;
                foreach (var kv in _contents) total += kv.Value;
                return total;
            }
        }

        public float FreeLitres { get { return Mathf.Max(0f, Capacity - StoredLitres); } }

        public void Bind(PlacedStructure structure)
        {
            Structure = structure;
        }

        /// <summary>Tips produce in and returns how many litres would not fit.</summary>
        public float Deposit(string cropId, float litres)
        {
            if (string.IsNullOrEmpty(cropId) || litres <= 0f) return litres;

            float accepted = Mathf.Min(litres, FreeLitres);
            if (accepted <= 0f) return litres;

            float existing;
            _contents.TryGetValue(cropId, out existing);
            _contents[cropId] = existing + accepted;

            return litres - accepted;
        }

        public float Withdraw(string cropId, float litres)
        {
            float existing;
            if (!_contents.TryGetValue(cropId, out existing)) return 0f;

            float taken = Mathf.Min(existing, litres);
            _contents[cropId] = existing - taken;
            if (_contents[cropId] <= 0.01f) _contents.Remove(cropId);
            return taken;
        }

        public System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<string, float>> Contents
        {
            get { return _contents; }
        }

        public void RestoreContents(string cropId, float litres)
        {
            if (string.IsNullOrEmpty(cropId) || litres <= 0f) return;
            _contents[cropId] = litres;
        }

        public string InteractPrompt
        {
            get { return string.Format("Grain bin - {0:0} / {1:0} L", StoredLitres, Capacity); }
        }

        public void Interact(GameObject interactor)
        {
            if (OpenRequested != null) OpenRequested(this);
        }

        /// <summary>
        /// Draws bulk produce back out as harvest items.
        ///
        /// Without this the bin is a black hole: a harvester could tip into it and
        /// nothing could ever come out again. Drawing costs the exact litres the items
        /// are worth, so the bin cannot be used to launder a fraction of a litre into a
        /// whole sack.
        /// </summary>
        public int Draw(CropDefinition crop, MadVoxel.Inventory.Inventory bag, int wantedItems)
        {
            if (crop == null || crop.harvestItem == null || bag == null || wantedItems <= 0) return 0;

            float stored = StoredOf(crop.stringId);
            int affordable = Mathf.Min(wantedItems, crop.ItemsFromLitres(stored));
            if (affordable <= 0) return 0;

            // Only as many as will actually fit. Taking the litres for items that then
            // bounce off a full bag would lose the harvest.
            while (affordable > 0 && !bag.CanFit(crop.harvestItem, affordable)) affordable--;
            if (affordable <= 0) return 0;

            float cost = crop.LitresForItems(affordable);
            if (Withdraw(crop.stringId, cost) < cost - 0.01f) return 0;

            bag.Add(crop.harvestItem, affordable);
            return affordable;
        }

        public float StoredOf(string cropId)
        {
            float litres;
            return !string.IsNullOrEmpty(cropId) && _contents.TryGetValue(cropId, out litres) ? litres : 0f;
        }
    }
}
