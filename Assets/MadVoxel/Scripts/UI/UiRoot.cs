using MadVoxel.Building;
using MadVoxel.Content;
using MadVoxel.Core;
using MadVoxel.Core.Player;
using MadVoxel.Horde;
using MadVoxel.Inventory;
using MadVoxel.World.Terrain;
using UnityEngine;
using UnityEngine.EventSystems;

namespace MadVoxel.UI
{
    public enum UiState
    {
        MainMenu,
        Playing,
        Inventory,
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
        public DebugOverlay Debug { get; private set; }

        public UiState State { get; private set; }

        public System.Action SaveRequested;
        public System.Action QuitToMenuRequested;
        public System.Action RespawnRequested;

        GameObject _gameplayUi;

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

        static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            DontDestroyOnLoad(go);
        }

        public void CreateGameplayUi(PlayerRig player, WorldClock clock, HordeDirector horde,
                                     ContentDatabase content, TerrainWorld voxels, ChunkStreamer streamer,
                                     int seed, bool developerTools)
        {
            DestroyGameplayUi();

            _gameplayUi = new GameObject("GameplayUI");
            _gameplayUi.transform.SetParent(transform, false);

            Hud = _gameplayUi.AddComponent<HudView>();
            Hud.Init(player, clock, horde);

            Inventory = _gameplayUi.AddComponent<InventoryScreen>();
            Inventory.Init(player, content);

            Debug = _gameplayUi.AddComponent<DebugOverlay>();
            Debug.Init(player, voxels, streamer, seed, developerTools);

            StorageStructure.OpenRequested += OnStorageOpen;
            CraftStationStructure.OpenRequested += OnStationOpen;
            CampfireStructure.OpenRequested += OnStationOpen;
        }

        public void DestroyGameplayUi()
        {
            StorageStructure.OpenRequested -= OnStorageOpen;
            CraftStationStructure.OpenRequested -= OnStationOpen;
            CampfireStructure.OpenRequested -= OnStationOpen;

            if (_gameplayUi != null) Destroy(_gameplayUi);
            _gameplayUi = null;
            Hud = null;
            Inventory = null;
            Debug = null;
        }

        void OnStorageOpen(StorageStructure storage)
        {
            if (Inventory == null || State == UiState.Dead) return;
            Inventory.OpenContainer(storage);
            SetState(UiState.Inventory);
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
                if (State == UiState.Inventory) SetState(UiState.Playing);
                else if (State == UiState.Playing) SetState(UiState.Paused);
                else if (State == UiState.Paused) SetState(UiState.Playing);
                return;
            }

            if (State == UiState.Dead) return;

            if (InputBridge.InventoryDown)
            {
                if (State == UiState.Inventory) SetState(UiState.Playing);
                else if (State == UiState.Playing)
                {
                    Inventory.Open(CraftStation.Hand);
                    SetState(UiState.Inventory);
                }
            }
        }

        public void SetState(UiState state)
        {
            State = state;

            bool menuOpen = state != UiState.Playing;

            if (state != UiState.Inventory && Inventory != null) Inventory.Close();
            if (state != UiState.Paused && Pause != null) Pause.Close();
            if (state != UiState.Dead && Death != null) Death.Close();
            if (state != UiState.MainMenu && MainMenu != null) MainMenu.Close();

            switch (state)
            {
                case UiState.MainMenu:
                    MainMenu.Open();
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
