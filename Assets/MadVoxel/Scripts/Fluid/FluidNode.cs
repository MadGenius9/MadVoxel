using System.Collections.Generic;
using UnityEngine;

namespace MadVoxel.Fluid
{
    /// <summary>One fitting on the water side. Engine-free, for the same reasons the power node is.</summary>
    public class FluidNode
    {
        public int Id;
        public FluidDeviceDefinition Definition;
        public Vector3 Position;

        public bool SwitchedOn = true;

        /// <summary>Tanks only. Litres held.</summary>
        public float Litres;

        /// <summary>Pumps only: the power node that feeds it, or 0 for none.</summary>
        public int PowerNodeId;

        /// <summary>Pumps only: set by the world. Is there actually water under it?</summary>
        public bool HasWetSource;

        /// <summary>Pumps only: set by the world from the power graph.</summary>
        public bool HasPower;

        /// <summary>A storm broke this fitting. It leaks, and nothing downstream runs.</summary>
        public bool IsBroken;

        /// <summary>Taps only: frozen solid until the weather turns or something warms it.</summary>
        public bool IsFrozen;

        /// <summary>Set by the solve: litres a minute actually moving through here.</summary>
        public float FlowLitresPerMinute;

        /// <summary>Set by the solve: the line reaches this fitting and it is working.</summary>
        public bool IsLive;

        public readonly List<int> Outputs = new List<int>();
        public readonly List<int> Inputs = new List<int>();

        public FluidDeviceKind Kind { get { return Definition != null ? Definition.kind : FluidDeviceKind.Pipe; } }

        public float Capacity { get { return Definition != null ? Definition.capacityLitres : 0f; } }

        public float Fraction
        {
            get { return Capacity > 0f ? Mathf.Clamp01(Litres / Capacity) : 0f; }
        }

        /// <summary>
        /// Everything a pump needs to turn over: a switch, watts and something wet
        /// under it. Brokenness is deliberately NOT part of this - a cracked pump still
        /// spins and still costs you the power, it just pumps its water onto the floor.
        /// The walk decides where those litres end up.
        /// </summary>
        public bool CanPump
        {
            get
            {
                return Kind == FluidDeviceKind.Pump && SwitchedOn && HasPower && HasWetSource;
            }
        }
    }
}
