using MadVoxel.Building;
using MadVoxel.Core;
using UnityEngine;

namespace MadVoxel.Farming.Plots
{
    /// <summary>
    /// A grain bin. Bulk produce is measured in litres, not stacks, which is the whole
    /// point of the field layer: a garden fills your bag, a field fills this.
    ///
    /// Phase 0 ships the store and its readout so the field harvester has somewhere to
    /// dump into; the tipping and selling flow is Phase 1.
    /// </summary>
    public class SiloStructure : MonoBehaviour, IInteractable
    {
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
            if (StoredLitres <= 0f)
            {
                Notifications.Post("The grain bin is empty. Harvest a field to fill it.");
                return;
            }

            foreach (var kv in _contents)
            {
                Notifications.PostFormat("{0}: {1:0} L", kv.Key, kv.Value);
            }
        }
    }
}
