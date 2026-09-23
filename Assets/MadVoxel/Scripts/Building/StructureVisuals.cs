using MadVoxel.Core;
using UnityEngine;

namespace MadVoxel.Building
{
    /// <summary>Placeholder art for snap pieces: tinted boxes with readable silhouettes.</summary>
    public static class StructureVisuals
    {
        public const string BodyName = "Body";

        public static void Build(PlacedStructure structure)
        {
            var def = structure.Definition;
            var mat = MaterialLibrary.Get(def.surfaceFamily, def.tint, 0.12f,
                def.surfaceFamily == SurfaceFamily.Metal ? 0.6f : 0f);

            // Pivot sits at the middle of the first cell so a 90 degree turn keeps the
            // piece inside its footprint; the body then uses plain cell-local coordinates.
            var pivot = new GameObject("RotationPivot");
            pivot.transform.SetParent(structure.transform, false);
            pivot.transform.localPosition = new Vector3(0.5f, 0f, 0.5f);
            pivot.transform.localRotation = Quaternion.Euler(0f, structure.RotationSteps * 90f, 0f);

            var body = new GameObject(BodyName);
            body.transform.SetParent(pivot.transform, false);
            body.transform.localPosition = new Vector3(-0.5f, 0f, -0.5f);

            switch (def.kind)
            {
                case StructureKind.Storage:
                    BuildCrate(body.transform, mat);
                    break;
                case StructureKind.CraftStation:
                    BuildBench(body.transform, mat);
                    break;
                case StructureKind.Campfire:
                    BuildCampfire(body.transform, mat);
                    break;
                case StructureKind.ToolCupboard:
                    BuildStake(body.transform, mat);
                    break;
                case StructureKind.Bedroll:
                    BuildBedroll(body.transform, mat);
                    break;
                case StructureKind.FarmPlot:
                    BuildFarmPlot(body.transform, def);
                    break;
                case StructureKind.Furnace:
                    BuildFurnace(body.transform, mat);
                    break;
                case StructureKind.Silo:
                    BuildSilo(body.transform, mat);
                    break;
                case StructureKind.ColonyBoard:
                    BuildBoard(body.transform, mat);
                    break;
                case StructureKind.PowerDevice:
                case StructureKind.FluidDevice:
                    BuildUtility(body.transform, def);
                    break;
                default:
                    PrimitiveBuilder.Box(body.transform, new Vector3(0.5f, 0.5f, 0.5f), Vector3.one * 0.98f, mat, "Block");
                    break;
            }

            AddCollider(structure);
        }

        static void AddCollider(PlacedStructure structure)
        {
            var def = structure.Definition;
            var size = PlacedStructure.RotatedFootprint(def.footprint, structure.RotationSteps);
            size = new Vector3Int(Mathf.Max(1, size.x), Mathf.Max(1, size.y), Mathf.Max(1, size.z));
            var box = structure.gameObject.AddComponent<BoxCollider>();
            box.center = new Vector3(size.x * 0.5f, size.y * 0.5f, size.z * 0.5f);
            box.size = new Vector3(Mathf.Max(0.2f, size.x - 0.04f), Mathf.Max(0.2f, size.y - 0.04f), Mathf.Max(0.2f, size.z - 0.04f));
            // Ladders and bedrolls are walk-through but must still be hittable.
            box.isTrigger = false;
            if (!def.blocksMovement)
            {
                // Thin, walk-through pieces still need a hittable slab for interaction.
                bool alongZ = structure.RotationSteps % 2 == 0;
                if (alongZ)
                {
                    box.size = new Vector3(box.size.x, box.size.y, 0.16f);
                    box.center = new Vector3(box.center.x, box.center.y, 0.09f);
                }
                else
                {
                    box.size = new Vector3(0.16f, box.size.y, box.size.z);
                    box.center = new Vector3(0.09f, box.center.y, box.center.z);
                }
            }
        }



