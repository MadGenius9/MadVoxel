using System;
using UnityEngine;

namespace MadVoxel.Core
{
    /// <summary>
    /// Owns game time only. It knows nothing about lighting, hordes or spawning; those
    /// systems subscribe to it. Day 1 starts at <see cref="GameConfig.startHour"/>.
    /// </summary>
    public class WorldClock : MonoBehaviour
    {
        GameConfig _config;

        /// <summary>Total elapsed in-game hours since the world was created.</summary>
        public double TotalHours { get; private set; }

        public bool Paused { get; set; }

        /// <summary>1-based day counter.</summary>
        public int Day
        {
            get { return (int)(TotalHours / 24.0) + 1; }
        }

        /// <summary>Hour within the current day, 0..24.</summary>
        public float HourOfDay
        {
            get { return (float)(TotalHours - Math.Floor(TotalHours / 24.0) * 24.0); }
        }

        public float DayProgress01
        {
            get { return HourOfDay / 24f; }
        }

        public bool IsNight
        {
            get
            {
                float h = HourOfDay;
                return h < _config.dawnHour || h >= _config.duskHour;
            }
        }

        public event Action<int> DayStarted;
        public event Action<int> NightFell;

        bool _wasNight;
        int _lastDay;

        public void Init(GameConfig config, double totalHours)
        {
            _config = config;
            TotalHours = totalHours;
            _wasNight = IsNight;
            _lastDay = Day;
        }

        public void SetTotalHours(double hours)
        {
            TotalHours = hours;
            _wasNight = IsNight;
            _lastDay = Day;
        }

        /// <summary>Jump forward to the next occurrence of an hour. Used by beds.</summary>
        public void SkipToHour(float hour)
        {
            double current = TotalHours;
            double dayStart = Math.Floor(current / 24.0) * 24.0;
            double target = dayStart + hour;
            if (target <= current) target += 24.0;
            TotalHours = target;
        }

        void Update()
        {
            if (_config == null || Paused) return;

            double hoursPerSecond = 24.0 / Mathf.Max(1f, _config.dayLengthSeconds);
            TotalHours += Time.deltaTime * hoursPerSecond;

            int day = Day;
            if (day != _lastDay)
            {
                _lastDay = day;
            }

            bool night = IsNight;
            if (night != _wasNight)
            {
                _wasNight = night;
                if (night) { if (NightFell != null) NightFell(day); }
                else { if (DayStarted != null) DayStarted(day); }
            }
        }

        /// <summary>
        /// Just the time, for callers that print the day themselves.
        ///
        /// Both HUD paths used to pass <see cref="FormatClock"/> - which already says
        /// the day - into a format that says it again, so the compass read
        /// "DAY 1   Day 1  08:59".
        /// </summary>
        public string FormatTime()
        {
            float h = HourOfDay;
            int hh = Mathf.FloorToInt(h);
            int mm = Mathf.FloorToInt((h - hh) * 60f);
            return string.Format("{0:00}:{1:00}", hh, mm);
        }

        /// <summary>The day and the time together, for a line that carries nothing else.</summary>
        public string FormatClock()
        {
            return string.Format("Day {0}  {1}", Day, FormatTime());
        }
    }
}
