using System.Collections.Generic;
using MadVoxel.AI;
using MadVoxel.Building;
using MadVoxel.Content;
using MadVoxel.Farming.Crops;
using MadVoxel.Inventory;
using MadVoxel.Quests;
using MadVoxel.World.Terrain;

namespace MadVoxel.Modding
{
    /// <summary>
    /// Resolves "this field points at that definition" by string id, after every mod has
    /// added its own content. An id that does not resolve is an error naming the id and
    /// the field, which is the single most common thing a mod author gets wrong.
    /// </summary>
    public class ModLinker
    {
        readonly ContentDatabase _db;
        readonly ModLog _log;
        readonly string _what;

        public ModLinker(ContentDatabase database, ModLog log, string what)
        {
            _db = database;
            _log = log;
            _what = what;
        }

        public ContentDatabase Database { get { return _db; } }
        public ModLog Log { get { return _log; } }

        bool TryId(JsonValue entry, string key, out string id, out JsonValue node)
        {
            id = null;
            node = entry != null ? entry[key] : null;
            if (node == null) return false;

            if (node.Kind == JsonKind.Null)
            {
                id = "";
                return true; // explicit null clears the reference
            }

            if (node.Kind != JsonKind.String)
            {
                _log.Error("{0}: '{1}' on line {2} should be an id in quotes", _what, key, node.Line);
                return false;
            }

            id = node.String;
            return true;
        }

        void Missing(string key, string id, JsonValue node, string kind)
        {
            _log.Error("{0}: '{1}' on line {2} refers to the {3} '{4}', which does not exist",
                _what, key, node != null ? node.Line : 0, kind, id);
        }

        public bool Item(JsonValue entry, string key, ref ItemDefinition field)
        {
            string id; JsonValue node;
            if (!TryId(entry, key, out id, out node)) return false;
            if (id.Length == 0) { field = null; return true; }

            var found = _db.Item(id);
            if (found == null) { Missing(key, id, node, "item"); return false; }
            field = found;
            return true;
        }

        public bool Vehicle(JsonValue entry, string key, ref MadVoxel.Vehicles.VehicleDefinition field)
        {
            string id; JsonValue node;
            if (!TryId(entry, key, out id, out node)) return false;
            if (id.Length == 0) { field = null; return true; }

            var found = Find(_db.vehicles, id, v => v.stringId);
            if (found == null) { Missing(key, id, node, "vehicle"); return false; }
            field = found;
            return true;
        }

        public bool Implement(JsonValue entry, string key, ref MadVoxel.Vehicles.ImplementDefinition field)
        {
            string id; JsonValue node;
            if (!TryId(entry, key, out id, out node)) return false;
            if (id.Length == 0) { field = null; return true; }

            var found = Find(_db.implements, id, i => i.stringId);
            if (found == null) { Missing(key, id, node, "implement"); return false; }
            field = found;
            return true;
        }

