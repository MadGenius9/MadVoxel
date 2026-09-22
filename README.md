# MadVoxel

A single-player survival-farming game in **Unity 6 (URP)** by **MadGenius**: Rust's
modular building and worn industrial look, 7 Days to Die's diggable terrain, perks,
traders and blood-moon hordes, and two layers of farming — a 7DTD garden you eat from
tonight, and Farming-Simulator-style acreage you sell from.

You cut the shape of your base out of the ground, armour it with snap pieces, and put
a garden beside it. Large but finite map. Not an endless cartoon-cube planet.

**Status: Phase 0 complete and playable.** See [`SCOPE.md`](SCOPE.md) for what is
implemented, what is data-only, and what is deliberately deferred.
See [`DESIGN.md`](DESIGN.md) for the core loop, and [`MODDING.md`](MODDING.md) to make mods.

---

## Requirements

- **Unity 6** (developed against `6000.0.32f1`; any Unity 6 release should upgrade cleanly)
- The Universal Render Pipeline package (pinned in `Packages/manifest.json`)

No external art, audio or plugin dependencies. Every material, texture, prop and UI
element is generated at runtime from code, so a fresh clone runs as-is.

## Getting started

1. **Open the project.** In Unity Hub, *Add project from disk* and pick the
   repository root — it is a Unity project folder (`Assets/`, `Packages/`,
   `ProjectSettings/`). Let Unity import; the first import takes a couple of minutes.
2. **Run the one-time setup.** Menu: **MadVoxel → Setup → Configure Project**.
   This sets *Active Input Handling* to **Both** (the game reads the legacy `Input`
   API), registers the shaders the runtime looks up by name so they survive a build,
   and adds the scene to Build Settings. If Unity asks to restart after the input
   change, say yes.
3. **Open the scene.** `Assets/MadVoxel/Scenes/MadVoxel.unity`, or menu
   **MadVoxel → Setup → Open Scene**. The scene holds exactly one object,
   `MadVoxel`, carrying the `GameBootstrap` component — everything else is built at
   runtime.
4. **Press Play.** The title screen appears.

### If the render pipeline is not configured

`Configure Project` reports whether a Scriptable Render Pipeline asset is assigned.
If none is, the game still runs on the Built-in pipeline (materials fall back to the
`Standard` shader). For the intended URP look, create
*Assets → Create → Rendering → URP Asset (with Universal Renderer)* and assign it in
*Project Settings → Graphics → Default Render Pipeline* and in *Project Settings → Quality*.

### Faster iteration

Select the `MadVoxel` object in the scene and tick **Quick Start** on `GameBootstrap`
to skip the title screen and drop straight into a named scratch world.

## Scenes

There is one scene: **`Assets/MadVoxel/Scenes/MadVoxel.unity`**.

It contains a single `GameBootstrap` object. The title screen, the world, the player
rig, the camera, the lights, the HUD and every menu are constructed in code at
runtime. That keeps the project free of prefab and material assets that would drift
out of sync with the scripts, and it means there is no scene merge conflict to
resolve. **MadVoxel → Setup → Rebuild Scene** regenerates the scene if it is ever lost.

## Controls

| Input | Action |
| --- | --- |
| `W` `A` `S` `D` | Move |
| Mouse | Look |
| `Shift` | Sprint (drains stamina) |
| `Ctrl` / `C` | Crouch |
| `Space` | Jump; climb while on a ladder |
| Left mouse (hold) | Dig a block, salvage with a wrench, attack — or **repair** a snap piece with the hammer |
| Right mouse | Place the held block or piece; **upgrade** a snap piece with the hammer; eat or drink |
| `E` | Interact — open a door, a crate, a workbench, a campfire, a bedroll |
| `R` | Rotate the deployable about to be placed |
| `1`–`9` / scroll | Select hotbar slot |
| `Tab` / `I` | Inventory, container and the work-order strip |
| `P` | Skills — spend perk points |
| Wire tool + LMB | Start or finish a wire or a hose; RMB drops the line or cuts a socket |
| `Esc` | Pause (this is the only thing that stops the world) |
| `F3` | Debug overlay — FPS, position, chunk, streaming state, seed |

The inventory deliberately does **not** pause the game, so crafting during a horde
night is still a risk.

