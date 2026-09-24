# MadVoxel — scope

Rust building and look, 7 Days to Die terrain, perks and hordes, plus two layers of
farming: a 7DTD garden and FS-style acreage. Single-player, local saves. No multiplayer and no netcode; the world, inventory, building, perk and save
layers are plain C# with no static player reference, so co-op stays possible later.

---

## Phase 0 — implemented

| # | Requirement | State |
| --- | --- | --- |
| 1 | Diggable streamed terrain | **Done** |
| 2 | FP controller, tools, inventory, save | **Done** |
| 3 | Craft/place farm plots; plant seeds; growth stages; harvest; cook/eat | **Done** |
| 4 | Flatten a pad; snap shack + door + box + cupboard + plots in a fenced dip | **Done** |
| 5 | Upgrade one wall | **Done** |
| 6 | Day/night zombies; horde can break a plot | **Done** |
| 7 | Respawn; plots, growth and holes persist | **Done** |
| — | *Also:* POIs, tool cupboard, stability, finite map | **Done** |
| — | *Also:* field grid, tillage state machine and litre yield (Phase 1 groundwork) | **Done** |

### Detail

**Terrain.** A 16³-chunk block volume, 192 blocks tall, streamed around the player with
a 1-chunk generation apron. Dirt, grass, clay, sand, gravel, stone, coal and iron veins,
bedrock floor. Generation, chunk file reads and greedy meshing all run on worker
threads. The map is **large but finite** — 3072 m square by default — with a soft edge
that pushes the player back.

**Digging (7DTD rules).** Every block carries hardness, a preferred tool and a required
tool tier. Bedrock is indestructible; dirt and sand are quick; stone, ore, concrete and
metal get progressively slower. Wrong tool costs you 65% of your speed, and a tier below
the block's requirement cannot harvest it at all. Tools wear and sprinting/swinging
drains stamina, so digging is a budget, not a free action. Trenches, pits, ramps,
rooms and shafts all work because the volume is genuinely 3D — you can undercut, overhang and
tunnel.

**Building (Rust snap on 7DTD land).** A 3 m cell grid in X and Z with free integer
metres in Y, so a foundation sits on whatever height you flatten to. Eleven kinds —
foundation, floor, wall, window wall, doorway, half wall, stairs, roof, ladder, hatch,
door — each with a full **Twig → Wood → Stone → Metal → Armored** chain. Only twig is
placeable; the **hammer** upgrades in place and repairs. Shared cell edges are one slot,
so two walls can never occupy the same gap.

**Stability.** A piece stands only if a chain of connected pieces reaches a foundation
resting on solid terrain. Undermine the ground and the structure above comes down. It
is reachability, not a force simulation — enough to forbid floating towers and to make
digging a legitimate attack on a base.

**Tool cupboard.** One per base, with a privilege radius. Wandering zombies leave
claimed ground alone; the horde walks straight at it.

**POIs.** Deterministic from the seed: two walled trader outposts with concrete
perimeters, corner towers and an iron strongroom; farm ruins with collapsing plank
walls and salvage; town fragments with concrete shells, punched windows and a cobble
road. Their pads are levelled, so they are also the easiest places to start building.
You get a bearing to the nearest outpost on waking.

**Horde.** Blood moon every 7 days, 22:00–04:00. The wave budget scales with player
level **and base footprint** — deployables inside the cupboard radius plus every snap
piece near it. They path at the cupboard, walk ramps, fall into pits, and chew through
whatever blocks them: one raycast handles snap pieces, deployables and terrain alike,
so a wall, a door and a dirt berm are all equally edible.

**Farming, layer A — the garden.** Farm plots are deployables that must sit on soil.
Three crops (potato, corn, wheat) with per-crop `growsOnPlot` / `growsOnField`,
`daysToMature` and `replants` flags exactly as the brief specifies. Growth is derived
from the world-clock hour the seed went in, so a crop keeps maturing across a save and
reload rather than only while you watch. Corn replants itself on harvest; potato and
wheat clear the bed and usually return seed. Wild yucca, grain and corn grow in the
world and drop plantable seeds, so the garden is reachable before any trader. Crops cook
at a campfire into meals that restore stamina as well as hunger — the only food that
does. Plots are low-health on purpose, so a horde that reaches them costs you dinner.

