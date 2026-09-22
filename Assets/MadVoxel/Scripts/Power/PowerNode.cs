using System.Collections.Generic;
using UnityEngine;

namespace MadVoxel.Power
{
    /// <summary>
    /// One device on the grid. Deliberately a plain class rather than a MonoBehaviour:
    /// the whole graph has to solve without a scene so it can be checked headlessly and
    /// so a save can rebuild it before any deployable exists.
    /// </summary>
    public class PowerNode
    {
        public int Id;
        public PowerDeviceDefinition Definition;
        public Vector3 Position;

        /// <summary>Hand switch. A generator or a switch that is off passes nothing.</summary>
        public bool SwitchedOn = true;

        /// <summary>Generators only. Litres in the tank.</summary>
        public float FuelLitres;

        /// <summary>Battery banks only. Watt-hours held.</summary>
        public float StoredWattHours;

        /// <summary>Burst consumers only: something is in front of the trap right now.</summary>
        public bool Triggered;

        /// <summary>Set by the solve: the device has the watts it asked for.</summary>
        public bool IsPowered;

        /// <summary>Set by the solve: what it is actually drawing this tick.</summary>
        public float WattsDrawn;

        /// <summary>Set by the solve: what a source is actually putting out this tick.</summary>
        public float WattsProduced;

        /// <summary>Set by the solve: wire hops from the nearest live source.</summary>
        public int DistanceFromSource = int.MaxValue;

        /// <summary>Wires leaving this device's output socket.</summary>
        public readonly List<int> Outputs = new List<int>();

        /// <summary>Wires arriving at this device's input socket.</summary>
        public readonly List<int> Inputs = new List<int>();

        public bool IsAlive = true;

        public PowerDeviceKind Kind { get { return Definition != null ? Definition.kind : PowerDeviceKind.Consumer; } }

        /// <summary>
        /// What this device wants this tick. A trap that nothing has walked into only
        /// asks for its idle trickle, which is what lets a small generator arm a fence
        /// it could never actually run at full tilt.
        /// </summary>
        public float DemandWatts
        {
            get
            {
                if (Definition == null || !SwitchedOn) return 0f;

                if (Definition.kind == PowerDeviceKind.Relay) return Definition.relayUpkeepWatts;
                if (Definition.kind != PowerDeviceKind.Consumer) return 0f;

                if (Definition.drawMode == PowerDrawMode.IdleThenBurst)
                {
                    return Triggered ? Definition.wattsConsumed : Definition.idleWatts;
                }
                return Definition.wattsConsumed;
            }
        }

        /// <summary>Whether a generator has what it needs to turn over at all.</summary>
        public bool CanProduce(bool isDay)
        {
            if (Definition == null || !SwitchedOn) return false;

            switch (Definition.kind)
            {
                case PowerDeviceKind.Generator: return FuelLitres > 0f;
                case PowerDeviceKind.SolarBank: return isDay;
                default: return false;
            }
        }

        public float FuelFraction
        {
            get
            {
                if (Definition == null || Definition.fuelCapacityLitres <= 0f) return 0f;
                return Mathf.Clamp01(FuelLitres / Definition.fuelCapacityLitres);
            }
        }

        public float ChargeFraction
        {
            get
            {
                if (Definition == null || Definition.storageWattHours <= 0f) return 0f;
                return Mathf.Clamp01(StoredWattHours / Definition.storageWattHours);
            }
        }
    }
}
