using MadVoxel.Content;
using MadVoxel.Core;
using MadVoxel.World.Biomes;
using UnityEngine;

namespace MadVoxel.World.Weather
{
    /// <summary>
    /// Runs the sky on the same clock the crops grow on. One state at a time, rolled
    /// from the region the player is standing in, and every effect it has is a dial on
    /// a system that already exists - the pump, the panel, the soil, the barrel.
    ///
    /// It deliberately does not simulate anything. There is no pressure field and no
    /// cloud layer; there is a state, an hour counter, and a set of multipliers other
    /// systems ask it for.
    /// </summary>
    public class WeatherDirector : MonoBehaviour
    {
        WorldClock _clock;
        ContentDatabase _content;
        TerrainWorldLocator _locator;

        double _lastHours;
        float _hoursRemaining;
        int _rollCounter;

        public WeatherDefinition Current { get; private set; }
        public WeatherKind Kind { get { return Current != null ? Current.kind : WeatherKind.Clear; } }
        public float HoursRemaining { get { return _hoursRemaining; } }

        /// <summary>Fires when the state changes, so the HUD can say so once.</summary>
        public event System.Action<WeatherDefinition> Changed;

        /// <summary>
        /// Where the player is, so the sky can be rolled from their region rather than
        /// from the world origin. A delegate rather than a reference because the player
        /// rig outlives several of the things that would otherwise own this.
        /// </summary>
        public delegate BiomeId TerrainWorldLocator();

        public void Init(WorldClock clock, ContentDatabase content, TerrainWorldLocator locator)
        {
            _clock = clock;
            _content = content;
            _locator = locator;
            _lastHours = clock != null ? clock.TotalHours : 0.0;

            if (Current == null) SetState(WeatherKind.Clear, GraceHours());
        }

        float GraceHours()
        {
            var table = _content != null ? _content.weather : null;
            return table != null ? Mathf.Max(1f, table.graceHours) : 30f;
        }

        // ------------------------------------------------------------------- ticks

        void Update()
        {
            if (_clock == null || _content == null || _content.weather == null) return;

            double now = _clock.TotalHours;
            float elapsed = (float)(now - _lastHours);
            if (elapsed <= 0f) return;
            _lastHours = now;

            _hoursRemaining -= elapsed;
            if (_hoursRemaining > 0f) return;

            RollNext();
        }

        void RollNext()
        {
            var biome = _locator != null ? _locator() : BiomeId.Farmland;
            var def = _content.biomes != null ? _content.biomes.Find(biome) : null;

            // "Night ahead" rather than "night now": a frost that rolled at dusk should
            // be allowed, and one that rolled at breakfast should not.
            bool nightAhead = _clock.IsNight || _clock.HourOfDay > 16f;

            var kind = WeatherSchedule.Roll(def, _content.weather, nightAhead, Kind, NextRoll());
            var state = _content.weather.Find(kind);
            float hours = WeatherSchedule.Duration(state, NextRoll());

            SetState(kind, hours);
        }

        /// <summary>
        /// Deterministic per world-hour rather than Random.value, so a save that
        /// reloads mid-state does not get a different sky than it would have had.
        /// </summary>
        float NextRoll()
        {
            _rollCounter++;
            return Noise.Hash01((int)_lastHours, _rollCounter, _clock != null ? _clock.Day : 0, 20261);
        }

        void SetState(WeatherKind kind, float hours)
        {
            var table = _content != null ? _content.weather : null;
            var state = table != null ? table.Find(kind) : null;

            bool changed = Current == null || Current.kind != kind;
            Current = state;
            _hoursRemaining = Mathf.Max(0.5f, hours);

            if (!changed) return;

            if (state != null && !string.IsNullOrEmpty(state.clockWord))
            {
                Notifications.Post(state.clockWord);
            }
            if (Changed != null) Changed(state);
        }

        /// <summary>Used by the save loader and by the developer hotkey.</summary>
        public void Force(WeatherKind kind, float hoursRemaining)
        {
            SetState(kind, hoursRemaining);
        }

        // ---------------------------------------------------------------- the dials

        /// <summary>Weather times the biome. Everything solar-facing multiplies by this.</summary>
        public float SolarMultiplier(BiomeId biome)
        {
            float weather = Current != null ? Current.solarMultiplier : 1f;
            var def = _content != null && _content.biomes != null ? _content.biomes.Find(biome) : null;
            return weather * (def != null ? def.solarMultiplier : 1f);
        }

        /// <summary>Weather times the biome. A drought on the dry flats is punishing.</summary>
        public float PumpMultiplier(BiomeId biome)
        {
            float weather = Current != null ? Current.pumpMultiplier : 1f;
            var def = _content != null && _content.biomes != null ? _content.biomes.Find(biome) : null;
            return weather * (def != null ? def.pumpMultiplier : 1f);
        }

        /// <summary>Field moisture change per game hour, biome drain included.</summary>
        public float MoisturePerHour(BiomeId biome)
        {
            float weather = Current != null ? Current.moisturePerHour : 0f;
            var def = _content != null && _content.biomes != null ? _content.biomes.Find(biome) : null;
            float drain = def != null ? def.moistureDrainMultiplier : 1f;

            // Wet weather adds outright; dry weather and the ground's own thirst both
            // scale the drying, so the flats bake and the bottomland holds.
            const float BaseDrainPerHour = -0.012f;
            return weather > 0f ? weather : (weather + BaseDrainPerHour) * drain;
        }

        public bool FreezingOutside { get { return Current != null && Current.freezesTaps; } }

        public float BarrelFillLitresPerHour { get { return Current != null ? Current.barrelFillLitresPerHour : 0f; } }

        public float BreakChancePerHour { get { return Current != null ? Current.breakChancePerHour : 0f; } }

        public float OutdoorMoralePerHour { get { return Current != null ? Current.outdoorMoralePerHour : 0f; } }

        public float StaminaDrainMultiplier { get { return Current != null ? Current.staminaDrainMultiplier : 1f; } }

        public float UnwateredPlotYieldLoss { get { return Current != null ? Current.unwateredPlotYieldLoss : 0f; } }

        public float PlotStageLossChance { get { return Current != null ? Current.plotStageLossChance : 0f; } }

        /// <summary>The compass line: "DAY 14   17:41   DROUGHT".</summary>
        public string ClockLine()
        {
            if (_clock == null) return "";
            // The bare time: ClockLine prints the day itself, and its check has always
            // passed it a bare "17:41".
            return WeatherSchedule.ClockLine(_clock.Day, _clock.FormatTime(), Current);
        }
    }
}
