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
four shapes, not four hues, plus a bone upright for the colony board — the only pip taller than it is
wide and not blood-coloured, so the colony never reads as a threat. Perk ranks are
filled or empty boxes. A work order that cannot be crafted says `SHORT`, not just a
dimmer colour. The board's founding checklist uses `[X]` and `[ ]`, not green and red.

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
| **Horde** | Blood moon active | Four oxidised edges (never a full-screen wash), blood threat ticks on the compass, and **one line**: `BLOOD MOON  00:18`, gaining `BREACHED` when the wave found a way in that needs no chewing. One word on a line that already exists, not a second indicator — the compass pips already say which way. No title card, no skull. |
| **Utilities look-at** | Crosshair on a device or fitting | The device answers for itself: `GEN BANK  31/100 W  FUEL 7L`, `RELAY  30M`, `PUMP  NEEDS 18 W`, `TANK  412 / 800 L`, `TAP  FROZEN`. Rust when it is broken, frozen or unpowered. |
| **Wiring** | Wire tool in hand with a line started | `RUNNING A LINE   REACH 30M   RMB TO DROP`, which takes the look-at line because it is the only thing you are thinking about. |
| **Trader counter** | `E` at a stall | Two tabs, GOODS and CONTRACTS, sharing the same two columns. |
| **Trader — goods** | The GOODS tab | A ticket saying who you are to them (`TRADER VANCE`, `REGULAR`, `140 REP TO NEXT`), your tokens as one big number, their board on the left and what they will take off you on the right. Every line is a name, a count and a price — no grid of pictures. A locked line keeps its place with `TRUSTED` where the price would be; an unaffordable one reads `SHORT` in rust, so the word carries the signal and the colour only confirms it. |
| **Trader — contracts** | The CONTRACTS tab | Their board on the left, what you are carrying on the right, `2 / 3` in the heading. A contract you cannot take keeps its place with the reason in place of the button — `LVL 6`, `TRUSTED`, `FULL` — for the same reason locked stock does. One issued by the other trader reads `ELSEWHERE` rather than hiding, so you know where to walk. |
| **Colony board — recruiting** | `E` at the board | One button. While someone is at the fence it reads `TAKE IN MARA` with `TURN THEM AWAY` beneath it; the rest of the time it is not a greyed-out button but the reason nobody is coming — `NO SPARE BED`, `NOT ENOUGH FOOD PUT BY`, `WORD TRAVELS SLOWLY - 2 DAYS`. The thing the player can act on, in the place they would look for it. |
| **Grain bin** | `E` at a bin | Litres as one big number with a sage fill gauge, then a line per crop: `WHEAT   1,840 L   =   460 GRAIN`, and a DRAW button. The footer names the trade-off it exists to pose — hand-carried produce sells for more than a tipped hopper. An empty bin says how to fill it instead of showing nothing. |
| **Bow draw** | Holding a bow's primary | A bar under the crosshair, filling as you draw: rust while the shot is not worth the arrow, gold as it builds, sage at `FULL DRAW`. The word carries the state and the colour confirms it, as everywhere else. It is the only place in the game where a fraction of a second changes the outcome, which is why it gets a bar rather than a line of text. |
| **Contracts** | Carrying any | Up to three lines under the horde line: `FIRST HARVEST   7 / 12 POTATO`. A finished one reads `READY TO HAND IN` in sage — the only cue that it is worth the walk back. |
| **Colonist look-at** | Crosshair on a person | `JULES  FARM  HUNGRY`, and `NO BED ASSIGNED` under it when that is why. |
| **Tractor** | Sitting on a machine | Speed as one big number, fuel in litres, hopper in litres, and an implement lamp with a word beside it: `SEED DRILL  WORKING`, `HARVESTER  HOPPER FULL`, `DISC PLOW  RAISED`. Colour and shape together, as everywhere else. The on-foot crosshair and look-at readout go away entirely — the player's own interaction is switched off while their hands are on the wheel, so leaving either up would show a frozen target over the gauges. |

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
- **Colony Board** — the only colony screen there is. Name the colony, read the two
  numbers that decide whether anyone stays (`FOOD 4.2d   WATER 1.8d`, rust when short),
  the claim's noise (`HEAT 37 - LOUD`), and a roster row per person with a job button
  that cycles. Unfounded, it shows the checklist — `[X] CUPBOARD  [ ] BED  [X] WATER` —
  so a refusal tells you what to go and build.
- **Title, pause, death** — riveted slate plates, stencil type, a rust rule under each
  heading. Death is one of the two places blood is allowed.

## Not built yet

**There is no quest journal**, deliberately. Three contracts fit on three lines under the
horde line, and the board itself is a tab on the trader screen. A filing cabinet for a
postcard.

The tractor cluster was built ahead of its system, and only because it was asked for
explicitly. It paid off: when the machines landed it needed no HUD surgery and no second
layout pass.

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
    UI/TractorPanel.cs         the gauge cluster, fed by VehicleWorld while driving
    UI/TraderScreen.cs         the ticket, the price board, and what they will buy
    UI/SiloScreen.cs           what is in the grain bin, and drawing it back out
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
