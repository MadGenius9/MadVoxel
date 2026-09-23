using System.Collections.Generic;
using MadVoxel.Content;
using MadVoxel.Core;
using MadVoxel.Core.Player;
using MadVoxel.Farming.Crops;
using MadVoxel.Farming.Plots;
using UnityEngine;
using UnityEngine.UI;

namespace MadVoxel.UI
{
    /// <summary>
    /// The grain bin. What is in it, by the litre, and a way to take some back out.
    ///
    /// The bin was a black hole before this screen: a harvester could tip into it and
    /// nothing could ever come out. Drawing turns litres back into harvest items at the
    /// crop's own rate, which is the same rate a trader prices litres by - so storing a
    /// harvest and carrying it are two ways of holding the same thing, not two
    /// economies.
    ///
    /// Bulk sells faster at a counter and hand-carried produce sells for more. Which of
    /// those you want is the decision this screen exists to put in front of you.
    /// </summary>
    public class SiloScreen : MonoBehaviour
    {
        const float RowHeight = 56f;
        const float RowGap = 6f;
        const float PanelWidth = 720f;

        PlayerRig _player;
        ContentDatabase _content;
        SiloStructure _bin;

        Canvas _canvas;
        RectTransform _panel, _rowRoot;
        Text _total, _footer, _heading;
        ClaimSlate.Gauge _fill;

        readonly List<Row> _rows = new List<Row>();

        public bool IsOpen { get { return _canvas != null && _canvas.enabled; } }
        public SiloStructure Bin { get { return _bin; } }

        class Row
        {
            public CropDefinition Crop;
            public Image Background;
            public Text Name, Detail;
            public Button Draw;
            public Text DrawLabel;
        }

        public void Init(PlayerRig player, ContentDatabase content)
        {
            _player = player;
            _content = content;

            _canvas = UIKit.CreateCanvas("SiloScreen", 12, transform);
            var backdrop = ClaimSlate.Surface(_canvas.transform, "Backdrop", ClaimSlate.Fade(ClaimSlate.OilBlack, 0.78f));
            ClaimSlate.Stretch(backdrop.rectTransform);

            Build();
            _canvas.enabled = false;
        }

        void Build()
        {
            var body = ClaimSlate.Plate(_canvas.transform, "SiloPlate", "GRAIN BIN",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(PanelWidth, 620f), true);
            _panel = (RectTransform)body.parent;

            var stamp = ClaimSlate.Stencil(_panel, "Stamp", "MADGENIUS  -  STORE", 15,
                TextAnchor.UpperLeft, ClaimSlate.Dim(ClaimSlate.Bone, 0.34f), false);
            ClaimSlate.Place(ClaimSlate.Holder(stamp), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(26f, -18f), new Vector2(420f, 18f));

            // The one number that matters, and a grain tick beside it - same shape the
            // food gauge uses, so bulk produce reads as food everywhere it appears.
            _total = ClaimSlate.Stencil(body, "Total", "0 L", 44, TextAnchor.UpperLeft, ClaimSlate.SodiumGold);
            ClaimSlate.Place(ClaimSlate.Holder(_total), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(24f, -24f), new Vector2(360f, 48f));

            _fill = ClaimSlate.CreateGauge(body, "Fill", ClaimSlate.CropSage, ClaimSlate.Tick.Grain, 300f);
            ClaimSlate.Place(_fill.Root, new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-24f, -34f), new Vector2(300f, 20f));

            _heading = ClaimSlate.Stencil(body, "Heading", "STORED", 18,
                TextAnchor.UpperLeft, ClaimSlate.OxideRust);
            ClaimSlate.Place(ClaimSlate.Holder(_heading), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(24f, -84f), new Vector2(PanelWidth - 48f, 20f));

            var rule = ClaimSlate.Fill(body, "Rule", ClaimSlate.Dim(ClaimSlate.Bone, 0.18f));
            ClaimSlate.Place(rule.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(24f, -108f), new Vector2(PanelWidth - 48f, 1f));

            _rowRoot = ClaimSlate.Rect(body, "Rows");
            ClaimSlate.Place(_rowRoot, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(24f, -118f), new Vector2(PanelWidth - 48f, 400f));

            _footer = ClaimSlate.Stencil(body, "Footer", "", 16,
                TextAnchor.LowerLeft, ClaimSlate.BoneDim, false);
            ClaimSlate.Place(ClaimSlate.Holder(_footer), new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(26f, 18f), new Vector2(PanelWidth - 52f, 42f));
        }

        // -------------------------------------------------------------------- open

