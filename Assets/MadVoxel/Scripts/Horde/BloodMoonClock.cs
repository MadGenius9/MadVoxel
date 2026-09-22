using UnityEngine;

namespace MadVoxel.Horde
{
    /// <summary>
    /// When the next blood moon falls, and how the HUD says so. Pure arithmetic over the
    /// world clock, kept apart from <see cref="HordeDirector"/> so it can be checked
    /// without a spawner, a sky or a player: the off-by-one here would be a night the
    /// horde arrives unannounced, which is the one bug this game cannot afford.
    /// </summary>
    public static class BloodMoonClock
    {
        /// <summary>Only the last few hours are worth a line on the visor.</summary>
        public const float WarningHours = 3f;

        /// <summary>True while the clock is inside a blood moon window.</summary>
        public static bool IsInWindow(int day, float hourOfDay, int everyNDays, float startHour, float endHour)
        {
            int every = Mathf.Max(1, everyNDays);

            bool tonight = day % every == 0 && hourOfDay >= startHour;
            bool tailOfLastNight = (day - 1) % every == 0 && (day - 1) > 0 && hourOfDay < endHour;
            return tonight || tailOfLastNight;
        }

        /// <summary>
        /// The next day number that carries a blood moon. Today still counts while the
        /// clock has not yet reached the start hour.
        /// </summary>
        public static int NextDay(int day, float hourOfDay, int everyNDays, float startHour)
        {
            int every = Mathf.Max(1, everyNDays);
            if (day % every == 0 && hourOfDay < startHour) return day;
            return ((day / every) + 1) * every;
        }

        /// <summary>In-game hours until the window opens. Never negative.</summary>
        public static float HoursUntil(int day, float hourOfDay, int everyNDays, float startHour)
        {
            int next = NextDay(day, hourOfDay, everyNDays, startHour);
            return Mathf.Max(0f, (next - day) * 24f + (startHour - hourOfDay));
        }

        /// <summary>
        /// The one line Claim Slate allows about the horde. Empty until it is close
        /// enough to act on - a countdown running all week is wallpaper, not a warning.
        /// </summary>
        public static string CountdownLine(float hoursUntil, bool active)
        {
            if (active) return "BLOOD MOON";
            if (hoursUntil > WarningHours) return "";

            int whole = Mathf.FloorToInt(hoursUntil);
            int minutes = Mathf.FloorToInt((hoursUntil - whole) * 60f);
            return string.Format("BLOOD MOON  {0:00}:{1:00}", whole, minutes);
        }

        /// <summary>The longer-range line for menus and the debug overlay.</summary>
        public static string StatusLine(int day, int nextBloodMoonDay, bool active)
        {
            if (active) return "BLOOD MOON";

            int days = nextBloodMoonDay - day;
            if (days <= 0) return "Blood moon tonight";
            return string.Format("Blood moon in {0} day{1}", days, days == 1 ? "" : "s");
        }
    }
}
