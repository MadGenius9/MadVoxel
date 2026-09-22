# MadVoxel — the loop

One page. Rust's building and look, 7 Days to Die's dirt, perks and hordes, and two
scales of farming.

```
   forage / mine ──► place plots ──► eat from the garden ──► flatten acreage
        ▲                                                          │
        │                                                          ▼
   replant ◄── horde wrecks a row ◄── armour the farm ◄── perk ◄── sell bulk ◄── tractor
```

Every arrow costs daylight. That is the game.

**Garden feeds you tonight. Fields fund the steel walls next month.**

---

## Dig and mine

The ground is a block volume, not a heightmap. Dirt, clay, stone, ore veins, bedrock
at the bottom. Anything but bedrock comes out with the right tool, and tool tier is
the gate: a stone pick scratches rock, iron is quick, steel is later. Dirt and sand
go fast, stone slower, ore slower still, concrete and metal slowest.

So you do not just *find* a base site. You **cut** one. Trench the approach, drop the
pad two metres below grade, leave one ramp in, hollow a room under the hill, sink a
shaft to the iron. Stamina drains and tools wear, so a big dig is a real commitment,
not a creative-mode doodle.

## Perk

XP comes from harvesting, mining, crafting, building, kills and contracts — never
from placing a block you just mined. Points go into Construction, Mining, Scavenging,
Combat, Fortitude and Vehicles. Perks unlock tool tiers, building tiers, yield and
carry weight. Mining and Construction are the two that change how the base gets built.

## Shape the killbox

The dig *is* the defence. A pit they fall into. A single ramp that funnels them past
your firing slot. A moat with no lip to grab. An overhang they have to chew through
from below. In single-player nothing else makes the horde interesting — a flat square
of walls is just a health bar.

## Rust-build and upgrade

On top of the shape you carved, snap pieces: foundations on the flattened pad, walls,
doorways with doors, window walls, half walls, floors, stairs, roofs, ladders, hatches.
They snap to each other and sit on the terrain you cut.

Everything goes up as **twig** — near-free, flimsy, there to lay out the shape. Then
the **hammer** upgrades in place: twig → wood → stone → metal → armored, each tier
tougher and costlier, metal and armored gated behind Construction. Upgrade the wall the
horde actually hits; leave the back of the base in wood.

Nothing floats. A piece stands only if it chains back to a foundation resting on solid
ground — so mining out the dirt under someone's wall brings it down, including yours.

Plant the **tool cupboard**: it is your claim, and it is what the horde walks toward.

## Farm

### The garden, beside the shack

Clear wild yucca and grain out of the old fields and they hand you seeds. Craft plots
from planks and fibre, drop them on dirt in a dip you dug beside the shack, fence it,
and plant.

Crops grow on the calendar, not on your attention: a potato is ready in a day and a
half whether you watched it or were down a mine. Pick it and the bed goes bare, usually
with seed back. Corn takes longer, stands taller, and keeps producing once it is in.

Then cook. Raw potato is a snack; baked potato, corn bread and stew are the only food
that gives stamina back as well as hunger — which is exactly what you need the evening
before a blood moon, and exactly what you lose when a shambler walks through the row.

### The field, out on the flat

The garden does not scale. When you want money rather than dinner, you take the pad you
flattened and keep flattening: plow, cultivate, sow, harvest in swaths, and measure the
result in **litres** into a grain bin rather than stacks into a bag.

Same crops where it makes sense — corn and wheat grow in both — so the second scale
teaches itself.

*(Phase 1: the tillage cycle, growth timing and yield are implemented; the tractor and
implements that work a swath are next. A hoe already breaks one cell by hand.)*

## Trade and quest

Two walled outposts out on the map, concrete perimeter and an iron strongroom, findable
by the bearing you get on waking. They buy scrap and sell what you cannot make yet.
Their board runs fetch, clear, deliver, mine-X-ore and survive-the-next-horde.

*(Phase 1: the outposts stand in the world today; stock, prices, reputation and the
contract flow land next.)*

## Horde night

Blood moon on the calendar. The budget scales with your level and the size of what you
have built, so the better your base the harder they come for it. They path to the
cupboard, and they respect what you dug: they walk your ramp, fall into your pit, and
where they cannot path they chew — wall, door, foundation or dirt, whichever is in
front of them. No flying, no phasing.

A bad dig or an under-upgraded wall gets breached. Plots are soft and they are outside
the strongest wall, so a bad night costs you the garden before it costs you the shack.
That is the feedback.

## Repair and expand

The world persists. Come dawn you hammer the walls back up, re-cut the bit they came
through, push the pit out another few metres, and spend what you earned.

Then it happens again, bigger.

---

## Why the pieces need each other

7DTD digging without Rust pieces is a mud hut. Rust pieces without digging is a flat box
on flat ground. Together: **you carve the shape, then armour it**.

Farming is why you stay. Without it the loop is a treadmill of looting cans; with it,
the pad you flattened for a base is also the pad you plow, the dip you dug is also the
garden, and the horde has something to take away that you actually made.

And the horde is the reason to do all of it.
