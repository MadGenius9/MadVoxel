using MadVoxel.AI;
using MadVoxel.Content;
using MadVoxel.Core.Player;
using MadVoxel.Horde;
using MadVoxel.World.Voxel;
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
            "F4 fly   F5 +1h   F6 dawn   F7 blood moon   F8 spawn zombie\n" +
            "F9 refill   F10 test kit   F11 invulnerable";

        ContentDatabase _content;
        WorldClock _clock;
        HordeSchedule _schedule;
        HordeDirector _horde;
        SpawnDirector _spawner;
        VoxelWorld _voxels;
        PlayerRig _player;

        public void Init(ContentDatabase content, WorldClock clock, HordeDirector horde, HordeSchedule schedule,
                         SpawnDirector spawner, VoxelWorld voxels, PlayerRig player)
        {
            _content = content;
            _clock = clock;
            _horde = horde;
            _schedule = schedule;
            _spawner = spawner;
            _voxels = voxels;
            _player = player;
        }

        void Update()
        {
            if (_player == null) return;

            if (Input.GetKeyDown(KeyCode.F4)) ToggleFly();
            if (Input.GetKeyDown(KeyCode.F5)) SkipHours(1f);
            if (Input.GetKeyDown(KeyCode.F6)) SkipToDawn();
            if (Input.GetKeyDown(KeyCode.F7)) JumpToBloodMoon();
            if (Input.GetKeyDown(KeyCode.F8)) SpawnZombieAhead();
            if (Input.GetKeyDown(KeyCode.F9)) Refill();
            if (Input.GetKeyDown(KeyCode.F10)) GiveTestKit();
            if (Input.GetKeyDown(KeyCode.F11)) ToggleInvulnerable();
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

            Give(ItemIds.PieceDoor, 4);
            Give(ItemIds.PieceLadder, 8);
            Give(ItemIds.PieceStorageBox, 4);
            Give(ItemIds.PieceWorkbench, 2);
            Give(ItemIds.PieceCampfire, 2);
            Give(ItemIds.PieceClaimStake, 1);
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
