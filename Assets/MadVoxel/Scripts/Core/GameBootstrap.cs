using System.Collections.Generic;
using MadVoxel.Content;
using MadVoxel.Modding;
using MadVoxel.UI;
using UnityEngine;

namespace MadVoxel.Core
{
    /// <summary>
    /// The one component the scene needs. It loads content, builds the UI root and the
    /// session, and hands control to the title screen.
    /// </summary>
    public class GameBootstrap : MonoBehaviour
    {
        [Tooltip("Leave empty to load Resources/MadVoxel/ContentDatabase, or fall back to the code-defined content.")]
        public ContentDatabase contentOverride;

        [Tooltip("Load mod folders from the Mods directory at startup. Turn off to play pure vanilla.")]
        public bool loadMods = true;

        [Tooltip("Test-session hotkeys (fly, skip time, force a blood moon, grant a kit). Turn this off for a release build.")]
        public bool developerTools = true;

        [Tooltip("Skip the title screen and drop straight into a scratch world. Handy while iterating.")]
        public bool quickStart;
        public string quickStartWorld = "Playtest";
        public int quickStartSeed = 133742;

        ContentDatabase _content;
        UiRoot _ui;
        GameSession _session;

        /// <summary>Mods that loaded this session, in load order.</summary>
        public IReadOnlyList<ModManifest> LoadedMods { get; private set; }
        public ModLog ModLog { get; private set; }

        void Awake()
        {
            Application.targetFrameRate = -1;
            QualitySettings.vSyncCount = 1;

            _content = contentOverride != null ? contentOverride : ContentDatabase.LoadOrBuild();
            _content.Build();

            // Mods layer on top of the base content before anything reads it, so every
            // system downstream sees one merged database and needs no mod awareness.
            ModLog = new ModLog();
            LoadedMods = loadMods
                ? ModLoader.LoadAll(_content, ModLog)
                : new List<ModManifest>();
            ModLog.Flush();
            _content.Build();

            var uiGo = new GameObject("UI");
            uiGo.transform.SetParent(transform, false);
            _ui = uiGo.AddComponent<UiRoot>();
            _ui.Init();
            _ui.SetModSummary(LoadedMods, ModLog);

            var sessionGo = new GameObject("Session");
            sessionGo.transform.SetParent(transform, false);
            _session = sessionGo.AddComponent<GameSession>();
            _session.Init(_content, _ui);
            _session.ActiveMods = LoadedMods;
            _session.DeveloperToolsEnabled = developerTools;

            _ui.MainMenu.NewWorldRequested += _session.StartNewWorld;
            _ui.MainMenu.LoadWorldRequested += _session.LoadWorld;
            _ui.MainMenu.QuitRequested += Quit;
        }

        void Start()
        {
            if (!quickStart) return;

            if (Save.SavePaths.WorldExists(quickStartWorld)) _session.LoadWorld(quickStartWorld);
            else _session.StartNewWorld(quickStartWorld, quickStartSeed);
        }

        static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