The UI is **Claim Slate** — a mode-aware overlay that keeps the centre of the screen
clear and shows a layer only while the thing it describes is in front of you. On foot
that is a compass tape, a toolbelt and two short bars; a snap piece in hand adds the
build plate; a blood moon adds an oxidised rim and one countdown line. There is no
minimap. See [`UI.md`](UI.md).

## Building

Two systems, on purpose.

**Terrain** is a block volume. Dig it with a pick, shovel or hatchet: trenches, pits,
ramps, rooms, ore shafts. Hardness and tool tier decide what you can cut and how fast,
so a stone pick will not open iron ore and nothing but time opens concrete. You can
place dirt and stone back, which is how you flatten a pad.

**Snap pieces** are Rust-style modular parts on a 3 m grid: foundation, floor, wall,
window wall, doorway, door, half wall, stairs, roof, ladder, hatch. They snap to each
other and sit on the terrain you flattened. Craft them from planks and place them from
the hotbar — they always go up as **twig**, which is nearly free and nearly useless.

The **hammer** is what makes it a base:

- **Right mouse** upgrades the piece you are looking at one tier:
  twig → wood → stone → metal → armored. Each tier costs materials and takes
  proportionally less damage.
- **Left mouse** repairs it.

Metal and armored need Construction perk ranks; wood and stone do not.

**Nothing floats.** A piece only stands if it chains back to a foundation resting on
solid ground. Dig the dirt out from under a wall and everything it was holding up comes
down — which is also how a horde gets in.

Plant the **tool cupboard** to claim the area. Wandering zombies avoid claimed ground;
the horde walks straight at it.

## Farming

Two layers, at two scales, on purpose.

### The garden (playable now)

Craft a **farm plot** from planks and fibre and drop it on dirt — it will not sit on
stone or concrete. Plant a seed with `E`, and the crop grows on the world clock:
seedling → growing → mature → ready, visible as the plant gets taller and turns
gold when it is ripe. `E` again harvests it into your bag.

Seeds come from the world before they come from a trader: **wild yucca, grain and corn**
grow in the old fields and drop seeds when you clear them. Three crops to start:

| Crop | Matures | After harvest | Also a field crop |
| --- | --- | --- | --- |
| Potato | 1.5 days | Plot goes bare, usually returns seed | No |
| Corn | 2.5 days | Keeps growing — pick it again | Yes |
| Wheat | 2 days | Plot goes bare, usually returns seed | Yes |

Cook at a campfire: baked potato, corn bread, vegetable stew. **Cooked food is the only
thing that restores stamina as well as hunger**, which is what makes the garden matter
the day before a blood moon.

A horde that reaches your plots will smash them and take the crop with them, so fence
the garden or keep it inside the walls.

### The field (Phase 1)

The bulk layer: a one-metre cell grid over the terrain running the FS tillage cycle —
wild → plowed → cultivated → seeded → growing → ready → stubble — with moisture,
fertiliser and a per-cell yield factor. Harvest is measured in **litres**, not stacks,
and goes into a grain bin.

The whole state machine, the growth timing and the litre yield are implemented and
tested today. What Phase 1 adds is the tractor and implements that work a swath at a
time. You can already break ground by hand: hold the **hoe** and right-click open
ground to plow one cell — the block turns to tilled soil and stays that way.

## Power, water, weather and people

The farm runs on a grid now. A generator bank burns gasoline for watts, relays carry the
reach, and lights, a fridge, traps and the water pump draw it down. A pump set over
saturated ground fills a tank, a tank feeds a tap and a sprinkler, and the sprinkler pays
for itself in the harvest.

Six weather states turn dials on all of it: a drought cuts the pump and costs an
unwatered plot half its yield, a storm takes the panels to zero and can break an exposed
fitting, a frost night freezes the tap. Food spoils, and a fridge with watts in it is the
only thing that slows that down.

Once there is a cupboard, a bed, a fire, water and food, a **Colony Board** lets you
charter **Mad Colony** — up to three people who farm, guard, repair and cook, eat from
your crates and drink from your tap, and leave if you neglect them.

All of it is painted across five biomes, and all of it makes noise: a bright, busy,
populous claim is a **beacon**, and the blood moon reads that number.

