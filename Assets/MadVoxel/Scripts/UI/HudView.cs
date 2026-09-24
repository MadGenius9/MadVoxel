using System.Collections.Generic;
using MadVoxel.AI;
using MadVoxel.Content;
using MadVoxel.Building;
using MadVoxel.Core;
using MadVoxel.Core.Player;
using MadVoxel.Colony;
using MadVoxel.Farming.Plots;
using MadVoxel.Fluid;
using MadVoxel.Power;
using MadVoxel.Farming.Crops;
using MadVoxel.Horde;
using MadVoxel.Inventory;
using MadVoxel.World.Biomes;
using MadVoxel.World.Terrain;
using MadVoxel.World.Weather;
using UnityEngine;
using UnityEngine.UI;

namespace MadVoxel.UI
{
    /// <summary>What the player is doing, which decides what the HUD is allowed to show.</summary>
    public enum HudMode
    {
        /// <summary>Almost nothing: compass, toolbelt, two short bars.</summary>
        OnFoot,
        /// <summary>A snap piece is in hand, so the placement readout earns its corner.</summary>
        Building,
        /// <summary>Sitting on a machine: the gauge cluster, and no build layer.</summary>
        Tractor
    }

    /// <summary>
    /// Claim Slate's in-world layer: a scratched visor, not a dashboard. The centre of
    /// the screen stays the world - everything lives in a corner or on the compass, and
    /// each layer only appears while the thing it describes is in front of you.
    /// </summary>
    public class HudView : MonoBehaviour
    {
        const int ToastLimit = 5;
        const float ToastSeconds = 4.5f;
        const float SlotSize = 64f;
        const float SlotGap = 5f;

        /// <summary>Food and water stay off the visor until they are worth acting on.</summary>
        const float LowVitalFraction = 0.35f;

        /// <summary>How far out a POI still earns a pip.</summary>
        const float PoiRange = 900f;
        const float ThreatRange = 60f;

        /// <summary>How long a new region must hold before the visor announces it.</summary>
        const float BiomeSettleSeconds = 2.5f;

        PlayerRig _player;
        WorldClock _clock;
        HordeDirector _horde;
        StructureWorld _structures;
        SpawnDirector _spawner;
        TerrainWorld _voxels;
        WeatherDirector _weather;

        // The biome toast: one line on a crossing, then silence. A border that wanders
        // must not be able to chatter, so a new region has to hold before it counts.
        BiomeId _shownBiome = BiomeId.Farmland;
        BiomeId _candidateBiome = BiomeId.Farmland;
        float _candidateSince;
        bool _biomeKnown;

        Canvas _canvas;
        CompassStrip _compass;

        ClaimSlate.Gauge _health, _stamina, _food, _water;
        Text _levelLabel;
        Image _xpSliver;
        Image _pointPip;

        Text _lookLabel, _lookDetail;
        RectTransform _crosshair;
        Image _miningFill;
        RectTransform _miningRoot;

        RectTransform _buildPanel;
        Text _buildName, _buildState, _buildCost;

        RectTransform _hordeRim;
        Text _hordeLabel;

        TractorPanel _tractor;

        RectTransform _toastRoot;
        RectTransform _questRoot;
        RectTransform _drawBarRoot;
        Image _drawBarFill;
        Text _drawLabel;
        ContentDatabase _content;
        readonly List<Text> _questLines = new List<Text>();
        float _sinceQuestTick;
        Text _heldLabel;

        readonly List<SlotView> _belt = new List<SlotView>();
        readonly List<Image> _beltUnderlines = new List<Image>();
        readonly List<Text> _toasts = new List<Text>();
        readonly List<float> _toastExpiry = new List<float>();

        public HudMode Mode { get; private set; }

        /// <summary>The gauge cluster. VehicleWorld mounts it and feeds it.</summary>
        public TractorPanel Tractor { get { return _tractor; } }

        public void Init(PlayerRig player, WorldClock clock, HordeDirector horde,
                         StructureWorld structures, SpawnDirector spawner, TerrainWorld voxels,
                         WeatherDirector weather, ContentDatabase content)
        {
            _player = player;
            _clock = clock;
            _content = content;
            _horde = horde;
            _structures = structures;
            _spawner = spawner;
            _voxels = voxels;
            _weather = weather;

            _canvas = UIKit.CreateCanvas("HUD", 0, transform);

            BuildVisorScratches();
            BuildHordeRim();

            _compass = gameObject.AddComponent<CompassStrip>();
            _compass.Build(_canvas.transform);

            BuildCrosshair();
            BuildLookReadout();
            BuildVitals();
            BuildToolbelt();
            BuildBuildPanel();
            BuildHordeLine();
            BuildQuestTracker();
            BuildDrawBar();
            BuildToasts();

            _tractor = gameObject.AddComponent<TractorPanel>();
            _tractor.Build(_canvas.transform);

            Notifications.Posted += PushToast;
            _player.Inventory.Bag.Changed += RefreshToolbelt;
            _player.Inventory.SelectionChanged += RefreshToolbelt;
            RefreshToolbelt();
        }

