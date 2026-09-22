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
| **Obtainability** | Every tool, every placeable and every recipe ingredient can actually be acquired — from a recipe, a trader, a quest reward, a block drop, a crop or the starting kit. |
| **POIs** | Two trader outposts, off spawn and inside the map; no two POIs overlap; pads are genuinely level so foundations fit; layout is deterministic per seed; and the stamp puts real blocks into the chunk. |
| **Coordinates** | Floor division and modulo for negative world coordinates, world↔chunk mapping, and hash distribution across neighbouring chunks. |
| **Chunk storage** | Uniform-chunk compression, materialisation on first write, index packing over all 4096 cells, compaction, and the copy the save writer takes. |
| **Greedy mesher** | Coplanar faces merge, hidden faces are culled, fully buried and fully empty chunks emit nothing, different block types do not merge, normals face outward, triangle winding agrees with the normals, and index/normal/UV buffers stay in lockstep. |
| **Terrain** | Same seed rebuilds the same chunk; different seeds differ. Surface stays in range and has relief. Trees, scrap, coal, iron, caves, grass and bedrock all actually generate, and ore and caves stay under sanity thresholds. |
| **Inventory** | Stacking and top-up, overflow into new slots, rejection when full, removal across slot boundaries, tool durability and breakage, and transfer between containers without losing items when the destination fills. |
| **Crafting** | Ingredient checks, consumption, station gating (hand vs campfire vs workbench), skill gating, and refusal when the output has nowhere to go. |
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