**Farming, layer B — the field.** A one-metre cell grid with the full FS tillage cycle
(wild → plowed → cultivated → seeded → growing → ready → stubble), moisture, fertiliser
and a per-cell yield factor, harvesting to **litres** and a grain bin that stores them.
Repeated cropping without fertiliser measurably reduces yield, and trampling knocks a
growing cell back to stubble. The hoe plows one cell by hand, turning the block to tilled
soil so the work is visible and persists; the tractor and its four implements drive the
same operations across a swath.

**Death.** You keep your hotbar; the rest of the bag goes into a lootable backpack where
you fell, which persists like any other container.

**Persistence.** Only edited chunks reach disk, palette + run-length encoded against
stable block string ids. Player vitals, all 36 slots, level, XP, perk ranks, unlocked
recipes and respawn point are saved, as is every deployable and every snap piece with
its tier, health and open/closed state.

---

## Phase 1 — built

This heading used to read *"data is real, runtime is next"*, back when Phase 1 was
authored content with nothing reading it. That stopped being true some time ago: every
area below now has a runtime behind it, and the **Still to write** column is down to
scraps — a crossbow, a gun tier, a second seat, an irrigator, a dart trap, colonist
pathfinding.

What has *not* changed is the caveat. "Built" here means the code exists, compiles
against Unity reference assemblies, and passes the headless checks. It does not mean
anyone has played it. Phase 0's own success test still needs an editor, and the two
defects a player actually hit — everything rendering magenta, and a startup exception
from a uGUI input field — were both invisible to every check in this repository.
Assume tuning, and read `PLAYTEST.md` before believing any row in this table.

