using System.Collections.Generic;
using System.IO;
using MadVoxel.AI;
using MadVoxel.Building;
using MadVoxel.Content;
using MadVoxel.Farming.Crops;
using MadVoxel.Horde;
using MadVoxel.Inventory;
using MadVoxel.Perks;
using MadVoxel.Quests;
using MadVoxel.Traders;
using MadVoxel.Vehicles;
using MadVoxel.World.Terrain;
using UnityEngine;

namespace MadVoxel.Modding
{
    /// <summary>One definition still waiting for its cross-references to be resolved.</summary>
    public struct PendingLink
    {
        public string ModId;
        public string Kind;
        public JsonValue Entry;
        public Object Target;
        public string SourceFile;
    }

    /// <summary>
    /// Turns a mod's JSON into real definitions on the content database.
    ///
    /// Loading happens in two passes. The first creates and patches every definition's
    /// own values; the second wires up references between them. That split is what lets
    /// one mod's recipe use another mod's item regardless of which loaded first - and it
    /// is why a mod never has to care about load order for anything but overrides.
    /// </summary>
    public static class ModContentApplier
    {
        public const string OpKey = "$op";
        public const string IdKey = "id";

        // ------------------------------------------------------------- pass one

        public static void ApplyDefinitions(ContentDatabase database, ModManifest manifest, ModLog log,
                                            List<PendingLink> pending)
        {
            var files = ModLoader.ContentFiles(manifest);
            if (files.Count == 0)
            {
                log.Warn("has no content/*.json files, so it changes nothing");
                return;
            }

            for (int i = 0; i < files.Count; i++)
            {
                ApplyFile(database, manifest, files[i], log, pending);
            }
        }

        static void ApplyFile(ContentDatabase database, ModManifest manifest, string path, ModLog log,
                              List<PendingLink> pending)
        {
            string text;
            try
            {
                text = File.ReadAllText(path);
            }
            catch (System.Exception ex)
            {
                log.Error("could not read '{0}': {1}", Path.GetFileName(path), ex.Message);
                return;
            }

            string error;
            var root = Json.Parse(text, out error);
            if (root == null)
            {
                log.Error("{0} is not valid JSON - {1}", Path.GetFileName(path), error);
                return;
            }

            if (root.Kind != JsonKind.Object)
            {
                log.Error("{0} should be a JSON object whose fields are content types", Path.GetFileName(path));
                return;
            }

            string file = Path.GetFileName(path);

            foreach (var section in root.Object)
            {
                switch (section.Key)
                {
                    case "config":
                        PatchConfig(database, section.Value, log, file);
                        break;
                    case "hordeSchedule":
                        PatchHorde(database, section.Value, log, file, pending, manifest);
                        break;
                    case "startingItems":
                        pending.Add(Link(manifest, "startingItems", section.Value, null, file));
                        break;
                    default:
                        ApplySection(database, manifest, section.Key, section.Value, log, file, pending);
                        break;
                }
            }
        }

        static PendingLink Link(ModManifest manifest, string kind, JsonValue entry, Object target, string file)
        {
            return new PendingLink { ModId = manifest.Id, Kind = kind, Entry = entry, Target = target, SourceFile = file };
        }

        static void ApplySection(ContentDatabase database, ModManifest manifest, string kind, JsonValue list,
                                 ModLog log, string file, List<PendingLink> pending)
        {
            if (!IsKnownKind(kind))
            {
                log.Warn("{0}: '{1}' is not a content type this game knows. Try one of {2}",
                    file, kind, string.Join(", ", KnownKinds));
                return;
            }

            if (list.Kind != JsonKind.Array)
            {
                log.Error("{0}: '{1}' should be a list", file, kind);
                return;
            }

            for (int i = 0; i < list.Count; i++)
            {
                var entry = list[i];
                if (entry.Kind != JsonKind.Object)
                {
                    log.Error("{0}: entry {1} of '{2}' is not an object", file, i, kind);
                    continue;
                }

                var idValue = entry[IdKey];
                if (idValue == null || idValue.Kind != JsonKind.String || string.IsNullOrEmpty(idValue.String))
                {
                    log.Error("{0}: an entry in '{1}' on line {2} has no \"id\"", file, kind, entry.Line);
                    continue;
                }

                string id = idValue.String;
                string op = "patch";
                var opValue = entry[OpKey];
                if (opValue != null && opValue.Kind == JsonKind.String) op = opValue.String.ToLowerInvariant();

                if (op == "remove")
                {
                    Remove(database, kind, id, log, file);
                    continue;
                }

                if (op != "patch" && op != "replace" && op != "add")
                {
                    log.Error("{0}: '{1}' on line {2} is not a known \"$op\". Use patch, replace or remove.",
                        file, op, entry.Line);
                    continue;
                }

                var target = FindOrCreate(database, kind, id, op == "replace", log);
                if (target == null) continue;

                string what = string.Format("{0} {1} '{2}'", file, kind, id);
                ApplyOwnValues(kind, target, entry, log, what);
                pending.Add(Link(manifest, kind, entry, target, file));
            }
        }

