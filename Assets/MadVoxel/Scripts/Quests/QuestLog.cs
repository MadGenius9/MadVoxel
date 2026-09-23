using System.Collections.Generic;
using UnityEngine;

namespace MadVoxel.Quests
{
    /// <summary>
    /// The contracts a player is carrying and the ones they have finished.
    ///
    /// Plain C# and owned by the player rig rather than a manager, for the same reason
    /// the perk tree is: it is the player's state, it has to survive a save, and every
    /// rule in it is worth testing without an engine underneath.
    ///
    /// It moves items and keeps the tallies. It does not grant XP or reputation - those
    /// belong to the systems that own them, and the caller applies them once this has
    /// said the hand-in is good.
    /// </summary>
    public class QuestLog
    {
        readonly List<QuestEntry> _active = new List<QuestEntry>();
        readonly HashSet<string> _completed = new HashSet<string>();

        /// <summary>Which trader issued each active contract, so reputation goes to the right one.</summary>
        readonly Dictionary<string, string> _issuedBy = new Dictionary<string, string>();

        public IReadOnlyList<QuestEntry> Active { get { return _active; } }
        public ICollection<string> Completed { get { return _completed; } }
        public int ActiveCount { get { return _active.Count; } }

        public bool IsActive(string questId)
        {
            return IndexOf(questId) >= 0;
        }

        public bool IsComplete(string questId)
        {
            return _completed.Contains(questId);
        }

        public string IssuerOf(string questId)
        {
            string trader;
            return _issuedBy.TryGetValue(questId, out trader) ? trader : "";
        }

        public int IndexOf(string questId)
        {
            for (int i = 0; i < _active.Count; i++)
            {
                if (_active[i].QuestId == questId) return i;
            }
            return -1;
        }

        public QuestEntry EntryOf(string questId)
        {
            int index = IndexOf(questId);
            return index >= 0 ? _active[index] : default(QuestEntry);
        }

        // --------------------------------------------------------------- accepting

        public QuestResult Accept(QuestDefinition quest, string traderId, int playerLevel,
                                  int reputationTier, double nowHours)
        {
            if (quest == null) return QuestResult.UnknownQuest;

            var check = QuestService.CanAccept(quest, playerLevel, reputationTier, _active.Count,
                                               IsActive(quest.stringId), IsComplete(quest.stringId));
            if (check != QuestResult.Ok) return check;

            _active.Add(new QuestEntry
            {
                QuestId = quest.stringId,
                AcceptedAtHours = nowHours,
                Counter = 0,
                TurnedIn = false
            });
            _issuedBy[quest.stringId] = traderId ?? "";

            return QuestResult.Ok;
        }

        /// <summary>
        /// Drops a contract. The tally goes with it, so re-taking a clear contract
        /// starts from zero rather than letting you bank kills against a slot you are
        /// not currently using.
        /// </summary>
        public bool Abandon(string questId)
        {
            int index = IndexOf(questId);
            if (index < 0) return false;

            _active.RemoveAt(index);
            _issuedBy.Remove(questId);
            return true;
        }

        // ----------------------------------------------------------------- turn-in

        /// <summary>
        /// Hands a contract in: takes the goods if it wants them, pays the item rewards,
        /// and files it as done. XP and reputation are the caller's to apply.
        /// </summary>
        public QuestResult TurnIn(QuestDefinition quest, Inventory.Inventory bag, double nowHours)
        {
            if (quest == null) return QuestResult.UnknownQuest;

            int index = IndexOf(quest.stringId);
            if (index < 0) return IsComplete(quest.stringId) ? QuestResult.AlreadyDone : QuestResult.UnknownQuest;

            var check = QuestService.CanTurnIn(quest, _active[index], bag, nowHours);
            if (check != QuestResult.Ok) return check;

            // Goods first, then rewards: the space the goods free is what the rewards
            // are counted against, and doing it the other way round can fail on a bag
            // that is about to have room.
            if (QuestService.ConsumesGoods(quest.kind) && quest.objectiveItem != null)
            {
                int removed = bag.Remove(quest.objectiveItem, quest.objectiveCount);
                if (removed < quest.objectiveCount)
                {
                    bag.Add(quest.objectiveItem, removed);
                    return QuestResult.GoodsMissing;
                }
            }

            for (int i = 0; i < quest.rewards.Count; i++)
            {
                var reward = quest.rewards[i];
                if (reward.item == null || reward.count <= 0) continue;

                bag.Add(reward.item, reward.count);
            }

            _active.RemoveAt(index);
            _issuedBy.Remove(quest.stringId);
            _completed.Add(quest.stringId);

            return QuestResult.Ok;
        }

        // ---------------------------------------------------------------- tallying

        /// <summary>
        /// A kill, offered to every clear contract in the log. A contract with no
        /// zombie named takes anything; one that names a type only counts that type.
        /// </summary>
        public int ReportKill(System.Func<string, QuestDefinition> lookup, string zombieId)
        {
            return Tally(lookup, QuestKind.Clear, quest =>
                string.IsNullOrEmpty(quest.objectiveZombieId) || quest.objectiveZombieId == zombieId, 1);
        }

        /// <summary>Litres of a crop sold or delivered, offered to every bulk contract.</summary>
        public int ReportLitres(System.Func<string, QuestDefinition> lookup, string cropId, int litres)
        {
            if (litres <= 0) return 0;

            return Tally(lookup, QuestKind.DeliverLitres, quest =>
                string.IsNullOrEmpty(quest.objectiveCropId) || quest.objectiveCropId == cropId, litres);
        }

        int Tally(System.Func<string, QuestDefinition> lookup, QuestKind kind,
                  System.Func<QuestDefinition, bool> matches, int amount)
        {
            if (lookup == null || amount <= 0) return 0;

            int touched = 0;
            for (int i = 0; i < _active.Count; i++)
            {
                var entry = _active[i];

                var quest = lookup(entry.QuestId);
                if (quest == null || quest.kind != kind || !matches(quest)) continue;

                // Capped at the objective so a long-running contract does not bank a
                // number far past what it asked for.
                entry.Counter = Mathf.Min(entry.Counter + amount, Mathf.Max(1, quest.objectiveCount));
                _active[i] = entry;
                touched++;
            }
            return touched;
        }

        // ------------------------------------------------------------------ saving

        public void LoadState(IEnumerable<QuestEntry> active, IEnumerable<string> completed,
                              IEnumerable<KeyValuePair<string, string>> issuers)
        {
            _active.Clear();
            _completed.Clear();
            _issuedBy.Clear();

            if (active != null)
            {
                foreach (var entry in active)
                {
                    if (string.IsNullOrEmpty(entry.QuestId)) continue;
                    if (_active.Count >= QuestService.MaxActive) break;
                    _active.Add(entry);
                }
            }

            if (completed != null)
            {
                foreach (var id in completed)
                {
                    if (!string.IsNullOrEmpty(id)) _completed.Add(id);
                }
            }

            if (issuers != null)
            {
                foreach (var pair in issuers)
                {
                    if (!string.IsNullOrEmpty(pair.Key)) _issuedBy[pair.Key] = pair.Value ?? "";
                }
            }
        }

        public void Clear()
        {
            _active.Clear();
            _completed.Clear();
            _issuedBy.Clear();
        }
    }
}
