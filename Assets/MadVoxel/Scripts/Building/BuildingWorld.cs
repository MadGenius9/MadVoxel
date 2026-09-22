using System;
using System.Collections.Generic;
using MadVoxel.Core;
using MadVoxel.Inventory;
using MadVoxel.World.Terrain;
using UnityEngine;

namespace MadVoxel.Building
{
    /// <summary>
    /// Owns every placed snap piece, the slot index, and stability.
    ///
    /// Stability is a reachability test rather than a force simulation: a piece stands
    /// if there is a chain of connected pieces from it down to a foundation that is
    /// itself resting on solid terrain. That is enough to forbid floating towers and to
    /// make "mine out the ground under their base" collapse it, without pretending to
    /// be structural engineering.
    /// </summary>
    public class BuildingWorld : MonoBehaviour
    {
        const float StabilityInterval = 0.25f;
        const int FoundationSupportColumns = 4; // of the nine 1 m columns under a 3 m cell

        readonly Dictionary<BuildAddress, BuildPiece> _pieces = new Dictionary<BuildAddress, BuildPiece>();
        readonly List<BuildPiece> _all = new List<BuildPiece>();
        readonly HashSet<BuildAddress> _reachable = new HashSet<BuildAddress>();
        readonly Queue<BuildAddress> _frontier = new Queue<BuildAddress>();
        readonly List<BuildAddress> _connectionScratch = new List<BuildAddress>(16);

        TerrainWorld _terrain;
        Transform _root;
        bool _stabilityDirty;
        float _stabilityTimer;

        public event Action<BuildPiece> Placed;
        public event Action<BuildPiece> Removed;

        public IReadOnlyList<BuildPiece> All { get { return _all; } }
        public int Count { get { return _all.Count; } }

        public void Init(TerrainWorld terrain)
        {
            _terrain = terrain;

            var rootGo = new GameObject("Buildings");
            rootGo.transform.SetParent(transform, false);
            _root = rootGo.transform;

            _terrain.BlockChanged += OnBlockChanged;
        }

        void OnDestroy()
        {
            if (_terrain != null) _terrain.BlockChanged -= OnBlockChanged;
        }

        void OnBlockChanged(Vector3Int cell, ushort oldId, ushort newId)
        {
            // Digging under a base is how you bring it down, so any terrain edit that
            // removes a block has to re-test the whole structure.
            if (_terrain.Registry.IsAir(newId)) _stabilityDirty = true;
        }

        void LateUpdate()
        {
            if (!_stabilityDirty) return;

            _stabilityTimer += Time.deltaTime;
            if (_stabilityTimer < StabilityInterval) return;

            _stabilityTimer = 0f;
            _stabilityDirty = false;
            RecomputeStability();
        }

        // ----------------------------------------------------------------- queries

        public BuildPiece GetAt(BuildAddress address)
        {
            BuildPiece piece;
            return _pieces.TryGetValue(BuildAddress.Canonical(address), out piece) ? piece : null;
        }

        public bool IsOccupied(BuildAddress address)
        {
            return _pieces.ContainsKey(BuildAddress.Canonical(address));
        }

        public bool IsStable(BuildAddress address)
        {
            return _reachable.Contains(BuildAddress.Canonical(address));
        }

        /// <summary>Highest floor-slot piece in a cell at or below a height. Walls hang off these.</summary>
        public BuildPiece FindFloorAtOrBelow(int cellX, int cellZ, float worldY, float tolerance = 4f)
        {
            BuildPiece best = null;
            for (int i = 0; i < _all.Count; i++)
            {
                var piece = _all[i];
                if (piece.Definition.slot != BuildSlot.Floor) continue;
                if (piece.Address.X != cellX || piece.Address.Z != cellZ) continue;
                if (piece.Address.Y > worldY + 0.6f) continue;
                if (worldY - piece.Address.Y > tolerance) continue;
                if (best == null || piece.Address.Y > best.Address.Y) best = piece;
            }
            return best;
        }

        // --------------------------------------------------------------- placement

