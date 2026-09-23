# Modding MadVoxel

A mod is a **folder you drop in**. Nothing is compiled and nothing is executed — a mod
describes content in JSON and the game builds it. That means installing a mod from the
internet cannot run code on your machine.

---

## Installing a mod

Drop the mod's folder into:

| Platform | Mods folder |
| --- | --- |
| Windows | `%USERPROFILE%\AppData\LocalLow\MadGenius\MadVoxel\Mods` |
| macOS | `~/Library/Application Support/MadGenius/MadVoxel/Mods` |
| Linux | `~/.config/unity3d/MadGenius/MadVoxel/Mods` |

The folder is created on first launch. In the editor, **MadVoxel → Mods → Open Mods
Folder** opens it.

The title screen shows how many mods loaded, and names anything that errored. Full
detail goes to the console.

**A mod can be switched off** without deleting it: set `"enabled": false` in its
`mod.json`.

---

## Making one

```
Mods/
  my_mod/
    mod.json            required
    content/
      anything.json     as many files as you like, in any sub-folders
```

`Mods/example_pumpkin/` in this repository is a complete worked example — a new crop
with its seed, produce, wild plant, cooking recipe and trader stock, plus patches to
vanilla items and the horde schedule. Copy it and edit.

### mod.json

```json
{
  "id": "my_mod",
  "name": "My Mod",
  "version": "1.0.0",
  "author": "you",
  "description": "What it does.",
  "loadOrder": 100,
  "dependencies": [],
  "enabled": true
}
```

- **`id`** is the namespace for everything you add, and must be lowercase letters,
  digits and underscores. Prefix every id you create with it: `my_mod:iron_hoe`.
- **`loadOrder`** decides who wins when two mods patch the same thing — lower loads
  first, so **higher numbers overwrite**. Ties break alphabetically by id, so load
  order is always deterministic.
- **`dependencies`** are mod ids that must load before yours. If one is missing your
  mod is skipped with a clear message rather than half-applied.

### content files

Each file is a JSON object whose fields are content types:

```json
{
  "items":   [ { "id": "my_mod:thing", "displayName": "Thing" } ],
  "recipes": [ { "id": "my_mod:make_thing", "output": "my_mod:thing", "station": "Hand",
                 "ingredients": [ { "item": "madvoxel:plank", "count": 2 } ] } ]
}
```

Types: `blocks`, `items`, `recipes`, `structures`, `buildPieces`, `crops`, `zombies`,
`perks`, `quests`, `traders`, `vehicles`, `implements`, plus the singletons `config`,
`hordeSchedule` and `startingItems`.

**Comments and trailing commas are allowed.** Mod files are written by hand; the parser
accepts `//`, `/* */` and a comma before a closing brace.

---

## Add, patch, replace, remove

Every entry needs an `"id"`. What happens next depends on `"$op"`:

| `$op` | Effect |
| --- | --- |
| *(omitted)* or `"patch"` | Add it if the id is new; otherwise change **only the fields you list** |
| `"replace"` | Throw away the existing definition and build a fresh one from your fields |
| `"remove"` | Delete the definition |

Patching is the important one. This changes one number on a vanilla item and leaves
everything else — name, stack size, tool stats — exactly as it was:

```json
{ "items": [ { "id": "madvoxel:baked_potato", "$op": "patch", "staminaRestore": 22 } ] }
```

### Finding ids

**MadVoxel → Mods → Write Content Id Reference** writes `ContentIds.txt` next to the
project with every id in the game, grouped by type. That is the fastest way to find the
exact string for the thing you want to patch or point at.

---

## References

Any field that points at another definition takes an id in quotes:

```json
{ "crops": [ {
  "id": "my_mod:crop_carrot",
  "seedItem": "my_mod:seed_carrot",
  "harvestItem": "my_mod:carrot"
} ] }
```

**References resolve after every mod has loaded**, so you can point at content from a
mod that loads after yours, and you never have to think about load order for anything
but overrides. An id that does not resolve is an error naming the field, the id and the
line number.

`null` clears a reference.

---

## Field reference

Only the fields you name are changed. Colours are `"#rrggbb"`, `"#rrggbbaa"` or
`[r, g, b]` in 0–1. Enum values are the names below, case-insensitive.

### items

`displayName`, `description`, `category` *(Resource, Tool, Weapon, Block, Structure,
Consumable, Ammo, Quest, Misc)*, `maxStack`, `surfaceFamily` *(Dirt, Grass, Stone, Sand,
Wood, Plank, Metal, Concrete, Cloth, Foliage, Ore, Flesh, Emissive)*, `tint`,
`toolType` *(None, Pickaxe, Axe, Shovel, Wrench, Hammer, Melee)*, `toolTier`,
`harvestSpeed`, `meleeDamage`, `attackCooldown`, `maxDurability`, `foodRestore`,
`waterRestore`, `healthRestore`, `staminaRestore`, `fuelSeconds`, `tradeValue`,
`spoilHours`,
`rangedDamage`, `drawSeconds`, `minLaunchSpeed`, `maxLaunchSpeed`, `drawStamina`
→ `placeableBlock`, `placeableStructure`, `placeableBuildPiece`, `placeableVehicle`,
`hitchImplement`, `ammoItem`, `spoiledInto`

