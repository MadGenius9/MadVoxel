using MadVoxel.Inventory;
using UnityEngine;

namespace MadVoxel.Vehicles
{
    /// <summary>
    /// A land vehicle. Phase 0 ships the data and the craft recipe hook; Phase 1 adds
    /// the driving controller, the fuel burn and the storage crate.
    /// </summary>
    [CreateAssetMenu(menuName = "MadVoxel/Vehicle", fileName = "Vehicle")]
    public class VehicleDefinition : ScriptableObject
    {
        public string stringId = "madvoxel:vehicle";
        public string displayName = "Vehicle";

        [Header("Drive")]
        public float maxSpeed = 16f;
        public float acceleration = 7f;
        public float turnRate = 90f;
        [Tooltip("Maximum voxel step the wheels can climb without stopping dead.")]
        public float climbHeight = 1.05f;

        [Header("Fuel")]
        public float fuelCapacity = 100f;
        public float fuelPerSecond = 0.55f;
        public ItemDefinition fuelItem;
        public float fuelPerItem = 20f;

        [Header("Capacity")]
        public int seats = 1;
        public int storageSlots = 18;
        public float maxHealth = 400f;

        [Header("Look")]
        public Color tint = new Color(0.42f, 0.33f, 0.26f);
        public Vector3 chassisSize = new Vector3(1.5f, 0.7f, 2.6f);
    }
}
