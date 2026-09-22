using System.Collections.Generic;
using MadVoxel.Building;
using MadVoxel.Core;
using MadVoxel.Fluid;
using MadVoxel.Inventory;
using MadVoxel.Power;
using UnityEngine;

namespace MadVoxel.Content
{
    /// <summary>
    /// The grid and the plumbing. Numbers here are balanced against one another rather
    /// than against a spreadsheet: a small engine runs a shack's lights and one pump,
    /// and the moment you want a fridge and a trap as well you need a second bank or a
    /// battery to carry the night.
    /// </summary>
    public static partial class ContentLibrary
    {
        static readonly Color ColDirtyMetal = new Color(0.36f, 0.35f, 0.33f);
        static readonly Color ColPanel = new Color(0.20f, 0.23f, 0.29f);
        static readonly Color ColCopper = new Color(0.55f, 0.35f, 0.20f);
        static readonly Color ColPipe = new Color(0.44f, 0.42f, 0.38f);

        // ---------------------------------------------------------------- devices

        static PowerDeviceDefinition PowerDevice(string id, string name, PowerDeviceKind kind)
        {
            var def = ScriptableObject.CreateInstance<PowerDeviceDefinition>();
            def.name = "Power_" + ShortName(id);
            def.stringId = id;
            def.displayName = name;
            def.kind = kind;
            return def;
        }

        static FluidDeviceDefinition FluidDevice(string id, string name, FluidDeviceKind kind)
        {
            var def = ScriptableObject.CreateInstance<FluidDeviceDefinition>();
            def.name = "Fluid_" + ShortName(id);
            def.stringId = id;
            def.displayName = name;
            def.kind = kind;
            return def;
        }

        /// <summary>A deployable that carries an electrical device.</summary>
        static StructureDefinition PoweredPiece(string id, string name, PowerDeviceDefinition device,
                                                Vector3Int footprint, Color tint, float health)
        {
            var piece = Structure(id, name, StructureKind.PowerDevice, footprint, SurfaceFamily.Metal, tint, health);
            piece.powerDevice = device;
            device.structure = piece;
            return piece;
        }

        static StructureDefinition PlumbedPiece(string id, string name, FluidDeviceDefinition device,
                                                Vector3Int footprint, Color tint, float health)
        {
            var piece = Structure(id, name, StructureKind.FluidDevice, footprint, SurfaceFamily.Metal, tint, health);
            piece.fluidDevice = device;
            device.structure = piece;
            return piece;
        }

