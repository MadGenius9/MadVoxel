using MadVoxel.Horde;
using MadVoxel.UI;
using UnityEngine;

namespace MadVoxel.Headless
{
    /// <summary>
    /// The parts of Claim Slate that can be wrong without anyone seeing it: the palette
    /// the whole look is specified against, the compass bearings, and the blood-moon
    /// countdown. Layout and feel still need an editor and a person.
    /// </summary>
    public static class UiTests
    {
        public static void Run()
        {
            Palette();
            Compass();
            BloodMoon();
        }

        // ----------------------------------------------------------------- palette

        static void Palette()
        {
            Harness.Section("claim slate: palette");

            // The six colours are a specification, not a preference. Pinning them here
            // means a stray tweak in one screen cannot quietly drift the whole look.
            HexIs(ClaimSlate.OilBlack, 0x0C, 0x0B, 0x0A, "oil black is #0C0B0A");
            HexIs(ClaimSlate.Bone, 0xE6, 0xE0, 0xD6, "bone is #E6E0D6");
            HexIs(ClaimSlate.OxideRust, 0xB8, 0x5A, 0x32, "oxide rust is #B85A32");
            HexIs(ClaimSlate.SodiumGold, 0xD4, 0xA0, 0x17, "sodium gold is #D4A017");
            HexIs(ClaimSlate.CropSage, 0x6B, 0x7A, 0x4A, "crop sage is #6B7A4A");
            HexIs(ClaimSlate.Blood, 0x8B, 0x1E, 0x1E, "blood is #8B1E1E");

            Harness.Equal(ClaimSlate.OilBlack.a, 1f, "palette colours are opaque by default");

            // Blood is reserved. If it ever equals another palette entry the reservation
            // has stopped meaning anything.
            Harness.Check(ClaimSlate.Blood != ClaimSlate.OxideRust, "blood and rust are distinct colours");

            // Health has to cross into blood before it is too late to react, and must not
            // sit there the whole game.
            Harness.Check(ClaimSlate.VitalColour(1f) == ClaimSlate.Bone, "full health is bone, not green");
            Harness.Check(ClaimSlate.VitalColour(0.6f) == ClaimSlate.Bone, "a scratch is still bone");
            Harness.Check(ClaimSlate.VitalColour(0.4f) == ClaimSlate.OxideRust, "hurt reads as rust");
            Harness.Check(ClaimSlate.VitalColour(0.2f) == ClaimSlate.Blood, "critical reads as blood");
            Harness.Check(ClaimSlate.VitalColour(0f) == ClaimSlate.Blood, "dead-on-its-feet is blood");

            var dim = ClaimSlate.Dim(ClaimSlate.Bone, 0.5f);
            Harness.Check(Mathf.Abs(dim.r - ClaimSlate.Bone.r * 0.5f) < 0.0001f, "Dim scales the channels");
            Harness.Equal(dim.a, ClaimSlate.Bone.a, "Dim leaves alpha alone");

            var faded = ClaimSlate.Fade(ClaimSlate.OxideRust, 0.25f);
            Harness.Equal(faded.a, 0.25f, "Fade sets alpha");
            Harness.Equal(faded.r, ClaimSlate.OxideRust.r, "Fade leaves the hue alone");

            // Grease pencil must read as unbought next to bone, or the perk screen's
            // welded/penciled distinction collapses.
            Harness.Check(ClaimSlate.Pencil.a < ClaimSlate.Bone.a, "pencil is fainter than bone");
        }

        static void HexIs(Color colour, int r, int g, int b, string message)
        {
            bool ok = Mathf.RoundToInt(colour.r * 255f) == r
                   && Mathf.RoundToInt(colour.g * 255f) == g
                   && Mathf.RoundToInt(colour.b * 255f) == b;

            Harness.Check(ok, message + (ok ? "" : string.Format(" (got {0:X2}{1:X2}{2:X2})",
                Mathf.RoundToInt(colour.r * 255f), Mathf.RoundToInt(colour.g * 255f), Mathf.RoundToInt(colour.b * 255f))));
        }

        // ----------------------------------------------------------------- compass

