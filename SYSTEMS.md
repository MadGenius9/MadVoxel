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

## Field machines

The FS-style half of farming: a tractor and four implements that work the field grid in
bulk. Everything they do goes through `FieldWorld`, the same operations the hoe calls one
cell at a time — the tractor is a faster hand, not a second rule set.

**The tractor** (`VehicleRig`) is a `CharacterController`, not a `Rigidbody`, for the same
reason the player is: the ground is voxels the player has been digging, and a controller
climbs a dug ramp predictably where a physics body catches on a step and flips. It is slow,
heavy, turns on the spot only sluggishly, and burns fuel whenever it is moving. Getting on
disables `PlayerMotor` and `PlayerInteraction` — hands on the wheel — and the rig reads its
own keys from there. It is placed from a crafted kit and can be driven away, which makes it
the only placed thing in the game that is not saved against a cell.

**The implements** (`ImplementController`) hitch to the drawbar from your hand with `G`,
lower and raise with `F`, and load or tip with `V`. An unhitched implement is an item in a
bag, not an object in the world, which is the same rule every other deployable follows.
One operation each — plough, cultivator, seed drill, harvester. A combine that ploughed,
sowed and cut in one pass would collapse the whole tillage cycle into a single button.

Three decisions are doing the real work, and all three are invisible until it is too late:

1. **Where it worked.** `FieldSwath` sweeps the cells along the segment travelled since the
   last tick rather than sampling under the machine this frame. Sampling stripes a field at
   speed or at low frame rates, and the player finds out two in-game days later when the
   crop comes up in rows. The sweep is capped at 12 m so a lag spike or a teleport cannot
   ask for a million cells.
2. **What it could pay for.** `ImplementWork` decides how many of those cells the hopper
   covers *before* a single one is touched. A cell is worked only if it can be paid for in
   full, so a drill running out of seed leaves bare ground rather than ground that looks
   sown and comes up empty.
3. **When to stop.** A harvester that fills mid-swath stops there and says so. What will
   not fit is spilled and reported, never quietly pocketed.

The seed drill holds one crop at a time. Mixing would need a per-crop store on the machine
and a way to show it; the grain bin behind you is the thing that holds more than one crop.
A harvester that meets a different crop stops rather than mixing. Tipping is `V` within 8 m
of a bin, and the perk `Agronomist` scales what the field returns — the field harvest only,
not the hand one.

Fuel is written per in-game hour on the implement and converted to a burn rate with the
clock's day length, because a day's work is the unit a farmer reasons in. `Economiser`
divides that burn rather than multiplying it: better economy is less fuel.

Saved state is the machine's position, fuel and health, plus the implement hitched to it
and what is in its hopper. It comes back raised, deliberately — a machine that resumed
mid-furrow would plough the line between where it was saved and wherever it settles.

### Seeing it

A worked field used to be invisible: plowing turned the block to tilled soil, but a sown
cell, a growing cell and a ripe cell all looked identical. Once a tractor could sow an
acre in a minute that was the weakest thing in the loop, so `FieldCoverView` draws the
standing crop.

It is meshed, not built from objects. A garden plot makes five boxes per plot and destroys
them on every change; the same approach over ten thousand cells would be tens of thousands
of transforms. Instead: one mesh per 16 m patch, one sub-mesh per crop-and-ripeness, two
crossed quads per plant, and both faces of each quad drawn with an upward normal — URP's
Lit shader is single-sided, and a plant lit from above reads far better than one whose
back is black.

Two decisions make it cheap enough to leave running:

- **Bucketed height.** Growth is derived from the clock, so a plant's true height changes
  every frame, and meshing that would rebuild every patch of every field forever. Height
  is quantised into eight steps, so a patch settles until a plant crosses into the next
  one — which is also the point at which the change is visible.
- **Signatures, not timers.** A patch is remeshed only when a number derived from what is
  in it moves. Sowing, ripening, harvesting and digging the ground out from under a crop
  all move it; a frame passing does not.

Ripeness is read from the hours since sowing rather than the stored cell state, because
the state only advances when something touches the cell. A field therefore goes visibly
gold on time whether or not anyone walked past it.

---

## Traders

Two outposts have stood in the map since Phase 0, placed on farmland and clay hills
before anything else could take the ground. They were buildings with nobody in them.
Now each has a counter.

It is a stall, not a person. A trader NPC would need pathing, a schedule, a greeting
animation and somewhere to sleep, and none of that changes what trading is here: a
counter, a price board, and what is in your bag.

**The spread is the whole system.** The moment a trader will pay more for something than
they charge for it, the game is a button you hold down, and nothing on screen would tell
you. So the economy is pure C# and a headless check walks every line of every trader at
every reputation tier — 95 combinations — asserting the margin stays positive, then plays
out a real buy-and-sell-back at the top tier where it is thinnest. A trader also refuses
to buy their own tokens back, which would otherwise leak value on every round trip.

Every transaction is written the same way: work out the whole trade, refuse it outright
if any part fails, then move everything. There is no partial trade, and no path that
takes payment without handing over goods. A bag with no room refuses the sale rather than
taking the money — which is the one way a trade can silently cost you something for
nothing.

