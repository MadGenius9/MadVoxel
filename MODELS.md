# Dropping real models in

Every prop in this game is built out of boxes at runtime, because the project could not
ship art. That was always temporary. This is how you replace it — one prop at a time,
with nothing breaking in between.

## The rule

Put a prefab at:

```
Assets/MadVoxel/Resources/MadVoxel/Models/<id>.prefab
```

where `<id>` is the definition's string id **with the namespace stripped**:

| Definition | Model id |
| --- | --- |
| `madvoxel:zombie_shambler` | `zombie_shambler` |
| `madvoxel:zombie_brute` | `zombie_brute` |
| `madvoxel:storage_box` | `storage_box` |
| `madvoxel:vehicle_tractor` | `vehicle_tractor` |
| `madvoxel:implement_plow` | `implement_plow` |
| `madvoxel:piece_wall_armored` | `piece_wall_armored` |
| — the colony worker — | `colonist` |

That is the whole setup. Nothing to register, nothing to recompile. `ModelCatalogue`
looks for the prefab the first time something spawns; if it is there the model is used
and the box code is skipped, and if it is not, the boxes are built exactly as before.

**Snap pieces are per kind *and* tier**, so `piece_wall_wood` and `piece_wall_armored`
are separate models. That is deliberate — an armoured wall should be a different mesh,
not the same mesh painted darker. If you only make one, make the wood tier: it is what
players look at for the first several hours.

## Orientation and scale

A model is instantiated at the parent's origin with no rotation, which is the same
contract the box builders work to:

- **Origin at the base**, centred in X and Z. A structure's transform sits on the
  **corner** of its cell, and the box builders offset by 0.5 — so if a one-cell prop
  looks shifted by half a metre, that is why, and the fix belongs in the prefab.
- **One Unity unit is one metre**, and one block. A wall piece is 3 m across.
- Put any offset in the prefab, where you can see it, rather than asking for a table of
  magic numbers in code.

## What still comes from code

Colliders, gameplay bounds, claim footprints and placement rules all come from the
definition, never from the model. That is on purpose: **gameplay must not change
depending on whether art is installed.** A crate you can open is a crate you can open
whether it is a Synty mesh or six boxes.

The same goes for saves. Nothing about a model is written to disk.

## Zombies specifically

A real zombie model brings its own rig and its own animation, so the hand-written limb
swing is skipped entirely — `ZombieVisuals.Build` returns no limbs and `Animate` does
nothing. Drive the model with an `Animator` on the prefab.

The hit flash still works: it walks every `Renderer` under the body and drives them
through a `MaterialPropertyBlock`, which does not care where the mesh came from.

## Buying

- **[Synty](https://syntystore.com)** — POLYGON packs. Internally consistent across the
  whole library, which matters more than fidelity: mismatched art is what makes a game
  look amateur. **Apocalypse** and **Farm** are the two this game wants. A subscription
  unlocks the library for less than two packs cost outright — check whether the licence
  survives cancellation before committing.
- **[Mixamo](https://mixamo.com)** — free rigged humanoids and animations. The cheapest
  way to stop the zombies being boxes.
- **[ambientCG](https://ambientcg.com)** and **[Poly Haven](https://polyhaven.com)** —
  CC0 PBR materials, if the look goes realistic instead.

## If you go stylised

Flat-shaded low-poly wants different material settings than the current ones: no grime
texture, low smoothness, no metallic variation, and the baked vertex ambient occlusion
in `ChunkMesher` and `SurfaceNets` doing the work that textures would otherwise do.
That retune is a small change to `MaterialLibrary` and has not been made — say the word.
