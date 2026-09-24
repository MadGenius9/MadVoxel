using System;
using System.Collections.Generic;
using MadVoxel.Content;
using MadVoxel.Core;
using MadVoxel.World.Terrain;
using UnityEngine;

namespace MadVoxel.Building
{
    /// <summary>
    /// Owns every placed snap piece and the cell index that makes placement checks
    /// cheap. Structures are separate from voxels so a door can swing and a crate can
    /// hold items, while still living on the same grid.
    /// </summary>
    public class StructureWorld : MonoBehaviour
    {
        readonly Dictionary<Vector3Int, PlacedStructure> _byCell = new Dictionary<Vector3Int, PlacedStructure>();
        readonly List<PlacedStructure> _all = new List<PlacedStructure>();

        TerrainWorld _voxels;
        Transform _root;

        /// <summary>The terrain these pieces sit on. Plots and pumps ask it where they are.</summary>
        public TerrainWorld Voxels { get { return _voxels; } }

        public LandClaimRegistry Claims { get; private set; }

        /// <summary>Crops grow on the world clock, so plots need it.</summary>
        public WorldClock Clock { get; private set; }

        /// <summary>
        /// The player's perks, for deployables whose output they scale. Read through
        /// the owner rather than pushed into each piece, so something restored from a
        /// save gets it without anyone remembering to wire it up again.
        /// </summary>
        public MadVoxel.Perks.PlayerProgression Progression { get; set; }
        public ContentDatabase Content { get; private set; }

        public event Action<PlacedStructure> Placed;
        public event Action<PlacedStructure> Removed;

        public IReadOnlyList<PlacedStructure> All { get { return _all; } }

        public void Init(TerrainWorld voxels, WorldClock clock, ContentDatabase content)
        {
            _voxels = voxels;
            Clock = clock;
            Content = content;
            Claims = new LandClaimRegistry();

            var rootGo = new GameObject("Structures");
            rootGo.transform.SetParent(transform, false);
            _root = rootGo.transform;

            _voxels.BlockChanged += OnBlockChanged;
        }

        void OnDestroy()
        {
            if (_voxels != null) _voxels.BlockChanged -= OnBlockChanged;
        }

        void OnBlockChanged(Vector3Int cell, ushort oldId, ushort newId)
        {
            // Mining a block out from under a piece drops it. Cheap structural integrity.
            if (!_voxels.Registry.IsAir(newId)) return;

            var above = cell + Vector3Int.up;
            PlacedStructure structure;
            if (_byCell.TryGetValue(above, out structure) && structure.Definition.requiresSupport)
            {
                if (!HasSupport(structure.Definition, structure.Cell, structure.RotationSteps, structure))
                {
                    Destroy(structure, true);
                }
            }
        }

        public PlacedStructure GetAt(Vector3Int cell)
        {
            PlacedStructure s;
            return _byCell.TryGetValue(cell, out s) ? s : null;
        }

        public bool IsOccupied(Vector3Int cell)
        {
            return _byCell.ContainsKey(cell);
        }

        public bool BlocksMovement(Vector3Int cell)
        {
            PlacedStructure s;
            return _byCell.TryGetValue(cell, out s) && s.Definition.blocksMovement;
        }

        public bool CanPlace(StructureDefinition def, Vector3Int cell, int rotationSteps)
        {
            if (def == null) return false;
            var size = PlacedStructure.RotatedFootprint(def.footprint, rotationSteps);

            for (int y = 0; y < Mathf.Max(1, size.y); y++)
            for (int z = 0; z < Mathf.Max(1, size.z); z++)
            for (int x = 0; x < Mathf.Max(1, size.x); x++)
            {
                var c = new Vector3Int(cell.x + x, cell.y + y, cell.z + z);
                if (!TerrainWorld.InVerticalRange(c.y)) return false;
                if (_voxels.IsSolid(c.x, c.y, c.z)) return false;
                if (_byCell.ContainsKey(c)) return false;
            }

            if (def.requiresSoil && !IsSoilUnder(cell)) return false;

            return !def.requiresSupport || HasSupport(def, cell, rotationSteps, null);
        }

        /// <summary>Farm plots need dirt under them - not stone, concrete or planking.</summary>
        public bool IsSoilUnder(Vector3Int cell)
        {
            var def = _voxels.GetBlockDef(cell.x, cell.y - 1, cell.z);
            if (def == null || def.isAir) return false;

            return def.stringId == BlockIds.Dirt
                || def.stringId == BlockIds.Grass
                || def.stringId == BlockIds.Clay
                || def.stringId == BlockIds.Sand;
        }

        bool HasSupport(StructureDefinition def, Vector3Int cell, int rotationSteps, PlacedStructure ignore)
        {
            var size = PlacedStructure.RotatedFootprint(def.footprint, rotationSteps);
            for (int z = 0; z < Mathf.Max(1, size.z); z++)
            {
                for (int x = 0; x < Mathf.Max(1, size.x); x++)
                {
                    var below = new Vector3Int(cell.x + x, cell.y - 1, cell.z + z);
                    if (_voxels.IsSolid(below.x, below.y, below.z)) return true;

                    PlacedStructure s;
                    if (_byCell.TryGetValue(below, out s) && s != ignore && s.Definition.blocksMovement) return true;
                }
            }
            return false;
        }

        public PlacedStructure Place(StructureDefinition def, Vector3Int cell, int rotationSteps, float health = -1f, bool checkRules = true)
        {
            if (def == null) return null;
            if (checkRules && !CanPlace(def, cell, rotationSteps)) return null;

            var go = new GameObject(def.displayName);
            go.transform.SetParent(_root, false);

            var structure = go.AddComponent<PlacedStructure>();
            structure.Initialise(this, def, cell, rotationSteps, health);

            for (int i = 0; i < structure.OccupiedCells.Count; i++)
            {
                _byCell[structure.OccupiedCells[i]] = structure;
            }
            _all.Add(structure);

            if (Placed != null) Placed(structure);
            return structure;
        }

