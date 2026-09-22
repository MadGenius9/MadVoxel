using System;
using MadVoxel.AI;
using MadVoxel.Building;
using MadVoxel.Core;
using MadVoxel.Skills;
using UnityEngine;

namespace MadVoxel.Horde
{
    /// <summary>
    /// Runs blood-moon nights: warns during the day, turns the sky, spawns waves that
    /// walk at the land claim, and clears up at dawn.
    /// </summary>
    public class HordeDirector : MonoBehaviour
    {
        HordeSchedule _schedule;
        WorldClock _clock;
        SpawnDirector _spawner;
        StructureWorld _structures;
        SkyController _sky;
        PlayerProgression _progression;
        Transform _player;

        bool _active;
        int _warnedDay = -1;
        float _nextWaveTime;
        int _hordeNumber;

        public bool IsBloodMoonActive { get { return _active; } }
        public int HordeNumber { get { return _hordeNumber; } }

        public event Action<bool> BloodMoonChanged;

        public void Init(HordeSchedule schedule, WorldClock clock, SpawnDirector spawner,
                         StructureWorld structures, SkyController sky, PlayerProgression progression, Transform player)
        {
            _schedule = schedule;
            _clock = clock;
            _spawner = spawner;
            _structures = structures;
            _sky = sky;
            _progression = progression;
            _player = player;
        }

        void Update()
        {
            if (_schedule == null || _clock == null) return;

            bool shouldBeActive = IsInBloodMoonWindow();

            if (shouldBeActive && !_active) Begin();
            else if (!shouldBeActive && _active) End();

            if (_active && Time.time >= _nextWaveTime) SpawnWave();

            WarnIfTonight();
        }

        bool IsInBloodMoonWindow()
        {
            int every = Mathf.Max(1, _schedule.everyNDays);
            int day = _clock.Day;
            float hour = _clock.HourOfDay;

            bool tonight = day % every == 0 && hour >= _schedule.startHour;
            bool tailOfLastNight = (day - 1) % every == 0 && (day - 1) > 0 && hour < _schedule.endHour;
            return tonight || tailOfLastNight;
        }

        /// <summary>The next day number that carries a blood moon.</summary>
        public int NextBloodMoonDay()
        {
            int every = Mathf.Max(1, _schedule.everyNDays);
            int day = _clock.Day;
            if (day % every == 0 && _clock.HourOfDay < _schedule.startHour) return day;
            return ((day / every) + 1) * every;
        }

        void WarnIfTonight()
        {
            int day = _clock.Day;
            if (_warnedDay == day) return;

            int every = Mathf.Max(1, _schedule.everyNDays);
            if (day % every != 0) return;
            if (_clock.HourOfDay < 6f) return;

            _warnedDay = day;
            Notifications.Post("Blood moon tonight. Check your walls.");
        }

        void Begin()
        {
            _active = true;
            _hordeNumber++;
            _nextWaveTime = Time.time + 2f;

            if (_sky != null) _sky.BloodMoon = true;
            Notifications.Post("BLOOD MOON");
            if (BloodMoonChanged != null) BloodMoonChanged(true);
        }

        void End()
        {
            _active = false;
            if (_sky != null) _sky.BloodMoon = false;
            if (_spawner != null) _spawner.DespawnHordeUnits();

            if (_progression != null && _schedule.survivalXp > 0f)
            {
                _progression.AddXp(_schedule.survivalXp, XpSource.Kill);
            }
            Notifications.Post("Dawn. You survived the horde.");
            if (BloodMoonChanged != null) BloodMoonChanged(false);
        }

        Vector3 SiegePoint()
        {
            if (_structures != null && _structures.Claims.Any && _player != null)
            {
                var claim = _structures.Claims.Nearest(_player.position);
                if (claim != null) return claim.Centre;
            }
            return _player != null ? _player.position : Vector3.zero;
        }

        void SpawnWave()
        {
            _nextWaveTime = Time.time + _schedule.waveIntervalSeconds;
            if (_spawner == null) return;

            Vector3 siege = SiegePoint();
            int claimStructures = 0;
            if (_structures != null && _structures.Claims.Any)
            {
                claimStructures = _structures.CountInClaim(_structures.Claims.Nearest(siege));
            }

            int level = _progression != null ? _progression.Level : 1;
            int target = _schedule.WaveSize(level, claimStructures);
            int room = Mathf.Max(0, _schedule.maxAlive - _spawner.HordeUnitCount);
            int count = Mathf.Min(target, room);

            for (int i = 0; i < count; i++)
            {
                Vector3 position;
                if (!_spawner.TryFindSpawn(siege, _schedule.spawnRadiusMin, _schedule.spawnRadiusMax, out position)) continue;

                var def = PickDefinition(i, count);
                var zombie = _spawner.Spawn(def, position, true);
                if (zombie == null) continue;

                zombie.HasSiegeTarget = true;
                zombie.SiegeTarget = siege;
            }
        }

        ZombieDefinition PickDefinition(int index, int total)
        {
            if (_schedule.heavyZombie != null &&
                _hordeNumber >= _schedule.heavyFromHordeNumber &&
                index < Mathf.RoundToInt(total * _schedule.heavyShare))
            {
                return _schedule.heavyZombie;
            }
            return _schedule.baseZombie;
        }

        /// <summary>Restores the horde counter when a save is loaded.</summary>
        public void LoadState(int hordeNumber)
        {
            _hordeNumber = Mathf.Max(0, hordeNumber);
        }

        public string StatusLine
        {
            get
            {
                if (_active) return "BLOOD MOON";
                int next = NextBloodMoonDay();
                int days = next - _clock.Day;
                if (days <= 0) return "Blood moon tonight";
                return string.Format("Blood moon in {0} day{1}", days, days == 1 ? "" : "s");
            }
        }
    }
}
