using MadVoxel.AI;
using UnityEngine;

namespace MadVoxel.Horde
{
    /// <summary>
    /// Blood-moon calendar and wave curve. Everything that decides how hard a horde
    /// night is lives here so tuning never means editing code.
    /// </summary>
    [CreateAssetMenu(menuName = "MadVoxel/Horde Schedule", fileName = "HordeSchedule")]
    public class HordeSchedule : ScriptableObject
    {
        [Header("Calendar")]
        [Tooltip("A blood moon falls on every Nth day.")]
        public int everyNDays = 7;
        [Range(0f, 24f)] public float startHour = 22f;
        [Range(0f, 24f)] public float endHour = 4f;

        [Header("Wave size")]
        public int baseWaveSize = 4;
        [Tooltip("Extra zombies per player level.")]
        public float perPlayerLevel = 0.75f;
        [Tooltip("Extra zombies per snap piece inside the claim - big bases draw big hordes.")]
        public float perClaimStructure = 0.07f;
        public int maxAlive = 26;
        public float waveIntervalSeconds = 24f;

        [Header("Spawning")]
        public float spawnRadiusMin = 38f;
        public float spawnRadiusMax = 72f;

        [Header("Roster")]
        public ZombieDefinition baseZombie;
        [Tooltip("Optional tougher variant introduced from this blood moon number onward.")]
        public ZombieDefinition heavyZombie;
        public int heavyFromHordeNumber = 2;
        [Range(0f, 1f)] public float heavyShare = 0.25f;

        [Header("Reward")]
        public float survivalXp = 250f;

        public int WaveSize(int playerLevel, int claimStructures)
        {
            float size = baseWaveSize + playerLevel * perPlayerLevel + claimStructures * perClaimStructure;
            return Mathf.Clamp(Mathf.RoundToInt(size), 1, maxAlive);
        }
    }
}