        /// <summary>
        /// A squat stone box with a black mouth and a chimney. The mouth is the point:
        /// it is what tells you this is the thing that eats ore, from across a base.
        /// </summary>
        static void BuildFurnace(Transform parent, Material mat)
        {
            var stone = MaterialLibrary.Get(SurfaceFamily.Stone, new Color(0.38f, 0.36f, 0.34f), 0.05f);
            var iron = MaterialLibrary.Get(SurfaceFamily.Metal, new Color(0.24f, 0.22f, 0.21f), 0.3f, 0.7f);

            PrimitiveBuilder.Box(parent, new Vector3(0.5f, 0.45f, 0.5f), new Vector3(1.0f, 0.9f, 1.0f), stone, "Body");
            PrimitiveBuilder.Box(parent, new Vector3(0.5f, 0.94f, 0.5f), new Vector3(1.06f, 0.1f, 1.06f), stone, "Cap");

            // The mouth, set into the front face and unlit so it reads as a hole.
            PrimitiveBuilder.Box(parent, new Vector3(0.5f, 0.38f, 0.02f), new Vector3(0.52f, 0.42f, 0.12f),
                MaterialLibrary.GetUnlit(new Color(0.08f, 0.05f, 0.04f)), "Mouth");

            PrimitiveBuilder.Cylinder(parent, new Vector3(0.74f, 1.3f, 0.74f), new Vector3(0.22f, 0.4f, 0.22f), iron, "Chimney");
            PrimitiveBuilder.Box(parent, new Vector3(0.5f, 0.06f, 0.5f), new Vector3(1.1f, 0.12f, 1.1f), stone, "Base");
        }

        static void BuildCrate(Transform parent, Material mat)
        {
            PrimitiveBuilder.Box(parent, new Vector3(0.5f, 0.35f, 0.5f), new Vector3(0.9f, 0.7f, 0.9f), mat, "Crate");
            var band = MaterialLibrary.Get(SurfaceFamily.Metal, new Color(0.33f, 0.24f, 0.18f), 0.35f, 0.7f);
            PrimitiveBuilder.Box(parent, new Vector3(0.5f, 0.64f, 0.5f), new Vector3(0.94f, 0.06f, 0.94f), band, "Lid");
            PrimitiveBuilder.Box(parent, new Vector3(0.5f, 0.2f, 0.5f), new Vector3(0.94f, 0.05f, 0.94f), band, "Band");
        }

        static void BuildBench(Transform parent, Material mat)
        {
            PrimitiveBuilder.Box(parent, new Vector3(0.5f, 0.82f, 0.5f), new Vector3(1.0f, 0.1f, 0.9f), mat, "Top");
            var leg = MaterialLibrary.Get(SurfaceFamily.Wood, new Color(0.31f, 0.23f, 0.15f));
            PrimitiveBuilder.Box(parent, new Vector3(0.14f, 0.39f, 0.16f), new Vector3(0.1f, 0.78f, 0.1f), leg, "Leg0");
            PrimitiveBuilder.Box(parent, new Vector3(0.86f, 0.39f, 0.16f), new Vector3(0.1f, 0.78f, 0.1f), leg, "Leg1");
            PrimitiveBuilder.Box(parent, new Vector3(0.14f, 0.39f, 0.84f), new Vector3(0.1f, 0.78f, 0.1f), leg, "Leg2");
            PrimitiveBuilder.Box(parent, new Vector3(0.86f, 0.39f, 0.84f), new Vector3(0.1f, 0.78f, 0.1f), leg, "Leg3");
            var vice = MaterialLibrary.Get(SurfaceFamily.Metal, new Color(0.42f, 0.28f, 0.2f), 0.4f, 0.8f);
            PrimitiveBuilder.Box(parent, new Vector3(0.82f, 0.93f, 0.28f), new Vector3(0.16f, 0.14f, 0.16f), vice, "Vice");
        }

