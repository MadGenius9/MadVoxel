using MadVoxel.Building;
using MadVoxel.Farming.Plots;
using MadVoxel.Colony;
using MadVoxel.Content;
using MadVoxel.Core;
using MadVoxel.Core.Player;
using MadVoxel.AI;
using MadVoxel.Horde;
using MadVoxel.Inventory;
using MadVoxel.Modding;
using MadVoxel.World.Terrain;
using MadVoxel.World.Weather;
using UnityEngine;
using UnityEngine.EventSystems;

namespace MadVoxel.UI
{
    public enum UiState
    {
        MainMenu,
        Playing,
        Inventory,
        Perks,
        /// <summary>The colony board. The only colony screen there is.</summary>
        Board,
        /// <summary>A trader's counter.</summary>
        Trader,
        /// <summary>A grain bin.</summary>
        Silo,
        Paused,
        Dead
    }

    /// <summary>
    /// Owns the screens and the cursor. Gameplay code never touches Unity's cursor or
    /// asks "is a menu open?" - it reads InputBridge.Enabled, which this sets.
    /// </summary>
    public class UiRoot : MonoBehaviour
    {
        public MainMenuView MainMenu { get; private set; }
        public PauseView Pause { get; private set; }
        public DeathView Death { get; private set; }
        public HudView Hud { get; private set; }
        public InventoryScreen Inventory { get; private set; }
        public PerkScreen Perks { get; private set; }
        public ColonyBoardScreen Board { get; private set; }
        public TraderScreen Trader { get; private set; }
        public SiloScreen Silo { get; private set; }

        /// <summary>The machine yard, so the bag key can open a bed. Set by the session.</summary>
        public MadVoxel.Vehicles.VehicleWorld Vehicles { get; set; }
        public DebugOverlay Debug { get; private set; }

        public UiState State { get; private set; }

        public System.Action SaveRequested;
        public System.Action QuitToMenuRequested;
        public System.Action RespawnRequested;

        GameObject _gameplayUi;

        /// <summary>One line about mods, shown on the title screen.</summary>
        public string ModSummary { get; private set; }

        public void Init()
        {
            EnsureEventSystem();

            MainMenu = gameObject.AddComponent<MainMenuView>();
            MainMenu.Init();

            Pause = gameObject.AddComponent<PauseView>();
            Pause.Init();
            Pause.ResumeRequested += () => SetState(UiState.Playing);
            Pause.SaveRequested += () => { if (SaveRequested != null) SaveRequested(); };
            Pause.QuitToMenuRequested += () => { if (QuitToMenuRequested != null) QuitToMenuRequested(); };

            Death = gameObject.AddComponent<DeathView>();
            Death.Init();
            Death.RespawnRequested += () => { if (RespawnRequested != null) RespawnRequested(); };

            SetState(UiState.MainMenu);
        }

        public void SetModSummary(System.Collections.Generic.IReadOnlyList<ModManifest> mods, ModLog log)
        {
            if (mods == null || mods.Count == 0)
            {
                ModSummary = "No mods loaded";
            }
            else
            {
                var names = new System.Text.StringBuilder();
                for (int i = 0; i < mods.Count; i++)
                {
                    if (i > 0) names.Append(", ");
                    names.Append(mods[i].DisplayName);
                }
                ModSummary = string.Format("{0} mod(s): {1}", mods.Count, names);
            }

            if (log != null && (log.ErrorCount > 0 || log.WarningCount > 0))
            {
                ModSummary += string.Format("   [{0} error(s), {1} warning(s) - see the console]",
                    log.ErrorCount, log.WarningCount);
            }

            if (MainMenu != null) MainMenu.SetModSummary(ModSummary);
        }

        static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            DontDestroyOnLoad(go);
        }