        // --------------------------------------------------------------- lookup

        static readonly string[] KnownKinds =
        {
            "blocks", "items", "recipes", "structures", "buildPieces",
            "crops", "zombies", "perks", "quests", "traders", "vehicles", "implements"
        };

        static bool IsKnownKind(string kind)
        {
            for (int i = 0; i < KnownKinds.Length; i++) if (KnownKinds[i] == kind) return true;
            return false;
        }

        static Object FindExisting(ContentDatabase db, string kind, string id)
        {
            switch (kind)
            {
                case "blocks": return db.blocks.ByStringId(id);
                case "items": return db.Item(id);
                case "recipes": return db.Recipe(id);
                case "structures": return db.Structure(id);
                case "buildPieces": return db.BuildPiece(id);
                case "crops": return db.Crop(id);
                case "zombies": return db.Zombie(id);
                case "perks": return db.perkTree != null ? db.perkTree.Find(id) : null;
                case "quests": return Find(db.quests, id, q => q.stringId);
                case "traders": return Find(db.traders, id, t => t.stringId);
                case "vehicles": return Find(db.vehicles, id, v => v.stringId);
                case "implements": return Find(db.implements, id, i => i.stringId);
            }
            return null;
        }

        static T Find<T>(List<T> list, string id, System.Func<T, string> idOf) where T : Object
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] != null && idOf(list[i]) == id) return list[i];
            }
            return null;
        }

        static Object FindOrCreate(ContentDatabase db, string kind, string id, bool forceNew, ModLog log)
        {
            var existing = forceNew ? null : FindExisting(db, kind, id);
            if (existing != null) return existing;

            if (forceNew) Remove(db, kind, id, log, null);

            switch (kind)
            {
                case "blocks":
                {
                    var def = ScriptableObject.CreateInstance<BlockDefinition>();
                    def.stringId = id; def.name = id; def.displayName = id;
                    db.blocks.blocks.Add(def);
                    db.blocks.Build();
                    db.Build();
                    return def;
                }
                case "items": return Add(db, db.items, ScriptableObject.CreateInstance<ItemDefinition>(), id, d => d.stringId = id);
                case "recipes": return Add(db, db.recipes, ScriptableObject.CreateInstance<RecipeDefinition>(), id, d => d.stringId = id);
                case "structures": return Add(db, db.structures, ScriptableObject.CreateInstance<StructureDefinition>(), id, d => d.stringId = id);
                case "buildPieces": return Add(db, db.buildPieces, ScriptableObject.CreateInstance<BuildPieceDefinition>(), id, d => d.stringId = id);
                case "crops": return Add(db, db.crops, ScriptableObject.CreateInstance<CropDefinition>(), id, d => d.stringId = id);
                case "zombies": return Add(db, db.zombies, ScriptableObject.CreateInstance<ZombieDefinition>(), id, d => d.stringId = id);
                case "quests": return Add(db, db.quests, ScriptableObject.CreateInstance<QuestDefinition>(), id, d => d.stringId = id);
                case "traders": return Add(db, db.traders, ScriptableObject.CreateInstance<TraderDefinition>(), id, d => d.stringId = id);
                case "vehicles": return Add(db, db.vehicles, ScriptableObject.CreateInstance<VehicleDefinition>(), id, d => d.stringId = id);
                case "implements": return Add(db, db.implements, ScriptableObject.CreateInstance<MadVoxel.Vehicles.ImplementDefinition>(), id, d => d.stringId = id);
                case "perks":
                {
                    if (db.perkTree == null)
                    {
                        log.Error("this game has no perk tree to add '{0}' to", id);
                        return null;
                    }
                    var def = ScriptableObject.CreateInstance<PerkDefinition>();
                    def.stringId = id; def.name = id; def.displayName = id;
                    db.perkTree.perks.Add(def);
                    db.Build();
                    return def;
                }
            }
            return null;
        }

        static T Add<T>(ContentDatabase db, List<T> list, T created, string id, System.Action<T> setId) where T : Object
        {
            setId(created);
            created.name = id;
            list.Add(created);
            db.Build();
            return created;
        }

        static void Remove(ContentDatabase db, string kind, string id, ModLog log, string file)
        {
            var existing = FindExisting(db, kind, id);
            if (existing == null)
            {
                if (file != null) log.Warn("{0}: nothing called '{1}' to remove", file, id);
                return;
            }

            switch (kind)
            {
                case "blocks": db.blocks.blocks.Remove((BlockDefinition)existing); db.blocks.Build(); break;
                case "items": db.items.Remove((ItemDefinition)existing); break;
                case "recipes": db.recipes.Remove((RecipeDefinition)existing); break;
                case "structures": db.structures.Remove((StructureDefinition)existing); break;
                case "buildPieces": db.buildPieces.Remove((BuildPieceDefinition)existing); break;
                case "crops": db.crops.Remove((CropDefinition)existing); break;
                case "zombies": db.zombies.Remove((ZombieDefinition)existing); break;
                case "quests": db.quests.Remove((QuestDefinition)existing); break;
                case "traders": db.traders.Remove((TraderDefinition)existing); break;
                case "vehicles": db.vehicles.Remove((VehicleDefinition)existing); break;
                case "perks": if (db.perkTree != null) db.perkTree.perks.Remove((PerkDefinition)existing); break;
            }
            db.Build();
        }

        // -------------------------------------------------------- own values

        static void ApplyOwnValues(string kind, Object target, JsonValue entry, ModLog log, string what)
        {
            var r = new JsonReader(entry, log, what);

            switch (kind)
            {
                case "blocks": ModDefinitions.ReadBlock((BlockDefinition)target, r); break;
                case "items": ModDefinitions.ReadItem((ItemDefinition)target, r); break;
                case "recipes": ModDefinitions.ReadRecipe((RecipeDefinition)target, r); break;
                case "structures": ModDefinitions.ReadStructure((StructureDefinition)target, r); break;
                case "buildPieces": ModDefinitions.ReadBuildPiece((BuildPieceDefinition)target, r); break;
                case "crops": ModDefinitions.ReadCrop((CropDefinition)target, r); break;
                case "zombies": ModDefinitions.ReadZombie((ZombieDefinition)target, r); break;
                case "perks": ModDefinitions.ReadPerk((PerkDefinition)target, r); break;
                case "quests": ModDefinitions.ReadQuest((QuestDefinition)target, r); break;
                case "traders": ModDefinitions.ReadTrader((TraderDefinition)target, r); break;
                case "vehicles": ModDefinitions.ReadVehicle((VehicleDefinition)target, r); break;
                case "implements": ModDefinitions.ReadImplement((MadVoxel.Vehicles.ImplementDefinition)target, r); break;
            }
        }

        static void PatchConfig(ContentDatabase db, JsonValue entry, ModLog log, string file)
        {
            if (db.config == null) return;
            var r = new JsonReader(entry, log, file + " config");
            ModDefinitions.ReadConfig(db.config, r);
        }

        static void PatchHorde(ContentDatabase db, JsonValue entry, ModLog log, string file,
                               List<PendingLink> pending, ModManifest manifest)
        {
            if (db.hordeSchedule == null) return;
            var r = new JsonReader(entry, log, file + " hordeSchedule");
            ModDefinitions.ReadHorde(db.hordeSchedule, r);
            pending.Add(Link(manifest, "hordeSchedule", entry, db.hordeSchedule, file));
        }

        // -------------------------------------------------------------- pass two

        /// <summary>
        /// Wires up every reference once all mods have contributed their definitions, so
        /// a recipe can name an item that a later-loading mod added.
        /// </summary>
        public static void ResolveLinks(ContentDatabase database, List<PendingLink> pending, ModLog log)
        {
            database.Build();

            for (int i = 0; i < pending.Count; i++)
            {
                var link = pending[i];
                log.CurrentModId = link.ModId;

                string what = string.Format("{0} {1}", link.SourceFile, link.Kind);
                var links = new ModLinker(database, log, what);

                switch (link.Kind)
                {
                    case "blocks": ModDefinitions.LinkBlock((BlockDefinition)link.Target, link.Entry, links); break;
                    case "items": ModDefinitions.LinkItem((ItemDefinition)link.Target, link.Entry, links); break;
                    case "recipes": ModDefinitions.LinkRecipe((RecipeDefinition)link.Target, link.Entry, links); break;
                    case "structures": ModDefinitions.LinkStructure((StructureDefinition)link.Target, link.Entry, links); break;
                    case "buildPieces": ModDefinitions.LinkBuildPiece((BuildPieceDefinition)link.Target, link.Entry, links); break;
                    case "crops": ModDefinitions.LinkCrop((CropDefinition)link.Target, link.Entry, links); break;
                    case "zombies": ModDefinitions.LinkZombie((ZombieDefinition)link.Target, link.Entry, links); break;
                    case "quests": ModDefinitions.LinkQuest((QuestDefinition)link.Target, link.Entry, links); break;
                    case "traders": ModDefinitions.LinkTrader((TraderDefinition)link.Target, link.Entry, links); break;
                    case "vehicles": ModDefinitions.LinkVehicle((VehicleDefinition)link.Target, link.Entry, links); break;
                    case "implements": ModDefinitions.LinkImplement((MadVoxel.Vehicles.ImplementDefinition)link.Target, link.Entry, links); break;
                    case "hordeSchedule": ModDefinitions.LinkHorde((HordeSchedule)link.Target, link.Entry, links); break;
                    case "startingItems": ModDefinitions.LinkStartingItems(database, link.Entry, links); break;
                }
            }

            log.CurrentModId = null;
            database.Build();
        }
    }
}