        static void BuildCampfire(Transform parent, Material mat)
        {
            var stone = MaterialLibrary.Get(SurfaceFamily.Stone, new Color(0.35f, 0.34f, 0.33f));
            for (int i = 0; i < 6; i++)
            {
                float a = i / 6f * Mathf.PI * 2f;
                PrimitiveBuilder.Box(parent,
                    new Vector3(0.5f + Mathf.Cos(a) * 0.36f, 0.08f, 0.5f + Mathf.Sin(a) * 0.36f),
                    new Vector3(0.2f, 0.16f, 0.2f), stone, "Ring" + i);
            }
            PrimitiveBuilder.Box(parent, new Vector3(0.5f, 0.12f, 0.5f), new Vector3(0.5f, 0.12f, 0.16f), mat, "LogA");
            PrimitiveBuilder.Box(parent, new Vector3(0.5f, 0.2f, 0.5f), new Vector3(0.16f, 0.12f, 0.5f), mat, "LogB");
        }

        static void BuildStake(Transform parent, Material mat)
        {
            PrimitiveBuilder.Box(parent, new Vector3(0.5f, 0.85f, 0.5f), new Vector3(0.16f, 1.7f, 0.16f), mat, "Post");
            var cloth = MaterialLibrary.Get(SurfaceFamily.Cloth, new Color(0.62f, 0.24f, 0.14f));
            PrimitiveBuilder.Box(parent, new Vector3(0.72f, 1.45f, 0.5f), new Vector3(0.44f, 0.3f, 0.03f), cloth, "Flag");
            var plate = MaterialLibrary.Get(SurfaceFamily.Metal, new Color(0.45f, 0.4f, 0.36f), 0.35f, 0.8f);
            PrimitiveBuilder.Box(parent, new Vector3(0.5f, 1.05f, 0.5f), new Vector3(0.34f, 0.24f, 0.05f), plate, "Plate");
        }

        static void BuildBedroll(Transform parent, Material mat)
        {
            PrimitiveBuilder.Box(parent, new Vector3(0.5f, 0.09f, 0.5f), new Vector3(0.78f, 0.18f, 0.96f), mat, "Roll");
            PrimitiveBuilder.Box(parent, new Vector3(0.5f, 0.2f, 0.16f), new Vector3(0.5f, 0.14f, 0.24f),
                MaterialLibrary.Get(SurfaceFamily.Cloth, new Color(0.5f, 0.46f, 0.4f)), "Pillow");
        }

        /// <summary>A framed soil bed, the way a 7DTD farm plot reads: timber frame, tilled earth.</summary>
        static void BuildFarmPlot(Transform parent, StructureDefinition def)
        {
            var frame = MaterialLibrary.Get(SurfaceFamily.Plank, new Color(0.40f, 0.30f, 0.19f));
            var soil = MaterialLibrary.Get(SurfaceFamily.Dirt, new Color(0.24f, 0.17f, 0.11f));

            PrimitiveBuilder.Box(parent, new Vector3(0.5f, 0.12f, 0.5f), new Vector3(0.9f, 0.24f, 0.9f), soil, "Soil");

            PrimitiveBuilder.Box(parent, new Vector3(0.5f, 0.14f, 0.04f), new Vector3(1.0f, 0.28f, 0.08f), frame, "FrameN");
            PrimitiveBuilder.Box(parent, new Vector3(0.5f, 0.14f, 0.96f), new Vector3(1.0f, 0.28f, 0.08f), frame, "FrameS");
            PrimitiveBuilder.Box(parent, new Vector3(0.04f, 0.14f, 0.5f), new Vector3(0.08f, 0.28f, 1.0f), frame, "FrameW");
            PrimitiveBuilder.Box(parent, new Vector3(0.96f, 0.14f, 0.5f), new Vector3(0.08f, 0.28f, 1.0f), frame, "FrameE");

            // Tilled furrows, so bare soil still reads as worked ground.
            for (int i = 0; i < 3; i++)
            {
                PrimitiveBuilder.Box(parent, new Vector3(0.5f, 0.25f, 0.28f + i * 0.22f),
                    new Vector3(0.82f, 0.04f, 0.06f), soil, "Furrow" + i);
            }
        }

