using MadVoxel.Building;
using MadVoxel.Content;
using MadVoxel.Core;
using MadVoxel.Core.Player;
using MadVoxel.Perks;
using MadVoxel.Power;
using MadVoxel.World.Fields;
using MadVoxel.World.Terrain;
using UnityEngine;

namespace MadVoxel.Claim
{
    /// <summary>
    /// Keeps the claim's heat number current. It recomputes on the things that
    /// actually change it - a light toggling, the generator starting, a colonist
    /// arriving, dusk - rather than sampling the whole base every frame.
    /// </summary>
    public class ClaimHeatTracker : MonoBehaviour
    {
        const float SampleInterval = 2f;

        public HeatWeights Weights = HeatWeights.Default;

        StructureWorld _structures;
        PowerWorld _power;
        FieldWorld _fields;
        WorldClock _clock;
        TerrainWorld _voxels;
        ContentDatabase _content;
        PlayerProgression _progression;

        double _lastHours;
        float _sinceSample;
        bool _wasNight;

        /// <summary>0 to 100. The only number this system has.</summary>
        public float Heat { get; private set; }

        /// <summary>The quietest this claim can currently get.</summary>
        public float HeatFloor { get; private set; }

        /// <summary>How many people live here. Set by the colony once it exists.</summary>
        public int ColonistCount { get; set; }

        public string Word { get { return ClaimHeatMath.Describe(Heat); } }

        public void Init(StructureWorld structures, PowerWorld power, FieldWorld fields,
                         WorldClock clock, TerrainWorld voxels, ContentDatabase content,
                         PlayerProgression progression)
        {
            _structures = structures;
            _power = power;
            _fields = fields;
            _clock = clock;
            _voxels = voxels;
            _content = content;
            _progression = progression;
            _lastHours = clock != null ? clock.TotalHours : 0.0;
        }

        /// <summary>A gunshot is a burst, not a standing source.</summary>
        public void AddBurst(float amount)
        {
            Heat = Mathf.Clamp(Heat + amount, 0f, ClaimHeatMath.Max);
        }

        public void ReportGunshot()
        {
            AddBurst(Weights.perGunshot);
        }

        void Update()
        {
            if (_clock == null) return;

            _sinceSample += Time.deltaTime;
            if (_sinceSample < SampleInterval) return;
            _sinceSample = 0f;

            double now = _clock.TotalHours;
            float elapsed = Mathf.Max(0f, (float)(now - _lastHours));
            _lastHours = now;

            bool night = _clock.IsNight;
            bool dawnBroke = _wasNight && !night;
            _wasNight = night;

            float quiet = QuietFraction();

            HeatFloor = ClaimHeatMath.Floor(Weights, ColonistCount, WorkedFieldCells(), BiomeBonus(), quiet);
            float sources = ClaimHeatMath.Sources(LightHeat(night), GeneratorHeat(), TrapHeat(),
                MatureFieldCells(), Weights, quiet);

            Heat = ClaimHeatMath.Step(Heat, HeatFloor, sources, Weights, elapsed, dawnBroke);
        }

        /// <summary>Quiet Claim, straight off the perk tree.</summary>
        float QuietFraction()
        {
            if (_progression == null) return 0f;
            return Mathf.Clamp01(_progression.Effects.Bonus(PerkEffectType.ClaimHeatReduction));
        }

        float BiomeBonus()
        {
            if (_voxels == null || _content == null || _content.biomes == null) return 0f;

            var claim = FirstClaim();
            if (claim == null) return 0f;

            var def = _content.biomes.Find(_voxels.BiomeAt(claim.Centre));
            return def != null ? def.heatFloorBonus : 0f;
        }

        LandClaim FirstClaim()
        {
            if (_structures == null || _structures.Claims == null) return null;

            var claims = _structures.Claims.Claims;
            return claims.Count > 0 ? claims[0] : null;
        }

        /// <summary>Lights only count once the sun is down. A lit barn at noon is nothing.</summary>
        float LightHeat(bool night)
        {
            if (!night || _power == null) return 0f;

            float total = 0f;
            var devices = _power.Devices;
            for (int i = 0; i < devices.Count; i++)
            {
                var device = devices[i];
                if (device.Device.lightRange <= 0f) continue;

                var node = device.Node;
                if (node != null && node.IsPowered) total += device.Device.heatWhileLit;
            }
            return total;
        }

        float GeneratorHeat()
        {
            if (_power == null) return 0f;

            float total = 0f;
            var devices = _power.Devices;
            for (int i = 0; i < devices.Count; i++)
            {
                if (devices[i].Device.kind != PowerDeviceKind.Generator) continue;

                var node = devices[i].Node;
                if (node != null && node.WattsProduced > 0f) total += devices[i].Device.heatWhileRunning;
            }
            return total;
        }

        /// <summary>Only traps actually swinging. An armed, idle fence is silent.</summary>
        float TrapHeat()
        {
            if (_power == null) return 0f;

            float total = 0f;
            var devices = _power.Devices;
            for (int i = 0; i < devices.Count; i++)
            {
                if (devices[i].Device.trapDamage <= 0f) continue;

                var node = devices[i].Node;
                if (node != null && node.IsPowered && node.Triggered) total += devices[i].Device.heatWhileActive;
            }
            return total;
        }

        int WorkedFieldCells()
        {
            return _fields != null && _fields.Grid != null ? _fields.Grid.WorkedCellCount : 0;
        }

        float MatureFieldCells()
        {
            if (_fields == null || _fields.Grid == null) return 0f;

            int count = 0;
            foreach (var entry in _fields.Grid.Cells)
            {
                if (entry.Value.State == FieldCellState.Ready) count++;
            }
            return count;
        }

        /// <summary>The board line: "HEAT  37  ·  LOUD".</summary>
        public string Readout()
        {
            return string.Format("HEAT  {0}  -  {1}", Mathf.RoundToInt(Heat), Word);
        }

        /// <summary>Used by the save loader.</summary>
        public void LoadState(float heat)
        {
            Heat = Mathf.Clamp(heat, 0f, ClaimHeatMath.Max);
        }
    }
}