        static void Compass()
        {
            Harness.Section("claim slate: compass");

            var origin = new Vector3(100f, 12f, -40f);

            // Unity yaw: +Z is north and 0 degrees, +X is east and 90.
            Near(CompassMath.Bearing(origin, origin + new Vector3(0f, 0f, 10f)), 0f, "north is 0");
            Near(CompassMath.Bearing(origin, origin + new Vector3(10f, 0f, 0f)), 90f, "east is 90");
            Near(CompassMath.Bearing(origin, origin + new Vector3(0f, 0f, -10f)), 180f, "south is 180");
            Near(CompassMath.Bearing(origin, origin + new Vector3(-10f, 0f, 0f)), -90f, "west is -90");
            Near(CompassMath.Bearing(origin, origin + new Vector3(10f, 0f, 10f)), 45f, "north-east is 45");

            // Height must not tilt a bearing: a trader at the bottom of a quarry is still
            // due east of you.
            Near(CompassMath.Bearing(origin, origin + new Vector3(10f, -60f, 0f)), 90f,
                "a marker far below is still due east");
            Harness.Equal(CompassMath.Bearing(origin, origin), 0f, "a marker on top of you does not spin");

            Near(CompassMath.Offset(350f, 10f), 20f, "the offset wraps across north");
            Near(CompassMath.Offset(10f, 350f), -20f, "and wraps back the other way");
            Near(CompassMath.Offset(90f, 90f), 0f, "dead ahead is zero");

            // The tape: 760 px showing 120 degrees.
            const float Visible = 120f;
            const float PerDegree = 760f / 120f;

            float x;
            Harness.Check(CompassMath.TryPlace(0f, 0f, Visible, PerDegree, out x), "straight ahead is on the tape");
            Near(x, 0f, "and sits at the centre notch");

            Harness.Check(CompassMath.TryPlace(0f, 60f, Visible, PerDegree, out x), "the arc edge is still on the tape");
            Near(x, 380f, "and lands at the right-hand end");

            Harness.Check(!CompassMath.TryPlace(0f, 61f, Visible, PerDegree, out x),
                "a bearing past the arc is dropped, not pinned to the edge");
            Harness.Check(!CompassMath.TryPlace(0f, 180f, Visible, PerDegree, out x),
                "something behind you never shows on the tape");

            // Turning right must move the world left past the notch.
            CompassMath.TryPlace(0f, 20f, Visible, PerDegree, out x);
            float before = x;
            CompassMath.TryPlace(10f, 20f, Visible, PerDegree, out x);
            Harness.Check(x < before, "turning towards a pip walks it back to the centre");
        }

        static void Near(float actual, float expected, string message)
        {
            bool ok = Mathf.Abs(actual - expected) < 0.01f;
            Harness.Check(ok, message + (ok ? "" : string.Format(" (expected {0}, got {1})", expected, actual)));
        }

        // -------------------------------------------------------------- blood moon

        static void BloodMoon()
        {
            Harness.Section("claim slate: blood moon countdown");

            const int Every = 7;
            const float Start = 22f;
            const float End = 4f;

            // Day 7 at 22:00 is the first horde; the schedule must agree with the README.
            Harness.Equal(BloodMoonClock.NextDay(1, 8f, Every, Start), 7, "the first blood moon is day 7");
            Harness.Equal(BloodMoonClock.NextDay(7, 8f, Every, Start), 7, "on the day itself it is still today");
            Harness.Equal(BloodMoonClock.NextDay(7, 23f, Every, Start), 14,
                "once tonight's has begun the next one is a week out");
            Harness.Equal(BloodMoonClock.NextDay(8, 1f, Every, Start), 14, "the morning after points at day 14");

            Harness.Check(!BloodMoonClock.IsInWindow(7, 21.9f, Every, Start, End), "a minute early is not a horde night");
            Harness.Check(BloodMoonClock.IsInWindow(7, 22f, Every, Start, End), "22:00 on day 7 opens the window");
            Harness.Check(BloodMoonClock.IsInWindow(8, 3f, Every, Start, End), "the window runs past midnight into day 8");
            Harness.Check(!BloodMoonClock.IsInWindow(8, 4f, Every, Start, End), "and closes at the end hour");
            Harness.Check(!BloodMoonClock.IsInWindow(1, 2f, Every, Start, End),
                "day 1 before dawn is not the tail of a day-0 horde");

            Near(BloodMoonClock.HoursUntil(7, 20f, Every, Start), 2f, "two hours out reads as two hours");
            Near(BloodMoonClock.HoursUntil(6, 22f, Every, Start), 24f, "a day out reads as 24 hours");
            Harness.Check(BloodMoonClock.HoursUntil(7, 23f, Every, Start) > 160f,
                "just after it starts, the next one is a week away");

            // The line only appears when it is worth acting on.
            Harness.Equal(BloodMoonClock.CountdownLine(8f, false), "", "a day out says nothing");
            Harness.Equal(BloodMoonClock.CountdownLine(4f, false), "", "four hours out still says nothing");
            Harness.Equal(BloodMoonClock.CountdownLine(0.3f, false), "BLOOD MOON  00:18", "eighteen minutes out is the one line");
            Harness.Equal(BloodMoonClock.CountdownLine(2.5f, false), "BLOOD MOON  02:30", "and pads the hours");
            Harness.Equal(BloodMoonClock.CountdownLine(-1f, true), "BLOOD MOON", "while it runs there is no countdown");
            Harness.Equal(BloodMoonClock.CountdownLine(0f, false), "BLOOD MOON  00:00", "zero is a real reading, not empty");

            Harness.Equal(BloodMoonClock.StatusLine(5, 7, false), "Blood moon in 2 days", "the menu line pluralises");
            Harness.Equal(BloodMoonClock.StatusLine(6, 7, false), "Blood moon in 1 day", "and does not say '1 days'");
            Harness.Equal(BloodMoonClock.StatusLine(7, 7, false), "Blood moon tonight", "the day itself says tonight");
            Harness.Equal(BloodMoonClock.StatusLine(7, 14, true), "BLOOD MOON", "and an active night overrides it");
        }
    }
}
