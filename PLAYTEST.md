# What to try first

None of what follows has ever been rendered, and none of it has ever been *heard*. It
compiles, and 1,396 headless checks say the logic is right, but the tests are silent on
whether anything looks right, sits at a sensible height, is reachable with the camera
where it is, or sounds like the thing it is meant to be. If something looks or sounds
wrong, it probably is — believe your eyes and ears over this file.

Ordered so each step is reachable from the one before. `Shift+F6`-style developer hotkeys
are listed in `CLAUDE.md` and will skip most of the grind.

---

## 0. Combat, first (5 minutes)

**Start here.** A previous playtest could not hit zombies at all, and everything below
assumes you can survive long enough to reach it.

1. Find a shambler in daylight. Swing at it with the starting **stone axe**.
2. Then let one get right up against you and swing again.
3. Kill it. Seven axe blows, give or take.

**Expected:** every swing whooshes whether or not it connects. A landed hit flashes the
body red, staggers and shoves it, pops a hit marker on the crosshair, floats a damage
number, and thuds. The kill marker is rust-coloured and bigger. Taking a bite darkens
the screen edges red and grunts; under a third health the edges stay lit and breathe.

**Watch for:**
- **A swing that does nothing at all.** This was the bug. The swing is now a 60° cone
  out to 3.2 m rather than a pinpoint ray, so aiming at a shoulder should land.
- Hitting a zombie *through* a wall or a closed door — it should be impossible.
- Mining a block when you meant to hit a zombie standing in front of it, or the reverse:
  a body wins only when it is genuinely nearer than what the crosshair found.
- Punching your own colonists or your own walls. The sweep should never find either.
- Swinging a **hammer** at a damaged wall with a zombie beside you — it must still
  repair, not punch.
- "Too winded to swing" after sprinting. That is correct now; it used to fail silently.
- Audio that machine-guns, cuts itself off, or plays a hit from the wrong direction.

---

## 1. The field, by hand (5 minutes)

The oldest part of this and the best sanity check that nothing else broke.

1. Craft a **hoe**, break some ground. The block should turn to tilled soil.
2. Plant a seed in a **farm plot**. `F12` ripens crops if you do not want to wait.

**Watch for:** the plot's plant mesh appearing and growing.

---

## 2. Crop cover — the new rendering (2 minutes)

This is the newest and least certain thing in the build.

1. Hoe several field cells in a row, then sow them (a seeder, or `F12` after sowing).
2. Stand back and look at them.

**Expected:** crossed-quad plants, one per cell, jittered so it does not read as a grid,
growing in eight visible steps and turning gold when ripe.

**Watch for:**
- Plants floating above or sunk into the ground — the base should sit on the block top.
- Z-fighting or flicker where two patches meet.
- A frame-rate drop when a large field is on screen. Patches are 16 m and rebuild at most
  two per frame, but nobody has measured it.
- Plants that never appear: the cover only draws within 96 m of you.

---

## 2b. The bow (5 minutes)

Craftable by hand from the start: 8 plank + 12 fibre, then 4 stone arrows for 2 plank,
2 stone, 2 fibre.

1. Hold the primary button to draw — a bar fills under the crosshair.
2. Release at full draw at something far away, then try a snapped shot.

**Expected:** a full draw flies flat and takes two arrows to kill a shambler. A snap shot
drops fast and hits for a third. Landed arrows stay about twenty seconds and roughly half
can be walked over and picked up.

**Watch for:**
- Arrows passing through a wall you dug — the sweep is meant to stop that.
- The arrow model's orientation in flight: it should nose over as it falls.
- Whether the draw bar's `FULL DRAW` lines up with the shot actually feeling full.

---

## 3. The furnace (5 minutes)

1. Workbench → **Furnace** (40 stone, 20 clay, 4 iron ingots — the ingots come from a
   campfire, as before).
2. Place it, put iron ore and wood or coal in the same slots, close it.
3. Walk away, wait, come back. `F5` skips an hour.

**Expected:** ingots, without you standing there. The look-at line says what it is doing
and why it stopped (`OUT OF FUEL`, `FULL - NOWHERE TO PUT IT`).

**Watch for:** the orange glow only while it is actually working, and the chimney not
buried in the ground.

---

## 4. The tractor (10 minutes)

The biggest new thing, and the one most likely to be physically wrong.

1. Salvage scrap heaps until you get **2 Salvaged Engines** (roughly one heap in fourteen).
2. Workbench → **4 Wheels** (12 scrap + 4 cloth each) → **Tractor Kit**.
3. Set it down, `E` to get on.

**Expected keys:** `WASD` drives, `E` gets off, `F` lowers or raises the implement,
`G` hitches what you are holding, `V` loads a drill or empties a harvester.

**Watch for — this is the list I would check first:**
- **Does it fall through the terrain?** It is a `CharacterController`, not a rigidbody.
- **Does it climb a dug ramp?** Step height is 1.05 m.
- **Where is the camera?** The seat anchor is a guess: `(0, 0.4, -4.5)` behind the seat,
  pitched 12°. If you are inside the engine block, that is why.
