using System.Collections.Generic;
using MadVoxel.Content;
using MadVoxel.Core;
using MadVoxel.Core.Player;
using MadVoxel.Inventory;
using MadVoxel.Perks;
using MadVoxel.Quests;
using MadVoxel.Traders;
using UnityEngine;
using UnityEngine.UI;

namespace MadVoxel.UI
{
    /// <summary>
    /// The counter, as a Claim Slate screen: a ticket on the left saying who you are to
    /// this trader, their price board in the middle, and what they will take off you on
    /// the right.
    ///
    /// Numbers beat icon soup, so every line is a name, a count and a price rather than
    /// a grid of pictures. Locked lines stay on the board with the tier that opens them
    /// printed where the price would be - a shop that hides its stock until you qualify
    /// gives you no reason to qualify.
    /// </summary>
    public class TraderScreen : MonoBehaviour
    {
        const float RowHeight = 44f;
        const float QuestRowHeight = 76f;
        const float RowGap = 4f;
        const float ColumnWidth = 500f;

        /// <summary>Seconds between idle refreshes. Every action refreshes immediately.</summary>
        const float RefreshInterval = 0.1f;

        /// <summary>Holding shift buys or sells ten. The footer says so.</summary>
        const int BulkCount = 10;

        /// <summary>
        /// Goods and contracts share the two columns rather than fighting for a third.
        /// A counter is one conversation with two halves, not two screens.
        /// </summary>
        public enum Tab { Goods, Contracts }

        PlayerRig _player;
        ContentDatabase _content;
        WorldClock _clock;
        TraderPost _post;
        Tab _tab = Tab.Goods;
        float _sinceRefresh;

        Canvas _canvas;
        RectTransform _panel;
        RectTransform _stockRoot, _sellRoot;
        Text _who, _tier, _tierProgress, _tokens, _greeting, _footer, _sellHeading;

        Button _goodsTab, _contractsTab;
        Text _goodsTabLabel, _contractsTabLabel;
        Text _leftHeading;

        readonly List<StockRow> _stockRows = new List<StockRow>();
        readonly List<SellRow> _sellRows = new List<SellRow>();
        readonly List<QuestRow> _offerRows = new List<QuestRow>();
        readonly List<QuestRow> _activeRows = new List<QuestRow>();
        readonly List<ItemDefinition> _sellable = new List<ItemDefinition>();

        public bool IsOpen { get { return _canvas != null && _canvas.enabled; } }
        public TraderPost Post { get { return _post; } }

        class StockRow
        {
            public int Index;
            public Image Background;
            public Text Name, Detail, Price;
            public Button Buy;
            public Text BuyLabel;
        }

        class SellRow
        {
            public ItemDefinition Item;
            public Image Background;
            public Text Name, Detail;
            public Button Sell;
            public Text SellLabel;
        }

        class QuestRow
        {
            public QuestDefinition Quest;
            public Image Background;
            public Text Title, Detail;
            public Button Action;
            public Text ActionLabel;
        }

        public void Init(PlayerRig player, ContentDatabase content, WorldClock clock)
        {
            _player = player;
            _content = content;
            _clock = clock;

            _canvas = UIKit.CreateCanvas("TraderScreen", 12, transform);
            var backdrop = ClaimSlate.Surface(_canvas.transform, "Backdrop", ClaimSlate.Fade(ClaimSlate.OilBlack, 0.78f));
            ClaimSlate.Stretch(backdrop.rectTransform);

            Build();
            _canvas.enabled = false;
        }

        void Build()
        {
            var body = ClaimSlate.Plate(_canvas.transform, "TraderPlate", "TRADE",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1120f, 760f), true);
            _panel = (RectTransform)body.parent;

            var stamp = ClaimSlate.Stencil(_panel, "Stamp", "MADGENIUS  -  COUNTER", 15,
                TextAnchor.UpperLeft, ClaimSlate.Dim(ClaimSlate.Bone, 0.34f), false);
            ClaimSlate.Place(ClaimSlate.Holder(stamp), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(26f, -18f), new Vector2(420f, 18f));