        public enum PlacementResult
        {
            Ok,
            SlotTaken,
            NoSupport,
            NeedsHost,
            OutsideWorld
        }

        public PlacementResult CanPlace(BuildPieceDefinition def, BuildAddress address)
        {
            if (def == null) return PlacementResult.NeedsHost;
            address = BuildAddress.Canonical(address);

            if (!TerrainWorld.InVerticalRange(address.Y) || !TerrainWorld.InVerticalRange(address.Y + BuildGrid.LevelHeight))
                return PlacementResult.OutsideWorld;

            if (_pieces.ContainsKey(address)) return PlacementResult.SlotTaken;

            if (def.requiresHost)
            {
                var host = GetAt(address.WithSlot(BuildSlot.Wall, address.Side));
                if (host == null || host.Definition.kind != def.hostKind) return PlacementResult.NeedsHost;
                return PlacementResult.Ok;
            }

            if (def.restsOnTerrain)
            {
                return HasTerrainSupport(address) ? PlacementResult.Ok : PlacementResult.NoSupport;
            }

            return HasStableConnection(address) ? PlacementResult.Ok : PlacementResult.NoSupport;
        }

        public static string Describe(PlacementResult result)
        {
            switch (result)
            {
                case PlacementResult.SlotTaken: return "Something is already there";
                case PlacementResult.NoSupport: return "Nothing to build on";
                case PlacementResult.NeedsHost: return "Needs a doorway or wall to mount on";
                case PlacementResult.OutsideWorld: return "Outside the world";
                default: return "";
            }
        }

        /// <summary>A 3 m foundation needs solid ground under most of its nine columns.</summary>
        public bool HasTerrainSupport(BuildAddress address)
        {
            var origin = BuildGrid.CellOrigin(address.X, address.Y, address.Z);
            int solid = 0;

            for (int dz = 0; dz < 3; dz++)
            {
                for (int dx = 0; dx < 3; dx++)
                {
                    int wx = Mathf.FloorToInt(origin.x) + dx;
                    int wz = Mathf.FloorToInt(origin.z) + dz;
                    if (_terrain.IsSolid(wx, address.Y - 1, wz)) solid++;
                }
            }
            return solid >= FoundationSupportColumns;
        }

        bool HasStableConnection(BuildAddress address)
        {
            _connectionScratch.Clear();
            BuildGrid.EnumerateConnections(address, _connectionScratch);

            for (int i = 0; i < _connectionScratch.Count; i++)
            {
                var neighbour = _connectionScratch[i];
                if (!_pieces.ContainsKey(neighbour)) continue;
                if (_reachable.Contains(neighbour)) return true;
            }
            return false;
        }

        public BuildPiece Place(BuildPieceDefinition def, BuildAddress address, float health = -1f, bool checkRules = true)
        {
            if (def == null) return null;
            address = BuildAddress.Canonical(address);

            if (checkRules && CanPlace(def, address) != PlacementResult.Ok) return null;
            if (_pieces.ContainsKey(address)) return null;

            var go = new GameObject(def.displayName);
            go.transform.SetParent(_root, false);

            var piece = go.AddComponent<BuildPiece>();
            piece.Initialise(this, def, address, health);

            _pieces.Add(address, piece);
            _all.Add(piece);
            _reachable.Add(address); // provisional; the next stability pass confirms it

            _stabilityDirty = true;
            if (Placed != null) Placed(piece);
            return piece;
        }

        public void Remove(BuildPiece piece, bool silent)
        {
            if (piece == null) return;

            _pieces.Remove(piece.Address);
            _reachable.Remove(piece.Address);
            _all.Remove(piece);

            if (Removed != null) Removed(piece);
            if (!silent) Notifications.PostFormat("{0} collapsed", piece.Definition.displayName);

            UnityEngine.Object.Destroy(piece.gameObject);
            _stabilityDirty = true;
        }

        public void Clear()
        {
            for (int i = _all.Count - 1; i >= 0; i--)
            {
                if (_all[i] != null) UnityEngine.Object.Destroy(_all[i].gameObject);
            }
            _all.Clear();
            _pieces.Clear();
            _reachable.Clear();
        }