| Area | Shipped | Still to write |
| --- | --- | --- |
| **Perks** | 15 perks across Mining, Construction, Combat, Scavenging, Medicine, Vehicles and Farming. XP, levelling, point accrual, **the skills screen** (`P`) and the buy rules are live, and every effect type but one is applied in play: dig speed, block and crop yield, salvage, building tier, stamina pool and drain, melee damage, healing, repair, wire reach, pump rate, trap damage, storm and drought resistance, claim heat, field yield and fuel economy. Ranks and unlocked recipes save and reload. | Nothing — every effect type the tree grants is now read in play. |
| **Traders** | **Live.** A counter at each outpost, a Claim Slate price board, buy and sell from the bag, reputation tiers that unlock stock and improve rates, clock-derived restocking, and bulk produce sold by the litre straight out of a harvester. Saved per trader. | A trader NPC with a schedule, and the quest board. |
| **Quests** | **Live.** Four contracts on the trader's board, taken and handed in at the counter, three at a time, tracked on the HUD. Kills and delivered litres tally; fetch and survive-nights progress is recomputed. Saved with the player. | A mining contract, and contracts that are generated rather than authored. |
| **Vehicles** | **Live.** A tractor you craft, set down and drive, on a character controller so a dug ramp behaves; fuel burn that Economiser actually changes; a bed you can load and reach from the seat; damage, wrecking and save/reload where it was parked. The Scrap Buggy shares the rig. | Seats for more than one, and a machine that reacts to being rammed. |
| **Field machines** | **Live.** Plough, cultivator, seed drill, harvester and muck spreader, hitched from the hand and raised or lowered on a key; a swept swath that cannot stripe a field; hopper accounting that refuses to sow or spread what it cannot pay for; tipping litres into the grain bin; Agronomist's field yield. | A field irrigator. |
| **Grain bin** | **Live.** Stores bulk by the litre, tipped into by a harvester, drawn back out as produce at the crop's own rate, with its own screen. | Selling straight from the bin — you still carry or drive it to a counter. |
| **Field cover** | **Live.** The standing crop, meshed per 16 m patch, growing in eight visible steps and going gold on the clock. Remeshed only when something changes. Cultivated ground has its own block, so the second pass leaves a mark. | Wind. |
| **Electricity** | **Live.** Generator, battery and solar banks, relays, switches and splitters, lights, a fridge, a blade trap and a fence post, all on one graph with a wire tool, a predictable brown-out and a real fuel economy. | A dart trap, a turret, and the timer-relay puzzles this pass deliberately skipped. |
| **Water** | **Live.** A dug well on a water-table block, electric pump, pipes, tanks, barrels, taps and plot sprinklers on one fluid graph, with breaks, freezes and drought. | A field irrigator on the FS-style cells, and surface ponds as a source. |
| **Weather** | **Live.** Six states rolled per region, turning dials on the pump, the panels, the soil, barrels, morale and crops. | Seasons with their own economies. |
| **Spoilage** | **Live.** Shelf life on the stack, rot, a fridge that earns its watts, and rot composted back into field fertility so waste has somewhere to go. | — |
| **Claim heat** | **Live.** One number fed by lights, the generator, population, acreage and traps, read by the wanderer cap and the horde budget. | — |
| **Mad Colony** | **Live.** A charter on a board, up to six people with four jobs, needs, morale and a walk-out, fed and watered from the base you built. Wanderers arrive overnight at a claim with a spare bed, supplies and enough noise to be found. | A trader quest that sends someone; pathfinding; The Marker. |
| **Furnace** | **Live.** A deployable that smelts unattended on the world clock, one shared inventory for ore, fuel and output, burning cheapest fuel first. Catches up across a save. The campfire smelts stay as the way in. | Steel, and a bellows or a powered furnace that runs faster. |
| **Ranged combat** | **Live.** A bow you draw and loose, craftable in the first hour. Draw decides speed and damage; arrows sweep rather than teleport, land, and are half recoverable. Stone and iron heads. Hit zones: a head shot is worth two and a half body shots, a leg shot seven tenths of one — measured by height, since the drawn head has no collider. | A crossbow and a gun tier. |
| **Melee combat** | **Live.** A swing is a scored cone rather than a pinpoint ray, so it lands on a shoulder, on a body pressed against you, and on whatever is in the way — but never on your own walls or colonists, and never through a door. Hit flash, stagger, knockback, hit markers, damage numbers and a blood vignette. | Blocking or parrying, and a reason to choose one weapon over another beyond damage. |
| **Horde** | **Live.** Budgets on base footprint and heat, and reads the approach: twelve scored lanes round the claim, a wave spread across them by how walkable each is, each zombie given a way in before the base itself. | Ladders, and zombies that dig rather than route around. |

---

## Phase 2 — started

| Area | Shipped | Still to write |
| --- | --- | --- |
| **Steel** | **Live.** Forged from iron and only in a furnace, which is what turns the furnace from a faster iron smelter into the gate on tier three. Steel pickaxe, axe and shovel at tool tier 3 — faster and far longer-lived than iron. No perk gate: the station is the gate, same rule iron follows. | A steel melee weapon worth choosing over the club. |
| **Deeper ore** | **Live.** Tungsten, below y=22 and the only block in the game that needs a tier-3 pickaxe, so steel tools have somewhere to go. Density measured rather than guessed — roughly a third of iron's rate in a band less than half as tall, which makes a seam worth walking back up for. | A reason to dig past it. |
| **Armoured tier** | **In play.** Every armoured piece is paid for in steel plus one tungsten plate. The tier existed in the data since Phase 0 and cost more iron and a pile of coal, which made the top tier a bigger version of the one below it rather than a different material. | Armoured *deployables* — a crate or a door you can harden. |