            // ---- the ticket: who you are to this trader
            var ticket = ClaimSlate.Surface(body, "Ticket", ClaimSlate.Metal);
            ClaimSlate.Place(ticket.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(24f, -24f), new Vector2(620f, 92f));
            ClaimSlate.Frame(ticket.rectTransform, ClaimSlate.Dim(ClaimSlate.Bone, 0.20f), 1f);
            ClaimSlate.Rivets(ticket.rectTransform, 10f, 4f);

            _who = ClaimSlate.Stencil(ticket.transform, "Who", "TRADER", 26, TextAnchor.UpperLeft, ClaimSlate.Bone);
            ClaimSlate.Place(ClaimSlate.Holder(_who), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(18f, -14f), new Vector2(420f, 30f));

            _greeting = ClaimSlate.Stencil(ticket.transform, "Greeting", "", 16,
                TextAnchor.UpperLeft, ClaimSlate.BoneDim, false);
            ClaimSlate.Place(ClaimSlate.Holder(_greeting), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(18f, -48f), new Vector2(580f, 34f));

            _tier = ClaimSlate.Stencil(ticket.transform, "Tier", "STRANGER", 22,
                TextAnchor.UpperRight, ClaimSlate.SodiumGold);
            ClaimSlate.Place(ClaimSlate.Holder(_tier), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-18f, -14f), new Vector2(240f, 26f));

