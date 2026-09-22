# The four layers

Biomes, electricity and water, weather, and the colony. Each one was layered onto what
already existed — terrain, plots, building, inventory, Claim Slate — and none of it
replaced anything.

The thread running through all four: **every number is a dial on a system that was
already there.** There is no weather simulation, no colony AI planner and no fluid
dynamics. There is a state, a multiplier, and something old that reads it.

---

## Biomes

Five regions painted on the finite map. Not infinite worlds, not fantasy realms.

| | Ground | Good for | Bad for |
| --- | --- | --- | --- |
| **Farmland** | Deep soil, bare tilled patches | Every crop, a shallow well | Ore — scarce and thin |
| **Pine Scrub** | Thin soil over stone | Timber, forage, hiding a claim | Ploughing, big fields |
| **Clay Hills** | Clay and cut banks | Scrap, iron, pipe and wire | Crops, without hauled dirt |
| **Dry Flats** | Hardpan and sand | Solar, surface ore | Water, wheat, corn |
| **Frost Shelf** | Gravel over rock | Salvage | Farming, taps, everything |

**How the paint works.** `BiomeMap` is a warped Worley diagram: sites on a jittered
grid, nearest site wins, and the query point is pushed by low-frequency noise first so
borders wander instead of drawing bisectors. Only nine grid cells are ever tested, so
cost does not grow with the map.

Two placements are forced rather than rolled, because they are design:

- **Every site within 340 m of the origin is farmland.** You wake up on good dirt. It is
  not enough to force the origin's own cell — sites are jittered inside their cells, so
  a neighbour's site can land nearer to spawn than the origin cell's own.
- **The frost shelf only exists past 74% of the map extent.** Small, and at the edge,
  where you have to choose to go.

**It is never saved.** The paint is a pure function of the seed, so recomputing it on
load is cheaper than storing it and cannot desync. A headless check proves the same seed
repaints the same map.

**What it touches:** block palette and soil depth, ore density and depth band, tree,
forage and scrap density, crop yield, plot growth speed, field moisture drain, pump rate,
solar output, the claim heat floor, the water table depth, and which weather rolls. The
two trader outposts spiral outward to land in different regions — one farmland, one rust
belt — and fall back gracefully on a seed that has neither in range.

---

## Electricity

7 Days to Die's stations, Rust's sockets. No AND gates, no memory cells, no timers.

    generator / solar  →  [relay]  →  switch / splitter  →  consumers
                       ↘  battery bank  ↗

**Reach comes from relays, not from wire.** Every device has a short maximum wire length
and a relay's is 30 m. That is the only way to cover ground — you cannot run a kilometre
of free cable to a distant pump.

**A brown-out feeds the near devices first.** When demand beats supply the grid walks
outward from the source and pays for devices until the budget runs out. The lamp beside
the generator stays lit; the pump at the end of the line is what dies. It browns out the
same way twice, so you can reason about what to unplug.

**Batteries are the night shift.** They take the surplus and only push when the grid is
short, limited by both their discharge rating and what their stored energy covers for the
tick. A full bank with a small inverter still cannot run a big draw. Fuel empty plus dark
plus a flat bank is a dead grid, and every consumer knows it.

**Traps idle cheap.** A blade trap sips 2 W armed and costs 40 W only while swinging, so
a small generator can arm a fence it could never run flat out — and a trap adds nothing to
claim heat until something walks into it.

A generator burns fuel proportional to load with a floor, so running one lamp does not
drink like running the farm.

### The wire tool

One tool does both graphs. Click a device to pick up a line, click the next to join them,
right-click to drop the line or cut everything leaving a socket. Which graph it talks to
is decided by what is under the crosshair — a line started on the grid will not close on
a tap. Rings are refused: they buy nothing without logic gates and make the brown-out walk
ambiguous.

---

## Water

Rust's fluid chain, cut to the five pieces a farm needs.

    pump  →  pipe  →  tank  →  tap / sprinkler

**A well is a dig, not a recipe.** A new *water table* block generates in a band whose
depth the biome shifts. A pump wants saturated ground within six blocks below it, so a
well in the bottomland is an afternoon and one on the dry flats is a project. It is a
solid block, not a fluid — the game has no liquid simulation and does not need one for a
farm that runs on pipes.

**The model is a pool, not a pipe network.** Everything a pump can still reach shares its
tanks. That gives the four behaviours that matter without pretending to model head
pressure nobody can see:

- a tank buffers the night
- a dry or unpowered well starves the tap
- a broken fitting kills everything past it
- a sprinkler competes with a sink for the same litres

**A broken pump still spins.** It still costs you the watts; its water goes on the floor.

**Frost stops a tap** without freezing the line behind it. **Rain fills a barrel**, which
is the off-grid fallback before you have a pump.

---

## Weather

Six states, one at a time, rolled from the region the player is standing in.

| | Does |
| --- | --- |
| **Clear** | Nothing. It says nothing on the clock either. |
| **Overcast** | Halves the panels. |
| **Rain** | Fills barrels, wets the fields, helps the pump. |
| **Drought** | Multi-day. Cuts the pump to 40%, dries the fields, costs an unwatered plot 45% of its harvest. |
| **Storm** | Short. Panels to zero, and one exposed fitting an hour can break. |
| **Frost night** | Freezes taps, halves the pump, takes a stage off an unsheltered plot. |

