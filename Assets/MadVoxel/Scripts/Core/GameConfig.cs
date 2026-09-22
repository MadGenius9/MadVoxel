using UnityEngine;

namespace MadVoxel.Core
{
    /// <summary>
    /// Tuning values. Authored as a ScriptableObject so balance can be edited without
    /// touching code; <see cref="MadVoxel.Content.ContentLibrary"/> builds the defaults.
    /// </summary>
    [CreateAssetMenu(menuName = "MadVoxel/Game Config", fileName = "GameConfig")]
    public class GameConfig : ScriptableObject
    {
        [Header("World streaming")]
        [Tooltip("Chunks of mesh built around the player in each horizontal direction.")]
        public int viewDistanceChunks = 6;
        [Tooltip("Extra ring of generated-but-unmeshed chunks so meshing always has neighbours.")]
        public int generationPadding = 1;
        public int maxChunkBuildsPerFrame = 6;

        [Header("Day / night")]
        [Tooltip("Real seconds for one full in-game day.")]
        public float dayLengthSeconds = 1200f;
        [Range(0f, 24f)] public float dawnHour = 6f;
        [Range(0f, 24f)] public float duskHour = 20f;
        [Tooltip("Hour of day a fresh world starts at.")]
        public float startHour = 7f;

        [Header("Player")]
        public float walkSpeed = 4.4f;
        public float sprintSpeed = 7.0f;
        public float crouchSpeed = 2.0f;
        public float jumpHeight = 1.15f;
        public float gravity = -22f;
        public float mouseSensitivity = 2.2f;
        public float maxHealth = 100f;
        public float maxStamina = 100f;
        public float maxFood = 100f;
        public float maxWater = 100f;
        public float sprintStaminaPerSecond = 9f;
        public float staminaRegenPerSecond = 12f;
        public float foodDrainPerMinute = 1.6f;
        public float waterDrainPerMinute = 2.1f;
        public float reachDistance = 5f;

        [Header("Survival")]
        [Tooltip("How much of the bag (non-hotbar) is dropped into a death backpack.")]
        public bool dropBagOnDeath = true;
        public float respawnInvulnerableSeconds = 4f;

        [Header("Land claim")]
        public float claimRadius = 24f;

        [Header("Threats")]
        public int wanderingZombieCapDay = 6;
        public int wanderingZombieCapNight = 14;
        public float zombieSpawnRadiusMin = 34f;
        public float zombieSpawnRadiusMax = 64f;
        public float zombieDespawnRadius = 110f;

        [Header("Save")]
        public float autosaveIntervalSeconds = 120f;
    }
}
