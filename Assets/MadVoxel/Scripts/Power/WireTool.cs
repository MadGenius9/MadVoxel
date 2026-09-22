using MadVoxel.Core;
using MadVoxel.Fluid;
using UnityEngine;

namespace MadVoxel.Power
{
    /// <summary>
    /// Rust's wiring, on 7 Days to Die's stations. Click one device to pick up a line,
    /// click a second to join them; right-click drops the line, or cuts every wire
    /// leaving whatever you are pointing at.
    ///
    /// The same tool does the plumbing, because a player holding pliers does not want
    /// to remember which pocket the hose spanner is in. Which graph it is talking to is
    /// decided by what is under the crosshair, and a line started on the grid simply
    /// will not close on a tap.
    /// </summary>
    public class WireTool
    {
        /// <summary>The device a line is currently hanging from, or 0 for none.</summary>
        public int PendingPowerNode { get; private set; }
        public int PendingFluidNode { get; private set; }

        public Vector3 PendingFrom { get; private set; }

        public bool HasPending { get { return PendingPowerNode != 0 || PendingFluidNode != 0; } }

        /// <summary>How far the pending line may reach, for the ghost.</summary>
        public float PendingReach { get; private set; }

        public void Clear()
        {
            PendingPowerNode = 0;
            PendingFluidNode = 0;
            PendingReach = 0f;
        }

        /// <summary>
        /// A click on an electrical device. Either starts a line or closes one.
        /// </summary>
        public void ClickPower(PowerGraph graph, PowerDeviceStructure device)
        {
            if (graph == null || device == null) return;

            // Starting a line on the grid abandons a half-run hose, rather than leaving
            // two pending lines the player cannot see.
            PendingFluidNode = 0;

            if (PendingPowerNode == 0)
            {
                if (device.Device.kind == PowerDeviceKind.Consumer)
                {
                    Notifications.Post("Start at a source, relay or switch - a consumer has no power out");
                    return;
                }

                PendingPowerNode = device.NodeId;
                PendingFrom = device.transform.position;
                PendingReach = graph.ReachOf(device.NodeId);
                Notifications.PostFormat("Wire from {0} - click the next device", device.Device.displayName);
                return;
            }

            if (PendingPowerNode == device.NodeId)
            {
                Clear();
                Notifications.Post("Wire dropped");
                return;
            }

            var result = graph.Connect(PendingPowerNode, device.NodeId);
            if (result != WireResult.Ok)
            {
                Notifications.Post(PowerGraph.Describe(result));
                return;
            }

            Notifications.PostFormat("Wired to {0}", device.Device.displayName);
            Clear();
        }

        /// <summary>A click on a fitting. Same two-step, different graph.</summary>
        public void ClickFluid(FluidGraph graph, FluidDeviceStructure device)
        {
            if (graph == null || device == null) return;

            PendingPowerNode = 0;

            if (PendingFluidNode == 0)
            {
                if (device.Device.IsOutlet)
                {
                    Notifications.Post("Start at a pump, pipe or tank - a tap is the end of the line");
                    return;
                }

                PendingFluidNode = device.NodeId;
                PendingFrom = device.transform.position;
                PendingReach = device.Device.maxPipeLength + graph.ExtraReach;
                Notifications.PostFormat("Hose from {0} - click the next fitting", device.Device.displayName);
                return;
            }

            if (PendingFluidNode == device.NodeId)
            {
                Clear();
                Notifications.Post("Hose dropped");
                return;
            }

            var result = graph.Connect(PendingFluidNode, device.NodeId);
            if (result != PipeResult.Ok)
            {
                Notifications.Post(FluidGraph.Describe(result));
                return;
            }

            Notifications.PostFormat("Piped to {0}", device.Device.displayName);
            Clear();
        }

        /// <summary>
        /// Right-click. Drops a half-run line if there is one, otherwise cuts every
        /// wire or hose leaving what you are pointing at.
        /// </summary>
        public void Cut(PowerGraph powerGraph, PowerDeviceStructure power,
                        FluidGraph fluidGraph, FluidDeviceStructure fluid)
        {
            if (HasPending)
            {
                Clear();
                Notifications.Post("Line dropped");
                return;
            }

            if (power != null && powerGraph != null)
            {
                var node = powerGraph.Get(power.NodeId);
                if (node != null && node.Outputs.Count > 0)
                {
                    int cut = node.Outputs.Count;
                    for (int i = node.Outputs.Count - 1; i >= 0; i--)
                    {
                        powerGraph.Disconnect(node.Id, node.Outputs[i]);
                    }
                    Notifications.PostFormat(cut == 1 ? "Cut {0} wire" : "Cut {0} wires", cut);
                    return;
                }
            }

            if (fluid != null && fluidGraph != null)
            {
                var node = fluidGraph.Get(fluid.NodeId);
                if (node != null && node.Outputs.Count > 0)
                {
                    int cut = node.Outputs.Count;
                    for (int i = node.Outputs.Count - 1; i >= 0; i--)
                    {
                        fluidGraph.Disconnect(node.Id, node.Outputs[i]);
                    }
                    Notifications.PostFormat(cut == 1 ? "Cut {0} hose" : "Cut {0} hoses", cut);
                    return;
                }
            }

            Notifications.Post("Nothing to cut there");
        }

        /// <summary>The visor line while a run is in the air.</summary>
        public string Readout()
        {
            if (!HasPending) return "";
            return string.Format("RUNNING A LINE   REACH {0}M   RMB TO DROP", Mathf.RoundToInt(PendingReach));
        }
    }
}
