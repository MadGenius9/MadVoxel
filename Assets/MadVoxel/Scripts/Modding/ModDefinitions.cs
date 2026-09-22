using MadVoxel.AI;
using MadVoxel.Building;
using MadVoxel.Content;
using MadVoxel.Core;
using MadVoxel.Farming.Crops;
using MadVoxel.Horde;
using MadVoxel.Inventory;
using MadVoxel.Perks;
using MadVoxel.Quests;
using MadVoxel.Traders;
using MadVoxel.Vehicles;
using MadVoxel.World.Terrain;

namespace MadVoxel.Modding
{
    /// <summary>
    /// The field-by-field mapping between mod JSON and the game's definitions.
    ///
    /// Written out by hand rather than driven by reflection: it is the file that tells a
    /// mod author exactly which names exist, it survives IL2CPP stripping, and it lets an
    /// unknown key produce a warning naming the field instead of being quietly dropped.
    /// Each type gets a Read (its own values) and a Link (its references).
    /// </summary>
    public static class ModDefinitions
    {
        // ------------------------------------------------------------------ blocks

        public static void ReadBlock(BlockDefinition d, JsonReader r)
        {
            r.Read("displayName", ref d.displayName);
            r.Read("isAir", ref d.isAir);
            r.Read("solid", ref d.solid);
            r.Read("opaque", ref d.opaque);
            r.Read("hardness", ref d.hardness);
            r.Read("requiredToolTier", ref d.requiredToolTier);
            r.ReadEnum("preferredTool", ref d.preferredTool);
            r.Read("structureHealth", ref d.structureHealth);
            r.ReadEnum("surfaceFamily", ref d.surfaceFamily);
            r.ReadColour("tint", ref d.tint);
            r.Read("smoothness", ref d.smoothness);
            r.Read("metallic", ref d.metallic);
            r.Read("transparent", ref d.transparent);
            r.Read("lightEmission", ref d.lightEmission);
            r.Read("dropMin", ref d.dropMin);
            r.Read("dropMax", ref d.dropMax);
            r.Read("harvestXp", ref d.harvestXp);
            r.Read("secondaryDropChance", ref d.secondaryDropChance);
            r.Read("secondaryDropMin", ref d.secondaryDropMin);
            r.Read("secondaryDropMax", ref d.secondaryDropMax);
            r.Read("buildTier", ref d.buildTier);
            r.ReportUnknownKeys("dropItem", "secondaryDropItem", "upgradesTo",
                ModContentApplier.IdKey, ModContentApplier.OpKey);
        }

        public static void LinkBlock(BlockDefinition d, JsonValue e, ModLinker l)
        {
            l.Item(e, "dropItem", ref d.dropItem);
            l.Item(e, "secondaryDropItem", ref d.secondaryDropItem);
            l.Block(e, "upgradesTo", ref d.upgradesTo);
        }

        // ------------------------------------------------------------------- items

        public static void ReadItem(ItemDefinition d, JsonReader r)
        {
            r.Read("displayName", ref d.displayName);
            r.Read("description", ref d.description);
            r.ReadEnum("category", ref d.category);
            r.Read("maxStack", ref d.maxStack);
            r.ReadEnum("surfaceFamily", ref d.surfaceFamily);
            r.ReadColour("tint", ref d.tint);
            r.ReadEnum("toolType", ref d.toolType);
            r.Read("toolTier", ref d.toolTier);
            r.Read("harvestSpeed", ref d.harvestSpeed);
            r.Read("meleeDamage", ref d.meleeDamage);
            r.Read("attackCooldown", ref d.attackCooldown);
            r.Read("maxDurability", ref d.maxDurability);
            r.Read("foodRestore", ref d.foodRestore);
            r.Read("waterRestore", ref d.waterRestore);
            r.Read("healthRestore", ref d.healthRestore);
            r.Read("staminaRestore", ref d.staminaRestore);
            r.Read("fuelSeconds", ref d.fuelSeconds);
            r.Read("tradeValue", ref d.tradeValue);
            r.ReportUnknownKeys("placeableBlock", "placeableStructure", "placeableBuildPiece",
                ModContentApplier.IdKey, ModContentApplier.OpKey);
        }