        /// <summary>
        /// Builds every utility device, its deployable and the item that places it, and
        /// folds them into the maps the rest of the library is already filling.
        /// </summary>
        static void AddUtilityContent(Dictionary<string, ItemDefinition> items,
                                      Dictionary<string, StructureDefinition> structures)
        {
            // ---------------------------------------------------------- electricity

            // The small engine. Enough for a shack's lights and one pump, and audible
            // enough that running it all night is a decision.
            var generator = PowerDevice("madvoxel:device_generator", "Generator Bank", PowerDeviceKind.Generator);
            generator.wattsProduced = 100f;
            generator.fuelLitresPerHour = 1.6f;
            generator.fuelCapacityLitres = 24f;
            generator.maxOutputs = 4;
            generator.maxWireLength = 14f;
            generator.noiseRadius = 28f;
            generator.heatWhileRunning = 14f;

            var battery = PowerDevice("madvoxel:device_battery", "Battery Bank", PowerDeviceKind.BatteryBank);
            battery.storageWattHours = 240f;
            battery.dischargeWatts = 60f;
            battery.maxOutputs = 4;
            battery.maxWireLength = 12f;

            var solar = PowerDevice("madvoxel:device_solar", "Solar Bank", PowerDeviceKind.SolarBank);
            solar.wattsProduced = 45f;
            solar.maxOutputs = 2;
            solar.maxWireLength = 12f;

            // Range is the relay's whole job, and it costs a watt so it is not free.
            var relay = PowerDevice("madvoxel:device_relay", "Relay", PowerDeviceKind.Relay);
            relay.relayRange = 30f;
            relay.relayUpkeepWatts = 1f;
            relay.maxOutputs = 3;
            relay.maxWireLength = 30f;

            var breaker = PowerDevice("madvoxel:device_switch", "Switch", PowerDeviceKind.Switch);
            breaker.maxOutputs = 2;
            breaker.maxWireLength = 12f;

            var splitter = PowerDevice("madvoxel:device_splitter", "Splitter", PowerDeviceKind.Splitter);
            splitter.maxOutputs = 4;
            splitter.maxWireLength = 12f;

            var light = PowerDevice("madvoxel:device_light", "Work Light", PowerDeviceKind.Consumer);
            light.wattsConsumed = 6f;
            light.lightRange = 11f;
            light.lightColour = new Color(1f, 0.92f, 0.76f);
            light.heatWhileLit = 5f;

            // The fridge is the reason the grid matters to a farmer rather than to an
            // engineer: it is the difference between a harvest and a compost heap.
            var fridge = PowerDevice("madvoxel:device_fridge", "Fridge", PowerDeviceKind.Consumer);
            fridge.wattsConsumed = 14f;
            fridge.spoilSlowdown = 6f;

            var bladeTrap = PowerDevice("madvoxel:device_blade_trap", "Blade Trap", PowerDeviceKind.Consumer);
            bladeTrap.drawMode = PowerDrawMode.IdleThenBurst;
            bladeTrap.idleWatts = 2f;
            bladeTrap.wattsConsumed = 40f;
            bladeTrap.trapDamage = 22f;
            bladeTrap.trapRadius = 2.3f;
            bladeTrap.trapIntervalSeconds = 0.7f;
            bladeTrap.heatWhileActive = 8f;

            var fencePost = PowerDevice("madvoxel:device_fence_post", "Electric Fence Post", PowerDeviceKind.Consumer);
            fencePost.drawMode = PowerDrawMode.IdleThenBurst;
            fencePost.idleWatts = 1f;
            fencePost.wattsConsumed = 18f;
            fencePost.trapDamage = 9f;
            fencePost.trapRadius = 3.2f;
            fencePost.trapIntervalSeconds = 1.1f;
            fencePost.heatWhileActive = 4f;

            // ----------------------------------------------------------- plumbing

            var pumpFluid = FluidDevice("madvoxel:fitting_pump", "Water Pump", FluidDeviceKind.Pump);
            pumpFluid.litresPerMinute = 26f;
            pumpFluid.wattsRequired = 18f;
            pumpFluid.sourceSearchDepth = 6;
            pumpFluid.maxOutputs = 2;
            pumpFluid.maxPipeLength = 10f;

            // The pump is the one piece that sits on both graphs.
            var pumpPower = PowerDevice("madvoxel:device_pump", "Water Pump", PowerDeviceKind.Consumer);
            pumpPower.wattsConsumed = 18f;
            pumpPower.pumpLitresPerMinute = pumpFluid.litresPerMinute;

            var pipe = FluidDevice("madvoxel:fitting_pipe", "Pipe Section", FluidDeviceKind.Pipe);
            pipe.maxOutputs = 2;
            pipe.maxPipeLength = 10f;

            var tank = FluidDevice("madvoxel:fitting_tank", "Water Tank", FluidDeviceKind.Tank);
            tank.capacityLitres = 800f;
            tank.maxOutputs = 3;
            tank.maxPipeLength = 10f;

            // The barrel is the off-grid fallback: it fills in the rain and holds little.
            var barrel = FluidDevice("madvoxel:fitting_barrel", "Water Barrel", FluidDeviceKind.Tank);
            barrel.capacityLitres = 120f;
            barrel.maxOutputs = 2;
            barrel.maxPipeLength = 6f;

            var tap = FluidDevice("madvoxel:fitting_tap", "Tap", FluidDeviceKind.Tap);
            tap.servingLitres = 1.5f;
            tap.freezable = true;
            tap.maxOutputs = 0;

            var sprinkler = FluidDevice("madvoxel:fitting_sprinkler", "Plot Sprinkler", FluidDeviceKind.Sprinkler);
            sprinkler.drawLitresPerMinute = 5f;
            sprinkler.sprinklerRadius = 6.5f;
            sprinkler.wateredYieldBonus = 0.3f;
            sprinkler.moisturePerHour = 0.2f;
            sprinkler.maxOutputs = 0;

            // ------------------------------------------------------- the world side

            Register(items, structures, ItemIds.PieceGeneratorBank, StructureIds.GeneratorBank,
                PoweredPiece(StructureIds.GeneratorBank, "Generator Bank", generator,
                    new Vector3Int(2, 2, 2), ColDirtyMetal, 320f));

            Register(items, structures, ItemIds.PieceBatteryBank, StructureIds.BatteryBank,
                PoweredPiece(StructureIds.BatteryBank, "Battery Bank", battery,
                    new Vector3Int(2, 2, 2), new Color(0.30f, 0.30f, 0.33f), 260f));

            Register(items, structures, ItemIds.PieceSolarBank, StructureIds.SolarBank,
                PoweredPiece(StructureIds.SolarBank, "Solar Bank", solar,
                    new Vector3Int(2, 1, 2), ColPanel, 180f));

            Register(items, structures, ItemIds.PieceRelay, StructureIds.Relay,
                PoweredPiece(StructureIds.Relay, "Relay", relay, Vector3Int.one, ColDirtyMetal, 140f));

            Register(items, structures, ItemIds.PieceSwitch, StructureIds.Switch,
                PoweredPiece(StructureIds.Switch, "Switch", breaker, Vector3Int.one, ColDirtyMetal, 120f));

            Register(items, structures, ItemIds.PieceSplitter, StructureIds.Splitter,
                PoweredPiece(StructureIds.Splitter, "Splitter", splitter, Vector3Int.one, ColDirtyMetal, 120f));

            Register(items, structures, ItemIds.PieceLight, StructureIds.Light,
                PoweredPiece(StructureIds.Light, "Work Light", light, Vector3Int.one,
                    new Color(0.52f, 0.49f, 0.43f), 90f));

            // A fridge is a crate first and a grid device second: you open it like a
            // box, and the watts only decide how long what is inside lasts.
            var fridgePiece = Structure(StructureIds.Fridge, "Fridge", StructureKind.Storage,
                new Vector3Int(1, 2, 1), SurfaceFamily.Metal, new Color(0.62f, 0.62f, 0.60f), 220f);
            fridgePiece.storageSlots = 18;
            fridgePiece.powerDevice = fridge;
            fridge.structure = fridgePiece;
            Register(items, structures, ItemIds.PieceFridge, StructureIds.Fridge, fridgePiece);

            Register(items, structures, ItemIds.PieceBladeTrap, StructureIds.BladeTrap,
                PoweredPiece(StructureIds.BladeTrap, "Blade Trap", bladeTrap, Vector3Int.one, ColRust, 200f));

            Register(items, structures, ItemIds.PieceFencePost, StructureIds.FencePost,
                PoweredPiece(StructureIds.FencePost, "Electric Fence Post", fencePost,
                    new Vector3Int(1, 2, 1), ColDirtyMetal, 160f));

            // The pump deployable carries both devices.
            var pumpPiece = PlumbedPiece(StructureIds.WaterPump, "Water Pump", pumpFluid,
                Vector3Int.one, ColPipe, 240f);
            pumpPiece.powerDevice = pumpPower;
            pumpPiece.drawsPower = true;
            pumpPower.structure = pumpPiece;
            Register(items, structures, ItemIds.PieceWaterPump, StructureIds.WaterPump, pumpPiece);

            Register(items, structures, ItemIds.PiecePipe, StructureIds.Pipe,
                PlumbedPiece(StructureIds.Pipe, "Pipe Section", pipe, Vector3Int.one, ColPipe, 80f));

            Register(items, structures, ItemIds.PieceWaterTank, StructureIds.WaterTank,
                PlumbedPiece(StructureIds.WaterTank, "Water Tank", tank,
                    new Vector3Int(2, 3, 2), new Color(0.48f, 0.47f, 0.44f), 340f));

            Register(items, structures, ItemIds.PieceWaterBarrel, StructureIds.WaterBarrel,
                PlumbedPiece(StructureIds.WaterBarrel, "Water Barrel", barrel,
                    new Vector3Int(1, 2, 1), ColRust, 150f));

            Register(items, structures, ItemIds.PieceTap, StructureIds.Tap,
                PlumbedPiece(StructureIds.Tap, "Tap", tap, Vector3Int.one, ColCopper, 70f));

            Register(items, structures, ItemIds.PieceSprinkler, StructureIds.Sprinkler,
                PlumbedPiece(StructureIds.Sprinkler, "Plot Sprinkler", sprinkler, Vector3Int.one, ColPipe, 80f));

            // The wire tool: the one thing that makes any of it a grid.
            var wireTool = Tool(ItemIds.WireTool, "Wire Tool", ToolType.None, 0, 1f, 3f, 0, ColCopper, 30);
            wireTool.description = "Aim at a power or water fitting and click to start a line, "
                                 + "click the second fitting to join them. Right-click clears the line.";
            items[ItemIds.WireTool] = wireTool;

            var wire = Item(ItemIds.CopperWire, "Copper Wire", ItemCategory.Resource, 64,
                SurfaceFamily.Metal, ColCopper, 4);
            items[ItemIds.CopperWire] = wire;
        }

