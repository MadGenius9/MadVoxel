using System;
using System.Collections.Generic;
using MadVoxel.Save;
using UnityEngine;
using UnityEngine.UI;

namespace MadVoxel.UI
{
    /// <summary>Title screen: start a new seeded world or continue a saved one.</summary>
    public class MainMenuView : MonoBehaviour
    {
        Canvas _canvas;
        InputField _nameField;
        InputField _seedField;
        RectTransform _worldList;
        Text _modLine;
        readonly List<GameObject> _worldButtons = new List<GameObject>();

        public event Action<string, int> NewWorldRequested;
        public event Action<string> LoadWorldRequested;
        public event Action QuitRequested;

        public bool IsOpen { get { return _canvas != null && _canvas.enabled; } }

        public void Init()
        {
            _canvas = UIKit.CreateCanvas("MainMenu", 40, transform);

            var backdrop = ClaimSlate.Surface(_canvas.transform, "Backdrop", ClaimSlate.OilBlack);
            ClaimSlate.Stretch(backdrop.rectTransform);

            var title = ClaimSlate.Stencil(_canvas.transform, "Title", "MADVOXEL", 92, TextAnchor.UpperCenter, ClaimSlate.Bone);
            ClaimSlate.Place(ClaimSlate.Holder(title), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -66f), new Vector2(1200f, 110f));

            var rule = ClaimSlate.Fill(_canvas.transform, "TitleRule", ClaimSlate.OxideRust);
            ClaimSlate.Place(rule.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -166f), new Vector2(560f, 3f));