        public static void LinkItem(ItemDefinition d, JsonValue e, ModLinker l)
        {
            l.Block(e, "placeableBlock", ref d.placeableBlock);
            l.Structure(e, "placeableStructure", ref d.placeableStructure);
            l.BuildPiece(e, "placeableBuildPiece", ref d.placeableBuildPiece);
        }

        // ----------------------------------------------------------------- recipes

        public static void ReadRecipe(RecipeDefinition d, JsonReader r)
        {
            r.Read("outputCount", ref d.outputCount);
            r.ReadEnum("station", ref d.station);
            r.Read("craftSeconds", ref d.craftSeconds);
            r.Read("unlockedByDefault", ref d.unlockedByDefault);
            r.Read("requiredPerkId", ref d.requiredPerkId);
            r.Read("requiredPerkRank", ref d.requiredPerkRank);
            r.ReportUnknownKeys("output", "ingredients", ModContentApplier.IdKey, ModContentApplier.OpKey);
        }

        public static void LinkRecipe(RecipeDefinition d, JsonValue e, ModLinker l)
        {
            l.Item(e, "output", ref d.output);
            l.Ingredients(e, "ingredients", d.ingredients);
        }

        // -------------------------------------------------------------- structures

        public static void ReadStructure(StructureDefinition d, JsonReader r)
        {
            r.Read("displayName", ref d.displayName);
            r.ReadEnum("kind", ref d.kind);
            r.ReadVector3Int("footprint", ref d.footprint);
            r.Read("requiresSupport", ref d.requiresSupport);
            r.Read("blocksMovement", ref d.blocksMovement);
            r.ReadEnum("surfaceFamily", ref d.surfaceFamily);
            r.ReadColour("tint", ref d.tint);
            r.Read("maxHealth", ref d.maxHealth);
            r.Read("buildTier", ref d.buildTier);
            r.Read("storageSlots", ref d.storageSlots);
            r.ReadEnum("craftStation", ref d.craftStation);
            r.Read("lightRange", ref d.lightRange);
            r.ReadColour("lightColour", ref d.lightColour);
            r.Read("claimRadius", ref d.claimRadius);
            r.Read("requiresSoil", ref d.requiresSoil);
            r.Read("siloCapacityLitres", ref d.siloCapacityLitres);
            r.Read("salvageCount", ref d.salvageCount);
            r.ReportUnknownKeys("salvageItem", "upgradesTo", ModContentApplier.IdKey, ModContentApplier.OpKey);
        }

        public static void LinkStructure(StructureDefinition d, JsonValue e, ModLinker l)
        {
            l.Item(e, "salvageItem", ref d.salvageItem);
            l.Structure(e, "upgradesTo", ref d.upgradesTo);
        }

        // ------------------------------------------------------------ build pieces

        public static void ReadBuildPiece(BuildPieceDefinition d, JsonReader r)
        {
            r.Read("displayName", ref d.displayName);
            r.ReadEnum("kind", ref d.kind);
            r.ReadEnum("tier", ref d.tier);
            r.ReadEnum("slot", ref d.slot);
            r.Read("blocksMovement", ref d.blocksMovement);
            r.Read("restsOnTerrain", ref d.restsOnTerrain);
            r.Read("requiresHost", ref d.requiresHost);
            r.ReadEnum("hostKind", ref d.hostKind);
            r.Read("maxHealth", ref d.maxHealth);
            r.Read("damageResistance", ref d.damageResistance);
            r.ReadEnum("surfaceFamily", ref d.surfaceFamily);
            r.ReadColour("tint", ref d.tint);
            r.Read("smoothness", ref d.smoothness);
            r.Read("metallic", ref d.metallic);
            r.Read("requiredPerkId", ref d.requiredPerkId);
            r.Read("requiredPerkRank", ref d.requiredPerkRank);
            r.Read("salvageCount", ref d.salvageCount);
            r.ReportUnknownKeys("upgradesTo", "upgradeCost", "salvageItem",
                ModContentApplier.IdKey, ModContentApplier.OpKey);
        }

