using System;
using System.Collections.Generic;
using MadVoxel.Content;
using MadVoxel.Core;
using MadVoxel.Core.Player;
using MadVoxel.Perks;
using UnityEngine;
using UnityEngine.UI;

namespace MadVoxel.UI
{
    /// <summary>
    /// The skills screen as a shop-manual wiring diagram. A rust trunk runs down the
    /// category, each perk hangs off it on a lead, and the node at the junction says
    /// whether the circuit is live: welded shut when you own a rank, drawn in grease
    /// pencil when you do not. Rank pips repeat the same reading as filled or empty
    /// boxes, so ownership never depends on colour alone.
    /// </summary>
    public class PerkScreen : MonoBehaviour
    {
        const float RowHeight = 104f;
        const float RowGap = 8f;
        const float ListWidth = 1000f;
        const float CategoryWidth = 236f;

        const float BusX = 22f;
        const float NodeX = 44f;
        const float CardX = 70f;

        PlayerRig _player;
        PerkTreeDefinition _tree;

        Canvas _canvas;
        RectTransform _panel;
        RectTransform _listContent;
        RectTransform _bus;
        Text _header;
        UIKit.Bar _xpBar;

        PerkCategory _category = PerkCategory.Mining;

        readonly List<CategoryTab> _tabs = new List<CategoryTab>();
        readonly List<PerkNode> _nodes = new List<PerkNode>();

        public bool IsOpen { get { return _canvas != null && _canvas.enabled; } }

        class CategoryTab
        {
            public PerkCategory Category;
            public Image Background;
            public Image Marker;
            public Text Label;
        }

        /// <summary>One perk: its junction on the trunk, and the card hanging off it.</summary>
        class PerkNode
        {
            public PerkDefinition Perk;
            public Image Card;
            public Image Lead;
            public Image Junction;
            public Image JunctionCore;
            public Text Title;
            public Text Description;
            public Text Effects;
            public Button Buy;
            public Text BuyLabel;
            public Image BuyBackground;
            public readonly List<Image> RankPips = new List<Image>();
            public readonly List<Image> RankFills = new List<Image>();
        }

        public void Init(PlayerRig player, ContentDatabase content)
        {
            _player = player;
            _tree = content != null ? content.perkTree : null;

            _canvas = UIKit.CreateCanvas("PerkScreen", 11, transform);

            var backdrop = ClaimSlate.Surface(_canvas.transform, "Backdrop", ClaimSlate.Fade(ClaimSlate.OilBlack, 0.82f));
            ClaimSlate.Stretch(backdrop.rectTransform);

            BuildFrame();
            BuildCategories();

            if (_player != null && _player.Progression != null) _player.Progression.Changed += OnProgressionChanged;
            _canvas.enabled = false;
        }

        void OnDestroy()
        {
            if (_player != null && _player.Progression != null) _player.Progression.Changed -= OnProgressionChanged;
        }

        void OnProgressionChanged()
        {
            if (IsOpen) Refresh();
        }

        // ------------------------------------------------------------------- build

        void BuildFrame()
        {
            var plate = ClaimSlate.Surface(_canvas.transform, "Panel", ClaimSlate.SlateDeep);
            ClaimSlate.Place(plate.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(1340f, 830f));
            _panel = plate.rectTransform;
            ClaimSlate.Frame(_panel, ClaimSlate.Dim(ClaimSlate.Bone, 0.24f));
            ClaimSlate.Rivets(_panel, 16f, 6f);

            var title = ClaimSlate.Stencil(_panel, "Title", "SKILLS", 34, TextAnchor.UpperLeft, ClaimSlate.Bone);
            ClaimSlate.Place(ClaimSlate.Holder(title), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(32f, -20f), new Vector2(400f, 40f));

            var stamp = ClaimSlate.Stencil(_panel, "Stamp", "MADGENIUS  -  FIELD MANUAL", 15,
                TextAnchor.UpperLeft, ClaimSlate.Dim(ClaimSlate.Bone, 0.34f), false);
            ClaimSlate.Place(ClaimSlate.Holder(stamp), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(34f, -58f), new Vector2(400f, 20f));

            _header = ClaimSlate.Stencil(_panel, "Header", "", 22, TextAnchor.UpperRight, ClaimSlate.Bone);
            ClaimSlate.Place(ClaimSlate.Holder(_header), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-32f, -26f), new Vector2(700f, 28f));

            _xpBar = UIKit.CreateBar(_panel, "XpBar", ClaimSlate.SodiumGold);
            ClaimSlate.Place(_xpBar.Root, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(32f, -86f), new Vector2(1276f, 14f));

