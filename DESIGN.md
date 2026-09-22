# MadVoxel — the loop

One page. Everything in the game exists to feed this cycle.

---

## Wake

You come round on a ruined farm at 07:00 on day 1 with a stone axe, two cans, two
bottles of water and a handful of fibre. The planet is infinite and generated from
your seed: pine scrub on the hills, tilled dirt in the hollows, rusted heaps of scrap
poking out of the fields, coal and iron in the rock below, caves under all of it.

The clock is the antagonist. A day is twenty real minutes. **Night falls at 20:00**,
and on **every seventh day a blood moon** brings a horde that walks straight at
whatever you have built.

## Mine and build

Chop pine for logs, punch and pick stone, break scrap heaps for metal. Everything you
break is a voxel and everything you place is a voxel, so the world is the building
material. Better tools mine faster and unlock harder blocks: bare hands barely scratch
stone, a stone pickaxe opens coal and iron, iron tools make it quick.

Craft in your hands for the basics, at a **campfire** to smelt ore and glass, at a
**workbench** for doors, crates, the claim stake and iron tools. Build a shelter you
can close: walls of frames or cobble, a **door** that swings, a **ladder** to a roof,
a **storage box** for what you cannot carry. Plant a **land claim stake** and the
ground around it becomes yours — wandering dead leave it alone, and the horde knows
exactly where it is.

Harvesting, killing and crafting all pay XP. Putting back a block you just mined pays
nothing.

## Trade and contract

Trader outposts sit out on the grid, stocked with food, water, bandages, ingots, tools
and, eventually, engine parts. They buy what you scavenge and sell what you cannot yet
make. Their board carries contracts: **bring me twenty scrap**, **thin the herd by
twelve**, **keep your claim and your skin for two nights**. Reputation opens the
better shelves.

*(Phase 1: the outposts, stock, prices and three contracts are authored data today;
the shop and quest-tracking UI land next.)*

## Prepare for the horde

The game tells you the blood moon is coming at dawn on the day it falls. That morning
is the real gameplay: patch the walls the last raid chewed, upgrade wood to cobble to
iron, dig a moat, funnel the approach, stock the crate, cook, fill bottles, sleep at
the bedroll to set your spawn.

At 22:00 the moon goes red. Waves spawn out in the dark and walk at your claim. They
scale with your level and with how big your base has grown — the more you build, the
harder they come for it. They climb, they path, and when they cannot path they chew
through the wall, block by block, until either the wall or the night gives out. At
04:00 it ends and the survivors burn off.

Die and you keep your hotbar; the rest of your bag stays in a backpack where you fell.
The world does not reset. Your base, your crates and your claim are exactly where you
left them.

## Expand

Spend the skill points. **Mining** for speed and yield, **Construction** for the
cobble → iron → steel chain, **Combat** for damage and stamina, **Scavenging** for
loot, **Medicine** to patch yourself up, **Vehicles** to get a buggy running.

Push further out for better rock, deeper caves and richer wrecks. Every trip out is a
bet against the clock: how far can you get and still be behind a door by 20:00.

## Vehicle out

Once you can build the **Scrap Buggy** the map opens. Fuel it, load the storage crate,
run a hauling loop out to the far scrap fields and the second trader, and get home
before the moon turns.

*(Phase 1: the buggy, its fuel economy and its craft recipe are authored data; driving
lands next.)*

## Repeat, harder

Bigger base, bigger horde. Better tools, deeper mine. More reputation, better stock.
Further out, more to lose on the way home.

---

## The pressure diagram

```
        harvest ──────────► craft ──────────► build
           ▲                   │                 │
           │                   ▼                 ▼
        explore ◄──── trade / quests        land claim
           ▲                                     │
           │                                     ▼
           └──────────── survive ◄──────── horde night
                            │
                            ▼
                       XP ─► skills ─► better tools, better blocks
```

Every arrow costs daylight. That is the game.
