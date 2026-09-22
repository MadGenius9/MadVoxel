using MadVoxel.Building;
using MadVoxel.Core;
using MadVoxel.Core.Player;
using MadVoxel.Farming.Crops;
using MadVoxel.Farming.Plots;
using MadVoxel.Fluid;
using MadVoxel.Perks;
using MadVoxel.Power;
using UnityEngine;

namespace MadVoxel.World.Weather
{
    /// <summary>
    /// The weather's hands. The director decides what the sky is doing; this applies it
    /// to the things that already exist - barrels fill, exposed fittings break, frost
    /// takes a stage off an unsheltered plot.
    ///
    /// Everything here is rolled per game hour rather than per frame, so a fast clock
    /// and a slow one do the same damage over the same in-game night.
    /// </summary>
    public class WeatherEffects : MonoBehaviour
    {
        WeatherDirector _weather;
        StructureWorld _structures;
        PowerWorld _power;
        FluidWorld _fluid;
        WorldClock _clock;
        PlayerProgression _progression;

        System.Random _random;
        double _lastHours;
        float _hourAccumulator;

        public void Init(WeatherDirector weather, StructureWorld structures, PowerWorld power,
                         FluidWorld fluid, WorldClock clock, PlayerProgression progression, int seed)
        {
            _weather = weather;
            _structures = structures;
            _power = power;
            _fluid = fluid;
            _clock = clock;
            _progression = progression;
            _random = new System.Random(seed ^ 0x5F3A);
            _lastHours = clock != null ? clock.TotalHours : 0.0;
        }

        void Update()
        {
            if (_clock == null || _weather == null) return;

            double now = _clock.TotalHours;
            float elapsed = Mathf.Max(0f, (float)(now - _lastHours));
            _lastHours = now;
            if (elapsed <= 0f) return;

            FillBarrels(elapsed);

            // Breakage and frost are rolled once per whole game hour rather than
            // continuously, so a storm cannot shred a base because the clock ticked
            // fast while a chunk loaded.
            _hourAccumulator += elapsed;
            while (_hourAccumulator >= 1f)
            {
                _hourAccumulator -= 1f;
                RollBreakage();
                RollFrostDamage();
            }
        }

        /// <summary>Rain is a water source for anyone without a pump yet.</summary>
        void FillBarrels(float gameHours)
        {
            float litres = _weather.BarrelFillLitresPerHour * gameHours;
            if (litres <= 0f || _fluid == null) return;

            var devices = _fluid.Devices;
            for (int i = 0; i < devices.Count; i++)
            {
                var device = devices[i];
                if (device.Device.kind != FluidDeviceKind.Tank) continue;
                if (!device.Device.exposedToWeather) continue;

                var node = device.Node;
                if (node == null || node.IsBroken) continue;

                node.Litres = Mathf.Min(node.Capacity, node.Litres + litres);
            }
        }

        /// <summary>
        /// A storm looking for one exposed thing to break. One per hour at most, and
        /// the Electrician's fittings shrug some of it off - which is what makes that
        /// rank worth buying before the season turns.
        /// </summary>
        void RollBreakage()
        {
            float chance = _weather.BreakChancePerHour;
            if (chance <= 0f) return;

            if (_progression != null)
            {
                chance *= Mathf.Clamp01(1f - _progression.Effects.Bonus(PerkEffectType.StormResistance));
            }

            if (_random.NextDouble() > chance) return;

            // Half the time it finds the plumbing, half the grid - whichever is there.
            bool tryFluid = _random.Next(2) == 0;

            if (tryFluid && BreakFitting()) return;
            if (BreakDevice()) return;
            if (!tryFluid) BreakFitting();
        }

        bool BreakFitting()
        {
            if (_fluid == null) return false;

            var fitting = _fluid.PickExposedFitting(_random);
            if (fitting == null || fitting.Node == null) return false;

            fitting.Node.IsBroken = true;
            Notifications.PostFormat("The storm broke a {0} - everything past it is dry",
                fitting.Device.displayName.ToLowerInvariant());
            return true;
        }

        bool BreakDevice()
        {
            if (_power == null) return false;

            var device = _power.PickExposedDevice(_random);
            if (device == null) return false;

            // Electrical damage is dealt to the deployable rather than flagged, so the
            // hammer already knows how to put it right.
            var structure = device.Structure;
            if (structure == null) return false;

            structure.ApplyDamage(DamageInfo.Simple(structure.Definition.maxHealth * 0.35f, DamageKind.Weather));
            Notifications.PostFormat("The storm damaged the {0}", device.Device.displayName.ToLowerInvariant());
            return true;
        }

        /// <summary>
        /// Frost on an unsheltered bed. A plot under a roof is safe, which is the first
        /// reason anyone builds a greenhouse rather than a field.
        /// </summary>
        void RollFrostDamage()
        {
            float chance = _weather.PlotStageLossChance;
            if (chance <= 0f || _structures == null) return;

            var all = _structures.All;
            for (int i = 0; i < all.Count; i++)
            {
                var plot = all[i].GetComponent<FarmPlotStructure>();
                if (plot == null || plot.IsEmpty) continue;
                if (IsSheltered(all[i])) continue;
                if (_random.NextDouble() > chance) continue;

                plot.SetBackAStage();
            }
        }

        /// <summary>Anything solid overhead counts. Cheap, and it rewards a roof.</summary>
        static bool IsSheltered(PlacedStructure structure)
        {
            return Physics.Raycast(structure.transform.position + Vector3.up * 0.6f, Vector3.up, 6f,
                ~0, QueryTriggerInteraction.Ignore);
        }
    }
}
