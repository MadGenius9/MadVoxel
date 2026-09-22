using MadVoxel.Inventory;
using UnityEngine;

namespace MadVoxel.AI
{
    [CreateAssetMenu(menuName = "MadVoxel/Zombie", fileName = "Zombie")]
    public class ZombieDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string stringId = "madvoxel:zombie";
        public string displayName = "Shambler";

        [Header("Stats")]
        public float maxHealth = 55f;
        public float walkSpeed = 1.5f;
        [Tooltip("Speed once it has seen the player, or during a blood moon.")]
        public float chaseSpeed = 3.1f;
        public float meleeDamage = 9f;
        public float attackInterval = 1.25f;
        public float attackRange = 1.85f;
        [Tooltip("Damage per second dealt to voxels and snap pieces while digging.")]
        public float digDamagePerSecond = 22f;
        public float sightRange = 26f;
        public float xpReward = 18f;

        [Header("Look")]
        public Color tint = new Color(0.36f, 0.42f, 0.30f);
        public float height = 1.8f;

        [Header("Loot")]
        public ItemDefinition dropItem;
        public int dropMin = 0;
        public int dropMax = 2;
    }
}
