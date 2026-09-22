# MadVoxel — scope

What is built, what is data only, and what is deliberately not here yet.

**Mode:** single-player, local saves. No multiplayer, no dedicated server, no netcode,
and none is planned for this build. Systems are kept separable so it could be added
later — the voxel world, inventory, skills and save layers are plain C# with no static
player reference — but nothing here is written against a network model.

---

## Phase 0 — implemented

| # | Requirement | State |
| --- | --- | --- |
| 1 | Infinite voxel world, chunked, greedy meshed, streamed | **Done** |
| 2 | First-person move, look, jump, sprint, mine, place | **Done** |
| 3 | Harvest: terrain voxels and resource nodes drop items | **Done** |
| 4 | Inventory: hotbar + bag, stackables, save/load | **Done** |
| 5 | Crafting bench recipes: tools, blocks, campfire, claim stake, storage | **Done** |
| 6 | Land claim stake with a protected build radius | **Done** |
| 7 | Building: voxel blocks plus door, ladder, box, workbench | **Done** |
| 8 | Day/night cycle | **Done** |
| 9 | Wandering zombie and a scheduled horde night that targets the claim | **Done** |
| 10 | Die, drop bag, respawn, world persists | **Done** |

### Detail

**Voxel world.** 16³ chunks, world height 192 blocks (12 chunk layers), horizontal
streaming radius 6 chunks with a 1-chunk generation apron. Greedy meshing merges
coplanar faces of the same block into single quads, one sub-mesh per block type.
Terrain generation, chunk file reads and meshing run on worker threads; only mesh
upload touches the main thread. Chunks that are uniform keep no voxel array, and a
uniform chunk surrounded by chunks of the same opacity is never meshed at all.

**World content.** One biome band that blends ruined farmland (flat, tilled, sparse)
into pine scrub (hilly, wooded), plus height variation, cave systems, coal and iron
veins, and surface scrap heaps. Deterministic from the seed.

**Resource nodes are voxels.** Trees, scrap heaps and ore veins are blocks in the
world rather than separate entity prefabs. They drop items, award XP and respect tool
tiers exactly like any other block. There is no separate "node" entity type.

**Death rule (one was required, this is the one chosen):** you **keep your hotbar**;
the rest of your bag is dumped into a lootable backpack at the spot you fell. The
backpack persists in the save like any other container.

**Persistence.** Only chunks you have edited are written to disk, palette + run-length
encoded against stable block *string* ids. Player vitals, all 36 inventory slots,
level, XP, skill ranks, unlocked recipes and respawn point are saved, as is every
placed piece with its health, door state and container contents.

---

## Phase 1 — data is real, runtime is next

The brief's optional constraint was to hold Phase 1 code until Phase 0 passes its
success test. That is what has been done: **all Phase 1 content is authored and
loadable, and the hooks it plugs into exist, but the Phase 1 runtime systems are not
written.** Nothing below is an empty interface — the definitions are real
ScriptableObject types with real values behind them.

| Area | Data shipped | Runtime still to write |
| --- | --- | --- |
| **Skills** | 12 skills across all six categories (Mining, Construction, Combat, Scavenging, Medicine, Vehicles), with per-rank effects, level gates and recipe unlocks. XP, levelling and skill-point accrual **are live** and drive horde scaling. | Spending screen; applying the effect values to mining speed, yield, stamina, melee damage and healing. |
| **Traders** | 2 traders (Vance, Mara) with stock lists, per-item price multipliers, reputation gates, restock interval and currency item. | Outpost POI placement, shop UI, buy/sell, restock timer, reputation accrual. |
| **Quests** | 3 contracts: a fetch (20 scrap), a clear (12 shamblers), a survive (2 nights) — with XP, reputation and item rewards. | Accept/track/turn-in flow and the journal. |
| **Vehicles** | Scrap Buggy: speed, acceleration, climb height, fuel capacity and burn rate, fuel item, seats, storage slots, health; plus its parts and a skill-gated craft recipe. | Driving controller, fuel burn, seat and storage interaction, voxel-safe collision. |
| **Horde nights** | **Live.** Calendar blood moon every 7 days (22:00–04:00), waves scaling with player level and claim size, pathing at the claim, breaking voxel walls and snap pieces, a heavier variant from the second horde onward. | Nothing outstanding for Phase 0; Phase 1 adds more variants and smarter approach. |

