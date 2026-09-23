# MadVoxel — working notes for Claude

Unity 6 (URP) single-player survival sandbox. Rust's snap building, 7 Days to Die's
diggable terrain and stations, FS-style farming, a small colony. Product string is
**MadGenius**.

Read `SYSTEMS.md` first if you are touching power, water, weather, spoilage, claim
heat, the colony or biomes — it explains why each is shaped the way it is, not just
what it does. `UI.md` is the Claim Slate design contract. `SCOPE.md` lists what is
deliberately not built and what is known-broken.

---

## The single most important fact

**Almost none of this has ever been rendered.** ~20,000 lines of C# were written
without a Unity editor available. Everything was verified two ways instead:

1. **Compile check** against real Unity reference assemblies (2021.3 — the newest on
   NuGet, while the project targets Unity 6, so API drift between them is a real gap).
2. **972 headless checks** that run the *actual* gameplay sources against an executable
   `UnityEngine` shim in `Tests/Headless/`.

Those caught eight genuine bugs a compile could not see. They cannot tell you whether
anything looks right, feels right, or is fun.

**So: if the user reports something visual, spatial, or feel-related, believe them over
the tests.** The tests are strong on logic and silent on everything else.

---

## Verify your work

Always, before saying anything is done:

```bash
cd Tests/Headless && dotnet run      # 972 checks, exit 0 when clean
```

If .NET is missing, `dotnet` is a free install and worth it — this suite is the only
fast feedback loop that exists for this project.

The tests are not decoration. Several exist specifically to catch classes of mistake
that already happened once:

- **Obtainability** — every tool, placeable and ingredient must be reachable from a
  recipe, trader, quest, drop, crop or the starting kit. A bulk edit once silently
  deleted four recipes and every other check still passed.
- **Effect wiring** — every perk effect type the content grants must be either applied
  in play or on an explicit deferred list. Stops perks shipping as numbers that do
  nothing.
- **Build connection symmetry** — the stability flood fill walks both directions, so an
  asymmetric graph silently collapses roofs.

If you add a system, add the check that would have caught you getting it wrong.

---

## Architecture, in one screen

- **No god manager.** `GameSession` is a composition root that builds systems in a
  strict order and hands them to each other. Ordering bugs there are real — one was
  found where the colony was handed a spawner that did not exist yet.
- **No prefabs, no .mat assets, no art.** Every material, mesh, prop and UI element is
  built in code at runtime. The scene contains exactly one GameObject. This is
  deliberate: nothing can drift out of sync with the scripts, and there is no scene
  merge conflict.
- **Content is ScriptableObjects built in code** (`Content/ContentLibrary*.cs`), with
  an editor menu item to write them out as real `.asset` files.
- **Mods are drop-in JSON folders** with a hand-rolled parser and hand-written field
  mappings (IL2CPP-safe). See `MODDING.md`.
- **Saves key on block *string* ids**, so reordering or modding content cannot corrupt a
  world. Chunk files are palette + RLE.

### Layout

    Core/            bootstrap, session, clock, player rig, materials, primitives
    World/Terrain/   chunked voxels, generation, greedy mesher, streaming, POIs
    World/Biomes/    five regions; the paint is pure and never saved
    World/Weather/   six states that turn dials on other systems
    World/Fields/    FS-style tillage grid, and the meshed crop cover over it
    Vehicles/        tractor, implements, swath sweep, hopper accounting
    Building/        snap grid, stability, deployables, claim ring
    Farming/         garden plots and crops
    Power/ Fluid/    the grid and the plumbing, one graph each
    Colony/          Mad Colony: charter, morale, people, jobs
    Traders/         the counters, prices, reputation and restocking
    Quests/          contracts: derived progress where possible, tallies where not
    Claim/           claim heat
    Inventory/       items, stacks, crafting, spoilage
    Perks/           XP, tree, effect resolution, buy rules
    UI/              Claim Slate
    Save/            JSON for the world, binary RLE for chunks