**Frost only rolls onto a night**, and the state that just ended is pushed to the back of
the queue, so the dry flats cannot serve drought forever.

**Clear says nothing.** The one word rides in the compass beside the day and the time —
`DAY 14   17:41   DROUGHT` — and only when it means something. A readout that announces
the weather every day is wallpaper.

Breakage and frost roll once per whole game hour, so a fast clock cannot shred a base.
Frost sets a plot back by moving its planting hour, which keeps growth derived rather than
ticked: it still survives a save untouched.

---

## Spoilage

Two states: good, or spoiled. No nutrition spreadsheet.

Food carries its shelf life **on the stack**, because a stack moves — harvested into a
bag, tipped into a crate, eventually into a fridge. Splitting keeps the clock and merging
takes the older one, so topping a crate up with fresh corn cannot launder the corn that
has sat a week.

**A fridge with watts is the only thing that slows it** (6×). Without power it is a
cupboard. That is the whole reason the grid matters to a farmer rather than to an
engineer.

Canned food never spoils, which is why it is the starting kit. Raw produce keeps days, a
cooked meal keeps hours, dry grain keeps a fortnight.

---

## Claim heat

One number, 0–100. Not a second map layer, not a minigame.

**Up:** lights after sundown, a running generator, colonists, worked acreage, mature crop
cells, swinging traps, gunshots.
**Down:** the blackout switch, the generator off, dawn, and the Quiet Claim perk.

**It can never fall below a floor** that population and acreage set. A farm with three
colonists and an acre of wheat is never silent — so blacking out buys you less than moving
would, and that is the trade the whole system exists to pose.

Wandering zombies at night and the blood-moon spawn budget both read it. The board says
`HEAT  37  -  LOUD`.

---

## Mad Colony

Three people, at most. Not RimWorld, not a city builder.

**The cupboard is the charter.** To found: a tool cupboard, a bed, a campfire, water, and
food in a box. Each requirement is something a colonist needs on their first day — a
colony founded without them would starve in front of you, which is worse than being told
no. The board shows the checklist, so a refusal tells you what to go and build.

**They are hands, not planners.** A colonist never reasons about the future: they do not
stockpile, do not anticipate a blood moon, and will happily work a field while the stores
run out. The planning is yours, which is what makes the board worth reading.

The brain is an ordered list, and the order is the design: shelter beats everything, then
thirst, then hunger, then sleep, then work. A miserable colonist simply stops at the work
step and stands about — visible, and diagnosable.

**Jobs:** Farm (harvests ready plots into a crate), Guard (stands a post, can die), Repair
(hammers damaged pieces slowly), Cook (runs campfire recipes out of the stores), Idle.

**They eat the oldest good food first**, which is what makes a fridge worth wiring. They
drink through the fluid graph, so unplugging the pump really does make thirst rise.

**Morale** is a per-hour drain with legible causes: hungry, thirsty, no bed, working in
the dark, out in bad weather, the walls came down. Nothing wrong is not neutral — a fed,
watered, rested person cheers up, so a neglected colony is recoverable. Below the sulk
line they down tools; below the leave line they walk. They always stop working before they
walk out, so there is a warning.

**The board is the only colony screen.** No second HUD. A compass pip and a look-at line
(`JULES  FARM  HUNGRY`) are all it gets in your eye.

### The Marker

Not built, as asked. The hook is `ColonyWorld.ForbiddenActions`, an empty set carried on
purpose: one future thing must be off limits to colonists, and retrofitting that into a
finished AI is worse than carrying an empty list.

---

## What the headless checks cover

731 checks, all passing. The new ones:

- **Biomes** — spawn is always farmland; no shelf near spawn but shelf at the edge; all
  five regions appear; the same seed repaints the same map; borders smear; the regions
  actually differ from one another; every crop has an opinion and at least one refuses a
  region outright.
- **Weather** — frost never rolls onto a day; a region with zero weight never sees a
  state; the flats drought far more than the scrub; drought rarely follows drought;
  durations stay in range; clear adds no word to the clock.
- **Power** — wire range and the relay that beats it; loops, sockets and full outputs
  refused with wording; fuel, solar day/night, storm to zero; the whole brown-out ladder
  including that it is stable across ticks; batteries charging, discharging, running dry,
  and being rate-limited; traps arming cheap and starving a small generator.
- **Fluid** — an unpowered or dry well delivers nothing; drought scales it; a tank caps;
  a tap on a stopped pump is dry even with litres behind it; a tankless line still pours;
  a broken pipe cuts the tank and the tap; a broken pump leaks its whole output; frost
  freezes and thaws; a sprinkler with no water waters nothing.
- **Spoilage** — a powered fridge slows the clock by exactly its rating and an unplugged
  one does not; splitting and merging cannot launder a stack; everything perishable turns
  into something; raw keeps longer than cooked and grain longest.
- **Heat** — the floor, that Quiet Claim shaves it but cannot silence a farm, that a
  blackout is felt within the hour, that it settles at the floor and caps at 100, that
  dawn pulls harder, that an idle trap is silent.
- **Colony** — every founding refusal and its wording, cupboard checked first, supplies in
  days, morale rising when settled and falling when not, thirst worse than hunger, the
  slide to downing tools taking hours not minutes, and that they always sulk before they
  leave.

What they cannot cover is anything that needs the engine: whether a colonist walks into a
wall, whether the wire ghost reads at range, how a storm looks, or whether any of it is
any fun. That needs an editor and a person.
