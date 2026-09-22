using System;
using System.Collections.Generic;
using MadVoxel.Core;
using UnityEngine;

namespace MadVoxel.Skills
{
    public enum XpSource
    {
        Harvest,
        Build,
        Craft,
        Kill,
        Quest,
        Discovery
    }

    /// <summary>
    /// XP, level and skill points. Phase 0 uses the level to scale horde pressure;
    /// Phase 1 spends the points in <see cref="SkillTreeDefinition"/>.
    /// Creative-only actions (placing a block you just mined back down) award nothing:
    /// XP for building is granted per crafted piece, not per placement.
    /// </summary>
    public class PlayerProgression : MonoBehaviour
    {
        public int Level { get; private set; }
        public float Xp { get; private set; }
        public int UnspentSkillPoints { get; private set; }

        readonly Dictionary<string, int> _skillRanks = new Dictionary<string, int>();
        readonly HashSet<string> _unlockedRecipes = new HashSet<string>();

        public event Action<int> LevelledUp;
        public event Action Changed;

        public ISet<string> UnlockedRecipes { get { return _unlockedRecipes; } }
        public IReadOnlyDictionary<string, int> SkillRanks { get { return _skillRanks; } }

        [Tooltip("Skill points granted per level.")]
        public int pointsPerLevel = 1;

        void Awake()
        {
            if (Level < 1) Level = 1;
        }

        /// <summary>XP needed to go from the given level to the next.</summary>
        public static float XpForNextLevel(int level)
        {
            return 90f + (level - 1) * 55f + Mathf.Pow(level, 1.55f) * 8f;
        }

        public float XpToNext { get { return XpForNextLevel(Level); } }
        public float XpProgress01 { get { return Mathf.Clamp01(Xp / XpToNext); } }

        public void AddXp(float amount, XpSource source)
        {
            if (amount <= 0f) return;
            Xp += amount;

            bool levelled = false;
            while (Xp >= XpToNext)
            {
                Xp -= XpToNext;
                Level++;
                UnspentSkillPoints += pointsPerLevel;
                levelled = true;
                if (LevelledUp != null) LevelledUp(Level);
            }

            if (levelled)
            {
                Notifications.PostFormat("Level {0}  -  {1} skill point(s) to spend", Level, UnspentSkillPoints);
            }
            if (Changed != null) Changed();
        }

        public int GetRank(string skillId)
        {
            int rank;
            return _skillRanks.TryGetValue(skillId, out rank) ? rank : 0;
        }

        public void SetRank(string skillId, int rank)
        {
            if (string.IsNullOrEmpty(skillId)) return;
            _skillRanks[skillId] = rank;
            if (Changed != null) Changed();
        }

        public void SpendPoint(string skillId, int newRank)
        {
            if (UnspentSkillPoints <= 0) return;
            UnspentSkillPoints--;
            SetRank(skillId, newRank);
        }

        public void UnlockRecipe(string recipeId)
        {
            if (string.IsNullOrEmpty(recipeId)) return;
            if (_unlockedRecipes.Add(recipeId) && Changed != null) Changed();
        }

        public void LoadState(int level, float xp, int points, Dictionary<string, int> ranks, IEnumerable<string> unlockedRecipes)
        {
            Level = Mathf.Max(1, level);
            Xp = Mathf.Max(0f, xp);
            UnspentSkillPoints = Mathf.Max(0, points);

            _skillRanks.Clear();
            if (ranks != null)
            {
                foreach (var kv in ranks) _skillRanks[kv.Key] = kv.Value;
            }

            _unlockedRecipes.Clear();
            if (unlockedRecipes != null)
            {
                foreach (var id in unlockedRecipes) _unlockedRecipes.Add(id);
            }

            if (Changed != null) Changed();
        }

        public void ResetProgression()
        {
            Level = 1;
            Xp = 0f;
            UnspentSkillPoints = 0;
            _skillRanks.Clear();
            _unlockedRecipes.Clear();
            if (Changed != null) Changed();
        }
    }
}
