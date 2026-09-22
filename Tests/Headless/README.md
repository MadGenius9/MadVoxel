# Headless checks

Runs MadVoxel's engine-agnostic core **outside Unity**, on any machine with the
.NET SDK. No editor, no GPU, no licence.

```bash
cd Tests/Headless
dotnet run
```

Exit code is 0 when everything passes, 1 otherwise, so it drops straight into CI.

## What this actually is

`UnityShim.cs` provides executable stand-ins for the sliver of `UnityEngine` the
core touches — `Vector3`, `Mathf`, `Color`, `ScriptableObject`, `Debug`,
`Application.persistentDataPath`, the serialisation attributes. The project then
compiles **the real gameplay sources** from `Assets/MadVoxel/Scripts/` against that
shim. These are genuine runs of the shipping code, not a parallel copy of it.

The folder sits outside `Assets/`, so Unity never compiles it and the shim can
never collide with the real engine.

## Covered

| Area | What is checked |
| --- | --- |
| **Content wiring** | The whole `ContentLibrary` is built. Every block, item, recipe, structure, skill, quest, trader and vehicle resolves; no null or dangling cross-reference; ids are unique. Locked recipes are reachable through the skill tree and skill unlocks point at real recipes. Every mineable block drops something, no block demands a tool tier above the best craftable one, and the workbench and campfire are hand-craftable so there is no bootstrap deadlock. |
| **Build grid** | Cell maths including negative coordinates; shared-edge canonicalisation, so the wall between two cells is one slot however it is addressed; and that the connection graph the stability flood fill walks is **symmetric**, self-free, and links floors to the walls under them. |
| **Snap content** | Every piece kind has a complete Twig→Armored chain that climbs in health and resistance, every upgrade costs real materials, perk gates name perks that exist and are reachable, wood and stone stay ungated, and only twig is placeable from an item. |
| **Farming** | Crop content resolves both ways (seed → crop → harvest item), no crop is a dead end, wild plants drop plantable seeds so the garden is reachable without a trader, meals restore stamina and all have recipes, and the plot is hand-craftable and soil-only. Garden growth stages map correctly onto elapsed hours including negatives. The field tillage cycle walks wild → plowed → cultivated → seeded → growing → ready → stubble and back, rejects illegal transitions, yields litres, loses fertility with repeated cropping, and can be driven across a swath. |
| **Mods** | The JSON parser (comments, trailing commas, escapes, and errors that name the line), manifest id rules, load ordering with dependencies and cycles, and the full pipeline: JSON on disk becomes definitions, patches change only named fields, removals remove, cross-mod references resolve in either order, and a broken mod reports without stopping the others. The example mod shipped in this repo is loaded and asserted, so it can never rot. |
| **Obtainability** | Every tool, every placeable and every recipe ingredient can actually be acquired — from a recipe, a trader, a quest reward, a block drop, a crop or the starting kit. |
| **POIs** | Two trader outposts, off spawn and inside the map; no two POIs overlap; pads are genuinely level so foundations fit; layout is deterministic per seed; and the stamp puts real blocks into the chunk. |
| **Coordinates** | Floor division and modulo for negative world coordinates, world↔chunk mapping, and hash distribution across neighbouring chunks. |
| **Chunk storage** | Uniform-chunk compression, materialisation on first write, index packing over all 4096 cells, compaction, and the copy the save writer takes. |
| **Greedy mesher** | Coplanar faces merge, hidden faces are culled, fully buried and fully empty chunks emit nothing, different block types do not merge, normals face outward, triangle winding agrees with the normals, and index/normal/UV buffers stay in lockstep. |
| **Terrain** | Same seed rebuilds the same chunk; different seeds differ. Surface stays in range and has relief. Trees, scrap, coal, iron, caves, grass and bedrock all actually generate, and ore and caves stay under sanity thresholds. |
| **Inventory** | Stacking and top-up, overflow into new slots, rejection when full, removal across slot boundaries, tool durability and breakage, and transfer between containers without losing items when the destination fills. |
| **Crafting** | Ingredient checks, consumption, station gating (hand vs campfire vs workbench), skill gating, and refusal when the output has nowhere to go. |
| **Perks** | Effect sums across ranks and across two perks granting the same type, clamping of an over-max rank from a hand-edited save, and that a runaway negative from a mod cannot invert a multiplier. The buy gates and the order they are reported in (maxed outranks "no points"), multi-point ranks, and prerequisites. Buying a rank spends the points, raises the rank, unlocks exactly the right recipe, and refuses cleanly with none left. Loading re-grants every recipe the saved ranks had earned. Every perk-locked recipe is actually handed over by its perk at a rank it can reach, no perk is a dead button, and every effect type the tree grants is either applied in play or on an explicit list of Phase 1 gaps. |
| **Claim Slate** | The six palette hexes exactly as specified, so a tweak in one screen cannot drift the look; the health-colour thresholds and that blood stays reserved; `Dim` and `Fade`. Compass bearings for all four cardinals and the diagonals, that height never tilts a bearing, that a marker on top of you does not spin, tape placement at the centre and at the arc edge, and that an off-arc pip is dropped rather than pinned to the end of the tape. The whole blood-moon schedule: which day is next before and after the window opens, that the window runs past midnight and closes at the end hour, that day 1 is not the tail of a day-0 horde, and the exact countdown string including its padding and its silence outside the last three hours. |
| **Biomes** | Spawn is always farmland and no frost shelf is near it, but the far edge band is; all five regions appear somewhere; the same seed repaints the same map, which is why the paint is never saved; borders smear rather than stepping; the regions genuinely differ (the flats pump worse, dry faster and see more sun; the rust belt has the scrap and worse dirt; the scrub has the wood and hides a claim); every crop has a per-region opinion and at least one refuses a region outright. |
| **Weather** | All six states authored; frost never rolls onto a day but the shelf gets it at night; a region with zero weight for a state never sees it; the dry flats drought far more than the pine scrub; drought rarely follows drought; every state lasts inside its own range; a drought is multi-day and a storm is not; clear adds no word to the clock and a drought does. |
| **Power** | Wire range and the relay that beats it; loops, missing sockets, full outputs and self-wiring all refused with wording; removing a device clears the wires into it. Fuel burn proportional to load, an empty tank as a dead grid, solar by day and not by night and not in a storm. The whole brown-out ladder: the near lamp stays lit, the far ones go dark, the grid says so, and it browns out the same way on the next tick. Batteries charging from surplus, carrying the night, running flat, and being held to their discharge rating however full they are. Traps arming for a trickle and starving a small generator when they swing. |
| **Fluid** | Hose range, loops and sockets refused with wording. An unpowered pump and a dry well both deliver nothing; drought scales what is left; a tank caps rather than banking a lake. A tap on a stopped pump is dry even with litres in the tank behind it, and a tankless line still pours straight off the pump. A broken pipe cuts the tank and the tap and no water reaches past it; a broken pump leaks its whole output and delivers none of it; repairing brings it back. Frost freezes an outdoor tap without freezing the tank, and thaws. A sprinkler with no water is not watering anything. |
| **Spoilage** | A powered fridge slows the clock by exactly its rating, an unplugged one is just a box, and a bag is a bag. Food that runs out becomes rot and rot does not rot again. Splitting keeps the shelf life and merging takes the older clock, so a stack cannot be laundered. Raw produce keeps longer than a cooked meal and dry grain longest of all. Every perishable in the content table turns into something. |
| **Claim heat** | An empty claim has no floor and a populated farm always does; Quiet Claim shaves it but cannot silence a farm. A lit, running base gets louder within the hour and a blackout is felt in the same hour. Heat settles at the floor rather than zero, caps at a hundred, and dawn pulls harder than an ordinary hour. An armed idle trap is silent and a swinging one is not. |
| **Colony** | Every founding requirement and refusal, with the cupboard checked first because it is the one that explains the system. Supplies in days, including that an empty colony reports a dash rather than a made-up number. Morale rising when settled and falling when not; thirst worse than hunger and named as the dominant complaint; morale clamped at both ends; the slide from content to downing tools taking hours rather than minutes; and that they always sulk before they walk out, so there is a warning. The board is craftable and not perk-gated, and a colonist's appetite is one a garden can keep up with. |
| **Save** | Chunk files round-trip all 4096 voxels, including at negative coordinates. A reopened store finds earlier sessions' chunks. Re-saving replaces rather than appends. Run-length encoding compresses a uniform chunk to tens of bytes and still survives a worst-case pattern with no runs. A block removed from the registry degrades to air instead of shifting every other id. |

## Not covered

Anything that needs the engine: rendering, physics and collision, the character
controller, chunk streaming across threads, AI behaviour, the UI, and the feel of
any of it. Those need a Unity editor and a person.

Bugs caught here that a compile could not see:

- A steel block that demanded a tool tier no craftable tool reached, so a steel
  wall could never be mined back.
- A chunk hash that collided structurally for neighbouring chunks.
- An asymmetric connection graph: ceilings listed the walls under them but walls
  did not list ceilings, and stairs listed the floor above but not the reverse.
  The stability flood fill walks connections in both directions, so roofs and
  upper storeys would have silently collapsed.
- A perk unlocking a recipe id that no longer existed — which turned out to be the
  visible symptom of an edit that had silently deleted the wrench, both iron tool
  and the iron block recipes. Every other check still passed, because the *items*
  were all still there. That is what the obtainability checks now cover.
