using System;
using System.Collections.Generic;
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

        public LandClaimRegistry Claims { get; private set; }

        public event Action<PlacedStructure> Placed;
        public event Action<PlacedStructure> Removed;

        public IReadOnlyList<PlacedStructure> All { get { return _all; } }

        public void Init(TerrainWorld voxels)
        {
            _voxels = voxels;
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

            return !def.requiresSupport || HasSupport(def, cell, rotationSteps, null);
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

            // Anything inside a destroyed crate spills onto the ground as a new crate-less
            // pile is out of scope for Phase 0, so contents are simply lost with a warning.
            UnityEngine.Object.Destroy(structure.gameObject);
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