        public void Open(SiloStructure bin)
        {
            _bin = bin;
            if (_canvas != null) _canvas.enabled = true;

            Rebuild();
            Refresh();
        }

        public void Close()
        {
            if (_canvas != null) _canvas.enabled = false;
            _bin = null;
        }

        static int Count()
        {
            return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) ? 10 : 1;
        }

        void Rebuild()
        {
            for (int i = 0; i < _rows.Count; i++)
            {
                if (_rows[i].Background != null) Destroy(_rows[i].Background.gameObject);
            }
            _rows.Clear();

            if (_bin == null || _content == null) return;

            foreach (var entry in _bin.Contents)
            {
                var crop = _content.Crop(entry.Key);
                if (crop == null) continue;

                _rows.Add(BuildRow(crop, _rows.Count));
            }
        }

        Row BuildRow(CropDefinition crop, int slot)
        {
            var card = ClaimSlate.Surface(_rowRoot, "Crop" + slot, ClaimSlate.Metal);
            ClaimSlate.Place(card.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, -slot * (RowHeight + RowGap)), new Vector2(PanelWidth - 48f, RowHeight));
            ClaimSlate.Frame(card.rectTransform, ClaimSlate.Dim(ClaimSlate.Bone, 0.14f), 1f);

            var name = ClaimSlate.Stencil(card.transform, "Name", crop.displayName.ToUpperInvariant(), 20,
                TextAnchor.MiddleLeft, ClaimSlate.Bone);
            ClaimSlate.Place(ClaimSlate.Holder(name), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(16f, 0f), new Vector2(260f, 24f));

            var detail = ClaimSlate.Stencil(card.transform, "Detail", "", 17,
                TextAnchor.MiddleRight, ClaimSlate.SodiumGold, false);
            ClaimSlate.Place(ClaimSlate.Holder(detail), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-136f, 0f), new Vector2(300f, 22f));

            var draw = UIKit.Button(card.transform, "Draw", "DRAW", 16);
            ClaimSlate.Place(draw.GetComponent<RectTransform>(), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-10f, 0f), new Vector2(116f, 34f));

            var captured = crop;
            draw.onClick.AddListener(() => Draw(captured));

            return new Row
            {
                Crop = crop,
                Background = card,
                Name = name,
                Detail = detail,
                Draw = draw,
                DrawLabel = draw.GetComponentInChildren<Text>()
            };
        }

        void Draw(CropDefinition crop)
        {
            if (_bin == null || _player == null) return;

            int taken = _bin.Draw(crop, _player.Inventory.Bag, Count());
            if (taken <= 0)
            {
                Notifications.Post("Not enough in the bin, or no room in your bag");
                return;
            }

            Notifications.PostFormat("Drew {0} {1}", taken, crop.displayName);

            Rebuild();
            Refresh();
        }

        // ----------------------------------------------------------------- refresh

        void Update()
        {
            if (IsOpen) Refresh();
        }

        public void Refresh()
        {
            if (_bin == null) return;

            float stored = _bin.StoredLitres;
            float capacity = Mathf.Max(1f, _bin.Capacity);

            _total.text = string.Format("{0:0} L", stored);
            _fill.Set(stored, capacity, string.Format("{0:0}%", stored / capacity * 100f), ClaimSlate.CropSage);

            _heading.text = _rows.Count > 0 ? "STORED" : "STORED  -  EMPTY";

            for (int i = 0; i < _rows.Count; i++) RefreshRow(_rows[i]);

            _footer.text = _rows.Count > 0
                ? "SHIFT-CLICK DRAWS TEN   -   HAND-CARRIED PRODUCE SELLS FOR MORE THAN A TIPPED HOPPER"
                : "PARK A LOADED HARVESTER WITHIN 8 M AND PRESS V TO TIP A HAUL IN";
        }

        void RefreshRow(Row row)
        {
            float litres = _bin.StoredOf(row.Crop.stringId);
            int available = row.Crop.ItemsFromLitres(litres);

            row.Detail.text = string.Format("{0:0} L   =   {1} {2}", litres, available,
                row.Crop.harvestItem != null ? row.Crop.harvestItem.displayName.ToUpperInvariant() : "");

            bool canDraw = available > 0 && row.Crop.harvestItem != null
                           && _player.Inventory.Bag.CanFit(row.Crop.harvestItem, 1);

            row.Draw.interactable = canDraw;
            row.DrawLabel.text = available <= 0 ? "SHORT" : (canDraw ? "DRAW" : "BAG FULL");
        }
    }
}
