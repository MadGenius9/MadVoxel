using MadVoxel.Building;
using MadVoxel.Core;
using MadVoxel.Core.Player;
using MadVoxel.Inventory;
using UnityEngine;

namespace MadVoxel.Fluid
{
    /// <summary>
    /// A fitting in the world and its node on the water graph, kept in step. Taps are
    /// the only one you actually use by hand; the rest are plumbing you walk past.
    /// </summary>
    public class FluidDeviceStructure : MonoBehaviour, IInteractable
    {
        public static event System.Action<FluidDeviceStructure> Registered;
        public static event System.Action<FluidDeviceStructure> Unregistered;

        public PlacedStructure Structure { get; private set; }
        public FluidDeviceDefinition Device { get; private set; }

        public int NodeId { get; set; }
        public FluidGraph Graph { get; set; }

        public FluidNode Node { get { return Graph != null ? Graph.Get(NodeId) : null; } }

        public void Bind(PlacedStructure structure)
        {
            Structure = structure;
            Device = structure.Definition.fluidDevice;
            if (Registered != null) Registered(this);
        }

        void OnDestroy()
        {
            if (Unregistered != null) Unregistered(this);
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
                    case FluidDeviceKind.Tap:
                        if (node.IsFrozen) return "Tap - frozen solid";
                        if (!node.IsLive) return "Tap - no water";
                        return "Tap  [E] drink or fill a bottle";

                    case FluidDeviceKind.Tank:
                        return string.Format("{0} - {1} / {2} L", Device.displayName,
                            Mathf.FloorToInt(node.Litres), Mathf.FloorToInt(node.Capacity));

                    case FluidDeviceKind.Pump:
                        if (!node.HasWetSource) return "Pump - no water under it";
                        if (!node.HasPower) return "Pump - no power";
                        return node.SwitchedOn ? "Pump  [E] stop" : "Pump  [E] start";

                    default:
                        return Device.displayName;
                }
            }
        }

        public void Interact(GameObject interactor)
        {
            var node = Node;
            if (node == null || Graph == null) return;

            if (Device.kind == FluidDeviceKind.Pump)
            {
                node.SwitchedOn = !node.SwitchedOn;
                Notifications.Post(node.SwitchedOn ? "Pump running" : "Pump stopped");
                return;
            }

            if (Device.kind != FluidDeviceKind.Tap) return;

            if (node.IsFrozen)
            {
                Notifications.Post("The tap is frozen - warm it or wait for the thaw");
                return;
            }

            float served = Graph.DrawFromOutlet(NodeId, Device.servingLitres);
            if (served <= 0.01f)
            {
                Notifications.Post("The tap is dry");
                return;
            }

            // A bottle in hand gets filled; otherwise you drink where you stand.
            var stats = interactor != null ? interactor.GetComponent<PlayerStats>() : null;
            if (stats != null)
            {
                stats.Consume(0f, served * 22f, 0f);
                Notifications.Post("Drank from the tap");
            }
        }

        public string Readout()
        {
            var node = Node;
            if (node == null || Device == null) return "";

            switch (Device.kind)
            {
                case FluidDeviceKind.Pump:
                    if (node.IsBroken) return "PUMP   BROKEN";
                    if (!node.HasPower) return string.Format("PUMP   NEEDS {0} W", Mathf.RoundToInt(Device.wattsRequired));
                    if (!node.HasWetSource) return "PUMP   DRY WELL";
                    return string.Format("PUMP   {0} L/MIN", Mathf.RoundToInt(node.FlowLitresPerMinute));

                case FluidDeviceKind.Tank:
                    return string.Format("TANK   {0} / {1} L",
                        Mathf.FloorToInt(node.Litres), Mathf.FloorToInt(node.Capacity));

                case FluidDeviceKind.Tap:
                    if (node.IsFrozen) return "TAP   FROZEN";
                    return node.IsLive ? "TAP   RUNNING" : "TAP   DRY";

                case FluidDeviceKind.Sprinkler:
                    return node.IsLive
                        ? string.Format("SPRINKLER   {0} L/MIN", Mathf.RoundToInt(node.FlowLitresPerMinute))
                        : "SPRINKLER   DRY";

                default:
                    return node.IsBroken ? "PIPE   BROKEN" : (node.IsLive ? "PIPE   FLOWING" : "PIPE   DRY");
            }
        }
    }
}