        public void Destroy(PlacedStructure structure, bool dropSalvage)
        {
            if (structure == null) return;

            for (int i = 0; i < structure.OccupiedCells.Count; i++)
            {
                PlacedStructure occupant;
                if (_byCell.TryGetValue(structure.OccupiedCells[i], out occupant) && occupant == structure)
                {
                    _byCell.Remove(structure.OccupiedCells[i]);
                }
            }
            _all.Remove(structure);

            if (Removed != null) Removed(structure);

            Spill(structure);

            UnityEngine.Object.Destroy(structure.gameObject);
        }

        /// <summary>
        /// The sack a destroyed container leaves behind - the same one death drops,
        /// because it is already a container that persists, is lootable, and gets
        /// pruned when emptied. Looked up rather than injected so there is no wiring
        /// step for a future caller to forget.
        /// </summary>
        StructureDefinition SpillContainer
        {
            get { return Content != null ? Content.Structure(StructureIds.DeathBackpack) : null; }
        }

        /// <summary>
        /// Tips a destroyed container's contents onto the ground.
        ///
        /// They used to be deleted. A horde that reached a crate silently took
        /// everything in it, which is the worst thing this game can do to someone - an
        /// hour of mining is gone with no message, nothing on the floor, and no way to
        /// tell a bug from a rule. A base falling over should cost you the base, and
        /// make you go and pick your things up out of the wreckage.
        /// </summary>
        void Spill(PlacedStructure structure)
        {
            var contents = ContentsOf(structure);
            if (contents == null || contents.IsEmpty) return;

            // A sack does not spill into another sack. Without this a zombie chewing
            // the sack spawns an identical one in the same cell with the same things
            // in it, forever - an indestructible pile that keeps announcing itself and
            // keeps counting towards the horde's view of how big your base is.
            var existing = structure.GetComponent<StorageStructure>();
            if (existing != null && existing.IsDeathBackpack)
            {
                Notifications.PostFormat("The {0} was destroyed and its contents scattered",
                    structure.Definition.displayName);
                return;
            }

            var sack = PlaceSackNear(structure.Cell);
            if (sack != null)
            {
                contents.MoveAllTo(sack.Contents);

                // Anything that would not fit is still gone, but the player is told
                // which it was rather than left to discover the hole later.
                if (!contents.IsEmpty)
                {
                    Notifications.PostFormat("{0} was destroyed - some contents were lost",
                        structure.Definition.displayName);
                }
                else
                {
                    Notifications.PostFormat("{0} was destroyed - its contents are on the ground",
                        structure.Definition.displayName);
                }
                return;
            }

            Notifications.PostFormat("{0} was destroyed and its contents were lost",
                structure.Definition.displayName);
        }

        static MadVoxel.Inventory.Inventory ContentsOf(PlacedStructure structure)
        {
            var storage = structure.GetComponent<StorageStructure>();
            if (storage != null) return storage.Contents;

            // A furnace holds ore, fuel and finished metal in one inventory, and
            // losing a full one hurts as much as losing a crate.
            var furnace = structure.GetComponent<FurnaceStructure>();
            return furnace != null ? furnace.Contents : null;
        }

        /// <summary>
        /// Finds somewhere the sack can actually sit. The crate's own cell is free by
        /// the time this runs, so it is tried first and almost always takes it.
        /// </summary>
        StorageStructure PlaceSackNear(Vector3Int cell)
        {
            if (SpillContainer == null) return null;

            for (int y = 0; y < 3; y++)
            {
                var candidate = new Vector3Int(cell.x, cell.y + y, cell.z);
                if (candidate.y < 1 || candidate.y >= TerrainWorld.WorldHeight - 1) continue;
                if (IsOccupied(candidate)) continue;
                if (_voxels != null && _voxels.IsSolid(candidate.x, candidate.y, candidate.z)) continue;

                var placed = Place(SpillContainer, candidate, 0, -1f, false);
                if (placed == null) continue;

                var sack = placed.GetComponent<StorageStructure>();
                if (sack == null) continue;

                // Flagged like a death backpack, which is what stops empty ones
                // accumulating: the save layer drops any that are flagged and empty.
                sack.IsDeathBackpack = true;
                return sack;
            }

            return null;
        }

        public void DestroyAll()
        {
            for (int i = _all.Count - 1; i >= 0; i--)
            {
                if (_all[i] != null) UnityEngine.Object.Destroy(_all[i].gameObject);
            }
            _all.Clear();
            _byCell.Clear();
            Claims.Clear();
        }

        /// <summary>Counts pieces inside a claim. The horde uses this to scale its waves.</summary>
        public int CountInClaim(LandClaim claim)
        {
            if (claim == null) return 0;
            int count = 0;
            for (int i = 0; i < _all.Count; i++)
            {
                if (claim.Contains(_all[i].transform.position)) count++;
            }
            return count;
        }

        /// <summary>Nearest structure a zombie can chew through on its way to the claim.</summary>
        public PlacedStructure FindNearest(Vector3 point, float maxDistance)
        {
            PlacedStructure best = null;
            float bestDist = maxDistance * maxDistance;
            for (int i = 0; i < _all.Count; i++)
            {
                float d = (_all[i].transform.position - point).sqrMagnitude;
                if (d < bestDist) { bestDist = d; best = _all[i]; }
            }
            return best;
        }
    }
}