- Does it turn on the spot at a sane rate? Steering authority scales with speed.
- Does the exhaust stack tell you which way it is pointing from behind?

---

## 5. Implements and a real field (15 minutes)

1. Agronomist rank 2 → **Disc Plow**. Hitch it with `G`, drop it with `F`, drive.
2. Rank 3 → **Cultivator**. Same field, second pass.
3. Rank 4 → **Seed Drill**. Load seed with `V` holding a field seed. Sow.
4. Rank 5 → **Harvester**. `F12` to ripen, then cut.

**Expected:** a solid worked rectangle behind you — no stripes, no gaps. The swath is
swept along where you travelled, not sampled under the machine, so driving fast should not
skip rows. **If you see stripes, that is a real bug and the tests were wrong.**

**Watch for:**
- The implement mesh floating, clipping the tractor, or facing backwards.
- The gauge cluster appearing on mount and vanishing on dismount, with the on-foot
  crosshair gone while driving.
- The drill refusing to sow when out of seed rather than sowing bare ground.

---

## 6. Selling it (10 minutes)

1. Follow a gold diamond on the compass to a **trader outpost**.
2. `E` at the stall. Two tabs: **GOODS** and **CONTRACTS**.
3. Sell something. Take a contract. Park a loaded harvester within 14 m and press `V`.

While driving, `Tab` opens the machine's bed beside your bag — twelve slots on the
tractor. Load it before you set off and you can sell a real trip's worth.

**Expected:** the haul sells by the litre. A grain bin within 8 m takes it instead when
there is no counter nearby.

**Watch for:**
- The stall being interactable at all — it shipped unreachable once already because it has
  no health and the interaction probe was only looking for things that do.
- Contract lines in the top right of the HUD, going sage when one is ready.
- The price board's `SHORT` / `LOCKED` wording rather than just a greyed button.

---

## 6b. A blood moon, and whether your wall matters (10 minutes)

`F7` starts one. Craft a bow first — see step 2b.

1. Build a wall or dig a trench around your cupboard, and deliberately leave one gap.
2. Start a blood moon and watch where they come from.

**Expected:** most of the wave funnels through the gap rather than spreading evenly, and
the blood moon line reads `BREACHED` while an unchewed way in exists. Seal the gap and
they should switch to the thinnest part of the wall instead of the nearest.

**Watch for:**
- Zombies stopping *at* the gap instead of coming through it. They take the lane first
  and the base second; if they mill around on the ring, that handoff is broken.
- The lane sampling reading a dug trench as a wall. It probes at chest height precisely so
  a trench you can drop into does not count — if they refuse to enter a trench, that check
  is measuring the wrong height.

---

## 6c. Compost, and whether an acre keeps paying (10 minutes)

Fertility used to fall forever with no way back. It should now be a loop.

1. Let some food spoil into **rot** (or `F12`-ripen and just leave produce in the bag).
2. Craft **compost** — rot + fibre, or fibre + dirt if you have eaten everything.
3. Right-click a *plowed* field cell with compost in hand.
4. Then craft a **muck spreader** (workbench, no perk needed), hitch it, load it with
   compost on `V`, and drive a pass. One compost covers six cells by machine against one
   by hand.
5. Unhitch it while still loaded — the muck should come back out as whole compost.

While you are in the field: **plowed and cultivated ground should now look different.**
Cultivated is the lighter, drier one. Do a strip of each side by side — if you cannot
tell them apart at ten metres the tints need widening.

**Expected:** "Muck spread", one compost consumed. On wild ground it should say to break
it first; on ground that is already rich, that it will not take any more.

**Watch for:** harvesting the same strip four or five times and seeing the litres fall,
then recovering after muck. A strip left in stubble for a fortnight and then plowed
should also come back better than one turned straight round. The numbers say one dose a crop holds a field indefinitely;
nobody has farmed it.

---

## 7. The grain bin (2 minutes)

1. Tip a harvest into one, then `E` on it.
2. **DRAW** turns litres back into sacks.

**Expected:** `1,840 L = 460 GRAIN`, and drawing takes exactly the litres those sacks are
worth. Hand-carried produce sells for more than a tipped hopper — that trade-off is the
point of the screen.

---

## If the whole screen is magenta

No render pipeline asset is assigned, so URP shaders are running under the built-in
pipeline. **Assets → Create → Rendering → URP Asset (with Universal Renderer)**, then set
it as **Default Render Pipeline** in Project Settings → Graphics (and in Quality). The
game is playable without it — it falls back to the Standard shader — but it will not look
the way it is meant to.

---

## Destroyed crates

Blow up or mine a **full crate** and check a sack appears where it was with your things
in it. Contents used to be deleted outright. Same for a furnace mid-smelt.

---

## Known to be guesses

- **Every UI number.** All layout is a considered guess at 1920×1080. Nobody has seen any
  of these screens draw.
- **The seat camera**, as above.
- **Cover mesh density.** Two crossed quads per sown cell. A 40×40 field is 3,200 quads in
  one patch group; that should be nothing, but it is unmeasured.
- **Trader stall placement.** It stands 3.5 m south of the outpost centre on the flattened
  pad. If it is inside a wall, that offset is why.