        void OnDestroy()
        {
            Notifications.Posted -= PushToast;
            if (_player != null && _player.Inventory != null)
            {
                _player.Inventory.Bag.Changed -= RefreshToolbelt;
                _player.Inventory.SelectionChanged -= RefreshToolbelt;
            }
        }

        public void SetVisible(bool visible)
        {
            if (_canvas != null) _canvas.enabled = visible;
        }

        // ------------------------------------------------------------------- build

        /// <summary>
        /// Four faint scratches at the edges. Not decoration for its own sake: they give
        /// the overlay a physical plane, so the readouts read as etched into a visor
        /// rather than floating in front of the eye.
        /// </summary>
        void BuildVisorScratches()
        {
            Scratch(new Vector2(0f, 1f), new Vector2(180f, -90f), new Vector2(260f, 1f), -18f);
            Scratch(new Vector2(0f, 1f), new Vector2(120f, -150f), new Vector2(150f, 1f), -32f);
            Scratch(new Vector2(1f, 1f), new Vector2(-220f, -130f), new Vector2(320f, 1f), 12f);
            Scratch(new Vector2(1f, 0f), new Vector2(-160f, 240f), new Vector2(200f, 1f), -7f);
        }

        void Scratch(Vector2 anchor, Vector2 offset, Vector2 size, float angle)
        {
            var line = ClaimSlate.Fill(_canvas.transform, "Scratch", ClaimSlate.Scratch);
            ClaimSlate.Place(line.rectTransform, anchor, new Vector2(0.5f, 0.5f), offset, size);
            line.rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle);
        }

