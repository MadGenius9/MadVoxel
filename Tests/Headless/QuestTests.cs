using System.Collections.Generic;
using MadVoxel.Content;
using MadVoxel.Inventory;
using MadVoxel.Quests;
using UnityEngine;
using Inv = MadVoxel.Inventory.Inventory;

namespace MadVoxel.Headless
{
    /// <summary>
    /// Contracts. The interesting part is which half of a contract's progress is
    /// derived and which is tallied, and that handing one in can never cost you the
    /// goods without paying for them.
    /// </summary>
    public static class QuestTests
    {
        public static void Run(ContentDatabase db)
        {
            if (db == null) return;

            Accepting(db);
            DerivedProgress(db);
            CountedProgress(db);
            TurningIn(db);
            Content(db);
        }

        static QuestDefinition Quest(ContentDatabase db, QuestKind kind)
        {
            for (int i = 0; i < db.quests.Count; i++)
            {
                if (db.quests[i].kind == kind) return db.quests[i];
            }
            return null;
        }

        static System.Func<string, QuestDefinition> Lookup(ContentDatabase db)
        {
            return id =>
            {
                for (int i = 0; i < db.quests.Count; i++)
                {
                    if (db.quests[i].stringId == id) return db.quests[i];
                }
                return null;
            };
        }

        // --------------------------------------------------------------- accepting

        static void Accepting(ContentDatabase db)
        {
            Harness.Section("quests: taking a contract");

            var log = new QuestLog();
            var fetch = Quest(db, QuestKind.Fetch);
            Harness.Check(fetch != null, "there is a fetch contract");
            if (fetch == null) return;

            Harness.Equal((int)log.Accept(fetch, "t", 99, 9, 0.0), (int)QuestResult.Ok, "a qualified player takes it");
            Harness.Equal(log.ActiveCount, 1, "and it is in the log");
            Harness.Equal(log.IssuerOf(fetch.stringId), "t", "with the trader who issued it recorded");

            Harness.Equal((int)log.Accept(fetch, "t", 99, 9, 0.0), (int)QuestResult.AlreadyTaken,
                "taking the same one twice is refused");
            Harness.Equal(log.ActiveCount, 1, "and does not double up in the log");

            // Gates.
            var gated = (QuestDefinition)null;
            for (int i = 0; i < db.quests.Count; i++)
            {
                if (db.quests[i].requiredPlayerLevel > 1) gated = db.quests[i];
            }
            if (gated != null)
            {
                var fresh = new QuestLog();
                Harness.Equal((int)fresh.Accept(gated, "t", 1, 9, 0.0), (int)QuestResult.LevelTooLow,
                    "a contract above your level is refused");
                Harness.Equal(fresh.ActiveCount, 0, "and nothing is logged");
            }

            var repGated = (QuestDefinition)null;
            for (int i = 0; i < db.quests.Count; i++)
            {
                if (db.quests[i].requiredReputationTier > 0) repGated = db.quests[i];
            }
            if (repGated != null)
            {
                var fresh = new QuestLog();
                Harness.Equal((int)fresh.Accept(repGated, "t", 99, 0, 0.0), (int)QuestResult.ReputationTooLow,
                    "and so is one from a trader who barely knows you");
            }

            // The carry limit.
            var full = new QuestLog();
            int taken = 0;
            for (int i = 0; i < db.quests.Count; i++)
            {
                if (full.Accept(db.quests[i], "t", 99, 9, 0.0) == QuestResult.Ok) taken++;
            }
            Harness.Equal(taken, QuestService.MaxActive,
                string.Format("you can carry {0} contracts at once, no more", QuestService.MaxActive));
            Harness.Equal(full.ActiveCount, QuestService.MaxActive, "and the log agrees");

            // Abandoning frees a slot and forgets the tally.
            Harness.Check(full.Abandon(full.Active[0].QuestId), "a contract can be dropped");
            Harness.Equal(full.ActiveCount, QuestService.MaxActive - 1, "which frees the slot");
            Harness.Check(!full.Abandon("madvoxel:quest_nonexistent"), "dropping one you never had does nothing");
        }

        // ----------------------------------------------------------------- derived

