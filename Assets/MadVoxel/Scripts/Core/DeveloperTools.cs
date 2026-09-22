using MadVoxel.AI;
using MadVoxel.Content;
using MadVoxel.Core.Player;
using MadVoxel.Horde;
using MadVoxel.Claim;
using MadVoxel.Colony;
using MadVoxel.Perks;
using MadVoxel.World.Weather;
using MadVoxel.World.Terrain;
using UnityEngine;

namespace MadVoxel.Core
{
    /// <summary>
    /// Test-session shortcuts. A blood moon falls on day 7 at 22:00, which is over two
    /// hours of real play away, so verifying the Phase 0 loop without these is
    /// impractical. Disabled by the <c>developerTools</c> flag on GameBootstrap.
    ///
    /// Deliberately reads UnityEngine.Input directly rather than going through
    /// InputBridge, so the keys keep working while a menu has gameplay input suppressed.
    /// </summary>
    public class DeveloperTools : MonoBehaviour
    {
        public const string KeyHelp =
            "F1 utility kit   F2 +1 level   F4 fly   F5 +1h   F6 dawn   F7 blood moon\n" +
            "F8 spawn zombie   F9 refill   F10 test kit   F11 invulnerable   F12 ripen crops\n" +
            "Shift+F5 cycle weather   Shift+F6 found colony + recruit   Shift+F7 heat +25";

        ContentDatabase _content;
        WorldClock _clock;
        HordeSchedule _schedule;
        HordeDirector _horde;
        SpawnDirector _spawner;
        MadVoxel.Building.StructureWorld _structures;
        TerrainWorld _voxels;
        PlayerRig _player;

        public void Init(ContentDatabase content, WorldClock clock, HordeDirector horde, HordeSchedule schedule,
                         SpawnDirector spawner, MadVoxel.Building.StructureWorld structures,
                         TerrainWorld voxels, PlayerRig player)
        {
            _content = content;
            _clock = clock;
            _horde = horde;
            _schedule = schedule;
            _spawner = spawner;
            _structures = structures;
            _voxels = voxels;
            _player = player;
        }

        void Update()
        {
            if (_player == null) return;

            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

            if (Input.GetKeyDown(KeyCode.F1)) GiveUtilityKit();
            if (Input.GetKeyDown(KeyCode.F2)) GrantLevel();
            if (shift && Input.GetKeyDown(KeyCode.F5)) { CycleWeather(); return; }
            if (shift && Input.GetKeyDown(KeyCode.F6)) { FoundAndRecruit(); return; }
            if (shift && Input.GetKeyDown(KeyCode.F7)) { StokeHeat(); return; }
            if (Input.GetKeyDown(KeyCode.F4)) ToggleFly();
            if (Input.GetKeyDown(KeyCode.F5)) SkipHours(1f);
            if (Input.GetKeyDown(KeyCode.F6)) SkipToDawn();
            if (Input.GetKeyDown(KeyCode.F7)) JumpToBloodMoon();
            if (Input.GetKeyDown(KeyCode.F8)) SpawnZombieAhead();
            if (Input.GetKeyDown(KeyCode.F9)) Refill();
            if (Input.GetKeyDown(KeyCode.F10)) GiveTestKit();
            if (Input.GetKeyDown(KeyCode.F11)) ToggleInvulnerable();
            if (Input.GetKeyDown(KeyCode.F12)) RipenCrops();
        }

        void ToggleFly()
        {
            var motor = _player.Motor;
            motor.FlyMode = !motor.FlyMode;
            Notifications.Post(motor.FlyMode ? "Fly mode ON (Space up, Ctrl down)" : "Fly mode OFF");
        }

        void SkipHours(float hours)
        {
            _clock.SetTotalHours(_clock.TotalHours + hours);
            Notifications.Post("Clock: " + _clock.FormatClock());
        }

        void SkipToDawn()
        {
            _clock.SkipToHour(_content.config.dawnHour + 0.25f);
            Notifications.Post("Skipped to dawn: " + _clock.FormatClock());
        }