        public static void LinkBuildPiece(BuildPieceDefinition d, JsonValue e, ModLinker l)
        {
            l.BuildPiece(e, "upgradesTo", ref d.upgradesTo);
            l.Ingredients(e, "upgradeCost", d.upgradeCost);
            l.Item(e, "salvageItem", ref d.salvageItem);
        }

        // ------------------------------------------------------------------- crops

        public static void ReadCrop(CropDefinition d, JsonReader r)
        {
            r.Read("displayName", ref d.displayName);
            r.Read("growsOnPlot", ref d.growsOnPlot);
            r.Read("growsOnField", ref d.growsOnField);
            r.Read("harvestMin", ref d.harvestMin);
            r.Read("harvestMax", ref d.harvestMax);
            r.Read("daysToMature", ref d.daysToMature);
            r.Read("replants", ref d.replants);
            r.Read("seedReturnChance", ref d.seedReturnChance);
            r.Read("seedReturnMin", ref d.seedReturnMin);
            r.Read("seedReturnMax", ref d.seedReturnMax);
            r.Read("litresPerCell", ref d.litresPerCell);
            r.ReadColour("plantTint", ref d.plantTint);
            r.Read("matureHeight", ref d.matureHeight);
            r.Read("xpPerHarvest", ref d.xpPerHarvest);
            r.ReportUnknownKeys("seedItem", "harvestItem", ModContentApplier.IdKey, ModContentApplier.OpKey);
        }

        public static void LinkCrop(CropDefinition d, JsonValue e, ModLinker l)
        {
            l.Item(e, "seedItem", ref d.seedItem);
            l.Item(e, "harvestItem", ref d.harvestItem);
        }

        // ----------------------------------------------------------------- zombies

        public static void ReadZombie(ZombieDefinition d, JsonReader r)
        {
            r.Read("displayName", ref d.displayName);
            r.Read("maxHealth", ref d.maxHealth);
            r.Read("walkSpeed", ref d.walkSpeed);
            r.Read("chaseSpeed", ref d.chaseSpeed);
            r.Read("meleeDamage", ref d.meleeDamage);
            r.Read("attackInterval", ref d.attackInterval);
            r.Read("attackRange", ref d.attackRange);
            r.Read("digDamagePerSecond", ref d.digDamagePerSecond);
            r.Read("sightRange", ref d.sightRange);
            r.Read("xpReward", ref d.xpReward);
            r.ReadColour("tint", ref d.tint);
            r.Read("height", ref d.height);
            r.Read("dropMin", ref d.dropMin);
            r.Read("dropMax", ref d.dropMax);
            r.ReportUnknownKeys("dropItem", ModContentApplier.IdKey, ModContentApplier.OpKey);
        }

        public static void LinkZombie(ZombieDefinition d, JsonValue e, ModLinker l)
        {
            l.Item(e, "dropItem", ref d.dropItem);
        }

        // ------------------------------------------------------------------- perks

        public static void ReadPerk(PerkDefinition d, JsonReader r)
        {
            r.Read("displayName", ref d.displayName);
            r.Read("description", ref d.description);
            r.ReadEnum("category", ref d.category);
            r.Read("maxRank", ref d.maxRank);
            r.Read("pointCostPerRank", ref d.pointCostPerRank);
            r.Read("requiredPlayerLevel", ref d.requiredPlayerLevel);
            r.Read("requiresPerkId", ref d.requiresPerkId);
            r.Read("requiresPerkRank", ref d.requiresPerkRank);
            r.ReadStringList("unlocksRecipeIds", d.unlocksRecipeIds);

            var effects = r.Raw("effects");
            if (effects != null && effects.Kind == JsonKind.Array)
            {
                d.effects.Clear();
                for (int i = 0; i < effects.Count; i++)
                {
                    var row = effects[i];
                    if (row.Kind != JsonKind.Object) continue;

                    var effect = new PerkEffect();
                    var er = new JsonReader(row, r.Log, "perk effect");
                    er.ReadEnum("type", ref effect.type);
                    er.Read("valuePerRank", ref effect.valuePerRank);
                    er.ReportUnknownKeys();
                    d.effects.Add(effect);
                }
            }

            r.ReportUnknownKeys(ModContentApplier.IdKey, ModContentApplier.OpKey);
        }

