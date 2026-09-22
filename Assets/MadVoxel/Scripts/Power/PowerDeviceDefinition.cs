using MadVoxel.Inventory;
using UnityEngine;

namespace MadVoxel.Power
{
    /// <summary>
    /// What a device is on the grid. The kinds are deliberately few: 7 Days to Die's
    /// stations for production and storage, Rust's sockets and branching for the wiring,
    /// and nothing that needs a truth table. There is no AND gate, no memory cell and no
    /// timer here on purpose.
    /// </summary>
    public enum PowerDeviceKind
    {
        /// <summary>Burns gasoline for watts. Audible, and it can be switched off.</summary>
        Generator,
        /// <summary>Day only. Charges the bank rather than feeding the grid directly.</summary>
        SolarBank,
        /// <summary>Stores watt-hours and carries the grid when nothing is producing.</summary>
        BatteryBank,
        /// <summary>Extends wire reach. Without these you cannot run a mile of free wire.</summary>
        Relay,
        /// <summary>Hand-flipped. The blackout switch is this.</summary>
        Switch,
        /// <summary>Splits one feed into several without a length penalty of its own.</summary>
        Splitter,
        /// <summary>Anything that only draws: lights, fridges, traps, the pump.</summary>
        Consumer
    }

    /// <summary>
    /// How a consumer draws. Traps idle cheap and cost real watts only while working,
    /// which is what makes a defended base a power problem rather than a wiring puzzle.
    /// </summary>
    public enum PowerDrawMode
    {
        /// <summary>Always the full draw while switched on. Lights, fridges.</summary>
        Continuous,
        /// <summary>A trickle while armed, the full draw only while triggered.</summary>
        IdleThenBurst
    }

    [CreateAssetMenu(menuName = "MadVoxel/Power Device", fileName = "PowerDevice")]
    public class PowerDeviceDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string stringId = "madvoxel:power_device";
        public string displayName = "Device";
        public PowerDeviceKind kind = PowerDeviceKind.Consumer;

        [Header("Production")]
        [Tooltip("Watts this device puts on the grid when it is running.")]
        public float wattsProduced;
        [Tooltip("Generators only: litres of fuel burned per game hour at full output.")]
        public float fuelLitresPerHour = 1.5f;
        [Tooltip("Generators only: how much fuel the tank holds, in litres.")]
        public float fuelCapacityLitres = 20f;
        [Tooltip("Generators only: how far the noise carries, for claim heat.")]
        public float noiseRadius = 26f;
        [Tooltip("Generators only: heat added to the claim while running.")]
        public float heatWhileRunning = 14f;

        [Header("Storage")]
        [Tooltip("Battery banks only: watt-hours held.")]
        public float storageWattHours;
        [Tooltip("Battery banks only: the most it can push out at once.")]
        public float dischargeWatts = 60f;

        [Header("Draw")]
        [Tooltip("Watts this device needs to do its job.")]
        public float wattsConsumed;
        public PowerDrawMode drawMode = PowerDrawMode.Continuous;
        [Tooltip("IdleThenBurst only: the trickle while armed and waiting.")]
        public float idleWatts = 1f;

        [Header("Wiring")]
        [Tooltip("The longest single wire that may leave this device, in metres.")]
        public float maxWireLength = 12f;
        [Tooltip("Relays only: the reach they hand on. This is the whole point of them.")]
        public float relayRange = 30f;
        [Tooltip("Relays only: a small standing cost, so range is not free.")]
        public float relayUpkeepWatts = 1f;
        [Tooltip("How many wires may leave the output socket. Splitters raise this.")]
        public int maxOutputs = 1;

        [Header("Consumer behaviour")]
        [Tooltip("Lights: how far they throw, and what they do to claim heat after dark.")]
        public float lightRange;
        public Color lightColour = new Color(1f, 0.90f, 0.72f);
        public float heatWhileLit = 5f;
        [Tooltip("Fridges: how much a powered fridge slows spoilage.")]
        public float spoilSlowdown = 1f;
        [Tooltip("Traps: damage per hit while powered.")]
        public float trapDamage;
        public float trapRadius = 2.2f;
        public float trapIntervalSeconds = 0.8f;
        [Tooltip("Traps: heat added while actually spinning or arcing.")]
        public float heatWhileActive = 8f;
        [Tooltip("Pumps: litres a minute at full power, before biome and weather.")]
        public float pumpLitresPerMinute;

        [Header("Placement")]
        [Tooltip("Which deployable carries this device. The device is bound to it.")]
        public Building.StructureDefinition structure;
        public ItemDefinition repairItem;

        public bool IsSource
        {
            get { return kind == PowerDeviceKind.Generator || kind == PowerDeviceKind.SolarBank; }
        }

        public bool PassesPower
        {
            get
            {
                return kind == PowerDeviceKind.Relay
                    || kind == PowerDeviceKind.Switch
                    || kind == PowerDeviceKind.Splitter;
            }
        }

        /// <summary>The reach of a wire leaving this device, relays included.</summary>
        public float WireReach
        {
            get { return kind == PowerDeviceKind.Relay ? Mathf.Max(maxWireLength, relayRange) : maxWireLength; }
        }
    }
}
