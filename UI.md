# Claim Slate

MadVoxel's UI system. A weathered farm-command overlay: scratched visor in the world,
grease-pencil clipboard in the menus. Not a phone app, not cartoon candy squares, not
holographic glass.

Everything is built from untextured rects at runtime, so the project still carries no
sprite, prefab or material assets. Square corners come for free.

---

## Palette

| Token | Hex | Used for |
| --- | --- | --- |
| Oil black | `#0C0B0A` | Every backdrop, plate and track |
| Bone | `#E6E0D6` | Type, ticks, healthy readings |
| Oxide rust | `#B85A32` | Structure: rules, trunks, the selected slot, a waiting perk point |
| Sodium gold | `#D4A017` | Day, clock, fuel, XP, stamina, mining progress |
| Crop sage | `#6B7A4A` | Fields, plots, a valid snap, an engaged implement |
| Blood | `#8B1E1E` | **Reserved.** Critical health and the blood moon. Nothing else. |

Derived tones (`Slate`, `Metal`, `BoneDim`, `Pencil`, `Rivet`…) all come from these six
and live in one place: `Assets/MadVoxel/Scripts/UI/ClaimSlate.Palette.cs`. No screen
invents its own grey.

The six hexes are asserted by the headless checks, so a tweak in one screen cannot drift
the whole look.

---

## The four rules

**Mode-aware.** On foot the visor is nearly empty. A snap piece in hand adds the build
plate. A blood moon adds the rim and the threat ticks. The tractor cluster adds gauges.
`HudView.Mode` decides; each layer only exists while the thing it describes does.

**Corners only.** The centre of the screen is the world. The compass is a thin tape at
the top edge, the toolbelt a strip at the bottom, vitals bottom-left, toasts
bottom-right. The only thing allowed near the crosshair is two lines of look-at text.

**Numbers beat icon soup.** The compass reads `DAY 12   17:41`, not a sun glyph. A plot
reads `POTATO   STAGE 3/4   6H`. A snap piece reads `WALL   STONE   84%` and `STABLE`.

**Colour is never the only signal.** Every gauge carries a shape tick — a `+` for health,
stacked bars for stamina, a drop for water, a grain for food. The compass pips are a gold
diamond (trader), a rust square (claim), a sage bar (bed) and a blood tick (threat):
four shapes, not four hues. Perk ranks are filled or empty boxes. A work order that
cannot be crafted says `SHORT`, not just a dimmer colour.

---

## On-foot HUD

- **Top centre** — a compass tape: bearing ticks every 15°, cardinal letters, a rust
  notch for dead ahead, and `DAY 12   17:41` stamped inside it. A sodium dot appears
  beside the clock after dark. **There is no minimap**, on foot or anywhere else.
- **Bottom centre** — the toolbelt. Quiet metal slots, all the same shade; the selected
  one is marked by a rust underline rather than a glow, because ten lit squares is icon
  soup. The held item is named above it.
- **Bottom left** — health and stamina as short bars. **Food and water stay hidden until
  they drop below 35%**, and turn rust below 12%. Above them, `LV 4` and a two-pixel gold
  XP sliver; a rust pip appears when a perk point is waiting.
- **Bottom right** — the toast feed, fading out.
- **Under the crosshair** — one line of what you are looking at, and one of detail.

## Context layers

| Layer | Appears when | Shows |
| --- | --- | --- |
| **Building** | A snap piece is in hand | Right-hand plate: piece and tier, `SNAP OK` in sage or the refusal in rust, and the hammer verbs. The ghost itself stays in the world. |
| **Claim range** | Building, within 6 m of a claim edge | A faint rust ring on the dirt, raycast onto the real surface so it sinks into a trench instead of floating over it. |
| **Garden look-at** | Crosshair on a farm plot | `POTATO   STAGE 3/4   6H`, or `POTATO   READY` with `[E] HARVEST`. |
| **Build look-at** | Crosshair on a snap piece | `WALL   STONE   84%`, then `STABLE` or `UNSUPPORTED`, then the upgrade cost against what you carry. |
| **Fields on foot** | — | Nothing. The field grid gets no overlay until there is a machine on it. |
| **Horde** | Blood moon active | Four oxidised edges (never a full-screen wash), blood threat ticks on the compass, and **one line**: `BLOOD MOON  00:18`. No title card, no skull. |
| **Tractor** | Phase 1 | Speed, fuel in litres, hopper in litres, implement lamp. Built and wired; `IsDriving` is never true yet. |

## Menus

- **Inventory** — bag grid top-left, the container you opened top-right, and crafting as
  a **work-order strip** across the bottom: one stamped ticket per recipe with its
  materials as `4/6 PLANK`, and a rust `CRAFT` stamp that reads `SHORT` when you are.
  Both panels stay open while you queue, so you can craft out of a crate. No paper-doll:
  nothing in MadVoxel is equipped to a body slot, so a mannequin would be a picture of a
  system that does not exist.
- **Perks** — a shop-manual wiring diagram. A rust trunk runs down the category, each
  perk hangs off it on a lead, and the junction is **welded** (filled) once you own a
  rank or **penciled** (an empty outline) until then. Ranks repeat the reading as filled
  or empty boxes. The buy button reads `WELD RANK 3  (1 PT)`, or says why not.
- **Title, pause, death** — riveted slate plates, stencil type, a rust rule under each
  heading. Death is one of the two places blood is allowed.

## Not built yet

**Trader (ticket + price board)** and **silo (vertical tank + litres)** are specified but
not built. Neither system has a runtime behind it — there is no trader NPC to talk to and
no silo to fill — and a screen with nothing behind it is dead UI that rots before its
system arrives. They land with their systems in Phase 1. The tractor cluster is the
exception, and only because it was asked for explicitly: it exists, disabled, so the
mode it belongs to is not an empty branch.

**The toolbelt is nine slots, not ten.** `PlayerInventory` is 9 hotbar + 27 bag = 36, the
bag grid is 4 × 9, the save file stores 36 slots and the keys are `1`–`9`. Going to ten
means 10 + 30 = 40, a new bag grid, and a save migration — a change to a working system
rather than a coat of paint, so it is yours to call.

---

## Files

    UI/ClaimSlate.Palette.cs   the six colours and the pure decisions made from them
    UI/ClaimSlate.cs           plates, frames, rivets, stencil type, gauges, belt slots
    UI/CompassMath.cs          bearings and tape placement, testable without a canvas
    UI/CompassStrip.cs         the tape itself: ticks, cardinals, pips, day and clock
    UI/HudView.cs              the visor, and which layer each mode is allowed
    UI/TractorPanel.cs         the Phase 1 cluster, built and switched off
    UI/InventoryScreen.cs      bag, container, work-order strip
    UI/PerkScreen.cs           the wiring diagram
    UI/MenuScreens.cs          title, pause, death
    UI/UIKit.cs                the older builders, repointed at the Claim Slate palette
    Building/ClaimRangeRing.cs the claim edge, drawn on the dirt
    Horde/BloodMoonClock.cs    when the moon falls and how the one line reads

## What the headless checks cover

The palette hexes, the health-colour thresholds, `Dim`/`Fade`, compass bearings in all
four directions (including that height never tilts one), tape placement and the refusal
to pin an off-arc pip to the edge, and the whole blood-moon schedule: which day is next,
when the window opens and closes across midnight, and the exact countdown string.

What they cannot cover is anything that needs the engine: whether the layout holds at
other aspect ratios, whether the compass reads at a glance while moving, whether the rim
is too strong. That needs an editor and a person.