        // ------------------------------------------------------------------ quests

        public static void ReadQuest(QuestDefinition d, JsonReader r)
        {
            r.Read("title", ref d.title);
            r.Read("description", ref d.description);
            r.ReadEnum("kind", ref d.kind);
            r.Read("objectiveCount", ref d.objectiveCount);
            r.Read("objectiveZombieId", ref d.objectiveZombieId);
            r.Read("objectiveCropId", ref d.objectiveCropId);
            r.Read("requiredPlayerLevel", ref d.requiredPlayerLevel);
            r.Read("requiredReputationTier", ref d.requiredReputationTier);
            r.Read("xpReward", ref d.xpReward);
            r.Read("reputationReward", ref d.reputationReward);
            r.ReportUnknownKeys("objectiveItem", "rewards", ModContentApplier.IdKey, ModContentApplier.OpKey);
        }

        public static void LinkQuest(QuestDefinition d, JsonValue e, ModLinker l)
        {
            l.Item(e, "objectiveItem", ref d.objectiveItem);
            l.Rewards(e, "rewards", d.rewards);
        }

        // ----------------------------------------------------------------- traders

        public static void ReadTrader(TraderDefinition d, JsonReader r)
        {
            r.Read("displayName", ref d.displayName);
            r.Read("greeting", ref d.greeting);
            r.Read("sellMarkup", ref d.sellMarkup);
            r.Read("buyRate", ref d.buyRate);
            r.Read("restockHours", ref d.restockHours);
            r.Read("outpostGridSpacing", ref d.outpostGridSpacing);
            r.Read("outpostVariant", ref d.outpostVariant);
            r.Read("reputationTiers", ref d.reputationTiers);
            r.Read("reputationPerTier", ref d.reputationPerTier);
            r.ReportUnknownKeys("currencyItem", "stock", "questBoard",
                ModContentApplier.IdKey, ModContentApplier.OpKey);
        }

        public static void LinkTrader(TraderDefinition d, JsonValue e, ModLinker l)
        {
            l.Item(e, "currencyItem", ref d.currencyItem);
            l.Stock(e, "stock", d.stock);
            l.Quest(e, "questBoard", d.questBoard);
        }

        // ---------------------------------------------------------------- vehicles

        public static void ReadVehicle(VehicleDefinition d, JsonReader r)
        {
            r.Read("displayName", ref d.displayName);
            r.Read("maxSpeed", ref d.maxSpeed);
            r.Read("acceleration", ref d.acceleration);
            r.Read("turnRate", ref d.turnRate);
            r.Read("climbHeight", ref d.climbHeight);
            r.Read("fuelCapacity", ref d.fuelCapacity);
            r.Read("fuelPerSecond", ref d.fuelPerSecond);
            r.Read("fuelPerItem", ref d.fuelPerItem);
            r.Read("seats", ref d.seats);
            r.Read("storageSlots", ref d.storageSlots);
            r.Read("maxHealth", ref d.maxHealth);
            r.ReadColour("tint", ref d.tint);
            r.ReportUnknownKeys("fuelItem", ModContentApplier.IdKey, ModContentApplier.OpKey);
        }

        public static void LinkVehicle(VehicleDefinition d, JsonValue e, ModLinker l)
        {
            l.Item(e, "fuelItem", ref d.fuelItem);
        }

        // ------------------------------------------------------------------ tuning