        static T Find<T>(System.Collections.Generic.List<T> list, string id, System.Func<T, string> idOf)
            where T : class
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] != null && idOf(list[i]) == id) return list[i];
            }
            return null;
        }

        public bool Block(JsonValue entry, string key, ref BlockDefinition field)
        {
            string id; JsonValue node;
            if (!TryId(entry, key, out id, out node)) return false;
            if (id.Length == 0) { field = null; return true; }

            var found = _db.blocks.ByStringId(id);
            if (found == null) { Missing(key, id, node, "block"); return false; }
            field = found;
            return true;
        }

        public bool Structure(JsonValue entry, string key, ref StructureDefinition field)
        {
            string id; JsonValue node;
            if (!TryId(entry, key, out id, out node)) return false;
            if (id.Length == 0) { field = null; return true; }

            var found = _db.Structure(id);
            if (found == null) { Missing(key, id, node, "structure"); return false; }
            field = found;
            return true;
        }

        public bool BuildPiece(JsonValue entry, string key, ref BuildPieceDefinition field)
        {
            string id; JsonValue node;
            if (!TryId(entry, key, out id, out node)) return false;
            if (id.Length == 0) { field = null; return true; }

            var found = _db.BuildPiece(id);
            if (found == null) { Missing(key, id, node, "build piece"); return false; }
            field = found;
            return true;
        }

        public bool Zombie(JsonValue entry, string key, ref ZombieDefinition field)
        {
            string id; JsonValue node;
            if (!TryId(entry, key, out id, out node)) return false;
            if (id.Length == 0) { field = null; return true; }

            var found = _db.Zombie(id);
            if (found == null) { Missing(key, id, node, "zombie"); return false; }
            field = found;
            return true;
        }

        public bool Quest(JsonValue entry, string key, List<QuestDefinition> into)
        {
            var node = entry != null ? entry[key] : null;
            if (node == null) return false;
            if (node.Kind != JsonKind.Array)
            {
                _log.Error("{0}: '{1}' on line {2} should be a list of quest ids", _what, key, node.Line);
                return false;
            }

            into.Clear();
            for (int i = 0; i < node.Count; i++)
            {
                var item = node[i];
                if (item.Kind != JsonKind.String) { _log.Error("{0}: '{1}' should hold quest ids", _what, key); continue; }

                QuestDefinition found = null;
                for (int q = 0; q < _db.quests.Count; q++)
                {
                    if (_db.quests[q] != null && _db.quests[q].stringId == item.String) found = _db.quests[q];
                }
                if (found == null) { Missing(key, item.String, item, "quest"); continue; }
                into.Add(found);
            }
            return true;
        }

        /// <summary>Reads [{ "item": "id", "count": 2 }, ...] into a recipe's ingredients.</summary>
        public bool Ingredients(JsonValue entry, string key, List<RecipeIngredient> into)
        {
            var node = entry != null ? entry[key] : null;
            if (node == null) return false;
            if (node.Kind != JsonKind.Array)
            {
                _log.Error("{0}: '{1}' on line {2} should be a list like [{{\"item\": \"madvoxel:plank\", \"count\": 4}}]",
                    _what, key, node.Line);
                return false;
            }

            var built = new List<RecipeIngredient>();
            for (int i = 0; i < node.Count; i++)
            {
                var row = node[i];
                if (row.Kind != JsonKind.Object)
                {
                    _log.Error("{0}: entry {1} of '{2}' should be an object with \"item\" and \"count\"", _what, i, key);
                    continue;
                }

                ItemDefinition item = null;
                if (!Item(row, "item", ref item) || item == null) continue;

                int count = 1;
                var countNode = row["count"];
                if (countNode != null && countNode.Kind == JsonKind.Number) count = UnityEngine.Mathf.RoundToInt((float)countNode.Number);
                if (count < 1) count = 1;

                built.Add(new RecipeIngredient { item = item, count = count });
            }

            into.Clear();
            into.AddRange(built);
            return true;
        }

        /// <summary>Reads [{ "item": "id", "count": 2 }, ...] into quest rewards.</summary>
        public bool Rewards(JsonValue entry, string key, List<QuestReward> into)
        {
            var node = entry != null ? entry[key] : null;
            if (node == null || node.Kind != JsonKind.Array) return false;

            var built = new List<QuestReward>();
            for (int i = 0; i < node.Count; i++)
            {
                var row = node[i];
                if (row.Kind != JsonKind.Object) continue;

                ItemDefinition item = null;
                if (!Item(row, "item", ref item) || item == null) continue;

                int count = 1;
                var countNode = row["count"];
                if (countNode != null && countNode.Kind == JsonKind.Number) count = UnityEngine.Mathf.RoundToInt((float)countNode.Number);

                built.Add(new QuestReward { item = item, count = UnityEngine.Mathf.Max(1, count) });
            }

            into.Clear();
            into.AddRange(built);
            return true;
        }

        /// <summary>Reads a trader's stock rows.</summary>
        public bool Stock(JsonValue entry, string key, List<MadVoxel.Traders.TraderStockEntry> into)
        {
            var node = entry != null ? entry[key] : null;
            if (node == null || node.Kind != JsonKind.Array) return false;

            var built = new List<MadVoxel.Traders.TraderStockEntry>();
            for (int i = 0; i < node.Count; i++)
            {
                var row = node[i];
                if (row.Kind != JsonKind.Object) continue;

                ItemDefinition item = null;
                if (!Item(row, "item", ref item) || item == null) continue;

                var stock = new MadVoxel.Traders.TraderStockEntry
                {
                    item = item,
                    stockCount = 1,
                    priceMultiplier = 1.5f,
                    minReputationTier = 0
                };

                var reader = new JsonReader(row, _log, _what + " stock");
                reader.Read("count", ref stock.stockCount);
                reader.Read("priceMultiplier", ref stock.priceMultiplier);
                reader.Read("minReputationTier", ref stock.minReputationTier);
                reader.ReportUnknownKeys("item");

                built.Add(stock);
            }

            into.Clear();
            into.AddRange(built);
            return true;
        }
    }
}