        /// <summary>Winds the calendar to ten minutes before the next blood moon.</summary>
        void JumpToBloodMoon()
        {
            if (_horde == null || _schedule == null) return;

            int day = _horde.NextBloodMoonDay();
            double target = (day - 1) * 24.0 + _schedule.startHour - 0.17; // ~10 in-game minutes of grace

            if (target <= _clock.TotalHours)
            {
                // Already inside tonight's window, so aim at the following one.
                int every = Mathf.Max(1, _schedule.everyNDays);
                target += every * 24.0;
            }

            _clock.SetTotalHours(target);
            Notifications.PostFormat("Blood moon imminent - day {0}, {1}", _clock.Day, _clock.FormatClock());
        }

        void SpawnZombieAhead()
        {
            if (_spawner == null) return;

            var definition = _content.Zombie(ZombieIds.Shambler);
            if (definition == null) return;

            Vector3 ahead = _player.transform.position + _player.transform.forward * 6f;
            int surface = _voxels.GetSurfaceY(Mathf.FloorToInt(ahead.x), Mathf.FloorToInt(ahead.z));
            var spot = new Vector3(ahead.x, Mathf.Max(surface + 1.05f, _player.transform.position.y), ahead.z);

            _spawner.Spawn(definition, spot, false);
            Notifications.Post("Spawned a shambler in front of you");
        }

        /// <summary>
        /// Crops take in-game days. Rather than a separate time skip that also brings on
        /// night, this back-dates every planting so the whole garden is ready now.
        /// </summary>
        void RipenCrops()
        {
            if (_structures == null) return;

            var all = _structures.All;
            int ripened = 0;

            for (int i = 0; i < all.Count; i++)
            {
                if (all[i] == null) continue;
                var plot = all[i].GetComponent<MadVoxel.Farming.Plots.FarmPlotStructure>();
                if (plot == null || plot.Crop == null) continue;

                plot.RestoreCrop(plot.Crop, _clock.TotalHours - plot.Crop.HoursToMature - 1f);
                ripened++;
            }

            Notifications.PostFormat(ripened > 0 ? "Ripened {0} plot(s)" : "No planted plots to ripen", ripened);
        }

        void Refill()
        {
            _player.Stats.ResetToFull();
            Notifications.Post("Vitals refilled");
        }

        void ToggleInvulnerable()
        {
            var stats = _player.Stats;
            stats.Invulnerable = !stats.Invulnerable;
            Notifications.Post(stats.Invulnerable ? "Invulnerable ON" : "Invulnerable OFF");
        }

        /// <summary>The grid, the plumbing and the colony. Set by the session.</summary>
        public WeatherDirector Weather { get; set; }
        public ColonyWorld Colony { get; set; }
        public ClaimHeatTracker Heat { get; set; }

        /// <summary>
        /// Everything needed to wire a shack and plumb a well without first grinding
        /// thirty scrap. This is the kit the power and water success tests want.
        /// </summary>
        void GiveUtilityKit()
        {
            Give(ItemIds.WireTool, 1);
            Give(ItemIds.CopperWire, 40);

            Give(ItemIds.PieceGeneratorBank, 1);
            Give(ItemIds.PieceBatteryBank, 1);
            Give(ItemIds.PieceSolarBank, 2);
            Give(ItemIds.PieceRelay, 4);
            Give(ItemIds.PieceSwitch, 2);
            Give(ItemIds.PieceSplitter, 2);
            Give(ItemIds.PieceLight, 6);
            Give(ItemIds.PieceFridge, 1);
            Give(ItemIds.PieceBladeTrap, 2);
            Give(ItemIds.PieceFencePost, 4);

            Give(ItemIds.PieceWaterPump, 1);
            Give(ItemIds.PiecePipe, 16);
            Give(ItemIds.PieceWaterTank, 1);
            Give(ItemIds.PieceWaterBarrel, 2);
            Give(ItemIds.PieceTap, 2);
            Give(ItemIds.PieceSprinkler, 3);

            Give(ItemIds.PieceColonyBoard, 1);
            Give(ItemIds.GasCan, 8);

            Notifications.Post("Utility kit granted - dig to the water table for a well");
        }

