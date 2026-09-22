using System.Collections.Generic;
using MadVoxel.Building;
using MadVoxel.Core;
using MadVoxel.Core.Player;
using MadVoxel.Farming.Plots;
using MadVoxel.Perks;
using MadVoxel.Power;
using MadVoxel.World.Fields;
using MadVoxel.World.Terrain;
using MadVoxel.World.Weather;
using UnityEngine;

namespace MadVoxel.Fluid
{
    /// <summary>
    /// Owns the one fluid graph. It also answers the two questions the graph cannot:
    /// does this pump have power, and is there anything wet underneath it.
    ///
    /// The wet check is the link back to digging. A pump wants saturated ground within
    /// a few blocks below it, and where that ground sits depends on the region - an
    /// afternoon's dig in the bottomland, a project on the dry flats.
    /// </summary>
    public class FluidWorld : MonoBehaviour
    {
        const float SolveInterval = 1f;

        public FluidGraph Graph { get; private set; }

        StructureWorld _structures;
        TerrainWorld _voxels;
        WorldClock _clock;
        WeatherDirector _weather;
        PowerWorld _power;
        FieldWorld _fields;
        PlayerProgression _progression;

        readonly Dictionary<int, FluidDeviceStructure> _byNode = new Dictionary<int, FluidDeviceStructure>();
        readonly List<FluidDeviceStructure> _devices = new List<FluidDeviceStructure>();

        ushort _waterTableId;
        double _lastHours;
        float _sinceSolve;

        public void Init(StructureWorld structures, TerrainWorld voxels, WorldClock clock,
                         WeatherDirector weather, PowerWorld power, FieldWorld fields,
                         PlayerProgression progression)
        {
            _progression = progression;
            Graph = new FluidGraph();
            _structures = structures;
            _voxels = voxels;
            _clock = clock;
            _weather = weather;
            _power = power;
            _fields = fields;
            _lastHours = clock != null ? clock.TotalHours : 0.0;

            _waterTableId = voxels != null ? voxels.Registry.IdOf(BlockIds.WaterTable) : (ushort)0;

            FluidDeviceStructure.Registered += Adopt;
            FluidDeviceStructure.Unregistered += Drop;
        }

        void OnDestroy()
        {
            FluidDeviceStructure.Registered -= Adopt;
            FluidDeviceStructure.Unregistered -= Drop;
        }

        void Adopt(FluidDeviceStructure device)
        {
            if (device == null || device.Device == null || Graph == null) return;

            var node = Graph.Add(device.Device, device.transform.position);
            device.NodeId = node.Id;
            device.Graph = Graph;

            _byNode[node.Id] = device;
            _devices.Add(device);
        }

        void Drop(FluidDeviceStructure device)
        {
            if (device == null || Graph == null) return;

            Graph.Remove(device.NodeId);
            _byNode.Remove(device.NodeId);
            _devices.Remove(device);
        }

        public FluidDeviceStructure DeviceAt(int nodeId)
        {
            FluidDeviceStructure device;
            return _byNode.TryGetValue(nodeId, out device) ? device : null;
        }

        public static FluidDeviceStructure FromCollider(Component hit)
        {
            return hit != null ? hit.GetComponentInParent<FluidDeviceStructure>() : null;
        }

        public IReadOnlyList<FluidDeviceStructure> Devices { get { return _devices; } }

        // -------------------------------------------------------------------- tick

        void Update()
        {
            if (Graph == null || _clock == null) return;

            _sinceSolve += Time.deltaTime;
            if (_sinceSolve < SolveInterval) return;
            _sinceSolve = 0f;

            double now = _clock.TotalHours;
            float elapsed = Mathf.Max(0f, (float)(now - _lastHours));
            _lastHours = now;

            RefreshPumps();

            Graph.ExtraReach = _progression != null
                ? _progression.Effects.Bonus(PerkEffectType.WireLengthBonus) : 0f;

            Graph.Tick(elapsed, Conditions());
            WaterPlots(elapsed);
        }

        /// <summary>
        /// A pump is only as good as what is under it and what is wired to it. Both are
        /// re-checked every solve so digging out your own well, or losing the
        /// generator, shows up immediately.
        /// </summary>
        void RefreshPumps()
        {
            for (int i = 0; i < _devices.Count; i++)
            {
                var device = _devices[i];
                if (device.Device.kind != FluidDeviceKind.Pump) continue;

                var node = device.Node;
                if (node == null) continue;

                node.HasWetSource = HasWaterUnder(device.Structure.Cell, device.Device.sourceSearchDepth);
                node.HasPower = PumpHasPower(device);
            }
        }

        bool PumpHasPower(FluidDeviceStructure device)
        {
            if (_power == null) return true; // no grid in the world at all: do not punish

            // The pump's own deployable carries the power device when it needs watts.
            var electrical = device.GetComponent<PowerDeviceStructure>();
            if (electrical == null) return true;

            return _power.IsPowered(electrical.NodeId);
        }