See [`SYSTEMS.md`](SYSTEMS.md) for how each layer works and why.

## Skills

Everything you do pays XP — digging a block you did not place yourself, crafting,
killing, harvesting a crop. Each level hands over a perk point, and the HUD's level
line turns orange when one is waiting. Press **`P`**.

Fourteen perks across seven categories: Mining, Construction, Combat, Scavenging,
Medicine, Vehicles and Farming. Pick a category on the left, buy ranks on the right.
Every row shows what the next rank costs and, when it cannot be bought, why — "Needs
level 6", "Needs 1 point(s)", "Maxed" — rather than just greying out.

What a rank actually changes:

| Perk | Effect |
| --- | --- |
| Miner 69er | Dig faster |
| Motherlode | More ore and scrap per block |
| Carpenter | Unlocks cobblestone, iron and steel blocks, and the Metal and Armored snap tiers |
| Handyman | The hammer repairs more per swing |
| Heavy Hitter | More melee damage |
| Iron Lungs | A bigger stamina pool and slower drain |
| Scrapper | More out of a salvaged wreck |
| Pack Mule | Slower stamina drain |
| Field Medic / Physician | Food, water and bandages do more; Physician unlocks the bandage recipe |
| Living Off The Land | Bigger garden harvests and more seeds back |
| Grease Monkey | Repair speed, and the buggy kit recipe |
| Agronomist | Unlocks the grain bin; field yield lands with the tractor |
| Economiser | Fuel economy, once there is something to drive |

Ranked perks apply immediately — no re-equip, no reload. Ranks and unlocked recipes
are saved, and loading re-grants every recipe your ranks have earned, so a save made
before a perk gained an unlock is repaired rather than silently short.

Three effect types are authored but not yet read by anything: ranged damage, vehicle
fuel economy and field yield. They wait on the weapon, the drivable buggy and the
harvester. The test suite asserts that list, so a fourth cannot creep in unnoticed.

## The world

A finite map, 3072 m square by default (`worldRadiusChunks` in the game config), all of
it deterministic from the seed. Walk to the edge and the land runs out.

Scattered through it: **two trader outposts** with concrete perimeter walls, corner
towers and an iron strongroom; **farm ruins** with collapsing plank walls and salvage;
**town fragments** of concrete shells along a cobble road. Their ground is levelled, so
they double as ready-made building pads. You get a bearing to the nearest outpost when
you wake up.

## Developer hotkeys

A day is 20 real minutes and the first blood moon falls on day 7 at 22:00, so reaching
it honestly takes over two hours. These shortcuts make the Phase 0 loop testable in a
sitting. They are live whenever **Developer Tools** is ticked on the `MadVoxel` object
(on by default; untick it for a release build), and the list is shown in the `F3` overlay.

| Key | Action |
| --- | --- |
| `F1` | Utility kit: wire tool, the whole grid, the whole plumbing, a colony board, gas |
| `F2` | Grant one level, so the skills screen can be exercised straight away |
| `Shift`+`F5` | Step the weather on — clear, overcast, rain, drought, storm, frost |
| `Shift`+`F6` | Found the colony and take in a colonist |
| `Shift`+`F7` | Add 25 claim heat |
| `F4` | Toggle fly mode — `Space` up, `Ctrl` down, no gravity |
| `F5` | Skip one hour |
| `F6` | Skip to dawn |
| `F7` | Jump the calendar to ten in-game minutes before the next blood moon |
| `F8` | Spawn a shambler six metres in front of you |
| `F9` | Refill health, stamina, food and water |
| `F10` | Grant the test kit: iron tools, hammer, wrench, terrain blocks, the full snap set, deployables, food and bandages |
| `F11` | Toggle invulnerability |
| `F12` | Ripen every planted crop, so the garden can be tested without waiting days |

### Walking the Phase 0 success test in about ten minutes

1. New world. Dig a pit and a ramp out of it with the shovel, and flatten a pad beside
   it — that is the 7DTD half.
2. `F10` for the kit. Lay foundations on the pad, then walls, a doorway, a door and a
   roof. Put a storage box inside and something in the box.