        /// <summary>Builds the placing item for a deployable and files both away.</summary>
        static void Register(Dictionary<string, ItemDefinition> items,
                             Dictionary<string, StructureDefinition> structures,
                             string itemId, string structureId, StructureDefinition piece)
        {
            var item = Item(itemId, piece.displayName, ItemCategory.Structure, 8,
                piece.surfaceFamily, piece.tint, Mathf.Max(4, Mathf.RoundToInt(piece.maxHealth / 12f)));
            item.placeableStructure = piece;

            piece.salvageItem = item;
            piece.salvageCount = 1;
            piece.requiresSupport = true;

            items[itemId] = item;
            structures[structureId] = piece;
        }

        /// <summary>
        /// Recipes. The first generator is deliberately reachable on a workbench with
        /// scrap and iron - you can wire a shack on day three, exactly as the brief for
        /// this pass asks. The banks and the trap are where the perk tree bites.
        /// </summary>
        static void AddUtilityRecipes(Dictionary<string, ItemDefinition> it, List<RecipeDefinition> list)
        {
            list.Add(Recipe("madvoxel:craft_copper_wire", it[ItemIds.CopperWire], 4, CraftStation.Workbench, 1.4f,
                Ing(it[ItemIds.ScrapMetal], 3)));

            list.Add(Recipe("madvoxel:craft_wire_tool", it[ItemIds.WireTool], 1, CraftStation.Workbench, 2.5f,
                Ing(it[ItemIds.ScrapMetal], 6), Ing(it[ItemIds.CopperWire], 2), Ing(it[ItemIds.Plank], 2)));

            list.Add(Recipe("madvoxel:craft_generator_bank", it[ItemIds.PieceGeneratorBank], 1, CraftStation.Workbench, 9f,
                Ing(it[ItemIds.ScrapMetal], 30), Ing(it[ItemIds.IronIngot], 6), Ing(it[ItemIds.CopperWire], 6)));

            list.Add(Recipe("madvoxel:craft_relay", it[ItemIds.PieceRelay], 2, CraftStation.Workbench, 2.5f,
                Ing(it[ItemIds.ScrapMetal], 6), Ing(it[ItemIds.CopperWire], 3)));

            list.Add(Recipe("madvoxel:craft_switch", it[ItemIds.PieceSwitch], 2, CraftStation.Workbench, 1.8f,
                Ing(it[ItemIds.ScrapMetal], 4), Ing(it[ItemIds.CopperWire], 2)));

            list.Add(Recipe("madvoxel:craft_splitter", it[ItemIds.PieceSplitter], 1, CraftStation.Workbench, 2.2f,
                Ing(it[ItemIds.ScrapMetal], 6), Ing(it[ItemIds.CopperWire], 4)));

            list.Add(Recipe("madvoxel:craft_light", it[ItemIds.PieceLight], 2, CraftStation.Workbench, 2f,
                Ing(it[ItemIds.ScrapMetal], 3), Ing(it[ItemIds.CopperWire], 2), Ing(it[ItemIds.BlockGlass], 1)));

            var batteryRecipe = Recipe("madvoxel:craft_battery_bank", it[ItemIds.PieceBatteryBank], 1,
                CraftStation.Workbench, 10f,
                Ing(it[ItemIds.ScrapMetal], 24), Ing(it[ItemIds.IronIngot], 10), Ing(it[ItemIds.CopperWire], 10));
            batteryRecipe.unlockedByDefault = false;
            batteryRecipe.requiredPerkId = MadVoxel.Perks.PerkIds.Electrician;
            batteryRecipe.requiredPerkRank = 1;
            list.Add(batteryRecipe);

            var solarRecipe = Recipe("madvoxel:craft_solar_bank", it[ItemIds.PieceSolarBank], 1,
                CraftStation.Workbench, 12f,
                Ing(it[ItemIds.ScrapMetal], 18), Ing(it[ItemIds.IronIngot], 8),
                Ing(it[ItemIds.BlockGlass], 4), Ing(it[ItemIds.CopperWire], 8));
            solarRecipe.unlockedByDefault = false;
            solarRecipe.requiredPerkId = MadVoxel.Perks.PerkIds.Electrician;
            solarRecipe.requiredPerkRank = 2;
            list.Add(solarRecipe);

            var fridgeRecipe = Recipe("madvoxel:craft_fridge", it[ItemIds.PieceFridge], 1,
                CraftStation.Workbench, 8f,
                Ing(it[ItemIds.ScrapMetal], 16), Ing(it[ItemIds.IronIngot], 5), Ing(it[ItemIds.CopperWire], 5));
            fridgeRecipe.unlockedByDefault = false;
            fridgeRecipe.requiredPerkId = MadVoxel.Perks.PerkIds.Electrician;
            fridgeRecipe.requiredPerkRank = 3;
            list.Add(fridgeRecipe);

            list.Add(Recipe("madvoxel:craft_blade_trap", it[ItemIds.PieceBladeTrap], 1, CraftStation.Workbench, 7f,
                Ing(it[ItemIds.ScrapMetal], 14), Ing(it[ItemIds.IronIngot], 6), Ing(it[ItemIds.CopperWire], 4)));

            list.Add(Recipe("madvoxel:craft_fence_post", it[ItemIds.PieceFencePost], 2, CraftStation.Workbench, 4f,
                Ing(it[ItemIds.ScrapMetal], 8), Ing(it[ItemIds.CopperWire], 4)));

            // ----------------------------------------------------------- plumbing

            list.Add(Recipe("madvoxel:craft_pipe", it[ItemIds.PiecePipe], 4, CraftStation.Workbench, 1.6f,
                Ing(it[ItemIds.ScrapMetal], 4)));

            list.Add(Recipe("madvoxel:craft_water_barrel", it[ItemIds.PieceWaterBarrel], 1, CraftStation.Workbench, 3.5f,
                Ing(it[ItemIds.ScrapMetal], 10), Ing(it[ItemIds.Plank], 4)));

            list.Add(Recipe("madvoxel:craft_water_pump", it[ItemIds.PieceWaterPump], 1, CraftStation.Workbench, 7f,
                Ing(it[ItemIds.ScrapMetal], 14), Ing(it[ItemIds.IronIngot], 4), Ing(it[ItemIds.CopperWire], 4)));

            list.Add(Recipe("madvoxel:craft_water_tank", it[ItemIds.PieceWaterTank], 1, CraftStation.Workbench, 8f,
                Ing(it[ItemIds.ScrapMetal], 22), Ing(it[ItemIds.IronIngot], 6)));

            list.Add(Recipe("madvoxel:craft_tap", it[ItemIds.PieceTap], 2, CraftStation.Workbench, 2f,
                Ing(it[ItemIds.ScrapMetal], 4), Ing(it[ItemIds.IronIngot], 1)));

            var sprinklerRecipe = Recipe("madvoxel:craft_sprinkler", it[ItemIds.PieceSprinkler], 2,
                CraftStation.Workbench, 3.5f,
                Ing(it[ItemIds.ScrapMetal], 6), Ing(it[ItemIds.IronIngot], 2));
            sprinklerRecipe.unlockedByDefault = false;
            sprinklerRecipe.requiredPerkId = MadVoxel.Perks.PerkIds.Farming;
            sprinklerRecipe.requiredPerkRank = 3;
            list.Add(sprinklerRecipe);
        }
    }
}