**Reputation** rises faster from buying than selling, and unlocks stock tiers. Locked
lines stay on the board with the tier that opens them printed where the price would be:
a shop that hides its stock until you qualify gives you no reason to qualify.

**Restocking is derived**, like crop growth. Whole elapsed cycles are credited and the
remainder kept, so a trader you visit every hour still restocks — reset the stored hour on
every visit and it never would — and one left alone for three days restocks by exactly the
same amount.

**Bulk produce** is what makes the field layer pay. Park a loaded harvester at the counter
and `V` sells the haul by the litre instead of tipping it into a bin. Litres are priced
through the crop's harvest item at a wholesale discount, using the *unrounded* per-item
value: routing them through the rounded price flattened corn and grain onto the same
number and made which crop you sowed an acre of stop mattering.

A seed drill's hopper also holds litres — of seed — so the counter refuses anything but a
harvester's load. Without that, buying seed and tipping it straight back is a laundry.

---

## Contracts

Four contracts have been written since Phase 0 and there was no way to take one. The
trader counter is where they live now: a second tab on the same screen, because a counter
is one conversation with two halves rather than two screens.

**Progress splits two ways, and which side a contract falls on is the only real decision
in the system.**

- **Derived.** A fetch contract's progress is however many of the item are in your bag
  right now. A survive-nights contract's is how many day boundaries have passed since you
  took it. Neither is stored, so neither can drift, be double-counted, or come back wrong
  from a save — and spending the goods takes the progress back with them, where a stored
  counter would have said "done" over an empty bag.
- **Counted.** Kills and litres delivered leave no standing record to read, so they are
  tallied on the entry as they happen, capped at what the contract asked for.

Everything that *can* be derived is, for the same reason crop growth is: a number the game
recomputes cannot disagree with the world, and a number it accumulates eventually will.

Handing one in is the same shape as a trade: work the whole thing out, refuse outright if
any part fails, then move everything. Whether the rewards fit is answered by playing the
hand-in out on a copy of your bag rather than by counting slots — the arithmetic version
was wrong in both directions, and a contract that takes twenty potatoes and pays in tokens
has to count the space those potatoes free. A full bag refuses the hand-in and leaves the
contract live so you can come back.

A contract is handed in to the trader who issued it. The other outpost lists it as
`ELSEWHERE` rather than hiding it, so you know where to walk.

Three at a time, tracked as three short lines under the horde line. A contract you cannot
see is one you forget you took, and a journal screen for three jobs would be a filing
cabinet for a postcard.

---

## What the headless checks cover

972 checks, all passing. The new ones:

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
- **Contracts** — that a fetch contract's progress follows the bag both ways; that a
  contract taken at dusk credits a night by morning but one taken at dawn does not credit
  one by dusk; that a tally caps at the objective and is dropped with the contract rather
  than banked; that a contract naming a zombie ignores the others; that a full bag refuses
  a hand-in instead of dropping the reward, leaving the contract live; that the goods you
  hand over make room for what you are paid; and that every shipped contract is on some
  trader's board and asks for a crop you can actually sow.
- **Traders** — that the spread never closes across all 95 line-and-tier combinations and
  that a buy-and-sell-back round trip always leaves you poorer; that a refused trade moves
  nothing at all, in either direction; that a trader will not buy their own tokens; that
  money does not buy what reputation gates; that bulk takes a wholesale cut but still
  prices two crops differently; that a seed item's worth of litres is worth less than the
  seed; and that a trader visited hourly and a trader left alone for three days restock
  identically.
- **Field cover** — that patches tile correctly through negative coordinates (naive
  integer division puts the cell west of the origin in the origin patch and overlays two
  fields on the same ground); that a newly sown cell still shows a shoot, so the drill
  gives feedback; that bare ground emits nothing; that both faces of every quad are drawn;
  that a plant stands on the surface rather than in it and its jitter keeps it in its own
  cell; that the same cell always meshes identically after a reload; and the one that
  matters — a crop growing across a bucket boundary remeshes once, not twenty times, and
  exactly eight times over its whole life.
- **Field machines** — that a stationary implement still covers its full width and hangs
  square across the heading; that a fast sweep works contiguous rows rather than a comb;
  that the 12 m cap trims the near end and not the far one; the whole four-pass cycle over
  one strip, checking the worked ground is a solid rectangle, that half a hopper of seed
  leaves the rest plainly bare, that a full harvester reports its spillage, and that every
  cut cell is left as stubble; and that each implement is carried, hitched, sized sanely
  and reachable through a rank of a skill you can actually buy.
- **Colony** — every founding refusal and its wording, cupboard checked first, supplies in
  days, morale rising when settled and falling when not, thirst worse than hunger, the
  slide to downing tools taking hours not minutes, and that they always sulk before they
  leave.

What they cannot cover is anything that needs the engine: whether a colonist walks into a
wall, whether the wire ghost reads at range, how a storm looks, or whether any of it is
any fun. That needs an editor and a person.