        /// <summary>A corrugated grain bin. Tall enough to be a landmark on a farm.</summary>
        static void BuildSilo(Transform parent, Material mat)
        {
            var metal = MaterialLibrary.Get(SurfaceFamily.Metal, new Color(0.55f, 0.53f, 0.49f), 0.3f, 0.7f);
            var roof = MaterialLibrary.Get(SurfaceFamily.Metal, new Color(0.40f, 0.38f, 0.35f), 0.35f, 0.75f);

            PrimitiveBuilder.Cylinder(parent, new Vector3(0.5f, 1.6f, 0.5f), new Vector3(1.9f, 1.6f, 1.9f), metal, "Bin");
            PrimitiveBuilder.Cylinder(parent, new Vector3(0.5f, 3.3f, 0.5f), new Vector3(1.5f, 0.3f, 1.5f), roof, "Cap");
            PrimitiveBuilder.Box(parent, new Vector3(0.5f, 0.12f, 0.5f), new Vector3(2.1f, 0.24f, 2.1f), roof, "Base");

            // Ladder up the side reads the scale.
            for (int i = 0; i < 8; i++)
            {
                PrimitiveBuilder.Box(parent, new Vector3(1.45f, 0.4f + i * 0.35f, 0.5f), new Vector3(0.3f, 0.05f, 0.05f), roof, "Rung" + i);
            }
        }

        /// <summary>
        /// The utilities. Each one is built from its device rather than its kind, so a
        /// deployable that is two things - the pump, the fridge - gets the silhouette of
        /// whichever one you actually look at it for.
        ///
        /// They read as farm-industrial salvage: a bolted frame, a vent, a gauge face.
        /// Nothing here is a toy cube with a label on it.
        /// </summary>
        static void BuildUtility(Transform parent, StructureDefinition def)
        {
            var frame = MaterialLibrary.Get(SurfaceFamily.Metal, new Color(0.33f, 0.32f, 0.30f), 0.25f, 0.8f);
            var panel = MaterialLibrary.Get(SurfaceFamily.Metal, def.tint, 0.3f, 0.6f);
            var dark = MaterialLibrary.Get(SurfaceFamily.Metal, new Color(0.16f, 0.15f, 0.14f), 0.2f, 0.5f);
            var brass = MaterialLibrary.Get(SurfaceFamily.Metal, new Color(0.62f, 0.50f, 0.26f), 0.5f, 0.9f);

            if (def.powerDevice != null)
            {
                switch (def.powerDevice.kind)
                {
                    case Power.PowerDeviceKind.Generator: BuildGeneratorBank(parent, frame, panel, dark, brass); return;
                    case Power.PowerDeviceKind.BatteryBank: BuildBatteryBank(parent, frame, panel, dark); return;
                    case Power.PowerDeviceKind.SolarBank: BuildSolarBank(parent, frame, panel); return;
                    case Power.PowerDeviceKind.Relay: BuildRelay(parent, frame, brass); return;
                    case Power.PowerDeviceKind.Switch:
                    case Power.PowerDeviceKind.Splitter: BuildBox(parent, frame, brass); return;
                }

                if (def.powerDevice.lightRange > 0f) { BuildLamp(parent, frame, brass); return; }
                if (def.powerDevice.trapDamage > 0f) { BuildTrap(parent, frame, dark, brass); return; }
            }

            if (def.fluidDevice != null)
            {
                switch (def.fluidDevice.kind)
                {
                    case Fluid.FluidDeviceKind.Tank: BuildTank(parent, frame, panel, def); return;
                    case Fluid.FluidDeviceKind.Tap: BuildTap(parent, brass); return;
                    case Fluid.FluidDeviceKind.Sprinkler: BuildSprinkler(parent, frame, brass); return;
                    case Fluid.FluidDeviceKind.Pump: BuildPump(parent, frame, panel, brass); return;
                    default: BuildPipe(parent, frame); return;
                }
            }

            PrimitiveBuilder.Box(parent, new Vector3(0.5f, 0.5f, 0.5f), Vector3.one * 0.9f, panel, "Body");
        }