            var hint = ClaimSlate.Stencil(_panel, "Hint", "P OR ESC TO CLOSE", 16,
                TextAnchor.LowerRight, ClaimSlate.BoneDim, false);
            ClaimSlate.Place(ClaimSlate.Holder(hint), new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-32f, 16f), new Vector2(400f, 22f));

            var viewport = ClaimSlate.Surface(_panel, "Viewport", ClaimSlate.Fade(ClaimSlate.OilBlack, 0.35f));
            ClaimSlate.Place(viewport.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-30f, -34f), new Vector2(ListWidth, 646f));
            viewport.gameObject.AddComponent<RectMask2D>();

            _listContent = ClaimSlate.Rect(viewport.transform, "Content");
            _listContent.anchorMin = new Vector2(0f, 1f);
            _listContent.anchorMax = new Vector2(1f, 1f);
            _listContent.pivot = new Vector2(0.5f, 1f);
            _listContent.anchoredPosition = Vector2.zero;
            _listContent.sizeDelta = Vector2.zero;

            // The trunk. Everything in the category hangs off this one line.
            var bus = ClaimSlate.Fill(_listContent, "Bus", ClaimSlate.OxideRust);
            bus.rectTransform.anchorMin = new Vector2(0f, 1f);
            bus.rectTransform.anchorMax = new Vector2(0f, 1f);
            bus.rectTransform.pivot = new Vector2(0.5f, 1f);
            bus.rectTransform.anchoredPosition = new Vector2(BusX, 0f);
            _bus = bus.rectTransform;

            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.content = _listContent;
            scroll.viewport = viewport.rectTransform;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 36f;
        }

        void BuildCategories()
        {
            var categories = (PerkCategory[])Enum.GetValues(typeof(PerkCategory));

            for (int i = 0; i < categories.Length; i++)
            {
                var category = categories[i];
                var card = ClaimSlate.Surface(_panel, "Tab" + category, ClaimSlate.Metal);
                ClaimSlate.Place(card.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                    new Vector2(32f, -114f - i * 58f), new Vector2(CategoryWidth, 50f));
                ClaimSlate.Frame(card.rectTransform, ClaimSlate.Dim(ClaimSlate.Bone, 0.18f), 1f);

                var button = card.gameObject.AddComponent<Button>();
                var colours = button.colors;
                colours.normalColor = Color.white;
                colours.highlightedColor = new Color(1.30f, 1.10f, 0.94f);
                colours.pressedColor = new Color(0.82f, 0.60f, 0.42f);
                button.colors = colours;

                // A rust spine on the selected tab, mirroring the trunk in the list.
                var marker = ClaimSlate.Fill(card.transform, "Marker", ClaimSlate.OxideRust);
                ClaimSlate.Place(marker.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                    Vector2.zero, new Vector2(4f, 50f));

                var label = ClaimSlate.Stencil(card.transform, "Label", category.ToString().ToUpperInvariant(), 22,
                    TextAnchor.MiddleLeft, ClaimSlate.Bone);
                ClaimSlate.Place(ClaimSlate.Holder(label), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                    new Vector2(20f, 0f), new Vector2(CategoryWidth - 30f, 26f));

                var captured = category;
                button.onClick.AddListener(() => SelectCategory(captured));

                _tabs.Add(new CategoryTab
                {
                    Category = category,
                    Background = card,
                    Marker = marker,
                    Label = label
                });
            }
        }

        // -------------------------------------------------------------------- open

        public void Open()
        {
            _canvas.enabled = true;
            RebuildNodes();
            Refresh();
        }

        public void Close()
        {
            if (_canvas != null) _canvas.enabled = false;
        }

        void SelectCategory(PerkCategory category)
        {
            _category = category;
            RebuildNodes();
            Refresh();
        }

        // ------------------------------------------------------------------- nodes

        void RebuildNodes()
        {
            for (int i = 0; i < _nodes.Count; i++)
            {
                if (_nodes[i].Card != null) Destroy(_nodes[i].Card.gameObject);
                if (_nodes[i].Lead != null) Destroy(_nodes[i].Lead.gameObject);
                if (_nodes[i].Junction != null) Destroy(_nodes[i].Junction.gameObject);
            }
            _nodes.Clear();

            if (_tree == null)
            {
                _listContent.sizeDelta = Vector2.zero;
                _bus.sizeDelta = new Vector2(2f, 0f);
                return;
            }

            int row = 0;
            for (int i = 0; i < _tree.perks.Count; i++)
            {
                var perk = _tree.perks[i];
                if (perk == null || perk.category != _category) continue;

                _nodes.Add(BuildNode(perk, row));
                row++;
            }

            float height = row * (RowHeight + RowGap) + 12f;
            _listContent.sizeDelta = new Vector2(0f, height);

            // The trunk stops at the last junction rather than running off the page.
            float busLength = row > 0 ? (row - 1) * (RowHeight + RowGap) + RowHeight * 0.5f + 8f : 0f;
            _bus.sizeDelta = new Vector2(2f, busLength);
        }

        PerkNode BuildNode(PerkDefinition perk, int index)
        {
            float top = -(index * (RowHeight + RowGap)) - 8f;
            float centre = top - RowHeight * 0.5f;

            var node = new PerkNode { Perk = perk };

            // Lead: the wire from the trunk out to the card.
            var lead = ClaimSlate.Fill(_listContent, "Lead_" + perk.stringId, ClaimSlate.Pencil);
            lead.rectTransform.anchorMin = new Vector2(0f, 1f);
            lead.rectTransform.anchorMax = new Vector2(0f, 1f);
            lead.rectTransform.pivot = new Vector2(0f, 0.5f);
            lead.rectTransform.anchoredPosition = new Vector2(BusX, centre);
            lead.rectTransform.sizeDelta = new Vector2(CardX - BusX, 2f);
            node.Lead = lead;

            // Junction: welded shut once a rank is owned, an empty pencil box until then.
            var junction = ClaimSlate.Fill(_listContent, "Node_" + perk.stringId, Color.clear);
            junction.rectTransform.anchorMin = new Vector2(0f, 1f);
            junction.rectTransform.anchorMax = new Vector2(0f, 1f);
            junction.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            junction.rectTransform.anchoredPosition = new Vector2(NodeX, centre);
            junction.rectTransform.sizeDelta = new Vector2(16f, 16f);
            ClaimSlate.Frame(junction.rectTransform, ClaimSlate.Pencil, 2f);
            node.Junction = junction;

            var core = ClaimSlate.Fill(junction.transform, "Core", ClaimSlate.OxideRust);
            ClaimSlate.Stretch(core.rectTransform, 4f);
            core.gameObject.SetActive(false);
            node.JunctionCore = core;

            // Card.
            float cardWidth = ListWidth - CardX - 24f;
            var card = ClaimSlate.Surface(_listContent, "Card_" + perk.stringId, ClaimSlate.Metal);
            card.rectTransform.anchorMin = new Vector2(0f, 1f);
            card.rectTransform.anchorMax = new Vector2(0f, 1f);
            card.rectTransform.pivot = new Vector2(0f, 1f);
            card.rectTransform.anchoredPosition = new Vector2(CardX, top);
            card.rectTransform.sizeDelta = new Vector2(cardWidth, RowHeight);
            ClaimSlate.Frame(card.rectTransform, ClaimSlate.Dim(ClaimSlate.Bone, 0.18f), 1f);
            node.Card = card;

            node.Title = ClaimSlate.Stencil(card.transform, "Title", perk.displayName.ToUpperInvariant(), 24,
                TextAnchor.UpperLeft, ClaimSlate.Bone);
            ClaimSlate.Place(ClaimSlate.Holder(node.Title), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(18f, -10f), new Vector2(520f, 28f));

            node.Description = ClaimSlate.Stencil(card.transform, "Desc", perk.description, 17,
                TextAnchor.UpperLeft, ClaimSlate.BoneDim, false);
            ClaimSlate.Place(ClaimSlate.Holder(node.Description), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(18f, -42f), new Vector2(cardWidth - 260f, 22f));

            node.Effects = ClaimSlate.Stencil(card.transform, "Effects", "", 16,
                TextAnchor.UpperLeft, ClaimSlate.SodiumGold, false);
            ClaimSlate.Place(ClaimSlate.Holder(node.Effects), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(18f, -68f), new Vector2(cardWidth - 260f, 22f));

            BuildRankPips(node, card.transform, perk);

            var buy = UIKit.Button(card.transform, "Buy", "", 18);
            ClaimSlate.Place(buy.GetComponent<RectTransform>(), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-16f, -8f), new Vector2(200f, 46f));
            node.Buy = buy;
            node.BuyLabel = buy.GetComponentInChildren<Text>();
            node.BuyBackground = buy.GetComponent<Image>();

            var captured = perk;
            buy.onClick.AddListener(() => Buy(captured));

            return node;
        }

        /// <summary>
        /// One box per rank. Filled means welded on; an outline means still penciled in.
        /// This is the colourblind half of the contract - the fill is a shape change.
        /// </summary>
        void BuildRankPips(PerkNode node, Transform parent, PerkDefinition perk)
        {
            const float PipWidth = 18f;
            const float PipGap = 4f;

            float total = perk.maxRank * (PipWidth + PipGap) - PipGap;

            for (int i = 0; i < perk.maxRank; i++)
            {
                var pip = ClaimSlate.Fill(parent, "Pip" + i, Color.clear);
                ClaimSlate.Place(pip.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f),
                    new Vector2(-16f - (total - (i * (PipWidth + PipGap)) - PipWidth), -14f),
                    new Vector2(PipWidth, 12f));
                ClaimSlate.Frame(pip.rectTransform, ClaimSlate.Pencil, 1f);

                var fill = ClaimSlate.Fill(pip.transform, "Fill", ClaimSlate.OxideRust);
                ClaimSlate.Stretch(fill.rectTransform, 2f);
                fill.gameObject.SetActive(false);

                node.RankPips.Add(pip);
                node.RankFills.Add(fill);
            }
        }

        void Buy(PerkDefinition perk)
        {
            if (_player == null || _player.Progression == null) return;

            var result = PerkService.TryBuyRank(_tree, _player.Progression, perk.stringId);
            if (result != PerkPurchase.Ok)
            {
                Notifications.Post(PerkService.Explain(result, perk, _tree));
            }
            Refresh();
        }

        // ----------------------------------------------------------------- refresh

        public void Refresh()
        {
            if (!IsOpen || _player == null || _player.Progression == null) return;

            var progression = _player.Progression;

            _header.text = string.Format("LEVEL {0}    {1} POINT(S) UNSPENT",
                progression.Level, progression.UnspentPerkPoints);
            _header.color = progression.UnspentPerkPoints > 0 ? ClaimSlate.OxideRust : ClaimSlate.Bone;

            _xpBar.Set(progression.Xp, progression.XpToNext,
                string.Format("XP {0} / {1}", Mathf.FloorToInt(progression.Xp), Mathf.FloorToInt(progression.XpToNext)));

            for (int i = 0; i < _tabs.Count; i++)
            {
                bool active = _tabs[i].Category == _category;
                _tabs[i].Background.color = active ? ClaimSlate.MetalLit : ClaimSlate.Metal;
                _tabs[i].Marker.color = active ? ClaimSlate.OxideRust : ClaimSlate.Fade(ClaimSlate.OxideRust, 0.18f);
                _tabs[i].Label.color = active ? ClaimSlate.Bone : ClaimSlate.BoneDim;
            }

            for (int i = 0; i < _nodes.Count; i++) RefreshNode(_nodes[i], progression);
        }

        void RefreshNode(PerkNode node, PlayerProgression progression)
        {
            var perk = node.Perk;
            int rank = progression.GetRank(perk.stringId);
            bool owned = rank > 0;

            // Welded or penciled: the lead and the junction both change, so the wiring
            // reads at a glance from the trunk outwards.
            node.Lead.color = owned ? ClaimSlate.Bone : ClaimSlate.Pencil;
            node.JunctionCore.gameObject.SetActive(owned);
            node.Card.color = owned ? ClaimSlate.MetalLit : ClaimSlate.Metal;
            node.Title.color = owned ? ClaimSlate.Bone : ClaimSlate.Pencil;

            for (int i = 0; i < node.RankFills.Count; i++)
            {
                bool filled = i < rank;
                if (node.RankFills[i].gameObject.activeSelf != filled) node.RankFills[i].gameObject.SetActive(filled);
            }

            node.Effects.text = PerkService.DescribeEffects(perk, rank);

            var verdict = PerkService.Evaluate(perk, rank, progression.Level,
                progression.UnspentPerkPoints, progression.GetRank);

            bool buyable = verdict == PerkPurchase.Ok;
            node.Buy.interactable = buyable;
            node.BuyLabel.text = buyable
                ? string.Format("WELD RANK {0}  ({1} PT)", rank + 1, PerkService.CostOfNextRank(perk))
                : PerkService.Explain(verdict, perk, _tree).ToUpperInvariant();
            node.BuyLabel.color = buyable ? ClaimSlate.Bone : ClaimSlate.BoneDim;
            node.BuyBackground.color = buyable ? ClaimSlate.OxideRust : ClaimSlate.Fade(ClaimSlate.OilBlack, 0.55f);
        }
    }
}