            var tagline = ClaimSlate.Stencil(_canvas.transform, "Tagline",
                "DIG IN.  BUILD UP.  SURVIVE THE BLOOD MOON.", 24, TextAnchor.UpperCenter, ClaimSlate.BoneDim, false);
            ClaimSlate.Place(ClaimSlate.Holder(tagline), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -186f), new Vector2(1200f, 34f));

            var stamp = ClaimSlate.Stencil(_canvas.transform, "Stamp", "MADGENIUS", 17, TextAnchor.UpperCenter,
                ClaimSlate.Dim(ClaimSlate.Bone, 0.32f), false);
            ClaimSlate.Place(ClaimSlate.Holder(stamp), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -224f), new Vector2(600f, 22f));

            BuildNewWorldPanel();
            BuildWorldListPanel();

            _modLine = UIKit.Label(_canvas.transform, "Mods", "", 20, TextAnchor.LowerCenter, UIKit.TextDim);
            UIKit.Place(_modLine.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 108f), new Vector2(1400f, 26f));

            var quit = UIKit.Button(_canvas.transform, "Quit", "Quit");
            UIKit.Place(quit.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(240f, 52f));
            quit.onClick.AddListener(() => { if (QuitRequested != null) QuitRequested(); });
        }

        void BuildNewWorldPanel()
        {
            var body = ClaimSlate.Plate(_canvas.transform, "NewWorld", "NEW WORLD",
                new Vector2(0.5f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-30f, -20f), new Vector2(560f, 340f));
            var panel = (RectTransform)body.parent;

            var nameLabel = UIKit.Label(panel.transform, "NameLabel", "World name", 22, TextAnchor.UpperLeft, UIKit.TextDim);
            UIKit.Place(nameLabel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(26f, -74f), new Vector2(400f, 26f));

            _nameField = UIKit.Input(panel.transform, "NameField", "Wasteland", "Wasteland");
            UIKit.Place(_nameField.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(26f, -106f), new Vector2(500f, 48f));

            var seedLabel = UIKit.Label(panel.transform, "SeedLabel", "Seed (blank = random)", 22, TextAnchor.UpperLeft, UIKit.TextDim);
            UIKit.Place(seedLabel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(26f, -168f), new Vector2(400f, 26f));

            _seedField = UIKit.Input(panel.transform, "SeedField", "e.g. 133742");
            UIKit.Place(_seedField.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(26f, -200f), new Vector2(500f, 48f));

            var create = UIKit.Button(panel.transform, "Create", "Start new world");
            UIKit.Place(create.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(26f, 26f), new Vector2(500f, 56f));
            create.onClick.AddListener(OnCreateClicked);
        }

        void BuildWorldListPanel()
        {
            var body = ClaimSlate.Plate(_canvas.transform, "Worlds", "CONTINUE",
                new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f), new Vector2(30f, -20f), new Vector2(560f, 340f));
            var panel = (RectTransform)body.parent;

            _worldList = UIKit.Rect(panel.transform, "List");
            UIKit.Place(_worldList, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(26f, -70f), new Vector2(500f, 250f));
        }

        void OnCreateClicked()
        {
            string worldName = string.IsNullOrEmpty(_nameField.text) ? "Wasteland" : _nameField.text;

            int seed;
            if (!int.TryParse(_seedField.text, out seed))
            {
                seed = string.IsNullOrEmpty(_seedField.text)
                    ? UnityEngine.Random.Range(int.MinValue, int.MaxValue)
                    : _seedField.text.GetHashCode();
            }

            // Never silently overwrite: suffix until the name is free.
            string unique = worldName;
            int suffix = 2;
            while (SavePaths.WorldExists(unique)) unique = worldName + " " + suffix++;

            if (NewWorldRequested != null) NewWorldRequested(unique, seed);
        }

        public void SetModSummary(string summary)
        {
            if (_modLine != null) _modLine.text = summary;
        }

        public void Open()
        {
            _canvas.enabled = true;

            // Switched on with the screen, not with the build. See UIKit.Input.
            UIKit.Wake(_nameField);
            UIKit.Wake(_seedField);

            RefreshWorldList();
        }

        public void Close()
        {
            _canvas.enabled = false;
        }

        void RefreshWorldList()
        {
            for (int i = 0; i < _worldButtons.Count; i++) Destroy(_worldButtons[i]);
            _worldButtons.Clear();

            var worlds = SavePaths.ListWorlds();
            if (worlds.Count == 0)
            {
                var empty = UIKit.Label(_worldList, "Empty", "No saved worlds yet.", 22, TextAnchor.UpperLeft, UIKit.TextDim);
                UIKit.Place(empty.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(500f, 28f));
                _worldButtons.Add(empty.gameObject);
                return;
            }

            for (int i = 0; i < worlds.Count && i < 6; i++)
            {
                var worldName = worlds[i];
                var meta = WorldSaveIO.ReadWorld(worldName);
                string caption = meta != null
                    ? string.Format("{0}   (seed {1})", worldName, meta.seed)
                    : worldName;

                var button = UIKit.Button(_worldList, "World" + i, caption, 22);
                UIKit.Place(button.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0f, 1f),
                    new Vector2(0f, -i * 46f), new Vector2(500f, 40f));
                button.onClick.AddListener(() => { if (LoadWorldRequested != null) LoadWorldRequested(worldName); });
                _worldButtons.Add(button.gameObject);
            }
        }
    }

    /// <summary>Pause overlay with the control list and the save/quit actions.</summary>
    public class PauseView : MonoBehaviour
    {
        Canvas _canvas;

        public event Action ResumeRequested;
        public event Action SaveRequested;
        public event Action QuitToMenuRequested;

        public bool IsOpen { get { return _canvas != null && _canvas.enabled; } }

        const string Controls =
            "WASD move    Shift sprint    Ctrl crouch    Space jump\n" +
            "Mouse look    LMB mine / attack    RMB place / use    E interact\n" +
            "Hammer: RMB upgrade a piece, LMB repair it\n" +
            "R rotate deployable    1-9 toolbelt    Scroll change slot\n" +
            "Tab inventory & work orders    P skills    F3 debug    Esc pause";

        public void Init()
        {
            _canvas = UIKit.CreateCanvas("Pause", 30, transform);

            var backdrop = ClaimSlate.Surface(_canvas.transform, "Backdrop", ClaimSlate.Fade(ClaimSlate.OilBlack, 0.80f));
            ClaimSlate.Stretch(backdrop.rectTransform);

            var title = ClaimSlate.Stencil(_canvas.transform, "Title", "PAUSED", 64, TextAnchor.UpperCenter, ClaimSlate.Bone);
            ClaimSlate.Place(ClaimSlate.Holder(title), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -110f), new Vector2(900f, 80f));

            var rule = ClaimSlate.Fill(_canvas.transform, "TitleRule", ClaimSlate.OxideRust);
            ClaimSlate.Place(rule.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -196f), new Vector2(420f, 2f));

            var controls = UIKit.Label(_canvas.transform, "Controls", Controls, 24, TextAnchor.MiddleCenter, UIKit.TextDim);
            UIKit.Place(controls.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 150f), new Vector2(1100f, 160f));

            var resume = UIKit.Button(_canvas.transform, "Resume", "Resume");
            UIKit.Place(resume.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -10f), new Vector2(380f, 56f));
            resume.onClick.AddListener(() => { if (ResumeRequested != null) ResumeRequested(); });

            var save = UIKit.Button(_canvas.transform, "Save", "Save now");
            UIKit.Place(save.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -76f), new Vector2(380f, 56f));
            save.onClick.AddListener(() => { if (SaveRequested != null) SaveRequested(); });

            var quit = UIKit.Button(_canvas.transform, "QuitToMenu", "Save and quit to menu");
            UIKit.Place(quit.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -142f), new Vector2(380f, 56f));
            quit.onClick.AddListener(() => { if (QuitToMenuRequested != null) QuitToMenuRequested(); });

            _canvas.enabled = false;
        }

        public void Open() { _canvas.enabled = true; }
        public void Close() { _canvas.enabled = false; }
    }

    /// <summary>Death overlay. Says what happened to the bag and offers a respawn.</summary>
    public class DeathView : MonoBehaviour
    {
        Canvas _canvas;
        Text _detail;

        public event Action RespawnRequested;

        public bool IsOpen { get { return _canvas != null && _canvas.enabled; } }

        public void Init()
        {
            _canvas = UIKit.CreateCanvas("Death", 35, transform);

            // Blood is reserved for exactly two things. This is one of them.
            var backdrop = ClaimSlate.Surface(_canvas.transform, "Backdrop",
                new Color(ClaimSlate.Blood.r * 0.35f, ClaimSlate.Blood.g * 0.35f, ClaimSlate.Blood.b * 0.35f, 0.78f));
            ClaimSlate.Stretch(backdrop.rectTransform);

            var title = ClaimSlate.Stencil(_canvas.transform, "Title", "YOU DIED", 78, TextAnchor.UpperCenter, ClaimSlate.Bone);
            ClaimSlate.Place(ClaimSlate.Holder(title), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 120f), new Vector2(900f, 90f));

            var rule = ClaimSlate.Fill(_canvas.transform, "TitleRule", ClaimSlate.Blood);
            ClaimSlate.Place(rule.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 74f), new Vector2(360f, 3f));

            _detail = UIKit.Label(_canvas.transform, "Detail", "", 26, TextAnchor.UpperCenter, UIKit.TextMain);
            UIKit.Place(_detail.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 30f), new Vector2(1000f, 90f));

            var respawn = UIKit.Button(_canvas.transform, "Respawn", "Respawn");
            UIKit.Place(respawn.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -90f), new Vector2(360f, 60f));
            respawn.onClick.AddListener(() => { if (RespawnRequested != null) RespawnRequested(); });

            _canvas.enabled = false;
        }

        public void Open(string detail)
        {
            _detail.text = detail;
            _canvas.enabled = true;
        }

        public void Close() { _canvas.enabled = false; }
    }
}