| **Traps on killboxes** | **Live.** A spike trap: unpowered, cheap, hand-craftable, and the only defence available before electricity. It blunts as it bites and stops at blunt rather than destroying itself, so a killbox is something you maintain between blood moons. Kills it makes pay XP, quest credit and loot, dropped where they fell. No claim heat — a blade trap is loud and draws the next wave larger; spikes are silent and charge you in scrap instead. | Barbed wire that slows rather than kills. |
| **Repair tension** | **Started.** The hammer mends deployables, which it never could before — every crate, furnace and trap in the game was previously a consumable, recoverable only with a wrench and a rebuild. Some pieces charge a material to mend; spikes do, which is where a killbox's upkeep lives. Salvage now scales with condition, so pulling a worn piece and re-placing it is never cheaper than mending it. | Tools degrading in a way you have to plan around, rather than just wearing out. |

Still untouched: more POIs, guns, a second vehicle, and a map and compass.

---

**Mods.** Drop-in folders of JSON that add to and patch the content database: blocks,
items, tools, recipes, crops, snap pieces, deployables, zombies, perks, quests, traders,
vehicles, the horde schedule, the tuning config and the starting loadout. Patching is
field-level, so a mod changes one number without restating a definition. References
resolve in a second pass after every mod has contributed, so mods can point at each
other regardless of load order. Dependencies and load order are declared and resolved
deterministically; a missing dependency skips a mod instead of half-applying it, and a
broken mod reports and is skipped without taking the others down. Nothing is compiled or
executed. Worlds record which mods built them and warn on load if one is missing. See
`MODDING.md`.

## Deliberately not implemented

- Multiplayer, dedicated server, netcode.
- HDRP or ray tracing — the project is URP.
- Every Rust monument or the full 7DTD perk encyclopedia.
- Generated art libraries. Every material is a procedural grime texture plus a tint and
  every prop is built from boxes at runtime — readable at 50–100 m, deliberately
  placeholder.

## Known limitations and shortcuts

- **Not play-tested in the Unity editor.** Scripts compile clean against Unity
  reference assemblies and 358 headless checks (`Tests/Headless`) execute the content
  wiring, build grid, chunk storage, mesher, terrain, POIs, inventory, crafting and
  save. None of that reaches rendering, physics, the character controller, streaming,
  AI behaviour or the UI. Expect tuning, not rewrites.
- **The hammer upgrades and repairs but does not place.** Pieces are placed from their
  own hotbar items, which is more discoverable than a build menu and avoided shipping
  UI that could not be tested. The brief asks for hammer-place too; that is a Phase 1
  cleanup.
- **Roofs are flat.** No pitched or conical roof pieces.
- **Stairs use stepped box colliders**, not a smooth ramp.
- **The look is the furthest thing from its target.** The brief is *Rust building and
  look, 7 Days to Die terrain* — and the build currently reads closer to Minecraft than
  to either, for two separate reasons that need two separate fixes:

  1. **Terrain shape is done; the digging granularity is not.** Natural ground is now
     meshed with surface nets and built blocks stay cubes, which is 7DTD's visual split
     and most of the look. What is still missing is the density field: 7DTD stores a
     density per voxel, so a crater there has a lip where this one has a whole-block
     edge. Adding it touches chunk storage, the save format and every caller that digs
     — and none of the smoothing has to be redone for it.
  2. **Props and characters are boxes.** Deliberately — `PrimitiveBuilder` is a
     placeholder and swapping real meshes in means replacing those calls and nothing
     else. But Rust-grade models are the one thing that cannot be generated from code;
     they need an artist, an asset store, or the project owner.

- **No ambient occlusion on terrain meshes.** Corners read flat, which is the single
  strongest reason the cube path reads as a toy. The mesh carries positions, normals and
  UVs and no vertex colours at all. Baking AO into the greedy mesher is cheap and helps
  whichever way the terrain goes.
- **No texture atlas.** One sub-mesh and material per block type per chunk.
- **Terrain structural integrity is one rule deep.** Snap pieces collapse when
  undermined; overhanging *terrain* does not fall.
- **POIs are block stamps**, not authored prefabs — no interiors, loot containers or
  trader NPCs yet.
