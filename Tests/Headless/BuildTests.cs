using System.Collections.Generic;
using MadVoxel.Building;
using MadVoxel.Content;
using MadVoxel.Inventory;
using UnityEngine;

namespace MadVoxel.Headless
{
    /// <summary>
    /// The snap grid and the snap content. Stability itself needs colliders and a scene,
    /// but the address algebra it rests on does not - and if that algebra is wrong the
    /// flood fill silently drops half a base.
    /// </summary>
    public static class BuildTests
    {
        public static void Run(ContentDatabase db)
        {
            GridMaths();
            EdgeSharing();
            ConnectionSymmetry();
            SnapContent(db);
        }

        static void GridMaths()
        {
            Harness.Section("build grid: cell maths");

            Harness.Equal(BuildGrid.CellIndex(0f), 0, "x=0 is cell 0");
            Harness.Equal(BuildGrid.CellIndex(2.9f), 0, "x=2.9 is still cell 0");
            Harness.Equal(BuildGrid.CellIndex(3f), 1, "x=3 starts cell 1");
            Harness.Equal(BuildGrid.CellIndex(-0.1f), -1, "x=-0.1 is cell -1, not 0");
            Harness.Equal(BuildGrid.CellIndex(-3f), -1, "x=-3 is cell -1");
            Harness.Equal(BuildGrid.CellIndex(-3.1f), -2, "x=-3.1 is cell -2");

            var origin = BuildGrid.CellOrigin(2, 40, -1);
            Harness.Check(Mathf.Abs(origin.x - 6f) < 0.001f && Mathf.Abs(origin.z + 3f) < 0.001f,
                "cell origin is the minimum corner");
            Harness.Check(Mathf.Abs(origin.y - 40f) < 0.001f, "cell origin keeps the level in metres");

            Harness.Equal(BuildGrid.OppositeSide(BuildGrid.SideNorth), BuildGrid.SideSouth, "north opposes south");
            Harness.Equal(BuildGrid.OppositeSide(BuildGrid.SideEast), BuildGrid.SideWest, "east opposes west");

            // Nearest edge: sample points just inside each side of cell (0,0).
            Harness.Equal(BuildGrid.NearestSide(0, 0, new Vector3(1.5f, 0f, 0.2f)), BuildGrid.SideNorth, "a point near -Z picks north");
            Harness.Equal(BuildGrid.NearestSide(0, 0, new Vector3(2.8f, 0f, 1.5f)), BuildGrid.SideEast, "a point near +X picks east");
            Harness.Equal(BuildGrid.NearestSide(0, 0, new Vector3(1.5f, 0f, 2.8f)), BuildGrid.SideSouth, "a point near +Z picks south");
            Harness.Equal(BuildGrid.NearestSide(0, 0, new Vector3(0.2f, 0f, 1.5f)), BuildGrid.SideWest, "a point near -X picks west");
        }

        static void EdgeSharing()
        {
            Harness.Section("build grid: shared edges");

            // The wall between cell (0,0) and (1,0) must be one slot, however it is named.
            var fromLeft = new BuildAddress(0, 30, 0, BuildSlot.Wall, BuildGrid.SideEast);
            var fromRight = new BuildAddress(1, 30, 0, BuildSlot.Wall, BuildGrid.SideWest);
            Harness.Check(fromLeft.Equals(fromRight), "east of a cell is the same slot as west of its neighbour");
            Harness.Equal(fromLeft.GetHashCode(), fromRight.GetHashCode(), "and hashes identically");

            var fromNear = new BuildAddress(0, 30, 0, BuildSlot.Wall, BuildGrid.SideSouth);
            var fromFar = new BuildAddress(0, 30, 1, BuildSlot.Wall, BuildGrid.SideNorth);
            Harness.Check(fromNear.Equals(fromFar), "south of a cell is the same slot as north of its neighbour");

            // Different levels and different slots must stay distinct.
            Harness.Check(!fromLeft.Equals(fromLeft.AtLevel(33)), "the same edge on another storey is a different slot");
            Harness.Check(!new BuildAddress(0, 30, 0, BuildSlot.Floor).Equals(new BuildAddress(0, 30, 0, BuildSlot.Ceiling)),
                "floor and ceiling of a cell are different slots");

            // Canonical form only ever uses north and west.
            bool canonical = true;
            for (int side = 0; side < 4; side++)
            {
                var a = new BuildAddress(4, 12, -3, BuildSlot.Wall, side);
                if (a.Side != BuildGrid.SideNorth && a.Side != BuildGrid.SideWest) canonical = false;
            }
            Harness.Check(canonical, "canonical wall addresses only use the north and west sides");

            Vector2Int cellA, cellB;
            fromLeft.EdgeCells(out cellA, out cellB);
            bool joinsBoth = (cellA == new Vector2Int(1, 0) && cellB == new Vector2Int(0, 0))
                          || (cellA == new Vector2Int(0, 0) && cellB == new Vector2Int(1, 0));
            Harness.Check(joinsBoth, "an edge slot reports both cells it sits between");
        }

