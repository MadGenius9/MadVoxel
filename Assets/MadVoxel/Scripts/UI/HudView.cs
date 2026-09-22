using System.Collections.Generic;
using MadVoxel.Core;
using MadVoxel.Core.Player;
using MadVoxel.Horde;
using UnityEngine;
using UnityEngine.UI;

namespace MadVoxel.UI
{
    /// <summary>
    /// The in-world readout: vitals, hotbar, clock, horde countdown, target prompt and
    /// a short toast feed.
    /// </summary>
    public class HudView : MonoBehaviour
    {
        const int ToastLimit = 5;
        const float ToastSeconds = 4.5f;
        const float SlotSize = 74f;

        PlayerRig _player;
        WorldClock _clock;
        HordeDirector _horde;

        Canvas _canvas;
        UIKit.Bar _health, _stamina, _food, _water;
        Text _clockLabel, _hordeLabel, _levelLabel, _promptLabel, _heldLabel;
        Image _miningFill;
        RectTransform _miningRoot;
        RectTransform _toastRoot;
        readonly List<SlotView> _hotbar = new List<SlotView>();
        readonly List<Text> _toasts = new List<Text>();
        readonly List<float> _toastExpiry = new List<float>();

        public void Init(PlayerRig player, WorldClock clock, HordeDirector horde)
        {
            _player = player;
            _clock = clock;
            _horde = horde;

            _canvas = UIKit.CreateCanvas("HUD", 0, transform);
            BuildCrosshair();
            BuildVitals();
            BuildHotbar();
            BuildTopRight();
            BuildPrompt();
            BuildToasts();

            Notifications.Posted += PushToast;
            _player.Inventory.Bag.Changed += RefreshHotbar;
            _player.Inventory.SelectionChanged += RefreshHotbar;
            RefreshHotbar();
        }

        void OnDestroy()
        {
            Notifications.Posted -= PushToast;
            if (_player != null && _player.Inventory != null)
            {
                _player.Inventory.Bag.Changed -= RefreshHotbar;
                _player.Inventory.SelectionChanged -= RefreshHotbar;
            }
        }

        public void SetVisible(bool visible)
        {
            if (_canvas != null) _canvas.enabled = visible;
        }

        // ------------------------------------------------------------------- build

        void BuildCrosshair()
        {
            var root = UIKit.Rect(_canvas.transform, "Crosshair");
            UIKit.Place(root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(26f, 26f));

            var horizontal = UIKit.Image(root, "H", new Color(1f, 1f, 1f, 0.72f));
            UIKit.Place(horizontal.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(18f, 2f));

            var vertical = UIKit.Image(root, "V", new Color(1f, 1f, 1f, 0.72f));
            UIKit.Place(vertical.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(2f, 18f));

            var miningBack = UIKit.Image(_canvas.transform, "MiningProgress", new Color(0f, 0f, 0f, 0.55f));
            UIKit.Place(miningBack.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -34f), new Vector2(150f, 8f));
            _miningRoot = miningBack.rectTransform;

            _miningFill = UIKit.Image(miningBack.transform, "Fill", UIKit.Accent);
            _miningFill.rectTransform.anchorMin = Vector2.zero;
            _miningFill.rectTransform.anchorMax = new Vector2(0f, 1f);
            _miningFill.rectTransform.offsetMin = new Vector2(0f, 1f);
            _miningFill.rectTransform.offsetMax = new Vector2(0f, -1f);
            _miningRoot.gameObject.SetActive(false);
        }

        void BuildVitals()
        {
            var root = UIKit.Rect(_canvas.transform, "Vitals");
            UIKit.Place(root, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(26f, 26f), new Vector2(320f, 120f));

            _health = MakeBar(root, "Health", new Color(0.72f, 0.20f, 0.17f), 0);
            _stamina = MakeBar(root, "Stamina", new Color(0.78f, 0.66f, 0.22f), 1);
            _food = MakeBar(root, "Food", new Color(0.50f, 0.42f, 0.22f), 2);
            _water = MakeBar(root, "Water", new Color(0.26f, 0.50f, 0.62f), 3);
        }