        public static void ReadConfig(GameConfig d, JsonReader r)
        {
            r.Read("worldRadiusChunks", ref d.worldRadiusChunks);
            r.Read("viewDistanceChunks", ref d.viewDistanceChunks);
            r.Read("maxChunkBuildsPerFrame", ref d.maxChunkBuildsPerFrame);
            r.Read("dayLengthSeconds", ref d.dayLengthSeconds);
            r.Read("dawnHour", ref d.dawnHour);
            r.Read("duskHour", ref d.duskHour);
            r.Read("startHour", ref d.startHour);
            r.Read("walkSpeed", ref d.walkSpeed);
            r.Read("sprintSpeed", ref d.sprintSpeed);
            r.Read("crouchSpeed", ref d.crouchSpeed);
            r.Read("jumpHeight", ref d.jumpHeight);
            r.Read("gravity", ref d.gravity);
            r.Read("mouseSensitivity", ref d.mouseSensitivity);
            r.Read("maxHealth", ref d.maxHealth);
            r.Read("maxStamina", ref d.maxStamina);
            r.Read("maxFood", ref d.maxFood);
            r.Read("maxWater", ref d.maxWater);
            r.Read("sprintStaminaPerSecond", ref d.sprintStaminaPerSecond);
            r.Read("staminaRegenPerSecond", ref d.staminaRegenPerSecond);
            r.Read("foodDrainPerMinute", ref d.foodDrainPerMinute);
            r.Read("waterDrainPerMinute", ref d.waterDrainPerMinute);
            r.Read("reachDistance", ref d.reachDistance);
            r.Read("dropBagOnDeath", ref d.dropBagOnDeath);
            r.Read("claimRadius", ref d.claimRadius);
            r.Read("wanderingZombieCapDay", ref d.wanderingZombieCapDay);
            r.Read("wanderingZombieCapNight", ref d.wanderingZombieCapNight);
            r.Read("autosaveIntervalSeconds", ref d.autosaveIntervalSeconds);
            r.ReportUnknownKeys();
        }

        public static void ReadHorde(HordeSchedule d, JsonReader r)
        {
            r.Read("everyNDays", ref d.everyNDays);
            r.Read("startHour", ref d.startHour);
            r.Read("endHour", ref d.endHour);
            r.Read("baseWaveSize", ref d.baseWaveSize);
            r.Read("perPlayerLevel", ref d.perPlayerLevel);
            r.Read("perClaimStructure", ref d.perClaimStructure);
            r.Read("maxAlive", ref d.maxAlive);
            r.Read("waveIntervalSeconds", ref d.waveIntervalSeconds);
            r.Read("spawnRadiusMin", ref d.spawnRadiusMin);
            r.Read("spawnRadiusMax", ref d.spawnRadiusMax);
            r.Read("heavyFromHordeNumber", ref d.heavyFromHordeNumber);
            r.Read("heavyShare", ref d.heavyShare);
            r.Read("survivalXp", ref d.survivalXp);
            r.ReportUnknownKeys("baseZombie", "heavyZombie");
        }

        public static void LinkHorde(HordeSchedule d, JsonValue e, ModLinker l)
        {
            l.Zombie(e, "baseZombie", ref d.baseZombie);
            l.Zombie(e, "heavyZombie", ref d.heavyZombie);
        }

        /// <summary>Replaces the new-world loadout with the mod's list.</summary>
        public static void LinkStartingItems(ContentDatabase db, JsonValue e, ModLinker l)
        {
            if (e == null || e.Kind != JsonKind.Array) return;

            var built = new System.Collections.Generic.List<ContentDatabase.StartingStack>();
            for (int i = 0; i < e.Count; i++)
            {
                var row = e[i];
                if (row.Kind != JsonKind.Object) continue;

                ItemDefinition item = null;
                if (!l.Item(row, "item", ref item) || item == null) continue;

                int count = 1;
                var countNode = row["count"];
                if (countNode != null && countNode.Kind == JsonKind.Number) count = UnityEngine.Mathf.RoundToInt((float)countNode.Number);

                built.Add(new ContentDatabase.StartingStack { item = item, count = UnityEngine.Mathf.Max(1, count) });
            }

            db.startingItems = built;
        }
    }
}
