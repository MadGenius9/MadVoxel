using UnityEngine;

namespace MadVoxel.Fluid
{
    /// <summary>
    /// Rust's fluid chain, kept to the five pieces that matter on a farm. There is no
    /// industrial splitter, no combiner and no fluid logic this pass.
    /// </summary>
    public enum FluidDeviceKind
    {
        /// <summary>Electric. Needs watts and a wet source under it.</summary>
        Pump,
        /// <summary>Carries flow. Can break in a storm, and a broken one kills the line.</summary>
        Pipe,
        /// <summary>Holds litres. The buffer that keeps the tap alive overnight.</summary>
        Tank,
        /// <summary>Drink, fill a bottle. Freezes on a frost night without heat.</summary>
        Tap,
        /// <summary>Waters garden plots in a radius, and pays for itself in yield.</summary>
        Sprinkler
    }

    [CreateAssetMenu(menuName = "MadVoxel/Fluid Device", fileName = "FluidDevice")]
    public class FluidDeviceDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string stringId = "madvoxel:fluid_device";
        public string displayName = "Fitting";
        public FluidDeviceKind kind = FluidDeviceKind.Pipe;

        [Header("Pump")]
        [Tooltip("Litres a minute at full power on a good source, before biome and weather.")]
        public float litresPerMinute = 24f;
        [Tooltip("Watts the pump needs. No power, no water.")]
        public float wattsRequired = 12f;
        [Tooltip("How far below the pump it will look for water, in blocks.")]
        public int sourceSearchDepth = 6;

        [Header("Tank")]
        public float capacityLitres = 400f;

        [Header("Outlet")]
        [Tooltip("Litres a minute an outlet pulls while it is running.")]
        public float drawLitresPerMinute = 6f;
        [Tooltip("Taps only: litres one drink or one bottle costs.")]
        public float servingLitres = 1.5f;
        [Tooltip("Taps only: a frost night stops this unless something nearby is warm.")]
        public bool freezable = true;

        [Header("Sprinkler")]
        public float sprinklerRadius = 6f;
        [Tooltip("Harvest multiplier on plots this sprinkler is keeping wet.")]
        public float wateredYieldBonus = 0.25f;
        [Tooltip("Field moisture added per game hour inside the radius.")]
        public float moisturePerHour = 0.18f;

        [Header("Plumbing")]
        [Tooltip("The longest single hose that may leave this fitting, in metres.")]
        public float maxPipeLength = 10f;
        public int maxOutputs = 1;
        [Tooltip("Out in the weather, so a storm can find it.")]
        public bool exposedToWeather = true;

        [Header("Placement")]
        public Building.StructureDefinition structure;

        public bool IsSource { get { return kind == FluidDeviceKind.Pump; } }
        public bool IsOutlet { get { return kind == FluidDeviceKind.Tap || kind == FluidDeviceKind.Sprinkler; } }
        public bool PassesFluid { get { return kind == FluidDeviceKind.Pipe || kind == FluidDeviceKind.Tank; } }
    }
}