        static void ConnectionSymmetry()
        {
            Harness.Section("build grid: connection symmetry");

            // The stability flood fill walks connections in both directions, so if A
            // lists B but B does not list A, whole wings of a base would collapse.
            var samples = new List<BuildAddress>
            {
                new BuildAddress(0, 30, 0, BuildSlot.Floor),
                new BuildAddress(0, 33, 0, BuildSlot.Floor),
                new BuildAddress(0, 30, 0, BuildSlot.Wall, BuildGrid.SideNorth),
                new BuildAddress(0, 30, 0, BuildSlot.Wall, BuildGrid.SideWest),
                new BuildAddress(0, 30, 0, BuildSlot.Wall, BuildGrid.SideEast),
                new BuildAddress(0, 30, 0, BuildSlot.Ceiling),
                new BuildAddress(0, 30, 0, BuildSlot.Interior),
                new BuildAddress(0, 30, 0, BuildSlot.Attachment, BuildGrid.SideNorth),
                new BuildAddress(-2, 27, 5, BuildSlot.Floor),
            };

            var forward = new List<BuildAddress>();
            var back = new List<BuildAddress>();
            var asymmetric = new List<string>();

            for (int i = 0; i < samples.Count; i++)
            {
                forward.Clear();
                BuildGrid.EnumerateConnections(samples[i], forward);

                for (int j = 0; j < forward.Count; j++)
                {
                    back.Clear();
                    BuildGrid.EnumerateConnections(forward[j], back);
                    if (!back.Contains(samples[i]))
                    {
                        asymmetric.Add(samples[i] + " -> " + forward[j]);
                    }
                }
            }
            Harness.Check(asymmetric.Count == 0,
                "every connection is mutual" + (asymmetric.Count > 0 ? ": " + string.Join("; ", asymmetric) : ""));

            // A floor must reach the walls under it, or an upper storey never grounds out.
            forward.Clear();
            BuildGrid.EnumerateConnections(new BuildAddress(0, 33, 0, BuildSlot.Floor), forward);
            bool reachesWallBelow = forward.Contains(new BuildAddress(0, 30, 0, BuildSlot.Wall, BuildGrid.SideNorth));
            Harness.Check(reachesWallBelow, "an upper floor connects to the walls holding it up");

            // And a wall must reach the floor it stands on.
            forward.Clear();
            BuildGrid.EnumerateConnections(new BuildAddress(0, 30, 0, BuildSlot.Wall, BuildGrid.SideNorth), forward);
            Harness.Check(forward.Contains(new BuildAddress(0, 30, 0, BuildSlot.Floor)), "a wall connects to the floor it stands on");
            Harness.Check(forward.Contains(new BuildAddress(0, 30, -1, BuildSlot.Floor)), "and to the floor on its other side");

            // No slot may list itself, which would make the fill spin.
            bool selfFree = true;
            for (int i = 0; i < samples.Count; i++)
            {
                forward.Clear();
                BuildGrid.EnumerateConnections(samples[i], forward);
                if (forward.Contains(samples[i])) selfFree = false;
            }
            Harness.Check(selfFree, "no slot connects to itself");
        }

