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
    /// The skills screen. One column of categories, one scrolling list of perks, and a
    /// buy button per row that says why it is disabled rather than just greying out.
    /// </summary>
    public class PerkScreen : MonoBehaviour
    {
        const float RowHeight = 104f;
        const float RowGap = 6f;
        const float ListWidth = 980f;
        const float CategoryWidth = 240f;

        PlayerRig _player;
        PerkTreeDefinition _tree;

        Canvas _canvas;
        RectTransform _panel;
        RectTransform _listContent;
        Text _header;
        UIKit.Bar _xpBar;

        PerkCategory _category = PerkCategory.Mining;

        readonly List<CategoryTab> _tabs = new List<CategoryTab>();
        readonly List<PerkRow> _rows = new List<PerkRow>();

        public bool IsOpen { get { return _canvas != null && _canvas.enabled; } }

        class CategoryTab
        {
            public PerkCategory Category;
            public Image Background;
            public Text Label;
        }

        class PerkRow
        {
            public PerkDefinition Perk;
            public Image Background;
            public Text Title;
            public Text Description;
            public Text Effects;
            public Button Buy;
            public Text BuyLabel;
            public Image BuyBackground;
        }

        public void Init(PlayerRig player, ContentDatabase content)
        {
            _player = player;
            _tree = content != null ? content.perkTree : null;

            _canvas = UIKit.CreateCanvas("PerkScreen", 11, transform);
            var backdrop = UIKit.Image(_canvas.transform, "Backdrop", new Color(0f, 0f, 0f, 0.7f));
            UIKit.Stretch(backdrop.rectTransform);

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
            var panel = UIKit.Image(_canvas.transform, "Panel", UIKit.Panel);
            UIKit.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(1320f, 820f));
            _panel = panel.rectTransform;

            var title = UIKit.Label(panel.transform, "Title", "SKILLS", 34, TextAnchor.UpperLeft, UIKit.Accent);
            UIKit.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -18f), new Vector2(400f, 40f));

            _header = UIKit.Label(panel.transform, "Header", "", 24, TextAnchor.UpperRight, UIKit.TextMain);
            UIKit.Place(_header.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-28f, -22f), new Vector2(700f, 32f));

            _xpBar = UIKit.CreateBar(panel.transform, "XpBar", new Color(0.35f, 0.62f, 0.85f));
            UIKit.Place(_xpBar.Root, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -62f), new Vector2(1264f, 18f));

            var hint = UIKit.Label(panel.transform, "Hint", "P or Esc to close", 18, TextAnchor.LowerRight, UIKit.TextDim);
            UIKit.Place(hint.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-28f, 14f), new Vector2(400f, 24f));

            var viewport = UIKit.Image(panel.transform, "Viewport", new Color(0f, 0f, 0f, 0.25f));
            UIKit.Place(viewport.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-28f, -28f), new Vector2(ListWidth, 660f));
            viewport.gameObject.AddComponent<RectMask2D>();

            _listContent = UIKit.Rect(viewport.transform, "Content");
            _listContent.anchorMin = new Vector2(0f, 1f);
            _listContent.anchorMax = new Vector2(1f, 1f);
            _listContent.pivot = new Vector2(0.5f, 1f);
            _listContent.anchoredPosition = Vector2.zero;
            _listContent.sizeDelta = Vector2.zero;

            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.content = _listContent;
            scroll.viewport = viewport.rectTransform;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 34f;
        }

        void BuildCategories()
        {
            var panel = _panel;
            var categories = (PerkCategory[])Enum.GetValues(typeof(PerkCategory));

            for (int i = 0; i < categories.Length; i++)
            {
                var category = categories[i];
                var button = UIKit.Button(panel, "Tab" + category, category.ToString(), 24);
                UIKit.Place(button.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0f, 1f),
                    new Vector2(28f, -92f - i * 58f), new Vector2(CategoryWidth, 50f));

                var label = button.GetComponentInChildren<Text>();
                label.alignment = TextAnchor.MiddleLeft;
                label.rectTransform.offsetMin = new Vector2(16f, 0f);

                var captured = category;
                button.onClick.AddListener(() => SelectCategory(captured));

                _tabs.Add(new CategoryTab
                {
                    Category = category,
                    Background = button.GetComponent<Image>(),
                    Label = label
                });
            }
        }

        // -------------------------------------------------------------------- open

        public void Open()
        {
            _canvas.enabled = true;
            RebuildRows();
            Refresh();
        }

        public void Close()
        {
            if (_canvas != null) _canvas.enabled = false;
        }

        void SelectCategory(PerkCategory category)
        {
            _category = category;
            RebuildRows();
            Refresh();
        }

        // -------------------------------------------------------------------- rows

        void RebuildRows()
        {
            for (int i = 0; i < _rows.Count; i++)
            {
                if (_rows[i].Background != null) Destroy(_rows[i].Background.gameObject);
            }
            _rows.Clear();

            if (_tree == null)
            {
                _listContent.sizeDelta = Vector2.zero;
                return;
            }

            int row = 0;
            for (int i = 0; i < _tree.perks.Count; i++)
            {
                var perk = _tree.perks[i];
                if (perk == null || perk.category != _category) continue;

                _rows.Add(BuildRow(perk, row));
                row++;
            }

            _listContent.sizeDelta = new Vector2(0f, row * (RowHeight + RowGap) + 12f);
        }

        PerkRow BuildRow(PerkDefinition perk, int index)
        {
            var background = UIKit.Image(_listContent, "Perk_" + perk.stringId, UIKit.PanelSoft);
            UIKit.Place(background.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(8f, -(index * (RowHeight + RowGap)) - 8f), new Vector2(ListWidth - 32f, RowHeight));

            var title = UIKit.Label(background.transform, "Title", perk.displayName, 26, TextAnchor.UpperLeft, UIKit.TextMain);
            UIKit.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -12f), new Vector2(640f, 30f));

            var description = UIKit.Label(background.transform, "Desc", perk.description, 19, TextAnchor.UpperLeft, UIKit.TextDim);
            UIKit.Place(description.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -44f), new Vector2(700f, 24f));

            var effects = UIKit.Label(background.transform, "Effects", "", 18, TextAnchor.UpperLeft, UIKit.Accent);
            UIKit.Place(effects.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -72f), new Vector2(760f, 24f));

            var buy = UIKit.Button(background.transform, "Buy", "", 20);
            UIKit.Place(buy.GetComponent<RectTransform>(), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-16f, 0f), new Vector2(200f, 56f));

            var captured = perk;
            buy.onClick.AddListener(() => Buy(captured));

            return new PerkRow
            {
                Perk = perk,
                Background = background,
                Title = title,
                Description = description,
                Effects = effects,
                Buy = buy,
                BuyLabel = buy.GetComponentInChildren<Text>(),
                BuyBackground = buy.GetComponent<Image>()
            };
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

            _header.text = string.Format("Level {0}    {1} point(s) unspent", progression.Level, progression.UnspentPerkPoints);
            _xpBar.Set(progression.Xp, progression.XpToNext,
                string.Format("XP {0} / {1}", Mathf.FloorToInt(progression.Xp), Mathf.FloorToInt(progression.XpToNext)));

            for (int i = 0; i < _tabs.Count; i++)
            {
                bool active = _tabs[i].Category == _category;
                _tabs[i].Background.color = active ? UIKit.SlotHot : UIKit.PanelSoft;
                _tabs[i].Label.color = active ? UIKit.Accent : UIKit.TextMain;
            }

            for (int i = 0; i < _rows.Count; i++)
            {
                var row = _rows[i];
                var perk = row.Perk;
                int rank = progression.GetRank(perk.stringId);

                row.Title.text = string.Format("{0}   rank {1}/{2}", perk.displayName, rank, perk.maxRank);
                row.Title.color = rank > 0 ? UIKit.Accent : UIKit.TextMain;
                row.Effects.text = PerkService.DescribeEffects(perk, rank);

                var verdict = PerkService.Evaluate(perk, rank, progression.Level,
                    progression.UnspentPerkPoints, progression.GetRank);

                bool buyable = verdict == PerkPurchase.Ok;
                row.Buy.interactable = buyable;
                row.BuyLabel.text = buyable
                    ? string.Format("Buy  ({0} pt)", PerkService.CostOfNextRank(perk))
                    : PerkService.Explain(verdict, perk, _tree);
                row.BuyLabel.color = buyable ? UIKit.TextMain : UIKit.TextDim;
                row.BuyBackground.color = buyable ? UIKit.SlotHot : new Color(0.09f, 0.09f, 0.1f, 0.9f);
            }
        }
    }
}