        /// <summary>
        /// The horde layer, built once and left off. Four oxidised edges, not a full
        /// screen wash - the world in the middle has to stay legible while it is on.
        /// </summary>
        void BuildHordeRim()
        {
            _hordeRim = ClaimSlate.Rect(_canvas.transform, "HordeRim");
            ClaimSlate.Stretch(_hordeRim);

            RimEdge("RimTop", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 120f));
            RimEdge("RimBottom", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 120f));
            RimEdge("RimLeft", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(150f, 0f));
            RimEdge("RimRight", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(150f, 0f));

            _hordeRim.gameObject.SetActive(false);
        }

        void RimEdge(string name, Vector2 min, Vector2 max, Vector2 size)
        {
            var edge = ClaimSlate.Fill(_hordeRim, name, ClaimSlate.Fade(ClaimSlate.Blood, 0.16f));
            var rect = edge.rectTransform;
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = Vector2.zero;
        }

        void BuildCrosshair()
        {
            var root = ClaimSlate.Rect(_canvas.transform, "Crosshair");
            ClaimSlate.Place(root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(26f, 26f));
            _crosshair = root;

            // A gap in the middle, so the crosshair never hides the block it is on.
            Tick(root, "Left", new Vector2(-9f, 0f), new Vector2(8f, 2f));
            Tick(root, "Right", new Vector2(9f, 0f), new Vector2(8f, 2f));
            Tick(root, "Up", new Vector2(0f, 9f), new Vector2(2f, 8f));
            Tick(root, "Down", new Vector2(0f, -9f), new Vector2(2f, 8f));

            var miningBack = ClaimSlate.Fill(_canvas.transform, "MiningProgress", ClaimSlate.Fade(ClaimSlate.OilBlack, 0.7f));
            ClaimSlate.Place(miningBack.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -30f), new Vector2(132f, 6f));
            _miningRoot = miningBack.rectTransform;

            _miningFill = ClaimSlate.Fill(miningBack.transform, "Fill", ClaimSlate.SodiumGold);
            _miningFill.rectTransform.anchorMin = Vector2.zero;
            _miningFill.rectTransform.anchorMax = new Vector2(0f, 1f);
            _miningFill.rectTransform.offsetMin = Vector2.zero;
            _miningFill.rectTransform.offsetMax = Vector2.zero;
            _miningRoot.gameObject.SetActive(false);
        }

        void Tick(Transform parent, string name, Vector2 offset, Vector2 size)
        {
            var tick = ClaimSlate.Fill(parent, name, ClaimSlate.Fade(ClaimSlate.Bone, 0.75f));
            ClaimSlate.Place(tick.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), offset, size);
        }

        /// <summary>
        /// Two lines under the crosshair and nothing else: what you are looking at, and
        /// the one number that matters about it.
        /// </summary>
        void BuildLookReadout()
        {
            _lookLabel = ClaimSlate.Stencil(_canvas.transform, "LookAt", "", 22, TextAnchor.UpperCenter, ClaimSlate.Bone);
            ClaimSlate.Place(ClaimSlate.Holder(_lookLabel), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f),
                new Vector2(0f, -44f), new Vector2(820f, 26f));

            _lookDetail = ClaimSlate.Stencil(_canvas.transform, "LookDetail", "", 18, TextAnchor.UpperCenter, ClaimSlate.BoneDim);
            ClaimSlate.Place(ClaimSlate.Holder(_lookDetail), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f),
                new Vector2(0f, -70f), new Vector2(820f, 22f));
        }

        void BuildVitals()
        {
            var root = ClaimSlate.Rect(_canvas.transform, "Vitals");
            ClaimSlate.Place(root, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(30f, 30f), new Vector2(200f, 130f));

            _water = Gauge(root, "Water", ClaimSlate.Hex(0x4A6B7A), ClaimSlate.Tick.Drop, 3);
            _food = Gauge(root, "Food", ClaimSlate.CropSage, ClaimSlate.Tick.Grain, 2);
            _stamina = Gauge(root, "Stamina", ClaimSlate.SodiumGold, ClaimSlate.Tick.Bars, 1);
            _health = Gauge(root, "Health", ClaimSlate.Bone, ClaimSlate.Tick.Plus, 0);

            // Hidden from the first frame, not hidden after one frame of flashing.
            _food.SetVisible(false);
            _water.SetVisible(false);

            // Level sits above the bars as a number and a thin gold sliver - no ring, no
            // badge. The rust pip beside it is the only nag: a point is waiting.
            _levelLabel = ClaimSlate.Stencil(root, "Level", "LV 1", 17, TextAnchor.LowerLeft, ClaimSlate.BoneDim);
            ClaimSlate.Place(ClaimSlate.Holder(_levelLabel), new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(0f, 104f), new Vector2(120f, 20f));

            var xpTrack = ClaimSlate.Fill(root, "XpTrack", ClaimSlate.Fade(ClaimSlate.OilBlack, 0.7f));
            ClaimSlate.Place(xpTrack.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(0f, 100f), new Vector2(176f, 2f));

            _xpSliver = ClaimSlate.Fill(xpTrack.transform, "XpFill", ClaimSlate.SodiumGold);
            _xpSliver.rectTransform.anchorMin = Vector2.zero;
            _xpSliver.rectTransform.anchorMax = new Vector2(0f, 1f);
            _xpSliver.rectTransform.offsetMin = Vector2.zero;
            _xpSliver.rectTransform.offsetMax = Vector2.zero;

            _pointPip = ClaimSlate.Fill(root, "PointPip", ClaimSlate.OxideRust);
            ClaimSlate.Place(_pointPip.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(168f, 106f), new Vector2(8f, 8f));
            _pointPip.gameObject.SetActive(false);
        }

        ClaimSlate.Gauge Gauge(Transform parent, string name, Color colour, ClaimSlate.Tick tick, int row)
        {
            var gauge = ClaimSlate.CreateGauge(parent, name, colour, tick);
            ClaimSlate.Place(gauge.Root, new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(0f, row * 22f), new Vector2(176f, 18f));
            return gauge;
        }

        void BuildToolbelt()
        {
            var root = ClaimSlate.Rect(_canvas.transform, "Toolbelt");
            float width = PlayerInventory.HotbarSize * (SlotSize + SlotGap);
            ClaimSlate.Place(root, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 30f), new Vector2(width, SlotSize));

            for (int i = 0; i < PlayerInventory.HotbarSize; i++)
            {
                var slot = SlotView.Create(root, "Belt" + i, i, SlotSize);
                float x = (i - (PlayerInventory.HotbarSize - 1) * 0.5f) * (SlotSize + SlotGap);
                ClaimSlate.Place(slot.Background.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(x, 0f), new Vector2(SlotSize, SlotSize));

                slot.Background.color = ClaimSlate.Metal;
                ClaimSlate.Frame(slot.Background.rectTransform, ClaimSlate.Dim(ClaimSlate.Bone, 0.16f), 1f);

                var index = ClaimSlate.Stencil(slot.transform, "Index", (i + 1).ToString(), 14,
                    TextAnchor.UpperLeft, ClaimSlate.Dim(ClaimSlate.Bone, 0.42f), false);
                ClaimSlate.Stretch(ClaimSlate.Holder(index), 5f);

                // Selection is an underline, not a glow. Ten lit squares is icon soup.
                var underline = ClaimSlate.Fill(slot.transform, "Underline", ClaimSlate.OxideRust);
                ClaimSlate.Place(underline.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f),
                    new Vector2(0f, -4f), new Vector2(SlotSize, 3f));
                underline.gameObject.SetActive(false);

                _belt.Add(slot);
                _beltUnderlines.Add(underline);
            }

            _heldLabel = ClaimSlate.Stencil(_canvas.transform, "Held", "", 20, TextAnchor.LowerCenter, ClaimSlate.Bone);
            ClaimSlate.Place(ClaimSlate.Holder(_heldLabel), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 30f + SlotSize + 10f), new Vector2(700f, 24f));
        }

        /// <summary>The building layer: one corner plate, only while a snap piece is held.</summary>
        void BuildBuildPanel()
        {
            var plate = ClaimSlate.Fill(_canvas.transform, "BuildPanel", ClaimSlate.Fade(ClaimSlate.OilBlack, 0.72f));
            ClaimSlate.Place(plate.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-30f, 40f), new Vector2(330f, 104f));
            _buildPanel = plate.rectTransform;

            var rule = ClaimSlate.Fill(_buildPanel, "Rule", ClaimSlate.OxideRust);
            ClaimSlate.Place(rule.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(330f, 2f));

            _buildName = ClaimSlate.Stencil(_buildPanel, "Piece", "", 21, TextAnchor.UpperLeft, ClaimSlate.Bone);
            ClaimSlate.Place(ClaimSlate.Holder(_buildName), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(14f, -12f), new Vector2(304f, 24f));

            _buildState = ClaimSlate.Stencil(_buildPanel, "State", "", 18, TextAnchor.UpperLeft, ClaimSlate.CropSage);
            ClaimSlate.Place(ClaimSlate.Holder(_buildState), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(14f, -40f), new Vector2(304f, 22f));

            _buildCost = ClaimSlate.Stencil(_buildPanel, "Cost", "", 16, TextAnchor.UpperLeft, ClaimSlate.BoneDim);
            ClaimSlate.Place(ClaimSlate.Holder(_buildCost), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(14f, -66f), new Vector2(304f, 34f));

            _buildPanel.gameObject.SetActive(false);
        }

        /// <summary>One line. No title card, no skull.</summary>
        void BuildHordeLine()
        {
            _hordeLabel = ClaimSlate.Stencil(_canvas.transform, "Horde", "", 22, TextAnchor.UpperRight, ClaimSlate.Blood);
            ClaimSlate.Place(ClaimSlate.Holder(_hordeLabel), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-30f, -26f), new Vector2(420f, 26f));
        }

        /// <summary>
        /// The contracts you are carrying, under the horde line. Three short lines at
        /// most - a contract you cannot see is one you forget you took, and a journal
        /// screen for three jobs would be a filing cabinet for a postcard.
        /// </summary>
        void BuildQuestTracker()
        {
            _questRoot = ClaimSlate.Rect(_canvas.transform, "Contracts");
            ClaimSlate.Place(_questRoot, new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-30f, -62f), new Vector2(420f, 96f));

            for (int i = 0; i < Quests.QuestService.MaxActive; i++)
            {
                var line = ClaimSlate.Stencil(_questRoot, "Contract" + i, "", 16,
                    TextAnchor.UpperRight, ClaimSlate.BoneDim, false);
                ClaimSlate.Place(ClaimSlate.Holder(line), new Vector2(1f, 1f), new Vector2(1f, 1f),
                    new Vector2(0f, -i * 22f), new Vector2(420f, 20f));
                _questLines.Add(line);
            }
        }

        /// <summary>
        /// One line each: what it is and how far along. A finished contract goes sage,
        /// which is the only cue that it is worth walking back to the outpost.
        /// </summary>
        void UpdateQuestTracker()
        {
            if (_content == null || _player == null) return;

            var log = _player.Quests;
            var bag = _player.Inventory.Bag;
            double now = _clock != null ? _clock.TotalHours : 0.0;

            for (int i = 0; i < _questLines.Count; i++)
            {
                if (i >= log.Active.Count)
                {
                    if (_questLines[i].text.Length > 0) _questLines[i].text = "";
                    continue;
                }

                var entry = log.Active[i];
                var quest = _content.Quest(entry.QuestId);
                if (quest == null) { _questLines[i].text = ""; continue; }

                bool done = Quests.QuestService.IsComplete(quest, entry, bag, now);

                _questLines[i].text = string.Format("{0}   {1}",
                    quest.title.ToUpperInvariant(),
                    done ? "READY TO HAND IN" : Quests.QuestService.ProgressLine(quest, entry, bag, now));
                _questLines[i].color = done ? ClaimSlate.CropSage : ClaimSlate.BoneDim;
            }
        }

        /// <summary>
        /// The bow's draw, under the crosshair. It is the only thing in the game where
        /// a fraction of a second changes the outcome, so it gets a bar rather than a
        /// word - and the bar goes sage at full draw, which is the moment to loose.
        /// </summary>
        void BuildDrawBar()
        {
            _drawBarRoot = ClaimSlate.Rect(_canvas.transform, "Draw");
            ClaimSlate.Place(_drawBarRoot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -46f), new Vector2(180f, 26f));

            var back = ClaimSlate.Fill(_drawBarRoot, "Back", ClaimSlate.Fade(ClaimSlate.OilBlack, 0.7f));
            ClaimSlate.Place(back.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                Vector2.zero, new Vector2(180f, 8f));
            ClaimSlate.Frame(back.rectTransform, ClaimSlate.Dim(ClaimSlate.Bone, 0.25f), 1f);

            _drawBarFill = ClaimSlate.Fill(back.transform, "Fill", ClaimSlate.SodiumGold);
            var fill = _drawBarFill.rectTransform;
            fill.anchorMin = new Vector2(0f, 0f);
            fill.anchorMax = new Vector2(0f, 1f);
            fill.pivot = new Vector2(0f, 0.5f);
            fill.offsetMin = new Vector2(1f, 1f);
            fill.offsetMax = new Vector2(1f, -1f);

            _drawLabel = ClaimSlate.Stencil(_drawBarRoot, "DrawLabel", "", 15,
                TextAnchor.UpperCenter, ClaimSlate.BoneDim, false);
            ClaimSlate.Place(ClaimSlate.Holder(_drawLabel), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -12f), new Vector2(180f, 18f));

            _drawBarRoot.gameObject.SetActive(false);
        }

        void UpdateDrawBar()
        {
            var interaction = _player.Interaction;
            bool drawing = interaction != null && interaction.enabled && interaction.IsDrawing;

            if (_drawBarRoot.gameObject.activeSelf != drawing) _drawBarRoot.gameObject.SetActive(drawing);
            if (!drawing) return;

            float draw = Mathf.Clamp01(interaction.Draw01);
            bool full = draw >= 0.999f;

            _drawBarFill.rectTransform.anchorMax = new Vector2(draw, 1f);

            // Colour and word together, as everywhere else: rust while the shot is not
            // worth the arrow, gold while it is building, sage at full.
            _drawBarFill.color = full
                ? ClaimSlate.CropSage
                : (Combat.Ballistics.CanRelease(draw) ? ClaimSlate.SodiumGold : ClaimSlate.OxideRust);

            _drawLabel.text = Combat.Ballistics.DrawLine(draw);
            _drawLabel.color = full ? ClaimSlate.CropSage : ClaimSlate.BoneDim;
        }

        void BuildToasts()
        {
            _toastRoot = ClaimSlate.Rect(_canvas.transform, "Toasts");
            ClaimSlate.Place(_toastRoot, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-30f, 150f), new Vector2(460f, 180f));
        }

        // ------------------------------------------------------------------ update

        void Update()
        {
            if (_player == null) return;

            Mode = ResolveMode();

            UpdateVitals();
            UpdateCompass();
            UpdateLookReadout();
            UpdateDrawBar();
            UpdateBuildLayer();
            UpdateHordeLayer();

            // Four times a second. A contract's progress is a bag count or a clock
            // difference, and neither changes in a way you could see at sixty hertz.
            _sinceQuestTick += Time.deltaTime;
            if (_sinceQuestTick >= 0.25f)
            {
                _sinceQuestTick = 0f;
                UpdateQuestTracker();
            }
            UpdateBiomeCrossing();
            UpdateToasts();

            var held = _player.Inventory.SelectedStack;
            _heldLabel.text = held.IsEmpty ? "" : held.Item.displayName.ToUpperInvariant();
        }

        HudMode ResolveMode()
        {
            if (_tractor != null && _tractor.IsDriving) return HudMode.Tractor;
            return _player.Interaction != null && _player.Interaction.Build.Active ? HudMode.Building : HudMode.OnFoot;
        }

        void UpdateVitals()
        {
            var stats = _player.Stats;

            float healthFraction = stats.MaxHealth > 0f ? stats.Health / stats.MaxHealth : 0f;
            _health.Set(stats.Health, stats.MaxHealth, Mathf.CeilToInt(stats.Health).ToString(),
                ClaimSlate.VitalColour(healthFraction));
            _stamina.Set(stats.Stamina, stats.MaxStamina, Mathf.CeilToInt(stats.Stamina).ToString(), ClaimSlate.SodiumGold);

            // Food and water are not standing information. They appear when they bite.
            float foodFraction = stats.MaxFood > 0f ? stats.Food / stats.MaxFood : 1f;
            float waterFraction = stats.MaxWater > 0f ? stats.Water / stats.MaxWater : 1f;

            _food.SetVisible(foodFraction <= LowVitalFraction);
            if (foodFraction <= LowVitalFraction)
            {
                _food.Set(stats.Food, stats.MaxFood, Mathf.CeilToInt(stats.Food).ToString(),
                    foodFraction <= 0.12f ? ClaimSlate.OxideRust : ClaimSlate.CropSage);
            }

            _water.SetVisible(waterFraction <= LowVitalFraction);
            if (waterFraction <= LowVitalFraction)
            {
                _water.Set(stats.Water, stats.MaxWater, Mathf.CeilToInt(stats.Water).ToString(),
                    waterFraction <= 0.12f ? ClaimSlate.OxideRust : ClaimSlate.Hex(0x4A6B7A));
            }

            var progression = _player.Progression;
            _levelLabel.text = "LV " + progression.Level;
            _xpSliver.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(progression.XpProgress01), 1f);

            bool pointWaiting = progression.UnspentPerkPoints > 0;
            if (_pointPip.gameObject.activeSelf != pointWaiting) _pointPip.gameObject.SetActive(pointWaiting);
        }

        void UpdateCompass()
        {
            if (_compass == null) return;

            float yaw = _player.Look != null ? _player.Look.Yaw : _player.transform.eulerAngles.y;
            _compass.SetHeading(yaw);

            if (_clock != null)
            {
                // The weather owns the clock line, so one word can ride along with the
                // day and the time instead of earning a widget of its own.
                string line = _weather != null
                    ? _weather.ClockLine()
                    : string.Format("DAY {0}   {1}", _clock.Day, _clock.FormatTime());

                _compass.SetClock(line, _clock.IsNight);
            }

            Vector3 eye = _player.transform.position;
            _compass.BeginPips();

            AddTraderPips(eye);
            AddClaimPips(eye);

            if (_player.HasRespawnPoint)
            {
                _compass.AddPip(CompassStrip.PipKind.Bed, CompassStrip.BearingTo(eye, _player.RespawnPoint));
            }

            AddBoardPips(eye);

            // Threat ticks are a horde-night layer. On a quiet night the compass stays
            // a compass.
            if (_horde != null && _horde.IsBloodMoonActive) AddThreatPips(eye);

            _compass.EndPips();
        }

        void AddTraderPips(Vector3 eye)
        {
            var pois = TraderOutposts();
            if (pois == null) return;

            for (int i = 0; i < pois.Count; i++)
            {
                var poi = pois[i];
                var centre = new Vector3(poi.CentreX, eye.y, poi.CentreZ);
                if ((centre - eye).sqrMagnitude > PoiRange * PoiRange) continue;

                _compass.AddPip(CompassStrip.PipKind.Trader, CompassStrip.BearingTo(eye, centre));
            }
        }

        IReadOnlyList<Poi> _traderCache;

        /// <summary>
        /// POI layout is deterministic from the seed and never moves, so this is resolved
        /// once rather than walked every frame.
        /// </summary>
        IReadOnlyList<Poi> TraderOutposts()
        {
            if (_traderCache != null) return _traderCache;
            if (_voxels == null || _voxels.Terrain == null || _voxels.Terrain.Pois == null) return null;

            _traderCache = _voxels.Terrain.Pois.TraderOutposts;
            return _traderCache;
        }

        void AddClaimPips(Vector3 eye)
        {
            if (_structures == null || _structures.Claims == null) return;

            var claims = _structures.Claims.Claims;
            for (int i = 0; i < claims.Count; i++)
            {
                var centre = claims[i].Centre;
                if ((centre - eye).sqrMagnitude > PoiRange * PoiRange) continue;

                _compass.AddPip(CompassStrip.PipKind.Claim, CompassStrip.BearingTo(eye, centre));
            }
        }

        /// <summary>The colony board gets a pip, because it is the only colony screen.</summary>
        void AddBoardPips(Vector3 eye)
        {
            if (_structures == null) return;

            var all = _structures.All;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].GetComponent<ColonyBoardStructure>() == null) continue;
                if ((all[i].transform.position - eye).sqrMagnitude > PoiRange * PoiRange) continue;

                _compass.AddPip(CompassStrip.PipKind.Board, CompassStrip.BearingTo(eye, all[i].transform.position));
            }
        }

        void AddThreatPips(Vector3 eye)
        {
            if (_spawner == null) return;

            var alive = _spawner.Alive;
            for (int i = 0; i < alive.Count; i++)
            {
                var zombie = alive[i];
                if (zombie == null || !zombie.IsAlive) continue;

                Vector3 position = zombie.transform.position;
                if ((position - eye).sqrMagnitude > ThreatRange * ThreatRange) continue;

                _compass.AddPip(CompassStrip.PipKind.Threat, CompassStrip.BearingTo(eye, position));
            }
        }

        /// <summary>
        /// One line when you cross a fade, then nothing. The candidate has to hold for
        /// a beat before it counts, so walking the wiggle of a border does not chatter.
        /// </summary>
        void UpdateBiomeCrossing()
        {
            if (_voxels == null) return;

            var here = _voxels.BiomeAt(_player.transform.position);

            if (!_biomeKnown)
            {
                // The first sample is where you woke up, not a crossing.
                _biomeKnown = true;
                _shownBiome = here;
                _candidateBiome = here;
                return;
            }

            if (here != _candidateBiome)
            {
                _candidateBiome = here;
                _candidateSince = Time.time;
                return;
            }

            if (_candidateBiome == _shownBiome) return;
            if (Time.time - _candidateSince < BiomeSettleSeconds) return;

            _shownBiome = _candidateBiome;
            Notifications.Post("ENTERING  " + BiomeIds.DisplayName(_shownBiome).ToUpperInvariant());
        }

        // ---------------------------------------------------------- look-at readout

        void UpdateLookReadout()
        {
            var interaction = _player.Interaction;

            // Driving, the player's own interaction is switched off and its last target
            // is stale. A frozen readout and a crosshair over the gauge cluster would be
            // worse than nothing, so the whole on-foot layer goes away.
            bool aiming = Mode != HudMode.Tractor && interaction != null && interaction.enabled;
            if (_crosshair.gameObject.activeSelf != aiming) _crosshair.gameObject.SetActive(aiming);

            if (!aiming)
            {
                if (_miningRoot.gameObject.activeSelf) _miningRoot.gameObject.SetActive(false);
                _lookLabel.text = "";
                _lookDetail.text = "";
                return;
            }

            float mining = interaction.MiningProgress01;
            bool showMining = mining > 0.001f;
            if (_miningRoot.gameObject.activeSelf != showMining) _miningRoot.gameObject.SetActive(showMining);
            if (showMining) _miningFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(mining), 1f);

            string title, detail;
            Color colour;
            DescribeTarget(interaction, out title, out detail, out colour);

            _lookLabel.text = title;
            _lookLabel.color = colour;
            _lookDetail.text = detail;
        }

        void DescribeTarget(PlayerInteraction interaction, out string title, out string detail, out Color colour)
        {
            title = "";
            detail = "";
            colour = ClaimSlate.Bone;

            var target = interaction.Target;

            // Running a line takes the line, because while a wire is in the air that is
            // the only thing the player is thinking about.
            if (interaction.Wiring != null && interaction.Wiring.HasPending)
            {
                title = interaction.Wiring.Readout();
                colour = ClaimSlate.OxideRust;
            }

            // Utilities answer for themselves: a device knows its own watts, litres and
            // reasons better than the HUD ever could.
            var electrical = target.Structure != null
                ? target.Structure.GetComponentInChildren<PowerDeviceStructure>() : null;
            var fitting = target.Structure != null
                ? target.Structure.GetComponentInChildren<FluidDeviceStructure>() : null;

            if (electrical != null || fitting != null)
            {
                string power = electrical != null ? electrical.Readout() : "";
                string water = fitting != null ? fitting.Readout() : "";

                if (string.IsNullOrEmpty(title)) title = !string.IsNullOrEmpty(water) ? water : power;
                detail = !string.IsNullOrEmpty(water) && !string.IsNullOrEmpty(power) ? power : detail;

                colour = ClaimSlate.Bone;
                if (fitting != null && fitting.Node != null && (fitting.Node.IsBroken || fitting.Node.IsFrozen))
                    colour = ClaimSlate.OxideRust;
                else if (electrical != null && electrical.Node != null && !electrical.Node.IsPowered
                         && electrical.Device.wattsConsumed > 0f)
                    colour = ClaimSlate.OxideRust;

                return;
            }

            // A trap answers for itself too: how many bites it has left, or that it
            // has none. Blunt spikes look exactly like sharp ones, so the readout is
            // the only way anyone finds out before a blood moon rather than during.
            var spikes = target.Structure != null
                ? target.Structure.GetComponentInChildren<SpikeTrapStructure>() : null;

            if (spikes != null)
            {
                // Same guard the utility branch uses: a wire half-run is the more
                // urgent thing to say, and it has already claimed the title line.
                if (string.IsNullOrEmpty(title)) title = spikes.Readout;
                else detail = spikes.Readout;

                if (spikes.IsBlunt) colour = ClaimSlate.OxideRust;
                return;
            }

            // A person, not a health bar: "JULES  FARM  HUNGRY".
            var colonist = target.Damageable as Colonist;
            if (colonist != null && colonist.IsAlive)
            {
                title = colonist.Readout();
                colour = colonist.IsHungry || colonist.IsThirsty ? ClaimSlate.OxideRust : ClaimSlate.Bone;
                detail = colonist.Bed == null ? "NO BED ASSIGNED" : "";
                return;
            }

            var board = target.Structure != null ? target.Structure.GetComponent<ColonyBoardStructure>() : null;
            if (board != null)
            {
                title = board.Readout();
                colour = ClaimSlate.Bone;
                return;
            }

            // A garden plot is the one look-at that gets its own grammar, because the
            // number a farmer wants is hours, not a health bar.
            var plot = target.Structure != null ? target.Structure.GetComponentInChildren<FarmPlotStructure>() : null;
            if (plot != null)
            {
                DescribePlot(plot, out title, out detail);
                colour = ClaimSlate.CropSage;
                return;
            }

            if (target.Kind == TargetKind.BuildPiece && target.Piece != null)
            {
                DescribePiece(target.Piece, out title, out detail);
                return;
            }

            // Everything else keeps the prompt the interaction layer already writes.
            string prompt = interaction.TargetPrompt;
            if (string.IsNullOrEmpty(prompt)) return;

            title = prompt.ToUpperInvariant();
            colour = ClaimSlate.Bone;
        }

        void DescribePlot(FarmPlotStructure plot, out string title, out string detail)
        {
            if (plot.IsEmpty)
            {
                title = "FARM PLOT";
                detail = "EMPTY  -  [E] PLANT A SEED";
                return;
            }

            var crop = plot.Crop;
            var stage = plot.Stage;
            int index = Mathf.Clamp((int)stage, 1, 4);

            if (stage == CropStage.Ready)
            {
                title = crop.displayName.ToUpperInvariant() + "   READY";
                detail = "[E] HARVEST";
                return;
            }

            float remaining = Mathf.Max(0f, crop.HoursToMature - (float)plot.HoursGrown);
            title = string.Format("{0}   STAGE {1}/4   {2}H",
                crop.displayName.ToUpperInvariant(), index, Mathf.CeilToInt(remaining));
            detail = "";
        }

        void DescribePiece(BuildPiece piece, out string title, out string detail)
        {
            var def = piece.Definition;
            bool stable = piece.Owner != null && piece.Owner.IsStable(piece.Address);

            title = string.Format("{0}   {1}   {2}%",
                def.displayName.ToUpperInvariant(), def.tier.ToString().ToUpperInvariant(),
                Mathf.RoundToInt(piece.HealthFraction * 100f));

            // Stability is the number that decides whether the storey above survives, so
            // it is said in words, not implied by a tint.
            detail = stable ? "STABLE" : "UNSUPPORTED";

            if (!def.IsTopTier)
            {
                string cost = DescribeCost(def.upgradeCost);
                if (!string.IsNullOrEmpty(cost)) detail += "   -   RMB " + cost;
            }
        }

        string DescribeCost(List<RecipeIngredient> cost)
        {
            if (cost == null || cost.Count == 0) return "";

            var bag = _player.Inventory.Bag;
            var sb = new System.Text.StringBuilder();

            for (int i = 0; i < cost.Count; i++)
            {
                var ingredient = cost[i];
                if (ingredient.item == null) continue;
                if (sb.Length > 0) sb.Append(", ");

                sb.Append(bag.CountOf(ingredient.item)).Append('/').Append(ingredient.count)
                  .Append(' ').Append(ingredient.item.displayName.ToUpperInvariant());
            }
            return sb.ToString();
        }

        // ------------------------------------------------------------ mode layers

        void UpdateBuildLayer()
        {
            bool building = Mode == HudMode.Building;
            if (_buildPanel.gameObject.activeSelf != building) _buildPanel.gameObject.SetActive(building);
            if (!building) return;

            var readout = _player.Interaction.Build;

            _buildName.text = string.Format("{0}   {1}",
                readout.PieceName.ToUpperInvariant(), readout.Tier.ToUpperInvariant());

            if (!readout.Resolved)
            {
                _buildState.text = "NO SNAP POINT";
                _buildState.color = ClaimSlate.BoneDim;
            }
            else if (readout.Valid)
            {
                _buildState.text = "SNAP OK";
                _buildState.color = ClaimSlate.CropSage;
            }
            else
            {
                _buildState.text = readout.Reason.ToUpperInvariant();
                _buildState.color = ClaimSlate.OxideRust;
            }

            _buildCost.text = "R ROTATE   RMB UPGRADE   LMB REPAIR";
        }

        void UpdateHordeLayer()
        {
            if (_horde == null) return;

            bool active = _horde.IsBloodMoonActive;

            // One word on the line that is already there, not a second indicator. The
            // compass pips say which way; this says whether the wall is doing anything.
            string line = _horde.CountdownLine;
            if (active && _horde.IsBreached) line += "   BREACHED";

            _hordeLabel.text = line;
            _hordeLabel.color = active ? ClaimSlate.Blood : ClaimSlate.OxideRust;

            if (_hordeRim.gameObject.activeSelf != active) _hordeRim.gameObject.SetActive(active);
        }

        // ------------------------------------------------------------- belt, toasts

        void RefreshToolbelt()
        {
            for (int i = 0; i < _belt.Count; i++)
            {
                bool selected = i == _player.Inventory.SelectedIndex;
                _belt[i].Bind(_player.Inventory.Bag[i], selected);

                // Bind() tints the background; Claim Slate keeps every slot the same
                // quiet metal and marks the selection underneath instead.
                _belt[i].Background.color = ClaimSlate.Metal;
                if (_beltUnderlines[i].gameObject.activeSelf != selected)
                {
                    _beltUnderlines[i].gameObject.SetActive(selected);
                }
            }
        }

        void PushToast(string message)
        {
            var label = ClaimSlate.Stencil(_toastRoot, "Toast", message, 20, TextAnchor.LowerRight, ClaimSlate.Bone, false);
            ClaimSlate.Place(ClaimSlate.Holder(label), new Vector2(1f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(460f, 24f));

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
            if (_toasts[index] != null) Destroy(ClaimSlate.Holder(_toasts[index]).gameObject);
            _toasts.RemoveAt(index);
            _toastExpiry.RemoveAt(index);
        }

        void LayoutToasts()
        {
            for (int i = 0; i < _toasts.Count; i++)
            {
                int fromBottom = _toasts.Count - 1 - i;
                ClaimSlate.Place(ClaimSlate.Holder(_toasts[i]), new Vector2(1f, 0f), new Vector2(1f, 0f),
                    new Vector2(0f, fromBottom * 26f), new Vector2(460f, 24f));
            }
        }
    }
}