        UIKit.Bar MakeBar(Transform parent, string name, Color colour, int row)
        {
            var bar = UIKit.CreateBar(parent, name, colour);
            UIKit.Place(bar.Root, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, row * 28f), new Vector2(300f, 22f));
            return bar;
        }

        void BuildHotbar()
        {
            var root = UIKit.Rect(_canvas.transform, "Hotbar");
            float width = PlayerInventory.HotbarSize * (SlotSize + 6f);
            UIKit.Place(root, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 26f), new Vector2(width, SlotSize));

            for (int i = 0; i < PlayerInventory.HotbarSize; i++)
            {
                var slot = SlotView.Create(root, "Slot" + i, i, SlotSize);
                float x = (i - (PlayerInventory.HotbarSize - 1) * 0.5f) * (SlotSize + 6f);
                UIKit.Place(slot.Background.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(x, 0f), new Vector2(SlotSize, SlotSize));

                var index = UIKit.Label(slot.transform, "Index", (i + 1).ToString(), 16, TextAnchor.UpperRight, UIKit.TextDim);
                UIKit.Stretch(index.rectTransform, 4f);

                _hotbar.Add(slot);
            }

            _heldLabel = UIKit.Label(_canvas.transform, "HeldItem", "", 24, TextAnchor.LowerCenter, UIKit.TextMain);
            UIKit.Place(_heldLabel.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 26f + SlotSize + 8f), new Vector2(700f, 30f));
        }

        void BuildTopRight()
        {
            _clockLabel = UIKit.Label(_canvas.transform, "Clock", "", 30, TextAnchor.UpperRight, UIKit.TextMain);
            UIKit.Place(_clockLabel.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-26f, -22f), new Vector2(420f, 36f));

            _hordeLabel = UIKit.Label(_canvas.transform, "Horde", "", 22, TextAnchor.UpperRight, UIKit.TextDim);
            UIKit.Place(_hordeLabel.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-26f, -60f), new Vector2(420f, 28f));

            _levelLabel = UIKit.Label(_canvas.transform, "Level", "", 22, TextAnchor.UpperLeft, UIKit.TextDim);
            UIKit.Place(_levelLabel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(26f, -22f), new Vector2(420f, 28f));
        }

        void BuildPrompt()
        {
            _promptLabel = UIKit.Label(_canvas.transform, "Prompt", "", 24, TextAnchor.MiddleCenter, UIKit.TextMain);
            UIKit.Place(_promptLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -70f), new Vector2(760f, 30f));
        }

        void BuildToasts()
        {
            _toastRoot = UIKit.Rect(_canvas.transform, "Toasts");
            UIKit.Place(_toastRoot, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-26f, 150f), new Vector2(460f, 180f));
        }

        // ------------------------------------------------------------------ update

        void Update()
        {
            if (_player == null) return;

            var stats = _player.Stats;
            _health.Set(stats.Health, stats.MaxHealth, "HP " + Mathf.CeilToInt(stats.Health));
            _stamina.Set(stats.Stamina, stats.MaxStamina, "STAM " + Mathf.CeilToInt(stats.Stamina));
            _food.Set(stats.Food, stats.MaxFood, "FOOD " + Mathf.CeilToInt(stats.Food));
            _water.Set(stats.Water, stats.MaxWater, "WATER " + Mathf.CeilToInt(stats.Water));

            if (_clock != null) _clockLabel.text = _clock.FormatClock();
            if (_horde != null)
            {
                _hordeLabel.text = _horde.StatusLine;
                _hordeLabel.color = _horde.IsBloodMoonActive ? new Color(0.9f, 0.3f, 0.25f) : UIKit.TextDim;
            }

            var progression = _player.Progression;
            _levelLabel.text = string.Format("Level {0}   XP {1}/{2}{3}",
                progression.Level,
                Mathf.FloorToInt(progression.Xp),
                Mathf.FloorToInt(progression.XpToNext),
                progression.UnspentPerkPoints > 0 ? "   [" + progression.UnspentPerkPoints + " pts]" : "");

            var interaction = _player.Interaction;
            _promptLabel.text = interaction != null ? interaction.TargetPrompt : "";

            float mining = interaction != null ? interaction.MiningProgress01 : 0f;
            _miningRoot.gameObject.SetActive(mining > 0.001f);
            if (mining > 0.001f) _miningFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(mining), 1f);

            var held = _player.Inventory.SelectedStack;
            _heldLabel.text = held.IsEmpty ? "" : held.Item.displayName;

            UpdateToasts();
        }

        void RefreshHotbar()
        {
            for (int i = 0; i < _hotbar.Count; i++)
            {
                _hotbar[i].Bind(_player.Inventory.Bag[i], i == _player.Inventory.SelectedIndex);
            }
        }

        void PushToast(string message)
        {
            var label = UIKit.Label(_toastRoot, "Toast", message, 22, TextAnchor.LowerRight, UIKit.TextMain);
            UIKit.Place(label.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(460f, 26f));

            _toasts.Add(label);
            _toastExpiry.Add(Time.time + ToastSeconds);

            while (_toasts.Count > ToastLimit) RemoveToast(0);
            LayoutToasts();
        }

        void UpdateToasts()
        {
            bool changed = false;
            for (int i = _toasts.Count - 1; i >= 0; i--)
            {
                if (Time.time < _toastExpiry[i])
                {
                    float remaining = _toastExpiry[i] - Time.time;
                    var colour = _toasts[i].color;
                    colour.a = Mathf.Clamp01(remaining / 1.2f);
                    _toasts[i].color = colour;
                    continue;
                }
                RemoveToast(i);
                changed = true;
            }
            if (changed) LayoutToasts();
        }

        void RemoveToast(int index)
        {
            if (index < 0 || index >= _toasts.Count) return;
            if (_toasts[index] != null) Destroy(_toasts[index].gameObject);
            _toasts.RemoveAt(index);
            _toastExpiry.RemoveAt(index);
        }

        void LayoutToasts()
        {
            for (int i = 0; i < _toasts.Count; i++)
            {
                int fromBottom = _toasts.Count - 1 - i;
                UIKit.Place(_toasts[i].rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f),
                    new Vector2(0f, fromBottom * 28f), new Vector2(460f, 26f));
            }
        }
    }
}
