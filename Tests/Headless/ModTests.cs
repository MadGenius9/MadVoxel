using System.Collections.Generic;
using System.IO;
using MadVoxel.Content;
using MadVoxel.Modding;
using UnityEngine;

namespace MadVoxel.Headless
{
    /// <summary>
    /// The mod pipeline, run for real: JSON in a folder on disk goes through the loader
    /// and comes out as definitions on the content database. That includes loading the
    /// example mod that ships in the repo, so the worked example can never rot.
    /// </summary>
    public static class ModTests
    {
        public static void Run()
        {
            Parsing();
            ManifestRules();
            LoadOrdering();
            Pipeline();
            ShippedExample();
        }

        // ------------------------------------------------------------- the parser

        static JsonValue Ok(string text, string label)
        {
            string error;
            var value = Json.Parse(text, out error);
            Harness.Check(value != null, label + (value == null ? " (" + error + ")" : ""));
            return value;
        }

        static void Bad(string text, string label)
        {
            string error;
            var value = Json.Parse(text, out error);
            Harness.Check(value == null && !string.IsNullOrEmpty(error),
                label + (value != null ? " (it parsed anyway)" : " -> " + error));
        }

        static void Parsing()
        {
            Harness.Section("mods: JSON parsing");

            var root = Ok("{\"a\": 1, \"b\": \"two\", \"c\": [1, 2, 3], \"d\": {\"e\": true}, \"f\": null}",
                "parses objects, arrays, numbers, strings, bools and null");
            if (root != null)
            {
                Harness.Equal(root["a"].Number, 1.0, "numbers read back");
                Harness.Equal(root["b"].String, "two", "strings read back");
                Harness.Equal(root["c"].Count, 3, "arrays keep their length");
                Harness.Equal(root["d"]["e"].Bool, true, "nested objects read back");
                Harness.Equal(root["f"].IsNull, true, "null is null");
                Harness.Equal(root["nope"] == null, true, "a missing key reads as null, not a crash");
            }

            var comments = Ok("{\n // a line comment\n \"a\": 1, /* and a block one */ \"b\": 2,\n}",
                "allows comments and a trailing comma, which hand-written mods always have");
            if (comments != null) Harness.Equal(comments.Count, 2, "and still reads both fields");

            var escapes = Ok("{\"s\": \"a\\\"b\\\\c\\nd\\u0041\"}", "handles string escapes");
            if (escapes != null) Harness.Equal(escapes["s"].String, "a\"b\\c\ndA", "escapes decode correctly");

            var numbers = Ok("{\"a\": -1.5, \"b\": 2e3, \"c\": 0.25}", "handles negative, exponent and fractional numbers");
            if (numbers != null)
            {
                Harness.Equal(numbers["a"].Number, -1.5, "negatives");
                Harness.Equal(numbers["b"].Number, 2000.0, "exponents");
            }

            Bad("{\"a\": 1", "an unclosed object is an error");
            Bad("{\"a\": \"unterminated}", "an unterminated string is an error");
            Bad("{\"a\": 1, \"a\": 2}", "a duplicate field is an error");
            Bad("{a: 1}", "an unquoted field name is an error");
            Bad("", "an empty file is an error");
            Bad("{} {}", "trailing junk is an error");

            // Line numbers are the whole point of hand-rolling this.
            string err;
            Json.Parse("{\n  \"a\": 1,\n  \"b\": oops\n}", out err);
            Harness.Check(err != null && err.Contains("line 3"), "errors name the line: " + err);
        }

        // ----------------------------------------------------------------- manifest

