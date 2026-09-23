using System.Collections.Generic;
using MadVoxel.Inventory;
using UnityEngine;

namespace MadVoxel.Quests
{
    /// <summary>Why a contract could not be taken or handed in. All of these are shown.</summary>
    public enum QuestResult
    {
        Ok,
        UnknownQuest,
        AlreadyTaken,
        AlreadyDone,
        /// <summary>You are carrying as many contracts as you can hold.</summary>
        TooManyTaken,
        LevelTooLow,
        ReputationTooLow,
        NotFinished,
        /// <summary>The goods are the objective and they are not in your bag.</summary>
        GoodsMissing,
        NoRoomForReward
    }

    /// <summary>
    /// One contract in progress. The counter is only used by the kinds that cannot
    /// derive their progress - see <see cref="QuestService.IsCounted"/>.
    /// </summary>
    public struct QuestEntry
    {
        public string QuestId;
        public double AcceptedAtHours;
        public int Counter;
        public bool TurnedIn;
    }

    /// <summary>
    /// The rules for taking, tracking and handing in a contract.
    ///
    /// Progress splits two ways, and which side a contract falls on is the only real
    /// decision here:
    ///
    /// - <b>Derived.</b> A fetch contract's progress is however many of the item are in
    ///   your bag right now; a survive-nights contract's is how many nights have passed
    ///   since you took it. Neither is stored, so neither can drift, be double-counted,
    ///   or come back wrong from a save.
    /// - <b>Counted.</b> Kills and litres delivered have no standing record to read, so
    ///   they are tallied on the entry as they happen.
    ///
    /// Everything that *can* be derived is, for the same reason crop growth is: a number
    /// the game recomputes cannot disagree with the world, and a number it accumulates
    /// eventually will.
    /// </summary>
    public static class QuestService
    {
        /// <summary>Contracts you can carry at once. Enough to plan around, few enough to read.</summary>
        public const int MaxActive = 3;

        /// <summary>A "night survived" is one day boundary crossed after taking the contract.</summary>
        public const double HoursPerNight = 24.0;

        /// <summary>True when this kind tallies as it goes rather than being recomputed.</summary>
        public static bool IsCounted(QuestKind kind)
        {
            return kind == QuestKind.Clear || kind == QuestKind.DeliverLitres;
        }

        /// <summary>True when handing in takes the goods off you.</summary>
        public static bool ConsumesGoods(QuestKind kind)
        {
            return kind == QuestKind.Fetch || kind == QuestKind.Deliver;
        }

        // --------------------------------------------------------------- accepting

        public static QuestResult CanAccept(QuestDefinition quest, int playerLevel, int reputationTier,
                                            int activeCount, bool alreadyTaken, bool alreadyDone)
        {
            if (quest == null) return QuestResult.UnknownQuest;
            if (alreadyDone) return QuestResult.AlreadyDone;
            if (alreadyTaken) return QuestResult.AlreadyTaken;
            if (playerLevel < quest.requiredPlayerLevel) return QuestResult.LevelTooLow;
            if (reputationTier < quest.requiredReputationTier) return QuestResult.ReputationTooLow;
            if (activeCount >= MaxActive) return QuestResult.TooManyTaken;

            return QuestResult.Ok;
        }

        // ---------------------------------------------------------------- progress

        /// <summary>
        /// How far along a contract is, in its own units. Derived where it can be.
        /// </summary>
        public static int Progress(QuestDefinition quest, QuestEntry entry,
                                   Inventory.Inventory bag, double nowHours)
        {
            if (quest == null) return 0;

            switch (quest.kind)
            {
                case QuestKind.Fetch:
                case QuestKind.Deliver:
                    return quest.objectiveItem != null && bag != null ? bag.CountOf(quest.objectiveItem) : 0;

                case QuestKind.SurviveNights:
                    return NightsSince(entry.AcceptedAtHours, nowHours);

                default:
                    return Mathf.Max(0, entry.Counter);
            }
        }

        /// <summary>
        /// Nights survived since a contract was taken. Counted as whole day boundaries
        /// crossed, so taking one at dusk and living until morning is one night rather
        /// than a full day's wait - and taking one at dawn does not credit you a night
        /// you have not yet lived through.
        /// </summary>
        public static int NightsSince(double acceptedAtHours, double nowHours)
        {
            if (nowHours <= acceptedAtHours) return 0;

            int acceptedDay = Mathf.FloorToInt((float)(acceptedAtHours / HoursPerNight));
            int nowDay = Mathf.FloorToInt((float)(nowHours / HoursPerNight));

            return Mathf.Max(0, nowDay - acceptedDay);
        }

