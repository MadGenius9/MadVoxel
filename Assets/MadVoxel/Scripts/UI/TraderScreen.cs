using System.Collections.Generic;
using MadVoxel.Core.Player;
using MadVoxel.Inventory;
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
        const float RowGap = 4f;
        const float ColumnWidth = 500f;

        /// <summary>Holding shift buys or sells ten. The footer says so.</summary>
        const int BulkCount = 10;

        PlayerRig _player;
        TraderPost _post;

        Canvas _canvas;
        RectTransform _panel;
        RectTransform _stockRoot, _sellRoot;
        Text _who, _tier, _tierProgress, _tokens, _greeting, _footer, _sellHeading;

        readonly List<StockRow> _stockRows = new List<StockRow>();
        readonly List<SellRow> _sellRows = new List<SellRow>();
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

        public void Init(PlayerRig player)
        {
            _player = player;

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

            // ---- the two columns
            Column(body, "THEY SELL", new Vector2(0f, 1f), new Vector2(24f, -132f), out _stockRoot);

            var sellHeader = Column(body, "THEY BUY", new Vector2(1f, 1f), new Vector2(-24f, -132f), out _sellRoot);
            _sellHeading = sellHeader;

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
                new Vector2(offset.x, offset.y - 34f), new Vector2(ColumnWidth, 500f));

            return label;
        }

        // -------------------------------------------------------------------- open

        public void Open(TraderPost post)
        {
            _post = post;
            if (_canvas != null) _canvas.enabled = true;

            RebuildStock();
            RebuildSell();
            Refresh();
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
            for (int i = 0; i < _stockRows.Count; i++)
            {
                if (_stockRows[i].Background != null) Destroy(_stockRows[i].Background.gameObject);
            }
            _stockRows.Clear();

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

            int spent;
            var result = _post.State.Buy(_player.Inventory.Bag, index, Count(), out spent);

            if (result == TradeResult.Ok)
            {
                Core.Notifications.PostFormat("Bought {0} for {1} tokens",
                    _post.State.Line(index).item.displayName, spent);
            }
            else
            {
                Core.Notifications.Post(TraderState.Describe(result));
            }

            RebuildSell();
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
            for (int i = 0; i < _sellRows.Count; i++)
            {
                if (_sellRows[i].Background != null) Destroy(_sellRows[i].Background.gameObject);
            }
            _sellRows.Clear();
            _sellable.Clear();

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

            RebuildSell();
            Refresh();
        }

        // ----------------------------------------------------------------- refresh

        void Update()
        {
            if (IsOpen) Refresh();
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

            for (int i = 0; i < _stockRows.Count; i++) RefreshStockRow(_stockRows[i], tier, tokens);
            for (int i = 0; i < _sellRows.Count; i++) RefreshSellRow(_sellRows[i], tier, bag);

            _sellHeading.text = _sellRows.Count > 0 ? "THEY BUY" : "THEY BUY  -  NOTHING YOU ARE CARRYING";

            _footer.text = "SHIFT-CLICK TRADES TEN   -   PARK A LOADED HARVESTER AT THE COUNTER AND PRESS V TO SELL A HAUL";
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