        static void ManifestRules()
        {
            Harness.Section("mods: manifest");

            Harness.Equal(ModManifest.IsValidId("pumpkin_patch"), true, "lowercase and underscores are a valid id");
            Harness.Equal(ModManifest.IsValidId("mod2"), true, "digits are fine");
            Harness.Equal(ModManifest.IsValidId("Pumpkin"), false, "capitals are rejected");
            Harness.Equal(ModManifest.IsValidId("my mod"), false, "spaces are rejected");
            Harness.Equal(ModManifest.IsValidId("my:mod"), false, "colons are rejected");
            Harness.Equal(ModManifest.IsValidId(""), false, "an empty id is rejected");

            var log = new ModLog();
            string error;
            var json = Json.Parse("{\"id\":\"demo\",\"name\":\"Demo\",\"loadOrder\":50,\"dependencies\":[\"other\"]}", out error);
            var manifest = ModManifest.Parse(json, "/tmp/demo", log);

            Harness.Check(manifest != null, "a well-formed manifest parses");
            if (manifest != null)
            {
                Harness.Equal(manifest.Id, "demo", "id reads back");
                Harness.Equal(manifest.LoadOrder, 50, "load order reads back");
                Harness.Equal(manifest.Dependencies.Count, 1, "dependencies read back");
            }

            var badLog = new ModLog();
            var badJson = Json.Parse("{\"id\":\"Bad Id\"}", out error);
            Harness.Check(ModManifest.Parse(badJson, "/tmp/bad", badLog) == null, "an invalid id is rejected");
            Harness.Check(badLog.ErrorCount > 0, "and says why");

            // Unknown manifest fields should warn, not fail.
            var warnLog = new ModLog();
            var typoJson = Json.Parse("{\"id\":\"demo\",\"nmae\":\"typo\"}", out error);
            Harness.Check(ModManifest.Parse(typoJson, "/tmp/typo", warnLog) != null, "a typo does not kill the manifest");
            Harness.Check(warnLog.WarningCount > 0, "but it is reported as an unknown field");
        }

        // ------------------------------------------------------------ load ordering

        static ModManifest Mod(string id, int order, params string[] dependencies)
        {
            var manifest = new ModManifest { Id = id, Name = id, LoadOrder = order };
            manifest.Dependencies.AddRange(dependencies);
            return manifest;
        }

        static void LoadOrdering()
        {
            Harness.Section("mods: load order");

            var log = new ModLog();
            var ordered = ModLoader.Order(new List<ModManifest>
            {
                Mod("charlie", 100), Mod("alpha", 100), Mod("bravo", 50)
            }, log);

            Harness.Equal(ordered.Count, 3, "all three mods survive");
            Harness.Equal(ordered[0].Id, "bravo", "a lower load order goes first");
            Harness.Equal(ordered[1].Id, "alpha", "ties break alphabetically, so order is deterministic");
            Harness.Equal(ordered[2].Id, "charlie", "and stays deterministic");

            // A dependency must load first even if its load order says otherwise.
            log = new ModLog();
            ordered = ModLoader.Order(new List<ModManifest>
            {
                Mod("addon", 10, "core"), Mod("core", 900)
            }, log);
            Harness.Equal(ordered.Count, 2, "both load");
            Harness.Equal(ordered[0].Id, "core", "a dependency loads before the mod that needs it");

            // A missing dependency drops the mod rather than half-applying it.
            log = new ModLog();
            ordered = ModLoader.Order(new List<ModManifest> { Mod("addon", 100, "absent") }, log);
            Harness.Equal(ordered.Count, 0, "a mod with a missing dependency is skipped");
            Harness.Check(log.ErrorCount > 0, "and the player is told which mod is missing");

            // Dropping one mod must drop anything that needed it.
            log = new ModLog();
            ordered = ModLoader.Order(new List<ModManifest>
            {
                Mod("addon", 100, "middle"), Mod("middle", 100, "absent")
            }, log);
            Harness.Equal(ordered.Count, 0, "a dependency chain onto a missing mod is skipped whole");

            // A cycle is reported rather than hanging.
            log = new ModLog();
            ordered = ModLoader.Order(new List<ModManifest>
            {
                Mod("a", 100, "b"), Mod("b", 100, "a")
            }, log);
            Harness.Check(log.ErrorCount > 0, "a dependency cycle is reported");
            Harness.Equal(ordered.Count, 2, "and both mods still load, in declared order");
        }

        // ------------------------------------------------------------- the pipeline

        static string MakeModsRoot(string name)
        {
            var root = Path.Combine(Path.GetTempPath(), "MadVoxelModTests_" + name + "_" + System.Guid.NewGuid().ToString("N").Substring(0, 6));
            Application.persistentDataPath = root;
            Directory.CreateDirectory(Path.Combine(root, "Mods"));
            return root;
        }