        static void DerivedProgress(ContentDatabase db)
        {
            Harness.Section("quests: progress that is recomputed");

            var fetch = Quest(db, QuestKind.Fetch);
            var log = new QuestLog();
            log.Accept(fetch, "t", 99, 9, 0.0);

            var bag = new Inv(36);
            var entry = log.EntryOf(fetch.stringId);

            Harness.Equal(QuestService.Progress(fetch, entry, bag, 0.0), 0, "an empty bag is no progress");

            bag.Add(fetch.objectiveItem, fetch.objectiveCount);
            Harness.Equal(QuestService.Progress(fetch, entry, bag, 0.0), fetch.objectiveCount,
                "the bag is the progress - nothing is stored");
            Harness.Check(QuestService.IsComplete(fetch, entry, bag, 0.0), "so the contract reads complete");

            // THE point of deriving it: spending the goods un-completes the contract,
            // where a stored counter would have said "done" over an empty bag.
            bag.Remove(fetch.objectiveItem, fetch.objectiveCount);
            Harness.Check(!QuestService.IsComplete(fetch, entry, bag, 0.0),
                "and spending them takes the progress back with them");

            // Nights, derived from the hour it was taken.
            var survive = Quest(db, QuestKind.SurviveNights);
            if (survive != null)
            {
                var night = new QuestEntry { QuestId = survive.stringId, AcceptedAtHours = 18.0 };

                Harness.Equal(QuestService.NightsSince(18.0, 18.0), 0, "no time is no nights");
                Harness.Equal(QuestService.NightsSince(18.0, 23.9), 0, "and neither is dusk to midnight");
                Harness.Equal(QuestService.NightsSince(18.0, 24.1), 1,
                    "taking one at dusk and living to morning is one night");
                Harness.Equal(QuestService.NightsSince(18.0, 72.0), 3, "three day boundaries is three nights");
                Harness.Equal(QuestService.NightsSince(18.0, 5.0), 0, "and a clock that went backwards is none");

                // Taken at dawn, you must actually live through the night.
                Harness.Equal(QuestService.NightsSince(6.0, 20.0), 0,
                    "a contract taken at dawn credits nothing by dusk");
                Harness.Equal(QuestService.NightsSince(6.0, 30.0), 1, "and one the next morning");

                Harness.Check(!QuestService.IsComplete(survive, night, null, 30.0),
                    "one night does not finish a two-night contract");
                Harness.Check(QuestService.IsComplete(survive, night, null, 24.0 * 3.0),
                    "two does");
            }
        }

        // ----------------------------------------------------------------- counted

        static void CountedProgress(ContentDatabase db)
        {
            Harness.Section("quests: progress that is tallied");

            var clear = Quest(db, QuestKind.Clear);
            var bulk = Quest(db, QuestKind.DeliverLitres);
            var lookup = Lookup(db);

            if (clear != null)
            {
                var log = new QuestLog();
                log.Accept(clear, "t", 99, 9, 0.0);

                Harness.Equal(log.ReportKill(lookup, ZombieIds.Shambler), 1, "a kill reaches the contract");
                Harness.Equal(QuestService.Progress(clear, log.EntryOf(clear.stringId), null, 0.0), 1,
                    "and is tallied");

                for (int i = 0; i < clear.objectiveCount * 3; i++) log.ReportKill(lookup, ZombieIds.Shambler);

                Harness.Equal(QuestService.Progress(clear, log.EntryOf(clear.stringId), null, 0.0),
                    clear.objectiveCount,
                    "the tally caps at what was asked for rather than banking past it");

                // A contract that names a type must not count everything.
                var picky = ScriptableObject.CreateInstance<QuestDefinition>();
                picky.stringId = "test:quest_picky";
                picky.kind = QuestKind.Clear;
                picky.objectiveCount = 5;
                picky.objectiveZombieId = ZombieIds.Brute;

                var pickyLog = new QuestLog();
                pickyLog.Accept(picky, "t", 99, 9, 0.0);

                System.Func<string, QuestDefinition> pickyLookup = id => id == picky.stringId ? picky : null;

                pickyLog.ReportKill(pickyLookup, ZombieIds.Shambler);
                Harness.Equal(QuestService.Progress(picky, pickyLog.EntryOf(picky.stringId), null, 0.0), 0,
                    "a contract that names a zombie ignores the others");

                pickyLog.ReportKill(pickyLookup, ZombieIds.Brute);
                Harness.Equal(QuestService.Progress(picky, pickyLog.EntryOf(picky.stringId), null, 0.0), 1,
                    "and counts the one it asked for");

                // Dropping and retaking must not bank kills.
                pickyLog.Abandon(picky.stringId);
                pickyLog.Accept(picky, "t", 99, 9, 0.0);
                Harness.Equal(QuestService.Progress(picky, pickyLog.EntryOf(picky.stringId), null, 0.0), 0,
                    "dropping a contract drops its tally with it");
            }

            if (bulk != null)
            {
                var log = new QuestLog();
                log.Accept(bulk, "t", 99, 9, 0.0);

                Harness.Equal(log.ReportLitres(lookup, bulk.objectiveCropId, 500), 1, "litres reach the contract");
                Harness.Equal(QuestService.Progress(bulk, log.EntryOf(bulk.stringId), null, 0.0), 500,
                    "and are tallied by the litre");

                Harness.Equal(log.ReportLitres(lookup, "madvoxel:crop_potato", 500), 0,
                    "the wrong crop does not count");
                Harness.Equal(QuestService.Progress(bulk, log.EntryOf(bulk.stringId), null, 0.0), 500,
                    "and leaves the tally where it was");

                Harness.Equal(log.ReportLitres(lookup, bulk.objectiveCropId, 0), 0, "nothing delivered is nothing counted");
            }
        }

