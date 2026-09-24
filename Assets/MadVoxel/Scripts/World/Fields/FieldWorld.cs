using System.Collections.Generic;
using MadVoxel.Content;
using MadVoxel.Core;
using MadVoxel.Farming.Crops;
using MadVoxel.World.Terrain;
using UnityEngine;

namespace MadVoxel.World.Fields
{
    /// <summary>
    /// Owns the field grid and connects it to the terrain.
    ///
    /// A hoe plows one cell by hand: the terrain block turns to tilled soil so the work
    /// is visible and persists in the chunk file, and the agronomic state lands in the
    /// grid. The tractor's implements call these same operations across a swath - the
    /// state machine, growth timing and litre yield are shared, so the fast way and the
    /// slow way cannot drift apart.
    /// </summary>
    public class FieldWorld : MonoBehaviour
    {
        public FieldGrid Grid { get; private set; }

        TerrainWorld _terrain;
        WorldClock _clock;
        ContentDatabase _content;

        readonly List<CropDefinition> _fieldCrops = new List<CropDefinition>();

        public void Init(TerrainWorld terrain, WorldClock clock, ContentDatabase content)
        {
            _terrain = terrain;
            _clock = clock;
            _content = content;
            Grid = new FieldGrid();

            // Index 0 is reserved for "nothing sown", so the table starts at 1.
            _fieldCrops.Clear();
            _fieldCrops.Add(null);
            for (int i = 0; i < content.crops.Count; i++)
            {
                if (content.crops[i] != null && content.crops[i].growsOnField) _fieldCrops.Add(content.crops[i]);
            }
        }

        public CropDefinition CropAt(byte index)
        {
            return index > 0 && index < _fieldCrops.Count ? _fieldCrops[index] : null;
        }

        public byte IndexOf(CropDefinition crop)
        {
            for (byte i = 1; i < _fieldCrops.Count; i++)
            {
                if (_fieldCrops[i] == crop) return i;
            }
            return 0;
        }

        double NowHours { get { return _clock != null ? _clock.TotalHours : 0.0; } }

        // ------------------------------------------------------------ operations

        /// <summary>Is this surface column something a plow could break?</summary>
        public bool IsTillable(Vector3Int cell)
        {
            var def = _terrain.GetBlockDef(cell.x, cell.y, cell.z);
            if (def == null || def.isAir) return false;

            // Only open ground: nothing under a roof, a wall or another block.
            if (_terrain.IsSolid(cell.x, cell.y + 1, cell.z)) return false;

            return def.stringId == BlockIds.Grass
                || def.stringId == BlockIds.Dirt
                || def.stringId == BlockIds.TilledSoil
                || def.stringId == BlockIds.CultivatedSoil;
        }

        /// <summary>
        /// Plows one surface cell. Returns false when the ground cannot take it or it is
        /// already broken.
        /// </summary>
        public bool TryPlow(Vector3Int cell)
        {
            if (!IsTillable(cell)) return false;

            var tilled = _terrain.Registry.ByStringId(BlockIds.TilledSoil);
            if (tilled == null) return false;

            bool changed = Grid.Plow(cell.x, cell.z, NowHours);
            if (!changed) return false;

            // The visible half: the block itself becomes tilled soil, so the work shows
            // and is saved with the chunk like any other terrain edit.
            _terrain.SetBlock(cell.x, cell.y, cell.z, tilled.RuntimeId);
            return true;
        }

        public bool TryCultivate(Vector3Int cell)
        {
            // Checked before anything moves, and for the same reason TryPlow checks.
            // The field grid is two-dimensional: a cell broken months ago and since
            // paved over is still "cultivatable" as far as the grid knows, and writing
            // the block without asking would replace the player's cobblestone with
            // soil - no drop, no message, no way to tell what happened.
            if (!IsTillable(cell)) return false;

            var soil = _terrain.Registry.ByStringId(BlockIds.CultivatedSoil);
            if (soil == null) return false;

            if (!Grid.Cultivate(cell.x, cell.z, NowHours)) return false;

            // The visible half, same as plowing. A cultivated strip that looked exactly
            // like a plowed one meant the only record of the second pass was the
            // player's memory of having made it.
            _terrain.SetBlock(cell.x, cell.y, cell.z, soil.RuntimeId);
            return true;
        }

        /// <summary>Has this cell been broken into a field yet?</summary>
        public bool IsWorked(Vector3Int cell)
        {
            return Grid.Get(cell.x, cell.z).IsWorkable;
        }

        /// <summary>
        /// Spreads compost. False when the ground is wild, already rich, or not the
        /// surface of a field at all.
        ///
        /// The column check is not decoration. The grid is keyed on x and z only, so
        /// without it a player standing in a cellar could aim at the stone ceiling and
        /// feed the acre overhead.
        /// </summary>
        public bool TryFertilise(Vector3Int cell)
        {
            if (!IsTillable(cell)) return false;
            return Grid.Fertilise(cell.x, cell.z);
        }

        public bool TrySow(Vector3Int cell, CropDefinition crop)
        {
            if (crop == null || !crop.growsOnField) return false;

            byte index = IndexOf(crop);
            if (index == 0) return false;

            return Grid.Sow(cell.x, cell.z, index, NowHours);
        }

        /// <summary>Harvests one ready cell and returns the litres, or zero.</summary>
        public float TryHarvest(Vector3Int cell, out CropDefinition crop)
        {
            crop = null;

            var cellData = Grid.Get(cell.x, cell.z);
            var growing = CropAt(cellData.CropIndex);
            if (growing == null) return 0f;

            Grid.Refresh(cell.x, cell.z, NowHours, growing.HoursToMature);

            byte harvestedIndex;
            float litres = Grid.Harvest(cell.x, cell.z, growing.litresPerCell, NowHours, out harvestedIndex);
            if (litres <= 0f) return 0f;

            crop = CropAt(harvestedIndex);
            return litres;
        }

        /// <summary>A horde crossing a field knocks the crop back. Costs the harvest, not the land.</summary>
        public int TrampleAround(Vector3 world, float radius)
        {
            var centre = FieldGrid.CellOf(world);
            int cells = Mathf.CeilToInt(radius / FieldGrid.CellSize);
            int trampled = 0;

            for (int dz = -cells; dz <= cells; dz++)
            {
                for (int dx = -cells; dx <= cells; dx++)
                {
                    if (dx * dx + dz * dz > cells * cells) continue;
                    if (Grid.Trample(centre.x + dx, centre.y + dz, NowHours)) trampled++;
                }
            }
            return trampled;
        }

        public string DescribeAt(Vector3Int cell)
        {
            var data = Grid.Get(cell.x, cell.z);
            if (data.State == FieldCellState.Wild) return "";

            var crop = CropAt(data.CropIndex);
            if (crop == null) return "Field: " + data.State;

            float progress = Grid.Progress01(cell.x, cell.z, NowHours, crop.HoursToMature);
            return string.Format("Field: {0} {1} ({2:0}%)", crop.displayName, data.State, progress * 100f);
        }
    }
}