An item is a working bow once it has both a `rangedDamage` and an `ammoItem`; the draw,
the arc and the recovery are already generic over those numbers. Anything with
`category: "Ammo"` and a `rangedDamage` can be fired from any bow — the weapon picks the
hardest-hitting arrow in the bag rather than only the one it names.

### blocks

`displayName`, `isAir`, `solid`, `opaque`, `hardness` *(seconds to mine; negative is
indestructible)*, `requiredToolTier`, `preferredTool`, `structureHealth`,
`surfaceFamily`, `tint`, `smoothness`, `metallic`, `transparent`, `lightEmission`,
`dropMin`, `dropMax`, `harvestXp`, `secondaryDropChance`, `secondaryDropMin`,
`secondaryDropMax`, `buildTier`, → `dropItem`, `secondaryDropItem`, `upgradesTo`

### recipes

`outputCount`, `station` *(Hand, Campfire, Workbench, Forge)*, `craftSeconds`,
`unlockedByDefault`, `requiredPerkId`, `requiredPerkRank`, → `output`, `ingredients`

### crops

`displayName`, `growsOnPlot`, `growsOnField`, `harvestMin`, `harvestMax`,
`daysToMature`, `replants`, `seedReturnChance`, `seedReturnMin`, `seedReturnMax`,
`litresPerCell`, `plantTint`, `matureHeight`, `xpPerHarvest`,
→ `seedItem`, `harvestItem`

### structures *(deployables: crates, benches, plots, cupboards)*

`displayName`, `kind` *(Generic, Storage, CraftStation, Campfire, ToolCupboard,
Bedroll, FarmPlot, Silo)*, `footprint` *(`[x, y, z]`)*, `requiresSupport`,
`blocksMovement`, `surfaceFamily`, `tint`, `maxHealth`, `buildTier`, `storageSlots`,
`craftStation`, `lightRange`, `lightColour`, `claimRadius`, `requiresSoil`,
`siloCapacityLitres`, `salvageCount`, → `salvageItem`, `upgradesTo`

### buildPieces *(the Rust snap set)*

`displayName`, `kind` *(Foundation, Floor, Wall, WindowWall, Doorway, HalfWall, Stairs,
Roof, Ladder, Hatch, Door, Fence)*, `tier` *(Twig, Wood, Stone, Metal, Armored)*,
`slot` *(Floor, Wall, Ceiling, Interior, Attachment)*, `blocksMovement`,
`restsOnTerrain`, `requiresHost`, `hostKind`, `maxHealth`, `damageResistance`,
`surfaceFamily`, `tint`, `smoothness`, `metallic`, `requiredPerkId`,
`requiredPerkRank`, `salvageCount`, → `upgradesTo`, `upgradeCost`, `salvageItem`

### zombies

`displayName`, `maxHealth`, `walkSpeed`, `chaseSpeed`, `meleeDamage`, `attackInterval`,
`attackRange`, `digDamagePerSecond`, `sightRange`, `xpReward`, `tint`, `height`,
`dropMin`, `dropMax`, → `dropItem`

### perks

`displayName`, `description`, `category` *(Mining, Construction, Combat, Scavenging,
Medicine, Vehicles, Farming)*, `maxRank`, `pointCostPerRank`, `requiredPlayerLevel`,
`requiresPerkId`, `requiresPerkRank`, `unlocksRecipeIds`, `effects`

`effects` is a list of `{ "type": ..., "valuePerRank": 0.1 }` where type is one of
`MiningSpeedMultiplier`, `HarvestYieldMultiplier`, `BlockTierUnlock`,
`MaxStaminaBonus`, `StaminaDrainMultiplier`, `MeleeDamageMultiplier`,
`RangedDamageMultiplier`, `LootQuantityMultiplier`, `HealingMultiplier`,
`VehicleFuelEfficiency`, `RepairSpeedMultiplier`, `FieldYieldMultiplier`.

Effects of the same type sum across every perk that grants them, and a modded perk
appears on the skills screen and is applied in play without any code. Three types are
authored but not yet read by the game — `RangedDamageMultiplier`,
`VehicleFuelEfficiency` and `FieldYieldMultiplier` — so a perk built on those will show
its numbers and change nothing until the Phase 1 system that reads them lands.

Your perk's `category` must be one of the seven above; there is no way to add a new
category from JSON, because the screen builds its column from the enum.

### quests