3. **Drop farm plots in a fenced dip beside the shack**, plant potato and corn seeds in
   them, then press `F12` to ripen and `E` to harvest. Cook the potatoes at a campfire
   and eat one — watch stamina come back, not just hunger.
4. Plant the tool cupboard. Point the hammer at one wall and right-click twice:
   twig → wood → stone. Watch the piece change material and get tougher.
   Press `F2` a few times, then `P`, and put the points into **Carpenter** — the metal
   tier appears on the same wall, and **Miner 69er** is the one you will feel in step 5.
5. Try undermining your own foundation with the shovel — the wall above it should come
   down. Rebuild it.
6. Hold the hoe and right-click open ground: it turns to tilled soil. That is the field
   layer's first step.
7. `F7`, then hold the shack through the blood moon. They walk the ramp, fall in the
   pit, and chew whatever is in front of them — **including your plots**, so leave one
   outside the fence and check it gets smashed.
8. Quit to the menu, then **Continue**: the hole, the base, the box contents, your
   inventory **and the plots with their growth still part-done** should all come back.

`F11` and `F4` are there for when you want to watch the horde work on the base rather
than fight it.

## Starting a new world

Title screen → **NEW WORLD**:

- **World name** — becomes the save folder name. An existing name is never
  overwritten; a numeric suffix is added instead.
- **Seed** — any integer. Leave it blank for a random seed; type non-numeric text and
  its hash is used. The same seed always produces the same planet: terrain, caves,
  ore, trees and surface scrap are all derived from it deterministically.

**CONTINUE** lists saved worlds with their seeds.

## Save location

Saves live under Unity's persistent data path:

```
<persistentDataPath>/Saves/<World Name>/
    world.json          seed, elapsed hours, blood-moon counter, active mods, timestamps
    player.json         position, vitals, all 36 inventory slots, level, XP, perks
    structures.json     deployables, snap pieces, crate contents, plot crops and
                        planting times, grain bin litres, worked field cells
    chunks/c.<x>.<y>.<z>.mvc   edited chunks only, palette + run-length encoded
```

| Platform | `<persistentDataPath>` |
| --- | --- |
| Windows | `%USERPROFILE%\AppData\LocalLow\MadGenius\MadVoxel` |
| macOS | `~/Library/Application Support/MadGenius/MadVoxel` |
| Linux | `~/.config/unity3d/MadGenius/MadVoxel` |

*(**MadVoxel → Setup → Configure Project** sets the company and product names, so run it
before making a world you want to keep.)*

Only chunks you have actually changed are written to disk. Everything else is
regenerated from the seed, which is what keeps an infinite world off your drive.
The world autosaves every two minutes, on death, on pause-menu *Save now*, and on quit.

## Mods

Mods are **folders you drop in** — no compiling, no scripts, nothing executed. A mod is
JSON that adds to and patches the game's content database, so installing one from the
internet cannot run code on your machine.

```
<persistentDataPath>/Mods/
  my_mod/
    mod.json
    content/*.json
```

A mod can add blocks, items, tools, recipes, crops, snap pieces, deployables, zombies,
perks, quests and traders, and can **patch any vanilla definition field by field** —
change one number on an item and everything else about it stays as it was. References
resolve after every mod has loaded, so mods can point at each other in any order.

`Mods/example_pumpkin/` in this repository is a complete worked example. Read
[`MODDING.md`](MODDING.md) for the full field reference.

In the editor: **MadVoxel → Mods → Open Mods Folder**, **Write Content Id Reference**
(dumps every id in the game to `ContentIds.txt`) and **Validate Installed Mods**.

## Content and data

All game data is ScriptableObject-shaped: blocks, items, recipes, structures, zombies,
the horde schedule, skills, traders, quests and the vehicle.

By default the game builds that content in memory from
`Assets/MadVoxel/Scripts/Content/ContentLibrary.cs`, so it runs straight from a clone
with no assets to import. To edit the data by hand, run
**MadVoxel → Content → Generate ScriptableObject Assets**. That writes every
definition into `Assets/MadVoxel/Content/…` plus a `ContentDatabase` in `Resources`,
which the game then loads in preference to the code-defined defaults.
**MadVoxel → Content → Delete Generated Assets** reverts to the code path.

## Project layout