        // ----------------------------------------------------------------- upgrade

        public enum UpgradeResult
        {
            Ok,
            TopTier,
            MissingMaterials,
            PerkLocked
        }

        public UpgradeResult CanUpgrade(BuildPiece piece, MadVoxel.Inventory.Inventory inventory, Func<string, int> perkRank)
        {
            if (piece == null || piece.Definition.upgradesTo == null) return UpgradeResult.TopTier;

            var next = piece.Definition.upgradesTo;
            if (!string.IsNullOrEmpty(next.requiredPerkId) && perkRank != null &&
                perkRank(next.requiredPerkId) < next.requiredPerkRank)
            {
                return UpgradeResult.PerkLocked;
            }

            for (int i = 0; i < next.upgradeCost.Count; i++)
            {
                var cost = next.upgradeCost[i];
                if (cost.item == null || cost.count <= 0) continue;
                if (inventory.CountOf(cost.item) < cost.count) return UpgradeResult.MissingMaterials;
            }
            return UpgradeResult.Ok;
        }

        public UpgradeResult Upgrade(BuildPiece piece, MadVoxel.Inventory.Inventory inventory, Func<string, int> perkRank)
        {
            var check = CanUpgrade(piece, inventory, perkRank);
            if (check != UpgradeResult.Ok) return check;

            var next = piece.Definition.upgradesTo;
            for (int i = 0; i < next.upgradeCost.Count; i++)
            {
                var cost = next.upgradeCost[i];
                if (cost.item == null || cost.count <= 0) continue;
                inventory.Remove(cost.item, cost.count);
            }

            piece.ReplaceDefinition(next);
            return UpgradeResult.Ok;
        }

        public static string Describe(UpgradeResult result, BuildPiece piece)
        {
            switch (result)
            {
                case UpgradeResult.TopTier: return piece != null ? piece.Definition.displayName + " is already top tier" : "";
                case UpgradeResult.MissingMaterials: return "Not enough materials to upgrade";
                case UpgradeResult.PerkLocked: return "A perk is needed for that tier";
                default: return "";
            }
        }

        // --------------------------------------------------------------- stability

        /// <summary>
        /// Flood fill from every grounded foundation. Anything the fill does not reach is
        /// unsupported and comes down.
        /// </summary>
        public void RecomputeStability()
        {
            _reachable.Clear();
            _frontier.Clear();

            for (int i = 0; i < _all.Count; i++)
            {
                var piece = _all[i];
                if (!piece.Definition.restsOnTerrain) continue;
                if (!HasTerrainSupport(piece.Address)) continue;

                if (_reachable.Add(piece.Address)) _frontier.Enqueue(piece.Address);
            }

            while (_frontier.Count > 0)
            {
                var current = _frontier.Dequeue();

                _connectionScratch.Clear();
                BuildGrid.EnumerateConnections(current, _connectionScratch);

                for (int i = 0; i < _connectionScratch.Count; i++)
                {
                    var neighbour = _connectionScratch[i];
                    if (!_pieces.ContainsKey(neighbour)) continue;
                    if (_reachable.Add(neighbour)) _frontier.Enqueue(neighbour);
                }
            }

            // Collapse everything the fill missed.
            for (int i = _all.Count - 1; i >= 0; i--)
            {
                var piece = _all[i];
                if (_reachable.Contains(piece.Address)) continue;
                Remove(piece, false);
            }
        }

        /// <summary>Counts pieces whose cell centre falls inside a radius. Used by the horde budget.</summary>
        public int CountNear(Vector3 centre, float radius)
        {
            float r2 = radius * radius;
            int count = 0;
            for (int i = 0; i < _all.Count; i++)
            {
                var p = BuildGrid.CellCentre(_all[i].Address.X, _all[i].Address.Y, _all[i].Address.Z);
                if ((p - centre).sqrMagnitude <= r2) count++;
            }
            return count;
        }
    }
}
