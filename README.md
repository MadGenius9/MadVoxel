# MadVoxel

A single-player survival sandbox on an infinite voxel planet, built in **Unity 6 (URP)**.
Minecraft's mine/build loop, 7 Days to Die's skills, traders, quests and blood-moon
horde nights, Rust's raidable crafted bases.

**Status: Phase 0 complete and playable.** See [`SCOPE.md`](SCOPE.md) for what is
implemented, what is data-only, and what is deliberately deferred.
See [`DESIGN.md`](DESIGN.md) for the core loop.

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
| Left mouse (hold) | Mine a block, salvage with a wrench, or attack |
| Right mouse | Place the held block or piece; eat or drink a consumable |
| `E` | Interact — open a door, a crate, a workbench, a campfire, a bedroll |
| `R` | Rotate the piece about to be placed |
| `1`–`9` / scroll | Select hotbar slot |
| `Tab` / `I` | Inventory and crafting |
| `Esc` | Pause (this is the only thing that stops the world) |
| `F3` | Debug overlay — FPS, position, chunk, streaming state, seed |

The inventory deliberately does **not** pause the game, so crafting during a horde
night is still a risk.

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
    world.json          seed, elapsed hours, blood-moon counter, timestamps
    player.json         position, vitals, all 36 inventory slots, level, XP, skills
    structures.json     every placed snap piece, door states, crate contents
    chunks/c.<x>.<y>.<z>.mvc   edited chunks only, palette + run-length encoded
```

| Platform | `<persistentDataPath>` |
| --- | --- |
| Windows | `%USERPROFILE%\AppData\LocalLow\<Company>\MadVoxel` |
| macOS | `~/Library/Application Support/<Company>/MadVoxel` |
| Linux | `~/.config/unity3d/<Company>/MadVoxel` |

Only chunks you have actually changed are written to disk. Everything else is
regenerated from the seed, which is what keeps an infinite world off your drive.
The world autosaves every two minutes, on death, on pause-menu *Save now*, and on quit.

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
    Core/Player/     first-person motor, look, vitals, inventory, mine/place/interact
    World/Voxel/     chunks, terrain generation, greedy mesher, streaming, raycast
    Building/        snap pieces, structure world, land claims, build ghost
    Inventory/       items, stacks, containers, recipes, crafting
    Skills/          XP and levelling, skill tree definitions
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
- **Stable ids on disk.** Chunk files store a palette of block *string* ids, so
  reordering the block registry cannot corrupt an existing save. Unknown blocks
  degrade to air rather than shifting every other block.
- **Multiplayer is not implemented** and no netcode exists, but nothing here assumes
  a single local player: the world, inventory and save layers are plain C# with no
  static player reference.

## Verification

The voxel mesher and terrain generator are covered by headless tests that run outside
Unity (see the commit history for the harness). They check that greedy meshing merges
coplanar faces, culls hidden ones, emits outward-facing normals with matching winding,
and that terrain is deterministic per seed and actually produces surface, caves, ore,
trees and scrap. Everything else has been compile-verified against Unity reference
assemblies but **has not been play-tested in the editor** — see `SCOPE.md`.