- **Cultivated ground now reads differently from plowed** — a second, lighter tilled
  block, appended to the block order so older saves still decode.
- **Fertiliser is in; irrigation is not.** Compost is made from spoiled food and fibre,
  spread by hand or by a muck spreader, and ground left fallow recovers on the clock.
  The economics are pinned by tests. Moisture is still written by sprinklers and
  nothing else, and no implement waters an acre.
- **Farm snap pieces are limited to the fence.** Barn, shed, pen and greenhouse frame
  are Phase 1; the grain bin ships as a deployable rather than a snap piece.
- **The seed bag is just the seed stack.** No dedicated seeding container.
- **No perk effect is inert** any more. The check that tracked the gap now asserts the
  deferred list is empty, so one cannot come back quietly.
- **XP farming guard survives a reload.** Blocks you placed pay no harvest XP when
  re-mined, and the set is saved, so quitting no longer launders them. Crafted building
  blocks pay no harvest XP at all either.
- **A destroyed container now spills** into the same sack death drops, crates and
  furnaces alike. Where no sack will fit, the loss is reported rather than silent.
- **Developer hotkeys ship enabled.** Untick *Developer Tools* on the `MadVoxel` object
  before a release build.
- **Audio is synthesised and unheard.** Fourteen voices generated at runtime from a
  swept oscillator and a filtered noise burst — no audio files, same rule as the art.
  The tests check every voice is finite, audible, free of clipping and silent at both
  ends, which catches the failures that cannot be diagnosed by ear. Whether any of it
  actually sounds good is unknown; nobody has heard it.
- **The colony's second recruit path is unbuilt.** A wanderer turns up at the fence on
  their own now, but the trader quest that should also send someone is not written.
  `Shift+F6` still forces one for testing.
- **Colonists do not path around obstacles.** They walk towards a target and let the
  character controller handle the ground, which works on dug terrain and ramps but
  will wedge them on a wall corner.
- **The Marker is not built**, as asked. `ColonyWorld.ForbiddenActions` is the hook.
- **Field irrigation is still unbuilt.** A sprinkler wets field cells, but there is no
  implement or boom that waters an acre.
- **Surface ponds are not a pump source.** A well is dug to the water table; standing
  water has no fluid rendering and is not modelled.
- **The Claim Slate trader and silo screens are unbuilt.** Both are specified in
  `UI.md`, and both wait on a runtime to sit behind them. The tractor cluster is live.
- **The toolbelt is nine slots.** The inventory is 9 + 27 and the save stores 36
  slots; ten would be a model and save change, not a UI change.
- **The UI has never been rendered.** Every layout number in Claim Slate is a
  considered guess at a 1920x1080 reference canvas. Nobody has seen it draw.
- **A hopper holds one crop.** The seed drill sows one crop at a time and a harvester
  stops rather than mixing two. The grain bin is the thing that holds more than one.
- **Implements have no collision of their own.** The one hanging off the drawbar is a
  visual and a swath; it will pass through a fence the tractor would hit.
- **Mods are data only.** A mod can add a crop, a zombie, a plough, a machine or a bow
  because the game already knows how to do those things; it cannot add new behaviour.
  Script mods are the next layer and the loader is shaped for them.
- **Mods cannot ship art.** Every material is procedural and tinted, so `tint` and
  `surfaceFamily` are the only visual controls a mod has.

---

## Success test (Phase 0)

> Place farm plots → plant → wait stages → harvest food → cook → build shack beside the
> garden → horde breaks at least one plot or plant → reload with remaining plots and
> growth intact.

Every step has an implementation behind it. The test itself needs a Unity editor.

## Next milestone

1. **Script mods** — a sandboxed hook layer on top of the data loader. Everything else on
   this list is content; this is the one that changes what mods can be.
2. **Field crop cover at distance** — the cover draws within 96 m; a big farm seen from a
   hill is still bare ground past that.
3. **Zombies that dig** — they route around a wall now, and route well. The next step is
   the ones that go through it rather than round.