```
Assets/MadVoxel/
  Scenes/            the single bootstrap scene
  Scripts/
    Core/            bootstrap, session, clock, sky, noise, materials, input, damage
    Core/Player/     first-person motor, look, vitals, inventory, dig/place/interact
    World/Terrain/   chunks, terrain generation, POIs, greedy mesher, streaming, raycast
    World/Fields/    FS-style field cell grid, tillage state machine, litre yield
    Farming/Crops/   crop definitions shared by both farming layers
    Farming/Plots/   garden farm plot, plant growth visuals, grain bin
    Modding/         JSON parser, mod manifest, loader, content applier
    Building/        snap grid and pieces, stability, deployables, cupboard, build ghost
    Inventory/       items, stacks, containers, recipes, crafting
    Perks/           XP and levelling, perk tree definitions, effect resolution and buy rules
    UI/              Claim Slate: palette, compass, visor, inventory, perk diagram, menus
    Power/           the grid: devices, the graph, the wire tool, traps
    Fluid/           the water: pump, pipe, tank, tap, sprinkler
    Colony/          Mad Colony: the charter, morale, people and their jobs
    Claim/           claim heat
    World/Biomes/    the five regions and what they change
    World/Weather/   six states and the dials they turn
    Traders/         trader definitions and stock
    Quests/          quest definitions
    Vehicles/        vehicle definitions
    AI/              zombie definitions, brain, spawn director
    Horde/           blood-moon schedule and director
    UI/              HUD, inventory/crafting, menus, debug overlay
    Save/            save paths, JSON model, chunk files, save service
    Content/         the content library and the content database
    Editor/          project setup, content generation, scene rebuild
```

## Architecture notes

- **No god manager.** `GameSession` builds and tears down a play session and wires
  systems to each other; it holds no gameplay rules. Systems talk through events
  (`Notifications`, `Zombie.Died`, `StorageStructure.OpenRequested`) rather than
  reaching for a global singleton.
- **Threading.** Terrain generation, chunk file reads and greedy meshing all run on
  worker threads over plain arrays. Only mesh upload and chunk bookkeeping touch the
  main thread. No Unity API is called off-thread.
- **Chunks are 16³.** A chunk that is entirely one block type keeps no array at all,
  and a uniform chunk surrounded by chunks of the same opacity is never meshed. That
  is what makes a 192-block-tall streamed world affordable.
- **The build grid is 3 m in X/Z but free metres in Y.** Rust's foundation size makes
  walls and doorways read at the right scale; leaving Y at 1 m means a foundation can
  sit on any height you dig or flatten to, instead of only on multiples of three.
- **Stability is reachability, not physics.** A flood fill from grounded foundations
  decides what stands. Cheap, predictable, and it makes undermining a real attack.
- **Stable ids on disk.** Chunk files store a palette of block *string* ids, so
  reordering the block registry cannot corrupt an existing save. Unknown blocks
  degrade to air rather than shifting every other block.
- **Multiplayer is not implemented** and no netcode exists, but nothing here assumes
  a single local player: the world, inventory and save layers are plain C# with no
  static player reference.

## Verification

Run the headless checks on any machine with the .NET SDK — no Unity, no GPU, no licence:

```bash
cd Tests/Headless
dotnet run
```

**731 checks, all passing.** They compile the real gameplay sources against a small
executable `UnityEngine` shim and actually run them, covering content wiring, the build
grid and its connection graph, chunk storage and coordinates, the greedy mesher,
terrain generation, POI layout, crop growth timing, the field tillage state machine and
its yield, inventory, crafting, the perk buy rules and effect maths, the Claim Slate
palette, compass bearings and the blood-moon schedule, the biome paint, the weather
table, the power and fluid graphs, spoilage, claim heat, the colony's founding and
morale rules, the chunk-file save round-trip, and the whole mod pipeline — including loading the example mod that ships in
this repository.
See [`Tests/Headless/README.md`](Tests/Headless/README.md) for the full list and for
what is deliberately out of reach.

What they cannot cover is anything that needs the engine: rendering, physics, the
character controller, chunk streaming, AI behaviour, the UI, and how any of it feels.
**The game has not yet been run in a Unity editor** — see the limitations section of
`SCOPE.md`.