        static void WriteMod(string modsRoot, string id, string manifest, string content)
        {
            var dir = Path.Combine(Path.Combine(modsRoot, "Mods"), id);
            Directory.CreateDirectory(Path.Combine(dir, "content"));
            File.WriteAllText(Path.Combine(dir, "mod.json"), manifest);
            if (content != null) File.WriteAllText(Path.Combine(dir, "content", "content.json"), content);
        }

        static void Pipeline()
        {
            Harness.Section("mods: applying content");

            var originalPath = Application.persistentDataPath;
            var root = MakeModsRoot("pipeline");

            try
            {
                WriteMod(root, "testmod",
                    "{\"id\":\"testmod\",\"name\":\"Test Mod\"}",
                    @"{
                      ""items"": [
                        { ""id"": ""testmod:gizmo"", ""displayName"": ""Gizmo"", ""maxStack"": 12, ""tradeValue"": 99 },
                        { ""id"": ""madvoxel:plank"", ""$op"": ""patch"", ""tradeValue"": 42 },
                        { ""id"": ""madvoxel:bandage"", ""$op"": ""remove"" }
                      ],
                      ""recipes"": [
                        { ""id"": ""testmod:make_gizmo"", ""output"": ""testmod:gizmo"", ""outputCount"": 2,
                          ""station"": ""Workbench"", ""ingredients"": [ { ""item"": ""madvoxel:plank"", ""count"": 3 } ] }
                      ],
                      ""config"": { ""dayLengthSeconds"": 999 }
                    }");

                var db = ContentDatabase.LoadOrBuild();
                db.Build();

                int itemsBefore = db.items.Count;
                float dayBefore = db.config.dayLengthSeconds;
                int plankValueBefore = db.Item("madvoxel:plank").tradeValue;

                var log = new ModLog();
                var loaded = ModLoader.LoadAll(db, log);

                Harness.Equal(loaded.Count, 1, "the mod was discovered and loaded");
                Harness.Check(log.ErrorCount == 0, "with no errors" + (log.ErrorCount > 0 ? ": " + First(log) : ""));

                // Adding.
                var gizmo = db.Item("testmod:gizmo");
                Harness.Check(gizmo != null, "a new item was added");
                if (gizmo != null)
                {
                    Harness.Equal(gizmo.displayName, "Gizmo", "with its own values");
                    Harness.Equal(gizmo.maxStack, 12, "including numbers");
                }
                Harness.Equal(db.items.Count, itemsBefore + 1 - 1, "the item table grew by one and lost one");

                // Patching leaves untouched fields alone.
                var plank = db.Item("madvoxel:plank");
                Harness.Check(plank != null, "the patched vanilla item still exists");
                if (plank != null)
                {
                    Harness.Equal(plank.tradeValue, 42, "the patched field changed");
                    Harness.Check(plank.tradeValue != plankValueBefore, "and it really was different before");
                    Harness.Equal(plank.displayName, "Plank", "fields the mod did not mention are untouched");
                    Harness.Equal(plank.maxStack, 64, "including numbers it did not mention");
                }

                // Removing.
                Harness.Check(db.Item("madvoxel:bandage") == null, "a removed item is gone");

                // Cross-references resolve even though the recipe names a vanilla item
                // and a modded one.
                var recipe = db.Recipe("testmod:make_gizmo");
                Harness.Check(recipe != null, "the new recipe exists");
                if (recipe != null)
                {
                    Harness.Check(recipe.output == gizmo, "its output points at the new item");
                    Harness.Equal(recipe.ingredients.Count, 1, "its ingredients were read");
                    if (recipe.ingredients.Count > 0)
                    {
                        Harness.Check(recipe.ingredients[0].item == plank, "and point at the vanilla item");
                        Harness.Equal(recipe.ingredients[0].count, 3, "with the right count");
                    }
                }

                // Tuning.
                Harness.Equal(db.config.dayLengthSeconds, 999f, "game config was patched");
                Harness.Check(dayBefore != 999f, "and it really was different before");
            }
            finally
            {
                Application.persistentDataPath = originalPath;
                TryDelete(root);
            }

            CrossModReferences();
            BadModsAreContained();
        }

