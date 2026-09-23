using System;
using MadVoxel.AI;
using MadVoxel.Building;
using MadVoxel.Claim;
using MadVoxel.Core;
using MadVoxel.Perks;
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
        BuildingWorld _buildings;
        SkyController _sky;
        PlayerProgression _progression;
        Transform _player;

        bool _active;
        int _warnedDay = -1;
        float _nextWaveTime;
        int _hordeNumber;

        /// <summary>
        /// How loud the claim is. A bigger, brighter, noisier farm pulls a bigger
        /// horde - which is the whole reason a blackout switch is worth a slot.
        /// </summary>
        public ClaimHeatTracker Heat { get; set; }

        public bool IsBloodMoonActive { get { return _active; } }
        public int HordeNumber { get { return _hordeNumber; } }

        public event Action<bool> BloodMoonChanged;

        /// <summary>The terrain, so a wave can tell a ramp from a wall. Set by the session.</summary>
        public MadVoxel.World.Terrain.TerrainWorld Voxels { get; set; }

        readonly System.Collections.Generic.List<SiegeLane> _lanes = new System.Collections.Generic.List<SiegeLane>();

        /// <summary>The ways in the last wave found, for the readout and the tests.</summary>
        public System.Collections.Generic.IReadOnlyList<SiegeLane> Lanes { get { return _lanes; } }

        /// <summary>
        /// True when the last wave found a way in that needs no chewing. Worth telling
        /// the player: you can hear a horde walking in through a door you left open,
        /// and there is no satisfying version of finding that out at dawn.
        /// </summary>
        public bool IsBreached { get { return _lanes.Count > 0 && SiegeApproach.HasOpenLane(_lanes); } }

        public void Init(HordeSchedule schedule, WorldClock clock, SpawnDirector spawner,
                         StructureWorld structures, BuildingWorld buildings, SkyController sky,
                         PlayerProgression progression, Transform player)
        {
            _schedule = schedule;
            _clock = clock;
            _spawner = spawner;
            _structures = structures;
            _buildings = buildings;
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
            return BloodMoonClock.IsInWindow(_clock.Day, _clock.HourOfDay,
                _schedule.everyNDays, _schedule.startHour, _schedule.endHour);
        }

        /// <summary>The next day number that carries a blood moon.</summary>
        public int NextBloodMoonDay()
        {
            return BloodMoonClock.NextDay(_clock.Day, _clock.HourOfDay,
                _schedule.everyNDays, _schedule.startHour);
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
            // Base footprint drives the budget: deployables inside the cupboard radius
            // plus every snap piece near it. A bigger base pulls a bigger horde.
            int baseSize = 0;
            if (_structures != null && _structures.Claims.Any)
            {
                var claim = _structures.Claims.Nearest(siege);
                baseSize += _structures.CountInClaim(claim);
                if (_buildings != null) baseSize += _buildings.CountNear(claim.Centre, claim.Radius);
            }
            else if (_buildings != null)
            {
                baseSize += _buildings.CountNear(siege, 30f);
            }

            // Where they come from, before how many. A wall that moves the pressure
            // somewhere else is the whole point of building one.
            PlanApproach(siege);

            int level = _progression != null ? _progression.Level : 1;
            int target = _schedule.WaveSize(level, baseSize);
            if (Heat != null) target += ClaimHeatMath.HordeBonus(Heat.Heat, target);
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
                zombie.SiegeTarget = LaneTargetFor(siege);
                zombie.SiegeCentre = siege;
            }
        }

        // ------------------------------------------------------------------ approach

        /// <summary>
        /// Samples the ring around the base and scores each way in by what stands
        /// between it and the middle.
        /// </summary>
        void PlanApproach(Vector3 siege)
        {
            float radius = 26f;
            if (_structures != null && _structures.Claims.Any)
            {
                var claim = _structures.Claims.Nearest(siege);
                if (claim != null) radius = Mathf.Max(8f, claim.Radius);
            }

            SiegeApproach.Plan(siege, radius, SiegeApproach.DefaultLanes, CountBlocking, _lanes);
        }

        /// <summary>
        /// Walks a metre at a time from outside the base to the middle, counting the
        /// steps where something solid stands at chest height.
        ///
        /// Chest height rather than the ground is deliberate: a trench the player dug
        /// is not an obstacle to a zombie that can drop into it, but the wall they
        /// raised is. What this measures is "could I walk this line", which is the
        /// question a horde actually has.
        /// </summary>
        int CountBlocking(Vector3 from, Vector3 to)
        {
            if (Voxels == null) return 0;

            Vector3 run = to - from;
            run.y = 0f;

            float distance = run.magnitude;
            if (distance < 1f) return 0;

            Vector3 step = run / distance;
            int blocked = 0;

            for (float travelled = 0f; travelled < distance; travelled += 1f)
            {
                Vector3 at = from + step * travelled;
                int ground = Voxels.GetSurfaceY(Mathf.FloorToInt(at.x), Mathf.FloorToInt(at.z));

                // One block above the surface is where a body would be. Solid there and
                // the line is walled, not merely uneven.
                if (Voxels.IsSolid(Mathf.FloorToInt(at.x), ground + 1, Mathf.FloorToInt(at.z))) blocked++;
            }

            return blocked;
        }

        /// <summary>
        /// A point on the ring for one zombie to head for, chosen by lane weight. They
        /// spread across the ways in rather than all piling onto the same wall.
        /// </summary>
        Vector3 LaneTargetFor(Vector3 fallback)
        {
            int index = SiegeApproach.Pick(_lanes, UnityEngine.Random.value);
            return index >= 0 ? _lanes[index].Point : fallback;
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

        /// <summary>
        /// In-game hours until the blood moon window opens. Negative while one is
        /// running, so the HUD can tell "soon" from "now" without a second call.
        /// </summary>
        public float HoursUntilBloodMoon
        {
            get
            {
                if (_schedule == null || _clock == null) return float.MaxValue;
                if (_active) return -1f;

                return BloodMoonClock.HoursUntil(_clock.Day, _clock.HourOfDay,
                    _schedule.everyNDays, _schedule.startHour);
            }
        }

        /// <summary>
        /// The one line the HUD is allowed to show about the horde: "BLOOD MOON  00:18",
        /// counting down in game time. Empty until it is close enough to matter.
        /// </summary>
        public string CountdownLine
        {
            get
            {
                if (_schedule == null || _clock == null) return "";
                return BloodMoonClock.CountdownLine(HoursUntilBloodMoon, _active);
            }
        }

        public string StatusLine
        {
            get
            {
                if (_schedule == null || _clock == null) return "";
                return BloodMoonClock.StatusLine(_clock.Day, NextBloodMoonDay(), _active);
            }
        }
    }
}