        /// <summary>Steps the sky on, so a drought or a storm can be seen on demand.</summary>
        void CycleWeather()
        {
            if (Weather == null || _content == null || _content.weather == null) return;

            var states = _content.weather.states;
            int index = 0;
            for (int i = 0; i < states.Count; i++)
            {
                if (states[i] != null && states[i].kind == Weather.Kind) { index = i; break; }
            }

            var next = states[(index + 1) % states.Count];
            Weather.Force(next.kind, Mathf.Max(3f, next.minHours));

            Notifications.PostFormat("Weather: {0}",
                string.IsNullOrEmpty(next.clockWord) ? next.kind.ToString().ToUpperInvariant() : next.clockWord);
        }

        /// <summary>
        /// Founds the colony and takes in one person, so the board can be exercised
        /// without first waiting on a trader quest that does not exist yet.
        /// </summary>
        void FoundAndRecruit()
        {
            if (Colony == null) return;

            if (!Colony.Founded)
            {
                var result = Colony.TryFound("Mad Colony");
                if (result != FoundResult.Ok)
                {
                    Notifications.Post(ColonyCharter.Describe(result));
                    return;
                }
            }

            if (Colony.TryRecruit() == null) Notifications.Post("No room for another colonist");
        }

        void StokeHeat()
        {
            if (Heat == null) return;

            Heat.AddBurst(25f);
            Notifications.Post(Heat.Readout());
        }

        /// <summary>One level's worth of XP, so the skills screen can be exercised at once.</summary>
        void GrantLevel()
        {
            var progression = _player != null ? _player.Progression : null;
            if (progression == null) return;

            progression.AddXp(progression.XpToNext - progression.Xp + 1f, XpSource.Discovery);
            Notifications.PostFormat("Level {0}  -  {1} point(s) to spend (press P)",
                progression.Level, progression.UnspentPerkPoints);
        }

        /// <summary>
        /// Everything needed to walk the Phase 0 success test without first grinding
        /// the materials: tools, building blocks, and one of every snap piece.
        /// </summary>
        void GiveTestKit()
        {
            Give(ItemIds.IronPickaxe, 1);
            Give(ItemIds.IronAxe, 1);
            Give(ItemIds.StoneShovel, 1);
            Give(ItemIds.Club, 1);
            Give(ItemIds.Wrench, 1);

            Give(ItemIds.BlockWoodFrame, 128);
            Give(ItemIds.BlockCobblestone, 128);
            Give(ItemIds.BlockIron, 64);

            // The full snap set, so the whole shack can go up without grinding planks.
            Give(ItemIds.SnapFoundation, 24);
            Give(ItemIds.SnapFloor, 24);
            Give(ItemIds.SnapWall, 32);
            Give(ItemIds.SnapDoorway, 6);
            Give(ItemIds.SnapDoor, 6);
            Give(ItemIds.SnapWindowWall, 8);
            Give(ItemIds.SnapHalfWall, 8);
            Give(ItemIds.SnapStairs, 6);
            Give(ItemIds.SnapRoof, 16);
            Give(ItemIds.SnapLadder, 6);
            Give(ItemIds.SnapHatch, 4);
            Give(ItemIds.SnapFence, 24);

            Give(ItemIds.Hammer, 1);
            Give(ItemIds.Hoe, 1);
            Give(ItemIds.PieceStorageBox, 4);
            Give(ItemIds.PieceWorkbench, 2);
            Give(ItemIds.PieceCampfire, 2);
            Give(ItemIds.PieceToolCupboard, 1);

            // The garden, ready to plant.
            Give(ItemIds.PieceFarmPlot, 12);
            Give(ItemIds.SeedPotato, 16);
            Give(ItemIds.SeedCorn, 16);
            Give(ItemIds.SeedWheat, 16);
            Give(ItemIds.PieceBedroll, 1);

            Give(ItemIds.CannedFood, 10);
            Give(ItemIds.WaterBottle, 10);
            Give(ItemIds.Bandage, 10);

            Notifications.Post("Test kit granted (check your bag)");
        }

        void Give(string itemId, int count)
        {
            var item = _content.Item(itemId);
            if (item == null)
            {
                Debug.LogWarningFormat("DeveloperTools: unknown item '{0}'.", itemId);
                return;
            }
            _player.Inventory.Bag.Add(item, count);
        }
    }
}