        // ---------------------------------------------------------------- turn-in

        static void TurningIn(ContentDatabase db)
        {
            Harness.Section("quests: handing one in");

            var fetch = Quest(db, QuestKind.Fetch);
            var token = db.Item(ItemIds.TradeToken);

            var log = new QuestLog();
            log.Accept(fetch, "t", 99, 9, 0.0);

            var bag = new Inv(36);
            Harness.Equal((int)log.TurnIn(fetch, bag, 0.0), (int)QuestResult.NotFinished,
                "an unfinished contract is refused");
            Harness.Equal(log.ActiveCount, 1, "and stays in the log");

            bag.Add(fetch.objectiveItem, fetch.objectiveCount);
            Harness.Equal((int)log.TurnIn(fetch, bag, 0.0), (int)QuestResult.Ok, "a finished one goes through");

            Harness.Equal(bag.CountOf(fetch.objectiveItem), 0, "the goods are handed over");
            Harness.Check(bag.CountOf(token) > 0, "and the reward arrives");
            Harness.Equal(log.ActiveCount, 0, "the contract leaves the log");
            Harness.Check(log.IsComplete(fetch.stringId), "and is filed as done");

            Harness.Equal((int)log.Accept(fetch, "t", 99, 9, 0.0), (int)QuestResult.AlreadyDone,
                "a finished contract cannot be taken again");
            Harness.Equal((int)log.TurnIn(fetch, bag, 0.0), (int)QuestResult.AlreadyDone,
                "nor handed in twice");

            // THE one that would cost a player their afternoon: goods taken, reward
            // refused. Every slot full of something that does not stack with anything.
            var stuffed = new Inv(36);
            var brick = db.Item(ItemIds.EngineBlock);
            for (int i = 0; i < stuffed.Size; i++) stuffed.SetSlot(i, new ItemStack(brick, brick.maxStack));

            var clear = Quest(db, QuestKind.Clear);
            if (clear != null && clear.rewards.Count > 0)
            {
                var tallyLog = new QuestLog();
                tallyLog.Accept(clear, "t", 99, 9, 0.0);

                var lookup = Lookup(db);
                for (int i = 0; i < clear.objectiveCount; i++) tallyLog.ReportKill(lookup, ZombieIds.Shambler);

                Harness.Check(QuestService.IsComplete(clear, tallyLog.EntryOf(clear.stringId), stuffed, 0.0),
                    "the kill contract is finished");

                int bricksBefore = stuffed.CountOf(brick);
                Harness.Equal((int)tallyLog.TurnIn(clear, stuffed, 0.0), (int)QuestResult.NoRoomForReward,
                    "a full bag refuses the hand-in rather than dropping the reward");
                Harness.Equal(stuffed.CountOf(brick), bricksBefore, "and nothing in the bag is disturbed");
                Harness.Equal(tallyLog.ActiveCount, 1, "the contract stays live so you can come back");
            }

            // A fetch contract's own goods free the space its reward needs. Counting
            // the rewards against the bag as it stands - goods still in it - would make
            // a contract like this impossible to hand in at all.
            var swap = ScriptableObject.CreateInstance<QuestDefinition>();
            swap.stringId = "test:quest_swap";
            swap.kind = QuestKind.Fetch;
            swap.objectiveItem = fetch.objectiveItem;
            swap.objectiveCount = 12;
            swap.xpReward = 1f;
            swap.rewards.Add(new QuestReward { item = token, count = 100 });

            // Two slots: the goods in one, junk in the other. The payment only fits
            // once the goods have gone.
            var tight = new Inv(2);
            tight.SetSlot(0, new ItemStack(swap.objectiveItem, 12));
            tight.SetSlot(1, new ItemStack(brick, brick.maxStack));

            Harness.Check(!tight.CanFit(token, 100), "the bag has no room for the payment as it stands");

            var tightLog = new QuestLog();
            tightLog.Accept(swap, "t", 99, 9, 0.0);

            Harness.Equal((int)tightLog.TurnIn(swap, tight, 0.0), (int)QuestResult.Ok,
                "but the goods you hand over make room for what you are paid");
            Harness.Equal(tight.CountOf(token), 100, "and the payment arrives");
            Harness.Equal(tight.CountOf(swap.objectiveItem), 0, "with the goods gone");
        }