        /// <summary>A small engine on a skid, with a tank and an exhaust.</summary>
        static void BuildGeneratorBank(Transform parent, Material frame, Material panel, Material dark, Material brass)
        {
            PrimitiveBuilder.Box(parent, new Vector3(1f, 0.12f, 1f), new Vector3(2.0f, 0.24f, 2.0f), frame, "Skid");
            PrimitiveBuilder.Box(parent, new Vector3(1f, 0.85f, 1f), new Vector3(1.7f, 1.2f, 1.5f), panel, "Housing");

            // Louvres down one flank: the detail that makes it read as an engine.
            for (int i = 0; i < 5; i++)
            {
                PrimitiveBuilder.Box(parent, new Vector3(0.14f, 0.62f + i * 0.14f, 1f),
                    new Vector3(0.05f, 0.07f, 1.2f), dark, "Louvre" + i);
            }

            PrimitiveBuilder.Cylinder(parent, new Vector3(1.55f, 1.05f, 0.5f), new Vector3(0.5f, 0.55f, 0.5f), dark, "Tank");
            PrimitiveBuilder.Cylinder(parent, new Vector3(0.45f, 1.85f, 1.5f), new Vector3(0.18f, 0.45f, 0.18f), dark, "Exhaust");
            PrimitiveBuilder.Box(parent, new Vector3(1f, 1.52f, 0.22f), new Vector3(0.5f, 0.3f, 0.06f), brass, "Panel");
        }

        static void BuildBatteryBank(Transform parent, Material frame, Material panel, Material dark)
        {
            PrimitiveBuilder.Box(parent, new Vector3(1f, 0.1f, 1f), new Vector3(2.0f, 0.2f, 1.6f), frame, "Skid");

            // A rack of cells rather than one lump.
            for (int i = 0; i < 4; i++)
            {
                PrimitiveBuilder.Box(parent, new Vector3(0.38f + i * 0.42f, 0.78f, 1f),
                    new Vector3(0.34f, 1.0f, 1.2f), panel, "Cell" + i);
                PrimitiveBuilder.Box(parent, new Vector3(0.38f + i * 0.42f, 1.32f, 1f),
                    new Vector3(0.36f, 0.08f, 1.24f), dark, "Cap" + i);
            }
        }

        static void BuildSolarBank(Transform parent, Material frame, Material panel)
        {
            PrimitiveBuilder.Box(parent, new Vector3(1f, 0.08f, 1f), new Vector3(1.6f, 0.16f, 1.6f), frame, "Foot");
            PrimitiveBuilder.Box(parent, new Vector3(1f, 0.5f, 1f), new Vector3(0.16f, 0.7f, 0.16f), frame, "Post");

            var tilted = new GameObject("Panel");
            tilted.transform.SetParent(parent, false);
            tilted.transform.localPosition = new Vector3(1f, 0.95f, 1f);
            tilted.transform.localRotation = Quaternion.Euler(-28f, 0f, 0f);
            PrimitiveBuilder.Box(tilted.transform, Vector3.zero, new Vector3(2.0f, 0.08f, 1.4f), panel, "Face");
        }