        static void CrossModReferences()
        {
            Harness.Section("mods: cross-mod references");

            var originalPath = Application.persistentDataPath;
            var root = MakeModsRoot("cross");

            try
            {
                // "first" loads before "second" but refers to its item. Because links
                // resolve after every mod has contributed, this has to work.
                WriteMod(root, "aaa_first",
                    "{\"id\":\"aaa_first\",\"loadOrder\":1}",
                    @"{ ""recipes"": [ { ""id"": ""aaa_first:borrow"", ""output"": ""zzz_second:widget"",
                        ""station"": ""Hand"", ""ingredients"": [ { ""item"": ""madvoxel:plank"", ""count"": 1 } ] } ] }");

                WriteMod(root, "zzz_second",
                    "{\"id\":\"zzz_second\",\"loadOrder\":99}",
                    @"{ ""items"": [ { ""id"": ""zzz_second:widget"", ""displayName"": ""Widget"" } ] }");

                var db = ContentDatabase.LoadOrBuild();
                db.Build();

                var log = new ModLog();
                ModLoader.LoadAll(db, log);

                Harness.Check(log.ErrorCount == 0, "both mods load cleanly" + (log.ErrorCount > 0 ? ": " + First(log) : ""));

                var recipe = db.Recipe("aaa_first:borrow");
                var widget = db.Item("zzz_second:widget");
                Harness.Check(recipe != null && widget != null, "both definitions exist");
                if (recipe != null && widget != null)
                {
                    Harness.Check(recipe.output == widget,
                        "an earlier mod can reference a later mod's item, because links resolve last");
                }
            }
            finally
            {
                Application.persistentDataPath = originalPath;
                TryDelete(root);
            }
        }

        static void BadModsAreContained()
        {
            Harness.Section("mods: a bad mod is contained");

            var originalPath = Application.persistentDataPath;
            var root = MakeModsRoot("bad");

            try
            {
                WriteMod(root, "broken", "{\"id\":\"broken\"}", "{ this is not json ");
                WriteMod(root, "typo", "{\"id\":\"typo\"}",
                    @"{ ""items"": [ { ""id"": ""typo:thing"", ""dsiplayName"": ""oops"", ""maxStack"": 4 } ] }");
                WriteMod(root, "danglng", "{\"id\":\"danglng\"}",
                    @"{ ""recipes"": [ { ""id"": ""danglng:r"", ""output"": ""nope:missing"" } ] }");
                WriteMod(root, "good", "{\"id\":\"good\"}",
                    @"{ ""items"": [ { ""id"": ""good:fine"", ""displayName"": ""Fine"" } ] }");

                var db = ContentDatabase.LoadOrBuild();
                db.Build();

                var log = new ModLog();
                var loaded = ModLoader.LoadAll(db, log);

                Harness.Check(log.ErrorCount >= 2, string.Format("broken mods produce errors ({0})", log.ErrorCount));
                Harness.Check(log.WarningCount >= 1, "and a misspelled field produces a warning naming it");

                // The critical property: one bad mod must not stop the others.
                Harness.Check(db.Item("good:fine") != null, "a good mod still loads alongside broken ones");
                Harness.Check(db.Item("typo:thing") != null, "a mod with a typo still adds what it got right");
                if (db.Item("typo:thing") != null)
                {
                    Harness.Equal(db.Item("typo:thing").maxStack, 4, "and its valid fields applied");
                }
                Harness.Equal(loaded.Count, 4, "every discovered mod is reported, even the failing ones");
            }
            finally
            {
                Application.persistentDataPath = originalPath;
                TryDelete(root);
            }
        }

        // --------------------------------------------------------- shipped example

        static void ShippedExample()
        {
            Harness.Section("mods: the example mod that ships with the repo");

            var source = FindRepoMod("example_pumpkin");
            if (source == null)
            {
                Harness.Check(false, "found Mods/example_pumpkin in the repo");
                return;
            }

            var originalPath = Application.persistentDataPath;
            var root = MakeModsRoot("example");

            try
            {
                CopyDirectory(source, Path.Combine(Path.Combine(root, "Mods"), "example_pumpkin"));

                var db = ContentDatabase.LoadOrBuild();
                db.Build();

                float bakedBefore = db.Item("madvoxel:baked_potato").staminaRestore;

                var log = new ModLog();
                var loaded = ModLoader.LoadAll(db, log);

                Harness.Equal(loaded.Count, 1, "the example mod loads");
                Harness.Check(log.ErrorCount == 0,
                    "the example mod has no errors" + (log.ErrorCount > 0 ? ": " + First(log) : ""));
                Harness.Check(log.WarningCount == 0,
                    "and no warnings" + (log.WarningCount > 0 ? ": " + FirstWarning(log) : ""));

                var crop = db.Crop("example_pumpkin:crop_pumpkin");
                Harness.Check(crop != null, "it adds a crop");
                if (crop != null)
                {
                    Harness.Check(crop.seedItem != null && crop.seedItem.stringId == "example_pumpkin:seed_pumpkin",
                        "whose seed resolves");
                    Harness.Check(crop.harvestItem != null, "and whose produce resolves");
                    Harness.Check(db.CropForSeed(crop.seedItem) == crop, "and which is reachable from its seed");
                }

                var wild = db.blocks.ByStringId("example_pumpkin:wild_pumpkin");
                Harness.Check(wild != null && wild.secondaryDropItem == crop.seedItem,
                    "its wild plant drops the seed, so the crop is findable in world");

                var pie = db.Recipe("example_pumpkin:cook_pumpkin_pie");
                Harness.Check(pie != null && pie.output != null, "its cooking recipe resolves");
                if (pie != null) Harness.Equal(pie.ingredients.Count, 2, "with both ingredients");

                Harness.Check(db.Item("madvoxel:baked_potato").staminaRestore != bakedBefore,
                    "it patches a vanilla item");

                var scarecrow = db.Zombie("example_pumpkin:scarecrow");
                Harness.Check(scarecrow != null, "it adds a zombie");
                Harness.Check(db.hordeSchedule.heavyZombie == scarecrow,
                    "and puts it into the horde schedule");
                Harness.Equal(db.hordeSchedule.everyNDays, 8, "its horde tuning applied");

                var mara = FindTrader(db, "madvoxel:trader_mara");
                Harness.Check(mara != null, "the vanilla trader still exists");
                if (mara != null)
                {
                    bool sellsSeed = false;
                    for (int i = 0; i < mara.stock.Count; i++)
                    {
                        if (mara.stock[i].item != null && mara.stock[i].item.stringId == "example_pumpkin:seed_pumpkin") sellsSeed = true;
                    }
                    Harness.Check(sellsSeed, "and now stocks the modded seed");
                }
            }
            finally
            {
                Application.persistentDataPath = originalPath;
                TryDelete(root);
            }
        }

        static MadVoxel.Traders.TraderDefinition FindTrader(ContentDatabase db, string id)
        {
            for (int i = 0; i < db.traders.Count; i++)
            {
                if (db.traders[i] != null && db.traders[i].stringId == id) return db.traders[i];
            }
            return null;
        }

        // ------------------------------------------------------------------ helpers

        static string First(ModLog log)
        {
            for (int i = 0; i < log.Messages.Count; i++)
            {
                if (log.Messages[i].Kind == ModMessageKind.Error) return log.Messages[i].ToString();
            }
            return "";
        }

        static string FirstWarning(ModLog log)
        {
            for (int i = 0; i < log.Messages.Count; i++)
            {
                if (log.Messages[i].Kind == ModMessageKind.Warning) return log.Messages[i].ToString();
            }
            return "";
        }

        /// <summary>Walks up from the working directory looking for the repo's Mods folder.</summary>
        static string FindRepoMod(string id)
        {
            var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
            for (int i = 0; i < 6 && dir != null; i++)
            {
                var candidate = Path.Combine(Path.Combine(dir.FullName, "Mods"), id);
                if (Directory.Exists(candidate)) return candidate;
                dir = dir.Parent;
            }
            return null;
        }

        static void CopyDirectory(string from, string to)
        {
            Directory.CreateDirectory(to);
            foreach (var file in Directory.GetFiles(from))
            {
                File.Copy(file, Path.Combine(to, Path.GetFileName(file)), true);
            }
            foreach (var sub in Directory.GetDirectories(from))
            {
                CopyDirectory(sub, Path.Combine(to, Path.GetFileName(sub)));
            }
        }

        static void TryDelete(string path)
        {
            try
            {
                if (Directory.Exists(path)) Directory.Delete(path, true);
            }
            catch (System.Exception)
            {
                // A leftover temp folder is not worth failing a test run over.
            }
        }
    }
}