Recipes already carry `requiredSkillId` / `requiredSkillRank`, and the crafting screen
already filters on unlocked recipes — so wiring skill ranks to unlocks is a data
connection, not a refactor.

---

## Phase 2 — not started

Upgradable building tiers in play (the **data** chain wood frame → cobblestone → iron
→ steel already exists on both blocks and snap pieces, including `upgradesTo` links and
per-tier structure health, but there is no upgrade action). Tool durability and repair
— **durability is implemented and tools break**; repair is not. POIs (shacks, town
fragment, warehouse). Base decay or upkeep. Map, compass, backpack slots.

---

## Deliberately not implemented

- Multiplayer, dedicated server, client prediction — explicitly out of scope.
- HDRP or ray tracing — the project is URP.
- Economy simulation, clans, voice.
- Generated art libraries. Every material is a procedural grime texture plus a tint,
  and every prop is built from boxes at runtime. Readable at 50–100m, deliberately
  placeholder.

## Known limitations and shortcuts

These are real, and worth knowing before the first play session.

- **Not play-tested in the Unity editor.** The runtime and editor scripts compile
  clean against Unity reference assemblies, and 171 headless checks
  (`Tests/Headless`, `dotnet run`) actually execute the content wiring, chunk storage
  and coordinates, the greedy mesher, terrain generation, inventory, crafting and the
  save round-trip. What none of that reaches is anything needing the engine: rendering,
  physics, the character controller, chunk streaming across threads, AI behaviour, the
  UI, and how the whole thing feels. Expect tuning, not rewrites.
- **No ambient occlusion on chunk meshes.** Faces are lit by the directional light
  only. Corners read flatter than they should. A custom URP shader with baked
  per-vertex AO is the fix; it was skipped to avoid shipping an untestable shader.
- **No texture atlas.** Each block type is its own sub-mesh with its own material, so
  a varied chunk issues several draw calls. Fine at this scale, wrong long-term.
- **Structural integrity is one rule deep.** Mining the block under a snap piece drops
  that piece. Voxel structures themselves do not collapse — there is no stress
  propagation.
- **Rotated snap pieces assume a square footprint.** Every piece shipped is 1×N×1, so
  rotation is correct today; a 2×1×3 piece would need the collider work finishing.
- **XP farming guard is session-only.** Blocks you place are remembered in memory and
  award no XP when re-mined, but that set is not saved, so quitting and reloading
  forgets it. Crafted building blocks award no harvest XP at all, which covers the
  common case.
- **Destroying a full crate loses its contents.** They do not spill.
- **Smelting happens at the campfire.** The forge is Phase 2, so iron and glass are
  campfire recipes for now.
- **Audio.** There is none.
- **Developer hotkeys ship enabled.** `Developer Tools` on the `MadVoxel` object is on
  by default so the loop can be tested without a two-hour run-up. Untick it before
  building a release, or the test kit and the blood-moon skip go out with the game.

---

## Success test (Phase 0)

The loop the build is meant to satisfy:

> New world → walk out on infinite voxel terrain → mine and place blocks → claim and
> build a closable shelter with storage → survive the first night → survive a horde
> night against the base → quit → reload into the same world with the same base and
> inventory.

Every step has an implementation behind it. The test itself needs a Unity editor to
run, which this build has not had.

## Next steps after Phase 1

- **More biomes** — desert, burnt forest, wetland, snow line, with per-biome resource
  and threat tables. The generator already routes everything through one blend factor,
  so this is an extension rather than a rewrite.
- **Guns and ammo** — ranged combat, ammo crafting, a Combat skill branch that means
  something at distance.
- **Electricity** — generators, wiring, powered lights, traps and doors; the obvious
  next layer on land claims and horde defence.
- **More vehicles** — motorcycle, truck with a bed, and a repair/upgrade path tied to
  the Vehicles skills.
- **Structural integrity proper** — stress propagation through voxel builds so
  overhangs and undermined bases collapse.
- **Texture atlas and a voxel shader** — one draw call per chunk, vertex AO, wetness
  and damage states.