        // ---------------------------------------------------------------- content

        static void Content(ContentDatabase db)
        {
            Harness.Section("quests: the contracts as shipped");

            var problems = new List<string>();
            var kinds = new HashSet<QuestKind>();

            for (int i = 0; i < db.quests.Count; i++)
            {
                var quest = db.quests[i];
                kinds.Add(quest.kind);

                if (quest.objectiveCount <= 0) problems.Add(quest.stringId + " asks for nothing");
                if (quest.xpReward <= 0f) problems.Add(quest.stringId + " pays no xp");

                if (QuestService.ConsumesGoods(quest.kind) && quest.objectiveItem == null)
                    problems.Add(quest.stringId + " takes goods but names none");

                if (quest.kind == QuestKind.DeliverLitres && string.IsNullOrEmpty(quest.objectiveCropId))
                    problems.Add(quest.stringId + " delivers litres of nothing");

                for (int r = 0; r < quest.rewards.Count; r++)
                {
                    if (quest.rewards[r].item == null) problems.Add(quest.stringId + " has an empty reward");
                    if (quest.rewards[r].count <= 0) problems.Add(quest.stringId + " rewards zero of something");
                }
            }

            Harness.Check(problems.Count == 0,
                "every contract asks for something real and pays for it"
                + (problems.Count > 0 ? ": " + string.Join(", ", problems) : ""));

            Harness.Check(kinds.Count >= 3,
                string.Format("the board covers {0} kinds of work", kinds.Count));

            // A contract nobody hands out is a contract nobody can take.
            var offered = new HashSet<string>();
            for (int t = 0; t < db.traders.Count; t++)
            {
                for (int q = 0; q < db.traders[t].questBoard.Count; q++)
                {
                    if (db.traders[t].questBoard[q] != null) offered.Add(db.traders[t].questBoard[q].stringId);
                }
            }

            var orphans = new List<string>();
            for (int i = 0; i < db.quests.Count; i++)
            {
                if (!offered.Contains(db.quests[i].stringId)) orphans.Add(db.quests[i].stringId);
            }
            Harness.Check(orphans.Count == 0,
                "every contract is on some trader's board"
                + (orphans.Count > 0 ? ": " + string.Join(", ", orphans) : ""));

            // And a contract whose litres a trader will not buy can never be delivered.
            for (int i = 0; i < db.quests.Count; i++)
            {
                var quest = db.quests[i];
                if (quest.kind != QuestKind.DeliverLitres) continue;

                bool sowable = false;
                for (int c = 0; c < db.crops.Count; c++)
                {
                    if (db.crops[c].stringId == quest.objectiveCropId && db.crops[c].growsOnField) sowable = true;
                }
                Harness.Check(sowable, quest.title + " asks for a crop you can actually sow on a field");
            }
        }
    }
}