        public void CreateGameplayUi(PlayerRig player, WorldClock clock, HordeDirector horde,
                                     ContentDatabase content, TerrainWorld voxels, ChunkStreamer streamer,
                                     StructureWorld structures, SpawnDirector spawner,
                                     WeatherDirector weather, ColonyWorld colony,
                                     MadVoxel.Claim.ClaimHeatTracker heat,
                                     int seed, bool developerTools)
        {
            DestroyGameplayUi();

            _gameplayUi = new GameObject("GameplayUI");
            _gameplayUi.transform.SetParent(transform, false);

            Hud = _gameplayUi.AddComponent<HudView>();
            Hud.Init(player, clock, horde, structures, spawner, voxels, weather, content);

            Inventory = _gameplayUi.AddComponent<InventoryScreen>();
            Inventory.Init(player, content);

            Perks = _gameplayUi.AddComponent<PerkScreen>();
            Perks.Init(player, content);

            Board = _gameplayUi.AddComponent<ColonyBoardScreen>();
            Board.Init(colony);
            Board.Heat = heat;

            Trader = _gameplayUi.AddComponent<TraderScreen>();
            Trader.Init(player, content, clock);

            Silo = _gameplayUi.AddComponent<SiloScreen>();
            Silo.Init(player, content);

            Debug = _gameplayUi.AddComponent<DebugOverlay>();
            Debug.Init(player, voxels, streamer, seed, developerTools);

            StorageStructure.OpenRequested += OnStorageOpen;
            CraftStationStructure.OpenRequested += OnStationOpen;
            CampfireStructure.OpenRequested += OnStationOpen;
            ColonyBoardStructure.OpenRequested += OnBoardOpen;
            MadVoxel.Traders.TraderPost.OpenRequested += OnTraderOpen;
            SiloStructure.OpenRequested += OnSiloOpen;
            FurnaceStructure.OpenRequested += OnFurnaceOpen;
            MadVoxel.Vehicles.VehicleRig.StorageRequested += OnVehicleStorage;
        }

        public void DestroyGameplayUi()
        {
            StorageStructure.OpenRequested -= OnStorageOpen;
            CraftStationStructure.OpenRequested -= OnStationOpen;
            CampfireStructure.OpenRequested -= OnStationOpen;
            ColonyBoardStructure.OpenRequested -= OnBoardOpen;
            MadVoxel.Traders.TraderPost.OpenRequested -= OnTraderOpen;
            SiloStructure.OpenRequested -= OnSiloOpen;
            FurnaceStructure.OpenRequested -= OnFurnaceOpen;
            MadVoxel.Vehicles.VehicleRig.StorageRequested -= OnVehicleStorage;

            if (_gameplayUi != null) Destroy(_gameplayUi);
            _gameplayUi = null;
            Hud = null;
            Inventory = null;
            Perks = null;
            Board = null;
            Trader = null;
            Silo = null;
            Vehicles = null;
            Debug = null;
        }

        void OnStorageOpen(StorageStructure storage)
        {
            if (Inventory == null || State == UiState.Dead) return;
            Inventory.OpenContainer(storage);
            SetState(UiState.Inventory);
        }

        /// <summary>
        /// A furnace opens as a container and nothing else.
        ///
        /// It offered the forge's own recipes as hand work orders at first, which was
        /// free metal: those recipes charge no coal precisely because the furnace pays
        /// in burn time instead, so crafting them by hand skipped the fuel, the wait and
        /// the station. The furnace's job is to work while you are not there, and the
        /// only way to ask it to is to put ore in it.
        /// </summary>
        void OnFurnaceOpen(FurnaceStructure furnace)
        {
            if (Inventory == null || State == UiState.Dead) return;

            Inventory.OpenContainer(furnace.Contents, furnace.Structure.Definition.displayName, CraftStation.Hand);
            SetState(UiState.Inventory);
        }

        void OnVehicleStorage(MadVoxel.Vehicles.VehicleRig rig)
        {
            if (Inventory == null || State == UiState.Dead || rig.Storage == null) return;

            Inventory.OpenContainer(rig.Storage, rig.Definition.displayName + " BED", CraftStation.Hand);
            SetState(UiState.Inventory);
        }