`title`, `description`, `kind` *(Fetch, Clear, SurviveNights, Deliver, DeliverLitres)*,
`objectiveCount`, `objectiveZombieId`, `objectiveCropId`, `requiredPlayerLevel`,
`requiredReputationTier`, `xpReward`, `reputationReward`,
→ `objectiveItem`, `rewards`

### traders

`displayName`, `greeting`, `sellMarkup`, `buyRate`, `restockHours`,
`outpostGridSpacing`, `outpostVariant`, `reputationTiers`, `reputationPerTier`,
→ `currencyItem`, `stock`, `questBoard`

`stock` rows are `{ "item": id, "count": 8, "priceMultiplier": 1.5,
"minReputationTier": 0 }`.

### vehicles

`displayName`, `maxSpeed`, `acceleration`, `turnRate`, `climbHeight`, `fuelCapacity`,
`fuelPerSecond`, `fuelPerItem`, `seats`, `storageSlots`, `maxHealth`, `tint`,
`chassisSize` (three numbers, like `[1.5, 0.7, 2.6]`)
→ `fuelItem`

An item with `placeableVehicle` set to a machine's id becomes the kit that deploys it.

### implements

What hitches to the back of a machine and works the field grid.

`displayName`, `kind` (`Plow`, `Cultivator`, `Seeder`, `Harvester`), `workingWidth`,
`speedMultiplier`, `fuelLitresPerHour`, `hopperCapacityLitres`, `seedLitresPerCell`,
`litresPerSeedItem`
→ `item`

`item` is what you carry it as, and that item needs `hitchImplement` pointing back at
the implement — the pair is what lets you hitch it. A six-metre plough that halves your
speed is nine lines of JSON:

```json
"implements": [
  { "id": "mymod:subsoiler", "displayName": "Subsoiler", "kind": "Plow",
    "workingWidth": 6, "speedMultiplier": 0.4, "fuelLitresPerHour": 5,
    "item": "mymod:subsoiler_item" }
]
```

### config *(one object, not a list)*

`worldRadiusChunks`, `viewDistanceChunks`, `maxChunkBuildsPerFrame`,
`dayLengthSeconds`, `dawnHour`, `duskHour`, `startHour`, `walkSpeed`, `sprintSpeed`,
`crouchSpeed`, `jumpHeight`, `gravity`, `mouseSensitivity`, `maxHealth`, `maxStamina`,
`maxFood`, `maxWater`, `sprintStaminaPerSecond`, `staminaRegenPerSecond`,
`foodDrainPerMinute`, `waterDrainPerMinute`, `reachDistance`, `dropBagOnDeath`,
`claimRadius`, `wanderingZombieCapDay`, `wanderingZombieCapNight`,
`autosaveIntervalSeconds`

### hordeSchedule *(one object)*

`everyNDays`, `startHour`, `endHour`, `baseWaveSize`, `perPlayerLevel`,
`perClaimStructure`, `maxAlive`, `waveIntervalSeconds`, `spawnRadiusMin`,
`spawnRadiusMax`, `heavyFromHordeNumber`, `heavyShare`, `survivalXp`,
→ `baseZombie`, `heavyZombie`

### startingItems *(one list — replaces the new-world loadout)*

```json
{ "startingItems": [ { "item": "madvoxel:stone_axe", "count": 1 } ] }
```

---

## When something does not work

The loader is built to tell you why rather than fail quietly.

- **A typo'd field name** is a warning naming the field and the line. This is the most
  common cause of "my change did nothing".
- **A bad id** is an error naming the field, the id and the line.
- **Invalid JSON** is an error with the line number.
- **A broken mod does not take the others down.** It reports and is skipped; everything
  else still loads.

Errors and warnings go to the Unity console, and the count appears on the title screen.
In the editor, **MadVoxel → Mods → Validate Installed Mods** runs the whole load without
starting a game.

---

## Saves and mods

Chunk files store block **string ids**, not numbers, so adding or removing mods never
corrupts a world. A block whose mod has been removed degrades to air rather than
turning into the wrong block.

Each world records which mods were active. Load it without one and you get a warning
naming what is missing, before you walk into the hole where it used to be.

---

## What mods cannot do yet

This is a **data** mod system. It cannot add new behaviour — a new crop grows, a new
zombie fights, a new bow shoots and a new plough ploughs, because the game already knows
how to do those things, but a crop that explodes would need code.

The reach of that promise is worth keeping an eye on. Every system added to the game has
its own fields, and none of them are reachable from a mod until someone maps them — a
whole field-machine layer and a bow both shipped before the loader had heard of either.
There is a check that builds an implement, a machine and a crossbow from JSON and asserts
they come out working, which is what stops that gap opening again quietly.

Script mods are the obvious next layer, and the loader is shaped for it: `ModManifest`
already carries load order and dependencies, and `ModContentApplier` is the only thing
that touches the database. Custom meshes and textures are not supported either — every
material is procedural and tinted, so `tint` and `surfaceFamily` are the art controls
you have.
