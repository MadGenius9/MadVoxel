using System.Collections.Generic;
using MadVoxel.Building;
using MadVoxel.Content;
using MadVoxel.Core;
using MadVoxel.Core.Player;
using MadVoxel.Perks;
using MadVoxel.World.Terrain;
using MadVoxel.World.Weather;
using UnityEngine;

namespace MadVoxel.Power
{
    /// <summary>
    /// Owns the one power graph and keeps it in step with the world: deployables come
    /// and go, the weather moves the panels, and once a second the grid is solved and
    /// every device is told whether it has its watts.
    ///
    /// The solve runs on game hours rather than real seconds so fuel and charge behave
    /// the same whether the clock is running fast or the game was just loaded.
    /// </summary>
    public class PowerWorld : MonoBehaviour
    {
        /// <summary>How often the grid is re-solved, in real seconds.</summary>
        const float SolveInterval = 1f;

        public PowerGraph Graph { get; private set; }

        StructureWorld _structures;
        TerrainWorld _voxels;
        WorldClock _clock;
        WeatherDirector _weather;
        PlayerProgression _progression;

        readonly Dictionary<int, PowerDeviceStructure> _byNode = new Dictionary<int, PowerDeviceStructure>();
        readonly List<PowerDeviceStructure> _devices = new List<PowerDeviceStructure>();

        double _lastHours;
        float _sinceSolve;

        /// <summary>Raised after every solve, so the fluid side and the HUD can react.</summary>
        public event System.Action Solved;

        public void Init(StructureWorld structures, TerrainWorld voxels, WorldClock clock,
                         WeatherDirector weather, PlayerProgression progression)
        {
            _progression = progression;
            Graph = new PowerGraph();
            _structures = structures;
            _voxels = voxels;
            _clock = clock;
            _weather = weather;
            _lastHours = clock != null ? clock.TotalHours : 0.0;

            PowerDeviceStructure.Registered += Adopt;
            PowerDeviceStructure.Unregistered += Drop;
        }

        void OnDestroy()
        {
            PowerDeviceStructure.Registered -= Adopt;
            PowerDeviceStructure.Unregistered -= Drop;
        }

        // ------------------------------------------------------------------ device

        void Adopt(PowerDeviceStructure device)
        {
            if (device == null || device.Device == null || Graph == null) return;

            var node = Graph.Add(device.Device, device.transform.position);
            device.NodeId = node.Id;
            device.Graph = Graph;

            _byNode[node.Id] = device;
            _devices.Add(device);

            // A device that bites needs something watching for things to bite.
            if (device.Device.trapDamage > 0f)
            {
                device.gameObject.AddComponent<PowerTrap>().Init(device, _progression);
            }
        }

        void Drop(PowerDeviceStructure device)
        {
            if (device == null || Graph == null) return;

            // A destroyed bank takes its wires with it, which is exactly what a horde
            // breaking the generator shed should do to the lights.
            Graph.Remove(device.NodeId);
            _byNode.Remove(device.NodeId);
            _devices.Remove(device);
        }

        public PowerDeviceStructure DeviceAt(int nodeId)
        {
            PowerDeviceStructure device;
            return _byNode.TryGetValue(nodeId, out device) ? device : null;
        }

        /// <summary>Finds the device a raycast hit, for the wire tool.</summary>
        public static PowerDeviceStructure FromCollider(Component hit)
        {
            return hit != null ? hit.GetComponentInParent<PowerDeviceStructure>() : null;
        }

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

            // The Electrician's metres are applied to the graph rather than to each
            // device, so a rank bought this minute lengthens every existing run.
            Graph.ExtraReach = _progression != null
                ? _progression.Effects.Bonus(PerkEffectType.WireLengthBonus) : 0f;

            Graph.Tick(elapsed, Conditions());

            for (int i = 0; i < _devices.Count; i++) _devices[i].ApplyState();
            if (Solved != null) Solved();
        }

        PowerConditions Conditions()
        {
            bool day = _clock == null || !_clock.IsNight;

            float solar = 1f;
            if (_weather != null && _voxels != null)
            {
                // Panels are scored where they stand, not where the player is.
                var biome = _devices.Count > 0
                    ? _voxels.BiomeAt(_devices[0].transform.position)
                    : World.Biomes.BiomeId.Farmland;
                solar = _weather.SolarMultiplier(biome);
            }

            return new PowerConditions { IsDay = day, SolarMultiplier = solar };
        }

        // ------------------------------------------------------------------ damage

        /// <summary>
        /// A storm looking for something exposed to break. Returns the device it found,
        /// or null when the grid is all indoors - which is the point of building a shed.
        /// </summary>
        public PowerDeviceStructure PickExposedDevice(System.Random random)
        {
            if (_devices.Count == 0) return null;

            // Panels and relays live outside by nature; banks usually do not.
            var candidates = new List<PowerDeviceStructure>();
            for (int i = 0; i < _devices.Count; i++)
            {
                var kind = _devices[i].Device.kind;
                if (kind == PowerDeviceKind.SolarBank || kind == PowerDeviceKind.Relay) candidates.Add(_devices[i]);
            }

            if (candidates.Count == 0) return null;
            return candidates[random.Next(candidates.Count)];
        }

        /// <summary>Total watts the grid is producing, for the board and the debug line.</summary>
        public string StatusLine()
        {
            if (Graph == null) return "";

            return string.Format("{0}/{1} W   {2} Wh",
                Mathf.RoundToInt(Graph.DemandWatts),
                Mathf.RoundToInt(Graph.SupplyWatts + Graph.BatteryWatts),
                Mathf.RoundToInt(Graph.StoredWattHours));
        }

        /// <summary>Is a given device powered right now? Used by the fluid side.</summary>
        public bool IsPowered(int nodeId)
        {
            var node = Graph != null ? Graph.Get(nodeId) : null;
            return node != null && node.IsPowered;
        }

        /// <summary>Every powered fridge, so spoilage knows which boxes are cold.</summary>
        public IReadOnlyList<PowerDeviceStructure> Devices { get { return _devices; } }
    }
}