        void OnSiloOpen(SiloStructure bin)
        {
            if (Silo == null || State == UiState.Dead) return;

            Silo.Open(bin);
            SetState(UiState.Silo);
        }

        void OnTraderOpen(MadVoxel.Traders.TraderPost post)
        {
            if (Trader == null || State == UiState.Dead) return;

            Trader.Open(post);
            SetState(UiState.Trader);
        }

        void OnBoardOpen(ColonyBoardStructure board)
        {
            if (Board == null || State == UiState.Dead) return;

            Board.Open();
            SetState(UiState.Board);
        }

        void OnStationOpen(CraftStation station)
        {
            if (Inventory == null || State == UiState.Dead) return;
            Inventory.Open(station);
            SetState(UiState.Inventory);
        }

        void Update()
        {
            if (State == UiState.MainMenu) return;

            if (InputBridge.DebugDown && Debug != null) Debug.Toggle();

            if (InputBridge.PauseDown)
            {
                if (State == UiState.Inventory || State == UiState.Perks
                    || State == UiState.Board || State == UiState.Trader || State == UiState.Silo)
                    SetState(UiState.Playing);
                else if (State == UiState.Playing) SetState(UiState.Paused);
                else if (State == UiState.Paused) SetState(UiState.Playing);
                return;
            }

            if (State == UiState.Dead) return;

            if (InputBridge.PerksDown && Perks != null)
            {
                if (State == UiState.Perks) SetState(UiState.Playing);
                else if (State == UiState.Playing) SetState(UiState.Perks);
                return;
            }

            if (InputBridge.InventoryDown)
            {
                if (State == UiState.Inventory) { SetState(UiState.Playing); return; }
                if (State != UiState.Playing) return;

                // Sitting on a machine, the bag key opens the bed beside your own bag -
                // you loaded it standing next to the thing and should not have to get
                // off to reach it again.
                var driving = Vehicles != null ? Vehicles.Driving : null;
                if (driving != null && driving.Storage != null)
                {
                    OnVehicleStorage(driving);
                    return;
                }

                Inventory.Open(CraftStation.Hand);
                SetState(UiState.Inventory);
            }
        }

        public void SetState(UiState state)
        {
            State = state;

            bool menuOpen = state != UiState.Playing;

            if (state != UiState.Inventory && Inventory != null) Inventory.Close();
            if (state != UiState.Perks && Perks != null) Perks.Close();
            if (state != UiState.Board && Board != null) Board.Close();
            if (state != UiState.Trader && Trader != null) Trader.Close();
            if (state != UiState.Silo && Silo != null) Silo.Close();
            if (state != UiState.Paused && Pause != null) Pause.Close();
            if (state != UiState.Dead && Death != null) Death.Close();
            if (state != UiState.MainMenu && MainMenu != null) MainMenu.Close();

            switch (state)
            {
                case UiState.MainMenu:
                    MainMenu.Open();
                    break;
                case UiState.Perks:
                    if (Perks != null) Perks.Open();
                    break;
                case UiState.Board:
                    if (Board != null) Board.Open();
                    break;
                case UiState.Silo:
                    if (Silo != null && Silo.Bin != null) Silo.Open(Silo.Bin);
                    break;
                case UiState.Trader:
                    // Reopened rather than built: the counter is always the one the
                    // player walked up to, and it is already held by the screen.
                    if (Trader != null && Trader.Post != null) Trader.Open(Trader.Post);
                    break;
                case UiState.Paused:
                    Pause.Open();
                    break;
            }

            if (Hud != null) Hud.SetVisible(state == UiState.Playing || state == UiState.Inventory);

            InputBridge.Enabled = state == UiState.Playing;
            Cursor.lockState = menuOpen ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = menuOpen;

            // Only the pause screen stops the world; the inventory stays live so
            // crafting during a horde night is still a risk.
            Time.timeScale = state == UiState.Paused ? 0f : 1f;
        }

        public void ShowDeath(string detail)
        {
            if (Death == null) return;
            Death.Open(detail);
            SetState(UiState.Dead);
        }
    }
}