        static void BuildRelay(Transform parent, Material frame, Material brass)
        {
            PrimitiveBuilder.Box(parent, new Vector3(0.5f, 0.06f, 0.5f), new Vector3(0.5f, 0.12f, 0.5f), frame, "Foot");
            PrimitiveBuilder.Box(parent, new Vector3(0.5f, 0.85f, 0.5f), new Vector3(0.12f, 1.6f, 0.12f), frame, "Mast");
            PrimitiveBuilder.Box(parent, new Vector3(0.5f, 1.45f, 0.5f), new Vector3(0.42f, 0.3f, 0.3f), brass, "Head");
            PrimitiveBuilder.Box(parent, new Vector3(0.5f, 1.72f, 0.5f), new Vector3(0.7f, 0.05f, 0.05f), frame, "Crossarm");
        }

        static void BuildBox(Transform parent, Material frame, Material brass)
        {
            PrimitiveBuilder.Box(parent, new Vector3(0.5f, 0.45f, 0.5f), new Vector3(0.44f, 0.6f, 0.28f), frame, "Case");
            PrimitiveBuilder.Box(parent, new Vector3(0.5f, 0.52f, 0.34f), new Vector3(0.16f, 0.2f, 0.06f), brass, "Lever");
        }

        static void BuildLamp(Transform parent, Material frame, Material brass)
        {
            PrimitiveBuilder.Box(parent, new Vector3(0.5f, 0.9f, 0.5f), new Vector3(0.08f, 0.5f, 0.08f), frame, "Stem");
            PrimitiveBuilder.Cylinder(parent, new Vector3(0.5f, 0.62f, 0.5f), new Vector3(0.44f, 0.2f, 0.44f), frame, "Shade");
            PrimitiveBuilder.Box(parent, new Vector3(0.5f, 0.52f, 0.5f), new Vector3(0.22f, 0.08f, 0.22f), brass, "Bulb");
        }

        static void BuildTrap(Transform parent, Material frame, Material dark, Material brass)
        {
            PrimitiveBuilder.Box(parent, new Vector3(0.5f, 0.1f, 0.5f), new Vector3(0.8f, 0.2f, 0.8f), frame, "Base");
            PrimitiveBuilder.Cylinder(parent, new Vector3(0.5f, 0.34f, 0.5f), new Vector3(0.3f, 0.3f, 0.3f), dark, "Motor");

            // Four blades on a hub, so it reads as something that turns.
            for (int i = 0; i < 4; i++)
            {
                float angle = i * 90f * Mathf.Deg2Rad;
                PrimitiveBuilder.Box(parent,
                    new Vector3(0.5f + Mathf.Cos(angle) * 0.3f, 0.5f, 0.5f + Mathf.Sin(angle) * 0.3f),
                    new Vector3(0.5f, 0.05f, 0.12f), brass, "Blade" + i);
            }
        }

        static void BuildTank(Transform parent, Material frame, Material panel, StructureDefinition def)
        {
            float height = Mathf.Max(1.2f, def.footprint.y * 0.9f);

            PrimitiveBuilder.Box(parent, new Vector3(1f, 0.1f, 1f), new Vector3(2.0f, 0.2f, 2.0f), frame, "Base");
            PrimitiveBuilder.Cylinder(parent, new Vector3(1f, 0.2f + height * 0.5f, 1f),
                new Vector3(1.7f, height * 0.5f, 1.7f), panel, "Drum");

            // Bands, so a big drum does not read as a smooth plastic toy.
            for (int i = 0; i < 2; i++)
            {
                PrimitiveBuilder.Cylinder(parent, new Vector3(1f, 0.5f + i * height * 0.5f, 1f),
                    new Vector3(1.78f, 0.05f, 1.78f), frame, "Band" + i);
            }
        }

