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
growing cell back to stubble. All of it is implemented and tested; what Phase 1 adds is
the tractor and implements that drive it across a swath. The hoe already plows one cell
by hand, turning the block to tilled soil so the work is visible and persists.

**Death.** You keep your hotbar; the rest of the bag goes into a lootable backpack where
you fell, which persists like any other container.

**Persistence.** Only edited chunks reach disk, palette + run-length encoded against
stable block string ids. Player vitals, all 36 slots, level, XP, perk ranks, unlocked
recipes and respawn point are saved, as is every deployable and every snap piece with
its tier, health and open/closed state.

---

## Phase 1 — data is real, runtime is next

All Phase 1 content is authored and loadable and its hooks exist; the runtime systems
are deliberately unwritten until Phase 0 passes its success test.

| Area | Shipped | Still to write |
| --- | --- | --- |
| **Perks** | 14 perks across Mining, Construction, Combat, Scavenging, Medicine, Vehicles and Farming — including Living Off The Land for garden yield and seed returns, and Agronomist for field yield and the grain bin, with per-rank effects, level gates and recipe unlocks. XP, levelling and point accrual **are live** and scale the horde. Metal and Armored building tiers are already gated on Construction rank. | The perk screen; applying effect values to dig speed, yield, stamina, melee and healing. |
| **Traders** | 2 outposts standing in the world, with stock lists, price multipliers, reputation gates, restock interval and currency. | Trader NPC, shop UI, buy/sell, restock, reputation. |
| **Quests** | 3 contracts (fetch 20 scrap, clear 12 shamblers, survive 2 nights) with XP, reputation and item rewards. | Accept/track/turn-in, the journal, and a mining contract. |
| **Vehicles** | Scrap Buggy: speed, acceleration, climb height, fuel economy, seats, storage, health, parts and a perk-gated recipe. | Driving, fuel burn, seats and storage, collision against edited terrain and foundations. |
| **Field machines** | The whole grid, state machine, growth timing, litre yield and the grain bin. | Tractor, plow, seeder and harvester; working width and a hopper; sowing and harvesting a swath. |
| **Furnace** | Smelting exists as campfire recipes (ore → ingot, sand → glass). | A dedicated furnace deployable and its throughput. |
| **Horde** | Live, and already budgets on base footprint. | Waves that actively exploit an open dig rather than pathing at the cupboard. |

---

## Phase 2 — not started

Steel tools and the armored tier in play (the **data** chain exists on every piece, with
`upgradesTo` links, per-tier health and resistance, and perk gates). Traps on dug
killboxes. Deeper ore, more POIs, guns, a second vehicle. Map and compass. Mild repair
tension.

---

## Deliberately not implemented

- Multiplayer, dedicated server, netcode.
- HDRP or ray tracing — the project is URP.
- Every Rust monument or the full 7DTD perk encyclopedia.
- Generated art libraries. Every material is a procedural grime texture plus a tint and
  every prop is built from boxes at runtime — readable at 50–100 m, deliberately
  placeholder.

## Known limitations and shortcuts

- **Not play-tested in the Unity editor.** Scripts compile clean against Unity
  reference assemblies and 268 headless checks (`Tests/Headless`) execute the content
  wiring, build grid, chunk storage, mesher, terrain, POIs, inventory, crafting and
  save. None of that reaches rendering, physics, the character controller, streaming,
  AI behaviour or the UI. Expect tuning, not rewrites.
- **The hammer upgrades and repairs but does not place.** Pieces are placed from their
  own hotbar items, which is more discoverable than a build menu and avoided shipping
  UI that could not be tested. The brief asks for hammer-place too; that is a Phase 1
  cleanup.
- **Roofs are flat.** No pitched or conical roof pieces.
- **Stairs use stepped box colliders**, not a smooth ramp.
- **No ambient occlusion on terrain meshes.** Corners read flat. Fixing it properly
  needs a custom URP shader with baked vertex AO.
- **No texture atlas.** One sub-mesh and material per block type per chunk.
- **Terrain structural integrity is one rule deep.** Snap pieces collapse when
  undermined; overhanging *terrain* does not fall.
- **POIs are block stamps**, not authored prefabs — no interiors, loot containers or
  trader NPCs yet.
- **Field crops have no visuals.** Plowing shows as tilled soil, but a sown or growing
  field cell looks the same as a plowed one. Rendering crop cover across thousands of
  cells needs an instanced mesh pass, which belongs with the tractor in Phase 1.
- **No water or fertiliser gameplay.** The cell fields exist and fertility does fall
  with each crop, but nothing adds it back yet.
- **Farm snap pieces are limited to the fence.** Barn, shed, pen and greenhouse frame
  are Phase 1; the grain bin ships as a deployable rather than a snap piece.
- **The seed bag is just the seed stack.** No dedicated seeding container.
- **Perk points accrue but cannot be spent** until the Phase 1 perk screen exists.
- **XP farming guard is session-only.** Blocks you placed are remembered in memory and
  pay no XP when re-mined, but the set is not saved. Crafted building blocks pay no
  harvest XP at all, which covers the common case.
- **Destroying a full crate loses its contents.** They do not spill.
- **Developer hotkeys ship enabled.** Untick *Developer Tools* on the `MadVoxel` object
  before a release build.
- **Audio.** There is none.

---

## Success test (Phase 0)

> Place farm plots → plant → wait stages → harvest food → cook → build shack beside the
> garden → horde breaks at least one plot or plant → reload with remaining plots and
> growth intact.

Every step has an implementation behind it. The test itself needs a Unity editor.

## Next milestone

1. **Field machines** — tractor, plow and seeder or harvester; fuel, hitch, working
   width and a hopper that tips litres into the grain bin.
2. **Perk screen** — spend points, apply Mining, Construction and Farming effects.
3. **Trader runtime** — NPC in the strongroom, shop UI, restock, reputation.
4. **Quest flow** — accept, track, turn in; add the mining contract.
5. **Furnace** — a proper smelter, and the iron economy that feeds metal tier.
6. **Vehicle** — drive the buggy over edited terrain without falling through it.
7. **Horde that reads the dig** — prefer an open ramp or an unfinished wall over chewing
   the strongest face.
