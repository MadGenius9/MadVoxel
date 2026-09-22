using MadVoxel.Building;
using MadVoxel.Core;
using MadVoxel.Core.Player;
using MadVoxel.Inventory;
using UnityEngine;

namespace MadVoxel.Power
{
    /// <summary>
    /// The bridge between a deployable in the world and its node on the grid. The
    /// deployable is what a zombie chews and a wrench salvages; the node is what the
    /// solver sees. One of these keeps the two in step.
    /// </summary>
    public class PowerDeviceStructure : MonoBehaviour, IInteractable
    {
        /// <summary>Raised when one is placed or destroyed, so the world can re-index.</summary>
        public static event System.Action<PowerDeviceStructure> Registered;
        public static event System.Action<PowerDeviceStructure> Unregistered;

        public PlacedStructure Structure { get; private set; }
        public PowerDeviceDefinition Device { get; private set; }

        /// <summary>The node id in the graph. Zero until the world has adopted it.</summary>
        public int NodeId { get; set; }

        public PowerGraph Graph { get; set; }

        Light _light;
        bool _lastLit;

        public PowerNode Node { get { return Graph != null ? Graph.Get(NodeId) : null; } }

        public void Bind(PlacedStructure structure)
        {
            Structure = structure;
            Device = structure.Definition.powerDevice;

            if (Device != null && Device.lightRange > 0f) BuildLamp();
            if (Registered != null) Registered(this);
        }

        void OnDestroy()
        {
            if (Unregistered != null) Unregistered(this);
        }

        void BuildLamp()
        {
            var go = new GameObject("Lamp");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0.5f, 1.1f, 0.5f);

            _light = go.AddComponent<Light>();
            _light.type = LightType.Point;
            _light.range = Device.lightRange;
            _light.color = Device.lightColour;
            _light.intensity = 1.5f;
            _light.shadows = LightShadows.None;
            _light.enabled = false;
        }

        /// <summary>Called by the world after each solve.</summary>
        public void ApplyState()
        {
            var node = Node;
            if (node == null) return;

            bool lit = node.IsPowered && node.SwitchedOn;
            if (_light != null && lit != _lastLit)
            {
                _light.enabled = lit;
                _lastLit = lit;
            }
        }

        // ------------------------------------------------------------- interaction

        public string InteractPrompt
        {
            get
            {
                var node = Node;
                if (node == null || Device == null) return "";

                switch (Device.kind)
                {
                    case PowerDeviceKind.Generator:
                        return node.SwitchedOn
                            ? string.Format("{0} - {1} L fuel  [E] stop", Device.displayName, Mathf.FloorToInt(node.FuelLitres))
                            : string.Format("{0} - {1} L fuel  [E] start", Device.displayName, Mathf.FloorToInt(node.FuelLitres));
                    case PowerDeviceKind.Switch:
                        return node.SwitchedOn ? "Switch  [E] open" : "Switch  [E] close";
                    default:
                        return node.SwitchedOn
                            ? Device.displayName + "  [E] switch off"
                            : Device.displayName + "  [E] switch on";
                }
            }
        }

        public void Interact(GameObject interactor)
        {
            var node = Node;
            if (node == null) return;

            // Fuelling comes first: a generator with gas in your hand should take it
            // rather than toggle, because that is always what you meant.
            if (Device.kind == PowerDeviceKind.Generator && TryRefuel(interactor)) return;

            node.SwitchedOn = !node.SwitchedOn;
            Notifications.PostFormat("{0} {1}", Device.displayName, node.SwitchedOn ? "on" : "off");
        }

        bool TryRefuel(GameObject interactor)
        {
            var inventory = interactor != null ? interactor.GetComponent<PlayerInventory>() : null;
            if (inventory == null) return false;

            var held = inventory.SelectedItem;
            if (held == null || held.fuelSeconds <= 0f) return false;

            var node = Node;
            float room = Device.fuelCapacityLitres - node.FuelLitres;
            if (room <= 0.01f)
            {
                Notifications.Post("The tank is full");
                return true;
            }

            // One can is one can: the item's burn time is its litres, so a mod that adds
            // a bigger jerrycan works without touching this.
            float litres = Mathf.Min(room, Mathf.Max(1f, held.fuelSeconds / 60f));
            node.FuelLitres += litres;
            inventory.ConsumeSelected(1);

            Notifications.PostFormat("{0} - {1} L", Device.displayName, Mathf.FloorToInt(node.FuelLitres));
            return true;
        }

        /// <summary>The Claim Slate look-at line for this device.</summary>
        public string Readout()
        {
            var node = Node;
            if (node == null || Device == null) return "";

            switch (Device.kind)
            {
                case PowerDeviceKind.Generator:
                    return string.Format("{0}   {1}/{2} W   FUEL {3}L",
                        Device.displayName.ToUpperInvariant(),
                        Mathf.RoundToInt(node.WattsProduced),
                        Mathf.RoundToInt(Device.wattsProduced),
                        Mathf.FloorToInt(node.FuelLitres));

                case PowerDeviceKind.SolarBank:
                    return string.Format("{0}   {1}/{2} W",
                        Device.displayName.ToUpperInvariant(),
                        Mathf.RoundToInt(node.WattsProduced),
                        Mathf.RoundToInt(Device.wattsProduced));

                case PowerDeviceKind.BatteryBank:
                    return string.Format("{0}   {1}/{2} Wh",
                        Device.displayName.ToUpperInvariant(),
                        Mathf.RoundToInt(node.StoredWattHours),
                        Mathf.RoundToInt(Device.storageWattHours));

                case PowerDeviceKind.Relay:
                    return string.Format("RELAY   {0}M", Mathf.RoundToInt(Device.relayRange));

                case PowerDeviceKind.Switch:
                    return node.SwitchedOn ? "SWITCH   CLOSED" : "SWITCH   OPEN";

                default:
                    return node.IsPowered
                        ? string.Format("{0}   {1} W", Device.displayName.ToUpperInvariant(), Mathf.RoundToInt(node.WattsDrawn))
                        : string.Format("{0}   NEEDS {1} W", Device.displayName.ToUpperInvariant(), Mathf.RoundToInt(Device.wattsConsumed));
            }
        }
    }
}
