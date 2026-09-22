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
                case StructureKind.Silo:
                    BuildSilo(body.transform, mat);
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