        /// <summary>Saturated ground within reach below the pump.</summary>
        public bool HasWaterUnder(Vector3Int cell, int depth)
        {
            if (_voxels == null || _waterTableId == 0) return false;

            for (int i = 1; i <= Mathf.Max(1, depth); i++)
            {
                if (_voxels.GetBlock(cell.x, cell.y - i, cell.z) == _waterTableId) return true;
            }
            return false;
        }

        FluidConditions Conditions()
        {
            float multiplier = 1f;
            bool freezing = false;

            if (_weather != null)
            {
                var biome = _devices.Count > 0 && _voxels != null
                    ? _voxels.BiomeAt(_devices[0].transform.position)
                    : World.Biomes.BiomeId.Farmland;

                multiplier = _weather.PumpMultiplier(biome);
                freezing = _weather.FreezingOutside;
            }

            // Millwright pulls harder out of the same hole.
            if (_progression != null)
            {
                multiplier *= _progression.Effects.Multiplier(PerkEffectType.PumpRateMultiplier);
            }

            return new FluidConditions { PumpMultiplier = multiplier, FreezingOutside = freezing };
        }

        // ---------------------------------------------------------------- watering

        /// <summary>
        /// Sprinklers pay for themselves here: every plot inside a running sprinkler's
        /// radius carries its bonus into the harvest, and every field cell under one
        /// gains moisture. A sprinkler with no water waters nothing.
        /// </summary>
        void WaterPlots(float gameHours)
        {
            if (_structures == null) return;

            // Clear first: a sprinkler that lost its water must stop paying out. The
            // drought figure is refreshed in the same sweep, because the two of them
            // are read together at harvest and must never disagree.
            float drought = _weather != null ? _weather.UnwateredPlotYieldLoss : 0f;

            var plots = _structures.All;
            for (int i = 0; i < plots.Count; i++)
            {
                var plot = plots[i].GetComponent<FarmPlotStructure>();
                if (plot == null) continue;

                plot.WaterBonus = 0f;
                plot.DroughtLoss = drought;
            }

            for (int i = 0; i < _devices.Count; i++)
            {
                var device = _devices[i];
                if (device.Device.kind != FluidDeviceKind.Sprinkler) continue;

                var node = device.Node;
                if (node == null || !node.IsLive) continue;

                ApplySprinkler(device, gameHours);
            }
        }

        void ApplySprinkler(FluidDeviceStructure sprinkler, float gameHours)
        {
            float radius = sprinkler.Device.sprinklerRadius;
            float radiusSq = radius * radius;
            Vector3 centre = sprinkler.transform.position;

            var all = _structures.All;
            for (int i = 0; i < all.Count; i++)
            {
                if ((all[i].transform.position - centre).sqrMagnitude > radiusSq) continue;

                var plot = all[i].GetComponent<FarmPlotStructure>();
                if (plot != null) plot.WaterBonus = Mathf.Max(plot.WaterBonus, sprinkler.Device.wateredYieldBonus);
            }

            if (_fields == null || gameHours <= 0f) return;

            // Field cells in range gain moisture, which is the Phase 1 irrigator's job
            // done by hand until the implement lands.
            int minX = Mathf.FloorToInt(centre.x - radius), maxX = Mathf.CeilToInt(centre.x + radius);
            int minZ = Mathf.FloorToInt(centre.z - radius), maxZ = Mathf.CeilToInt(centre.z + radius);
            float gain = sprinkler.Device.moisturePerHour * gameHours;

            for (int z = minZ; z <= maxZ; z++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    if (!_fields.Grid.IsWorked(x, z)) continue;

                    var cell = _fields.Grid.Get(x, z);
                    cell.Moisture = Mathf.Clamp01(cell.Moisture + gain);
                    _fields.Grid.Set(x, z, cell);
                }
            }
        }

        // ------------------------------------------------------------------ damage

        /// <summary>A storm looking for an exposed fitting. Buried pipe is safe.</summary>
        public FluidDeviceStructure PickExposedFitting(System.Random random)
        {
            var candidates = new List<FluidDeviceStructure>();
            for (int i = 0; i < _devices.Count; i++)
            {
                if (_devices[i].Device.exposedToWeather && !_devices[i].Node.IsBroken) candidates.Add(_devices[i]);
            }

            if (candidates.Count == 0) return null;
            return candidates[random.Next(candidates.Count)];
        }

        public string StatusLine()
        {
            if (Graph == null) return "";

            return string.Format("{0} L/MIN   {1}/{2} L",
                Mathf.RoundToInt(Graph.InflowLitresPerMinute),
                Mathf.RoundToInt(Graph.StoredLitres),
                Mathf.RoundToInt(Graph.CapacityLitres));
        }
    }
}