---

## Conventions that matter

- **C# 9, Unity-flavoured.** No LINQ in hot paths. No allocation in `Update` — the
  graphs and the mesher reuse scratch lists deliberately.
- **Pure logic is split out so it can be tested.** `BloodMoonClock`, `CompassMath`,
  `ClaimHeatMath`, `ColonyCharter`, `ColonyMorale`, `SpoilRules`, `PerkService`,
  `BiomeMap`, `WeatherSchedule`, and both graphs are engine-free on purpose. If you
  write new logic worth testing, put it somewhere the headless project can compile.
  `ClaimSlate` is `partial` for exactly this reason — the palette half has no uGUI
  dependency.
- **Comments explain why, not what.** Match the surrounding density. The existing
  comments carry real reasoning (why a brown-out feeds near devices first, why growth
  is derived from a planting hour rather than ticked); keep that bar.
- **Derived state over ticked state** wherever it survives a save better. Crop growth
  is `now - plantedAt`, not an accumulator. Frost "setting a plot back" moves the
  planting hour rather than storing a stage.

---

## Running it

1. `MadVoxel → Setup → Configure Project` (input handling, shaders, build settings).
   Each step is fail-soft and names its manual fallback; none are needed to press Play.
2. Open `Assets/MadVoxel/Scenes/MadVoxel.unity`.
3. Tick **Quick Start** on the `MadVoxel` object to skip the title screen.

**A blank Scene view is correct** — one GameObject, everything else built at runtime.

### Developer hotkeys (on by default)

`F1` utility kit (whole grid + plumbing) · `F2` +1 level · `F4` fly · `F5` +1h ·
`F6` dawn · `F7` blood moon · `F8` spawn zombie · `F9` refill · `F10` test kit ·
`F11` invulnerable · `F12` ripen crops
`Shift+F5` cycle weather · `Shift+F6` found colony + recruit · `Shift+F7` +25 heat

### Player keys worth knowing

`E` interact / mount / dismount · `F` lower or raise the implement · `G` hitch or
unhitch · `V` load the drill from your hand, or empty a harvester — into a trader's
counter within 14 m, else a grain bin within 8 m · shift-click trades ten at a counter

### Editor log (when the user cannot paste a stack trace)

- Windows: `%LOCALAPPDATA%\Unity\Editor\Editor.log`
- macOS: `~/Library/Logs/Unity/Editor.log`

---

## Known-risky surfaces

- **`Scripts/Editor/*.cs`** are checked against a hand-written `UnityEditor` stub, not
  real assemblies. Weakest guarantee in the project.
- **Builds strip shaders.** Every material is made via `Shader.Find` at runtime, so a
  build needs URP Lit/Unlit in *Always Included Shaders* or everything goes magenta.
  `Configure Project` handles it; verify if a build looks wrong.
- **Every UI number is a guess** at a 1920×1080 canvas. Nobody has seen it lay out.
- **The interaction probe finds things by what they have, not what they are.**
  `PlayerInteraction.Probe` walks a fixed order — build piece, structure, chunk,
  damageable, interactable — and a new object that matches none of the early branches is
  simply invisible to the crosshair. The trader counter shipped unreachable for exactly
  this reason: it has no health, so every branch above it walked past. If you add
  something the player should be able to use, check it has a branch.
- **Colonists do not pathfind.** They walk at a target and let the character controller
  handle terrain. Fine on dug ground and ramps; they will wedge on a wall corner.
- **No machine has ever been driven.** The swath, the hopper and the tillage cycle are
  covered by tests; how a `CharacterController` tractor actually climbs a dug field, and
  whether the seat camera is anywhere sensible, are pure guesses.

---

## House rules

- Do not touch the sibling **Madfall** repo (an Unreal project). Different game.
- Commit messages: what changed and *why*, wrapped at ~78 columns. Look at the log.
- Do not claim something works because it compiles. Say what was verified and how.