            _tierProgress = ClaimSlate.Stencil(ticket.transform, "TierProgress", "", 15,
                TextAnchor.UpperRight, ClaimSlate.BoneDim, false);
            ClaimSlate.Place(ClaimSlate.Holder(_tierProgress), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-18f, -44f), new Vector2(240f, 20f));

            // ---- the purse
            var purse = ClaimSlate.Surface(body, "Purse", ClaimSlate.Metal);
            ClaimSlate.Place(purse.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-24f, -24f), new Vector2(420f, 92f));
            ClaimSlate.Frame(purse.rectTransform, ClaimSlate.Dim(ClaimSlate.Bone, 0.20f), 1f);

            var purseLabel = ClaimSlate.Stencil(purse.transform, "PurseLabel", "TOKENS", 15,
                TextAnchor.UpperLeft, ClaimSlate.BoneDim, false);
            ClaimSlate.Place(ClaimSlate.Holder(purseLabel), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(18f, -14f), new Vector2(200f, 18f));

            _tokens = ClaimSlate.Stencil(purse.transform, "Tokens", "0", 40,
                TextAnchor.UpperLeft, ClaimSlate.SodiumGold);
            ClaimSlate.Place(ClaimSlate.Holder(_tokens), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(18f, -36f), new Vector2(380f, 44f));

            // ---- the tabs
            _goodsTab = UIKit.Button(body, "GoodsTab", "GOODS", 17);
            ClaimSlate.Place(_goodsTab.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(24f, -124f), new Vector2(160f, 34f));
            _goodsTabLabel = _goodsTab.GetComponentInChildren<Text>();
            _goodsTab.onClick.AddListener(() => SetTab(Tab.Goods));

            _contractsTab = UIKit.Button(body, "ContractsTab", "CONTRACTS", 17);
            ClaimSlate.Place(_contractsTab.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(192f, -124f), new Vector2(190f, 34f));
            _contractsTabLabel = _contractsTab.GetComponentInChildren<Text>();
            _contractsTab.onClick.AddListener(() => SetTab(Tab.Contracts));

            // ---- the two columns
            _leftHeading = Column(body, "THEY SELL", new Vector2(0f, 1f), new Vector2(24f, -172f), out _stockRoot);
            _sellHeading = Column(body, "THEY BUY", new Vector2(1f, 1f), new Vector2(-24f, -172f), out _sellRoot);

            _footer = ClaimSlate.Stencil(body, "Footer", "", 16,
                TextAnchor.LowerLeft, ClaimSlate.BoneDim, false);
            ClaimSlate.Place(ClaimSlate.Holder(_footer), new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(26f, 18f), new Vector2(1040f, 42f));
        }

        Text Column(Transform body, string heading, Vector2 anchor, Vector2 offset, out RectTransform root)
        {
            var label = ClaimSlate.Stencil(body, heading, heading, 18, TextAnchor.UpperLeft, ClaimSlate.OxideRust);
            ClaimSlate.Place(ClaimSlate.Holder(label), anchor, anchor, offset, new Vector2(ColumnWidth, 20f));

            var rule = ClaimSlate.Fill(body, heading + "Rule", ClaimSlate.Dim(ClaimSlate.Bone, 0.18f));
            ClaimSlate.Place(rule.rectTransform, anchor, anchor,
                new Vector2(offset.x, offset.y - 24f), new Vector2(ColumnWidth, 1f));

            root = ClaimSlate.Rect(body, heading + "Rows");
            ClaimSlate.Place(root, anchor, anchor,
                new Vector2(offset.x, offset.y - 34f), new Vector2(ColumnWidth, 460f));

            return label;
        }

        // -------------------------------------------------------------------- open

        public void Open(TraderPost post)
        {
            _post = post;
            if (_canvas != null) _canvas.enabled = true;

            RebuildAll();
            Refresh();
        }

        void SetTab(Tab tab)
        {
            _tab = tab;
            RebuildAll();
            Refresh();
        }

        void RebuildAll()
        {
            ClearRows();

            if (_tab == Tab.Goods)
            {
                RebuildStock();
                RebuildSell();
            }
            else
            {
                RebuildOffers();
                RebuildActive();
            }
        }

        void ClearRows()
        {
            for (int i = 0; i < _stockRows.Count; i++) DestroyRow(_stockRows[i].Background);
            for (int i = 0; i < _sellRows.Count; i++) DestroyRow(_sellRows[i].Background);
            for (int i = 0; i < _offerRows.Count; i++) DestroyRow(_offerRows[i].Background);
            for (int i = 0; i < _activeRows.Count; i++) DestroyRow(_activeRows[i].Background);

            _stockRows.Clear();
            _sellRows.Clear();
            _offerRows.Clear();
            _activeRows.Clear();
            _sellable.Clear();
        }

        static void DestroyRow(Image background)
        {
            if (background != null) Destroy(background.gameObject);
        }

        public void Close()
        {
            if (_canvas != null) _canvas.enabled = false;
            _post = null;
        }

        static int Count()
        {
            return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) ? BulkCount : 1;
        }

        // ------------------------------------------------------------------- stock

        void RebuildStock()
        {
            if (_post == null || _post.State == null) return;

            for (int i = 0; i < _post.State.LineCount; i++)
            {
                if (_post.State.Line(i).item == null) continue;
                _stockRows.Add(BuildStockRow(i, _stockRows.Count));
            }
        }

        StockRow BuildStockRow(int index, int slot)
        {
            var card = ClaimSlate.Surface(_stockRoot, "Stock" + index, ClaimSlate.Metal);
            ClaimSlate.Place(card.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, -slot * (RowHeight + RowGap)), new Vector2(ColumnWidth, RowHeight));
            ClaimSlate.Frame(card.rectTransform, ClaimSlate.Dim(ClaimSlate.Bone, 0.14f), 1f);

            var name = ClaimSlate.Stencil(card.transform, "Name", "", 18, TextAnchor.MiddleLeft, ClaimSlate.Bone);
            ClaimSlate.Place(ClaimSlate.Holder(name), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(14f, 0f), new Vector2(230f, 22f));

            var detail = ClaimSlate.Stencil(card.transform, "Detail", "", 15,
                TextAnchor.MiddleLeft, ClaimSlate.BoneDim, false);
            ClaimSlate.Place(ClaimSlate.Holder(detail), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(250f, 0f), new Vector2(100f, 20f));

            var price = ClaimSlate.Stencil(card.transform, "Price", "", 20,
                TextAnchor.MiddleRight, ClaimSlate.SodiumGold);
            ClaimSlate.Place(ClaimSlate.Holder(price), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-104f, 0f), new Vector2(120f, 24f));

            var buy = UIKit.Button(card.transform, "Buy", "BUY", 16);
            ClaimSlate.Place(buy.GetComponent<RectTransform>(), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-8f, 0f), new Vector2(88f, 32f));

            int captured = index;
            buy.onClick.AddListener(() => Buy(captured));

            return new StockRow
            {
                Index = index,
                Background = card,
                Name = name,
                Detail = detail,
                Price = price,
                Buy = buy,
                BuyLabel = buy.GetComponentInChildren<Text>()
            };
        }

        void Buy(int index)
        {
            if (_post == null || _player == null) return;

            int spent, bought;
            var result = _post.State.Buy(_player.Inventory.Bag, index, Count(), out spent, out bought);

            if (result == TradeResult.Ok)
            {
                // The count, because a shift-click can be clamped by the shelf or the
                // purse and the player deserves to know which they got.
                Core.Notifications.PostFormat("Bought {0} {1} for {2} tokens",
                    bought, _post.State.Line(index).item.displayName, spent);
            }
            else
            {
                Core.Notifications.Post(TraderState.Describe(result));
            }

            RebuildAll();
            Refresh();
        }

        // -------------------------------------------------------------------- sell

        /// <summary>
        /// What is in your bag that they will take. Rebuilt whenever the bag changes,
        /// because the list is the bag - a fixed catalogue of "things traders buy"
        /// would show you rows for things you are not carrying.
        /// </summary>
        void RebuildSell()
        {
            if (_post == null || _player == null) return;

            var bag = _player.Inventory.Bag;
            for (int i = 0; i < bag.Size; i++)
            {
                var stack = bag[i];
                if (stack.IsEmpty || stack.Item == null) continue;
                if (_sellable.Contains(stack.Item)) continue;
                if (!TraderPricing.WillBuy(_post.Definition, stack.Item)) continue;

                _sellable.Add(stack.Item);
            }

            for (int i = 0; i < _sellable.Count; i++) _sellRows.Add(BuildSellRow(_sellable[i], i));
        }

        SellRow BuildSellRow(ItemDefinition item, int slot)
        {
            var card = ClaimSlate.Surface(_sellRoot, "Sell" + slot, ClaimSlate.Metal);
            ClaimSlate.Place(card.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, -slot * (RowHeight + RowGap)), new Vector2(ColumnWidth, RowHeight));
            ClaimSlate.Frame(card.rectTransform, ClaimSlate.Dim(ClaimSlate.Bone, 0.14f), 1f);

            var name = ClaimSlate.Stencil(card.transform, "Name", item.displayName.ToUpperInvariant(), 18,
                TextAnchor.MiddleLeft, ClaimSlate.Bone);
            ClaimSlate.Place(ClaimSlate.Holder(name), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(14f, 0f), new Vector2(250f, 22f));

            var detail = ClaimSlate.Stencil(card.transform, "Detail", "", 15,
                TextAnchor.MiddleRight, ClaimSlate.CropSage, false);
            ClaimSlate.Place(ClaimSlate.Holder(detail), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-104f, 0f), new Vector2(140f, 20f));

            var sell = UIKit.Button(card.transform, "Sell", "SELL", 16);
            ClaimSlate.Place(sell.GetComponent<RectTransform>(), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-8f, 0f), new Vector2(88f, 32f));

            var captured = item;
            sell.onClick.AddListener(() => Sell(captured));

            return new SellRow
            {
                Item = item,
                Background = card,
                Name = name,
                Detail = detail,
                Sell = sell,
                SellLabel = sell.GetComponentInChildren<Text>()
            };
        }

        void Sell(ItemDefinition item)
        {
            if (_post == null || _player == null) return;

            var bag = _player.Inventory.Bag;
            int wanted = Mathf.Min(Count(), bag.CountOf(item));

            int earned;
            var result = _post.State.Sell(bag, item, wanted, out earned);

            if (result == TradeResult.Ok)
            {
                Core.Notifications.PostFormat("Sold {0} {1} for {2} tokens",
                    wanted, item.displayName, earned);
            }
            else
            {
                Core.Notifications.Post(TraderState.Describe(result));
            }

            RebuildAll();
            Refresh();
        }

        // ------------------------------------------------------------- contracts

        double NowHours { get { return _clock != null ? _clock.TotalHours : 0.0; } }

        /// <summary>
        /// What this trader has on the board. Contracts you cannot take yet stay
        /// listed with the reason in place of the button, for the same reason locked
        /// stock does: a board that hides work gives you no reason to qualify for it.
        /// </summary>
        void RebuildOffers()
        {
            if (_post == null || _player == null || _post.Definition == null) return;

            var board = _post.Definition.questBoard;
            for (int i = 0; i < board.Count; i++)
            {
                var quest = board[i];
                if (quest == null) continue;
                if (_player.Quests.IsComplete(quest.stringId)) continue;
                if (_player.Quests.IsActive(quest.stringId)) continue;

                _offerRows.Add(BuildQuestRow(_stockRoot, quest, _offerRows.Count, "TAKE", () => Accept(quest)));
            }
        }

        void RebuildActive()
        {
            if (_player == null) return;

            var active = _player.Quests.Active;
            for (int i = 0; i < active.Count; i++)
            {
                var quest = _content != null ? _content.Quest(active[i].QuestId) : null;
                if (quest == null) continue;

                var captured = quest;
                _activeRows.Add(BuildQuestRow(_sellRoot, quest, _activeRows.Count, "HAND IN", () => TurnIn(captured)));
            }
        }

        QuestRow BuildQuestRow(RectTransform parent, QuestDefinition quest, int slot,
                               string action, UnityEngine.Events.UnityAction onClick)
        {
            var card = ClaimSlate.Surface(parent, "Quest" + slot, ClaimSlate.Metal);
            ClaimSlate.Place(card.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, -slot * (QuestRowHeight + RowGap)), new Vector2(ColumnWidth, QuestRowHeight));
            ClaimSlate.Frame(card.rectTransform, ClaimSlate.Dim(ClaimSlate.Bone, 0.14f), 1f);

            var title = ClaimSlate.Stencil(card.transform, "Title", quest.title.ToUpperInvariant(), 18,
                TextAnchor.UpperLeft, ClaimSlate.Bone);
            ClaimSlate.Place(ClaimSlate.Holder(title), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(14f, -10f), new Vector2(360f, 22f));

            var detail = ClaimSlate.Stencil(card.transform, "Detail", "", 15,
                TextAnchor.UpperLeft, ClaimSlate.BoneDim, false);
            ClaimSlate.Place(ClaimSlate.Holder(detail), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(14f, -34f), new Vector2(470f, 34f));

            var button = UIKit.Button(card.transform, "Action", action, 15);
            ClaimSlate.Place(button.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-8f, -8f), new Vector2(104f, 30f));
            button.onClick.AddListener(onClick);

            return new QuestRow
            {
                Quest = quest,
                Background = card,
                Title = title,
                Detail = detail,
                Action = button,
                ActionLabel = button.GetComponentInChildren<Text>()
            };
        }

        void Accept(QuestDefinition quest)
        {
            if (_post == null || _player == null) return;

            var result = _player.Quests.Accept(quest, _post.Definition.stringId,
                _player.Progression.Level, _post.State.Tier, NowHours);

            Notifications.Post(result == QuestResult.Ok
                ? "Took the contract: " + quest.title
                : QuestService.Describe(result));

            RebuildAll();
            Refresh();
        }

        /// <summary>
        /// Hands a contract in. The log moves the goods and the rewards; the XP and the
        /// reputation belong to the systems that own them, and are applied here once
        /// the log has said the hand-in went through.
        /// </summary>
        void TurnIn(QuestDefinition quest)
        {
            if (_post == null || _player == null) return;

            // A contract is handed in to the trader who issued it. Walking a finished
            // job to the other outpost should not pay out.
            string issuer = _player.Quests.IssuerOf(quest.stringId);
            if (!string.IsNullOrEmpty(issuer) && issuer != _post.Definition.stringId)
            {
                Notifications.Post("That contract belongs to another trader");
                return;
            }

            var result = _player.Quests.TurnIn(quest, _player.Inventory.Bag, NowHours);
            if (result != QuestResult.Ok)
            {
                Notifications.Post(QuestService.Describe(result));
                return;
            }

            _player.Progression.AddXp(quest.xpReward, XpSource.Quest);
            _post.State.AddReputation(quest.reputationReward);

            Notifications.PostFormat("{0} - paid, +{1} rep", quest.title, quest.reputationReward);

            RebuildAll();
            Refresh();
        }

        // ----------------------------------------------------------------- refresh

        /// <summary>
        /// Ten times a second, not sixty. Nothing on a counter changes faster than you
        /// can click, and the contracts tab asks whether each reward would fit - which
        /// plays the hand-in out on a copy of your bag. That is cheap once and wasteful
        /// sixty times a second for a screen nobody is watching change.
        /// </summary>
        void Update()
        {
            if (!IsOpen) return;

            _sinceRefresh += Time.unscaledDeltaTime;
            if (_sinceRefresh < RefreshInterval) return;

            _sinceRefresh = 0f;
            Refresh();
        }

        public void Refresh()
        {
            if (_post == null || _post.State == null || _player == null) return;

            var state = _post.State;
            var bag = _player.Inventory.Bag;
            int tier = state.Tier;

            _who.text = _post.Definition.displayName.ToUpperInvariant();
            _greeting.text = _post.Definition.greeting;
            _tier.text = state.TierName;

            int owed = state.ToNextTier;
            _tierProgress.text = owed > 0
                ? string.Format("{0} REP TO NEXT", owed)
                : "TOP OF THE BOOK";

            int tokens = state.Tokens(bag);
            _tokens.text = tokens.ToString();

            _goodsTabLabel.color = _tab == Tab.Goods ? ClaimSlate.SodiumGold : ClaimSlate.BoneDim;
            _contractsTabLabel.color = _tab == Tab.Contracts ? ClaimSlate.SodiumGold : ClaimSlate.BoneDim;
            _goodsTab.interactable = _tab != Tab.Goods;
            _contractsTab.interactable = _tab != Tab.Contracts;

            if (_tab == Tab.Goods)
            {
                for (int i = 0; i < _stockRows.Count; i++) RefreshStockRow(_stockRows[i], tier, tokens);
                for (int i = 0; i < _sellRows.Count; i++) RefreshSellRow(_sellRows[i], tier, bag);

                _leftHeading.text = "THEY SELL";
                _sellHeading.text = _sellRows.Count > 0 ? "THEY BUY" : "THEY BUY  -  NOTHING YOU ARE CARRYING";
                _footer.text = "SHIFT-CLICK TRADES TEN   -   PARK A LOADED HARVESTER AT THE COUNTER AND PRESS V TO SELL A HAUL";
                return;
            }

            for (int i = 0; i < _offerRows.Count; i++) RefreshOfferRow(_offerRows[i], tier);
            for (int i = 0; i < _activeRows.Count; i++) RefreshActiveRow(_activeRows[i], bag);

            _leftHeading.text = _offerRows.Count > 0 ? "ON THE BOARD" : "ON THE BOARD  -  NOTHING NEW";
            _sellHeading.text = string.Format("YOU ARE CARRYING  {0} / {1}",
                _player.Quests.ActiveCount, QuestService.MaxActive);

            _footer.text = "CONTRACTS ARE HANDED IN TO THE TRADER WHO ISSUED THEM";
        }

        void RefreshStockRow(StockRow row, int tier, int tokens)
        {
            var entry = _post.State.Line(row.Index);
            row.Name.text = entry.item.displayName.ToUpperInvariant();

            // A locked line stays on the board with the tier that opens it where the
            // price would be. Hiding it gives you no reason to earn the tier.
            if (!_post.State.IsOffered(row.Index))
            {
                row.Detail.text = "";
                row.Price.text = TraderPricing.TierName(entry.minReputationTier);
                row.Price.color = ClaimSlate.OxideRust;
                row.Buy.interactable = false;
                row.BuyLabel.text = "LOCKED";
                row.Name.color = ClaimSlate.BoneDim;
                return;
            }

            int stock = _post.State.StockOf(row.Index);
            int price = _post.State.PriceOf(row.Index);

            row.Name.color = stock > 0 ? ClaimSlate.Bone : ClaimSlate.BoneDim;
            row.Detail.text = stock > 0 ? stock + " LEFT" : "SOLD OUT";
            row.Detail.color = stock > 0 ? ClaimSlate.BoneDim : ClaimSlate.OxideRust;

            row.Price.text = price.ToString();
            // Colour and word together: SHORT is the signal, gold-versus-rust confirms it.
            bool affordable = tokens >= price;
            row.Price.color = affordable ? ClaimSlate.SodiumGold : ClaimSlate.OxideRust;

            row.Buy.interactable = stock > 0 && affordable;
            row.BuyLabel.text = stock <= 0 ? "NONE" : (affordable ? "BUY" : "SHORT");
        }

        void RefreshOfferRow(QuestRow row, int tier)
        {
            var quest = row.Quest;

            var check = QuestService.CanAccept(quest, _player.Progression.Level, tier,
                _player.Quests.ActiveCount, false, false);

            row.Detail.text = quest.description;
            row.Detail.color = check == QuestResult.Ok ? ClaimSlate.BoneDim : ClaimSlate.OxideRust;

            // The reason replaces the button's word rather than sitting beside it, so
            // there is only ever one thing to read about why you cannot take a job.
            row.Action.interactable = check == QuestResult.Ok;
            switch (check)
            {
                case QuestResult.Ok: row.ActionLabel.text = "TAKE"; break;
                case QuestResult.LevelTooLow:
                    row.ActionLabel.text = "LVL " + quest.requiredPlayerLevel; break;
                case QuestResult.ReputationTooLow:
                    row.ActionLabel.text = TraderPricing.TierName(quest.requiredReputationTier); break;
                case QuestResult.TooManyTaken: row.ActionLabel.text = "FULL"; break;
                default: row.ActionLabel.text = "NO"; break;
            }

            row.Title.color = check == QuestResult.Ok ? ClaimSlate.Bone : ClaimSlate.BoneDim;
        }

        void RefreshActiveRow(QuestRow row, MadVoxel.Inventory.Inventory bag)
        {
            var quest = row.Quest;
            var entry = _player.Quests.EntryOf(quest.stringId);

            bool mine = _player.Quests.IssuerOf(quest.stringId) == _post.Definition.stringId;
            var check = QuestService.CanTurnIn(quest, entry, bag, NowHours);

            row.Detail.text = QuestService.ProgressLine(quest, entry, bag, NowHours);

            bool done = check == QuestResult.Ok || check == QuestResult.NoRoomForReward;
            row.Detail.color = done ? ClaimSlate.CropSage : ClaimSlate.BoneDim;
            row.Title.color = done ? ClaimSlate.Bone : ClaimSlate.BoneDim;

            row.Action.interactable = mine && check == QuestResult.Ok;

            if (!mine) row.ActionLabel.text = "ELSEWHERE";
            else if (check == QuestResult.NoRoomForReward) row.ActionLabel.text = "BAG FULL";
            else if (check == QuestResult.Ok) row.ActionLabel.text = "HAND IN";
            else row.ActionLabel.text = "IN PROGRESS";
        }

        void RefreshSellRow(SellRow row, int tier, MadVoxel.Inventory.Inventory bag)
        {
            int held = bag.CountOf(row.Item);
            int each = TraderPricing.BuyPrice(_post.Definition, row.Item, tier);

            row.Detail.text = string.Format("{0} x {1}", held, each);
            row.Sell.interactable = held > 0;
            row.SellLabel.text = held > 0 ? "SELL" : "NONE";
        }
    }
}