        static void SnapContent(ContentDatabase db)
        {
            Harness.Section("build content: the snap set");

            Harness.Check(db.buildPieces.Count > 0, string.Format("{0} snap piece definitions", db.buildPieces.Count));

            // Every kind must have a complete Twig..Armored chain.
            var chains = new Dictionary<BuildPieceKind, List<BuildPieceDefinition>>();
            for (int i = 0; i < db.buildPieces.Count; i++)
            {
                var def = db.buildPieces[i];
                List<BuildPieceDefinition> list;
                if (!chains.TryGetValue(def.kind, out list))
                {
                    list = new List<BuildPieceDefinition>();
                    chains[def.kind] = list;
                }
                list.Add(def);
            }

            var brokenChains = new List<string>();
            foreach (var kv in chains)
            {
                if (kv.Value.Count != 5) { brokenChains.Add(kv.Key + " has " + kv.Value.Count + " tiers"); continue; }

                // Walk the chain from Twig and confirm it climbs one tier at a time.
                BuildPieceDefinition current = null;
                for (int i = 0; i < kv.Value.Count; i++) if (kv.Value[i].tier == BuildTier.Twig) current = kv.Value[i];
                if (current == null) { brokenChains.Add(kv.Key + " has no twig tier"); continue; }

                int steps = 0;
                var expected = new[] { BuildTier.Twig, BuildTier.Wood, BuildTier.Stone, BuildTier.Metal, BuildTier.Armored };
                while (current != null)
                {
                    if (current.tier != expected[steps]) brokenChains.Add(kv.Key + " tier order breaks at " + current.tier);
                    if (current.kind != kv.Key) brokenChains.Add(kv.Key + " upgrades into a different kind");
                    steps++;
                    if (steps >= expected.Length) break;
                    current = current.upgradesTo;
                }
                if (steps != 5) brokenChains.Add(kv.Key + " chain is " + steps + " long");
            }
            Harness.Check(brokenChains.Count == 0,
                string.Format("all {0} kinds have a full Twig..Armored chain", chains.Count)
                + (brokenChains.Count > 0 ? ": " + string.Join("; ", brokenChains) : ""));

            // Health and resistance must actually climb, or upgrading is cosmetic.
            var notClimbing = new List<string>();
            foreach (var kv in chains)
            {
                var ordered = new List<BuildPieceDefinition>();
                BuildPieceDefinition cursor = null;
                for (int i = 0; i < kv.Value.Count; i++) if (kv.Value[i].tier == BuildTier.Twig) cursor = kv.Value[i];
                while (cursor != null) { ordered.Add(cursor); cursor = cursor.upgradesTo; }

                for (int i = 1; i < ordered.Count; i++)
                {
                    if (ordered[i].maxHealth <= ordered[i - 1].maxHealth) notClimbing.Add(kv.Key + " health at " + ordered[i].tier);
                    if (ordered[i].damageResistance <= ordered[i - 1].damageResistance) notClimbing.Add(kv.Key + " resistance at " + ordered[i].tier);
                }
            }
            Harness.Check(notClimbing.Count == 0, "each tier is tougher than the last"
                + (notClimbing.Count > 0 ? ": " + string.Join("; ", notClimbing) : ""));

            // Upgrades above Twig must cost something real.
            var freeUpgrades = new List<string>();
            for (int i = 0; i < db.buildPieces.Count; i++)
            {
                var def = db.buildPieces[i];
                if (def.tier == BuildTier.Twig) continue;
                if (def.upgradeCost.Count == 0) { freeUpgrades.Add(def.stringId); continue; }
                for (int j = 0; j < def.upgradeCost.Count; j++)
                {
                    var cost = def.upgradeCost[j];
                    if (cost.item == null || cost.count < 1) freeUpgrades.Add(def.stringId);
                    else if (db.Item(cost.item.stringId) == null) freeUpgrades.Add(def.stringId + " (unknown material)");
                }
            }
            Harness.Check(freeUpgrades.Count == 0, "every upgrade costs real materials"
                + (freeUpgrades.Count > 0 ? ": " + string.Join("; ", freeUpgrades) : ""));

            // Perk gates must name perks that exist and ranks that are reachable.
            var badGates = new List<string>();
            for (int i = 0; i < db.buildPieces.Count; i++)
            {
                var def = db.buildPieces[i];
                if (string.IsNullOrEmpty(def.requiredPerkId)) continue;

                var perk = db.perkTree.Find(def.requiredPerkId);
                if (perk == null) badGates.Add(def.stringId + " -> unknown perk " + def.requiredPerkId);
                else if (def.requiredPerkRank > perk.maxRank) badGates.Add(def.stringId + " needs rank beyond the perk's max");
            }
            Harness.Check(badGates.Count == 0, "tier perk gates are reachable"
                + (badGates.Count > 0 ? ": " + string.Join("; ", badGates) : ""));

            // Wood and Stone must stay ungated: the Phase 0 success test upgrades a wall
            // to stone, and it has to be walkable before the first perk point lands.
            var earlyGated = new List<string>();
            for (int i = 0; i < db.buildPieces.Count; i++)
            {
                var def = db.buildPieces[i];
                if (def.tier != BuildTier.Wood && def.tier != BuildTier.Stone) continue;
                if (!string.IsNullOrEmpty(def.requiredPerkId)) earlyGated.Add(def.stringId);
            }
            Harness.Check(earlyGated.Count == 0, "wood and stone tiers need no perk"
                + (earlyGated.Count > 0 ? ": " + string.Join(", ", earlyGated) : ""));

            // Only Twig is placeable from an item; everything else is hammer-only.
            var placeableTiers = new List<string>();
            int placeableKinds = 0;
            for (int i = 0; i < db.items.Count; i++)
            {
                var item = db.items[i];
                if (item.placeableBuildPiece == null) continue;
                placeableKinds++;
                if (item.placeableBuildPiece.tier != BuildTier.Twig) placeableTiers.Add(item.stringId);
            }
            Harness.Equal(placeableKinds, chains.Count, "one placeable item per piece kind");
            Harness.Check(placeableTiers.Count == 0, "placeable items only ever place the twig tier"
                + (placeableTiers.Count > 0 ? ": " + string.Join(", ", placeableTiers) : ""));

            // Slot rules the solver relies on.
            var slotProblems = new List<string>();
            for (int i = 0; i < db.buildPieces.Count; i++)
            {
                var def = db.buildPieces[i];
                if (def.requiresHost && def.slot != BuildSlot.Attachment)
                    slotProblems.Add(def.stringId + " mounts on a host but is not an attachment");
                if (def.restsOnTerrain && def.slot != BuildSlot.Floor)
                    slotProblems.Add(def.stringId + " rests on terrain but is not a floor slot");
                if (def.maxHealth <= 0f) slotProblems.Add(def.stringId + " has no health");
            }
            Harness.Check(slotProblems.Count == 0, "slot and mounting flags are consistent"
                + (slotProblems.Count > 0 ? ": " + string.Join("; ", slotProblems) : ""));

            // Exactly one kind may rest on terrain, or everything floats.
            int terrainResting = 0;
            for (int i = 0; i < db.buildPieces.Count; i++) if (db.buildPieces[i].restsOnTerrain) terrainResting++;
            Harness.Equal(terrainResting, 5, "only the foundation chain rests on terrain");

            // The hammer has to exist, or nothing can be upgraded at all.
            var hammer = db.Item(ItemIds.Hammer);
            Harness.Check(hammer != null && hammer.toolType == ToolType.Hammer, "a building hammer exists");

            bool hammerByHand = false;
            for (int i = 0; i < db.recipes.Count; i++)
            {
                var r = db.recipes[i];
                if (r.output == hammer && r.station == CraftStation.Hand && r.unlockedByDefault) hammerByHand = true;
            }
            Harness.Check(hammerByHand, "the hammer is craftable by hand (the build loop is reachable)");
        }
    }
}