        static void BuildPump(Transform parent, Material frame, Material panel, Material brass)
        {
            PrimitiveBuilder.Box(parent, new Vector3(0.5f, 0.12f, 0.5f), new Vector3(0.9f, 0.24f, 0.9f), frame, "Pad");
            PrimitiveBuilder.Cylinder(parent, new Vector3(0.5f, 0.5f, 0.5f), new Vector3(0.6f, 0.3f, 0.6f), panel, "Casing");
            PrimitiveBuilder.Cylinder(parent, new Vector3(0.5f, 0.85f, 0.5f), new Vector3(0.26f, 0.22f, 0.26f), frame, "Motor");
            PrimitiveBuilder.Box(parent, new Vector3(0.84f, 0.45f, 0.5f), new Vector3(0.24f, 0.16f, 0.16f), brass, "Outlet");
        }

        static void BuildPipe(Transform parent, Material frame)
        {
            PrimitiveBuilder.Cylinder(parent, new Vector3(0.5f, 0.3f, 0.5f), new Vector3(0.22f, 0.3f, 0.22f), frame, "Riser");
            PrimitiveBuilder.Box(parent, new Vector3(0.5f, 0.58f, 0.5f), new Vector3(0.3f, 0.1f, 0.3f), frame, "Flange");
        }

        static void BuildTap(Transform parent, Material brass)
        {
            PrimitiveBuilder.Box(parent, new Vector3(0.5f, 0.45f, 0.5f), new Vector3(0.12f, 0.9f, 0.12f), brass, "Stand");
            PrimitiveBuilder.Box(parent, new Vector3(0.5f, 0.85f, 0.66f), new Vector3(0.08f, 0.08f, 0.32f), brass, "Spout");
            PrimitiveBuilder.Box(parent, new Vector3(0.5f, 0.95f, 0.5f), new Vector3(0.22f, 0.05f, 0.05f), brass, "Handle");
        }

        static void BuildSprinkler(Transform parent, Material frame, Material brass)
        {
            PrimitiveBuilder.Box(parent, new Vector3(0.5f, 0.08f, 0.5f), new Vector3(0.36f, 0.16f, 0.36f), frame, "Foot");
            PrimitiveBuilder.Box(parent, new Vector3(0.5f, 0.5f, 0.5f), new Vector3(0.1f, 0.8f, 0.1f), frame, "Riser");
            PrimitiveBuilder.Box(parent, new Vector3(0.5f, 0.92f, 0.5f), new Vector3(0.52f, 0.05f, 0.08f), brass, "ArmA");
            PrimitiveBuilder.Box(parent, new Vector3(0.5f, 0.92f, 0.5f), new Vector3(0.08f, 0.05f, 0.52f), brass, "ArmB");
        }

        /// <summary>A plank plaque with a paper on it. It is a charter, not a terminal.</summary>
        static void BuildBoard(Transform parent, Material mat)
        {
            var paper = MaterialLibrary.Get(SurfaceFamily.Cloth, new Color(0.78f, 0.74f, 0.64f), 0.05f, 0f);

            PrimitiveBuilder.Box(parent, new Vector3(0.5f, 0.9f, 0.14f), new Vector3(0.9f, 1.1f, 0.08f), mat, "Board");
            PrimitiveBuilder.Box(parent, new Vector3(0.5f, 0.95f, 0.2f), new Vector3(0.66f, 0.82f, 0.02f), paper, "Charter");
            PrimitiveBuilder.Box(parent, new Vector3(0.5f, 1.44f, 0.16f), new Vector3(0.96f, 0.08f, 0.12f), mat, "Lintel");
        }

        /// <summary>Darkens the piece as it takes damage so players can read base integrity.</summary>
        public static void ShowDamage(PlacedStructure structure)
        {
            float f = Mathf.Lerp(0.35f, 1f, structure.HealthFraction);
            var renderers = structure.GetComponentsInChildren<MeshRenderer>();
            for (int i = 0; i < renderers.Length; i++)
            {
                var block = new MaterialPropertyBlock();
                renderers[i].GetPropertyBlock(block);
                block.SetColor("_BaseColor", structure.Definition.tint * f);
                block.SetColor("_Color", structure.Definition.tint * f);
                renderers[i].SetPropertyBlock(block);
            }
        }
    }
}