        public static bool IsComplete(QuestDefinition quest, QuestEntry entry,
                                      Inventory.Inventory bag, double nowHours)
        {
            if (quest == null) return false;
            return Progress(quest, entry, bag, nowHours) >= Mathf.Max(1, quest.objectiveCount);
        }

        public static float Progress01(QuestDefinition quest, QuestEntry entry,
                                       Inventory.Inventory bag, double nowHours)
        {
            if (quest == null) return 0f;
            return Mathf.Clamp01(Progress(quest, entry, bag, nowHours) / (float)Mathf.Max(1, quest.objectiveCount));
        }

        // ---------------------------------------------------------------- turn-in

        public static QuestResult CanTurnIn(QuestDefinition quest, QuestEntry entry,
                                            Inventory.Inventory bag, double nowHours)
        {
            if (quest == null) return QuestResult.UnknownQuest;
            if (entry.TurnedIn) return QuestResult.AlreadyDone;
            if (!IsComplete(quest, entry, bag, nowHours)) return QuestResult.NotFinished;

            if (ConsumesGoods(quest.kind))
            {
                if (bag == null || quest.objectiveItem == null) return QuestResult.GoodsMissing;
                if (bag.CountOf(quest.objectiveItem) < quest.objectiveCount) return QuestResult.GoodsMissing;
            }

            // Asked before anything moves. Handing over twenty scrap for a reward that
            // will not fit is the one way turning a contract in can cost you.
            if (!HasRoomForRewards(quest, bag)) return QuestResult.NoRoomForReward;

            return QuestResult.Ok;
        }

        /// <summary>
        /// Whether the rewards fit, played out on a copy of the bag.
        ///
        /// This was arithmetic once - count the slots the goods free, count the slots
        /// the rewards need, compare. It was wrong in both directions: twelve potatoes
        /// out of a sixty-four stack free a whole slot but divided to zero, and three
        /// rewards that would have shared a partial stack were counted as three slots.
        /// Simulating is exact, and it is only ever run on a button refresh.
        /// </summary>
        public static bool HasRoomForRewards(QuestDefinition quest, Inventory.Inventory bag)
        {
            if (quest == null || bag == null) return false;
            if (quest.rewards.Count == 0) return true;

            var scratch = new Inventory.Inventory(bag.Size);
            for (int i = 0; i < bag.Size; i++) scratch.SetSlotQuiet(i, bag[i]);

            // The goods go first, because the space they free is what the rewards land in.
            if (ConsumesGoods(quest.kind) && quest.objectiveItem != null)
            {
                scratch.Remove(quest.objectiveItem, quest.objectiveCount);
            }

            for (int i = 0; i < quest.rewards.Count; i++)
            {
                var reward = quest.rewards[i];
                if (reward.item == null || reward.count <= 0) continue;

                if (scratch.Add(reward.item, reward.count) > 0) return false;
            }

            return true;
        }

        public static string Describe(QuestResult result)
        {
            switch (result)
            {
                case QuestResult.Ok: return "Done";
                case QuestResult.AlreadyTaken: return "You already took that one";
                case QuestResult.AlreadyDone: return "That one is finished";
                case QuestResult.TooManyTaken: return "You are carrying too many contracts";
                case QuestResult.LevelTooLow: return "You are not experienced enough";
                case QuestResult.ReputationTooLow: return "They do not know you well enough";
                case QuestResult.NotFinished: return "Not finished yet";
                case QuestResult.GoodsMissing: return "You are not carrying the goods";
                case QuestResult.NoRoomForReward: return "No room in your bag for the reward";
                default: return "No such contract";
            }
        }

        /// <summary>The one line the log and the board show about a contract's state.</summary>
        public static string ProgressLine(QuestDefinition quest, QuestEntry entry,
                                          Inventory.Inventory bag, double nowHours)
        {
            if (quest == null) return "";

            int done = Progress(quest, entry, bag, nowHours);
            int want = Mathf.Max(1, quest.objectiveCount);

            switch (quest.kind)
            {
                case QuestKind.Clear:
                    return string.Format("{0} / {1} KILLED", Mathf.Min(done, want), want);
                case QuestKind.SurviveNights:
                    return string.Format("{0} / {1} NIGHTS", Mathf.Min(done, want), want);
                case QuestKind.DeliverLitres:
                    return string.Format("{0} / {1} L DELIVERED", Mathf.Min(done, want), want);
                default:
                    return string.Format("{0} / {1} {2}", Mathf.Min(done, want), want,
                        quest.objectiveItem != null ? quest.objectiveItem.displayName.ToUpperInvariant() : "");
            }
        }
    }
}
