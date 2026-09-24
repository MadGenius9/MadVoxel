using MadVoxel.Core;
using UnityEngine;

namespace MadVoxel.Vehicles
{
    /// <summary>
    /// Placeholder art for machines, built from the same tinted primitives as every
    /// other object in the game.
    ///
    /// The silhouette is doing real work here: big back wheels, small front wheels and
    /// a high seat read as "tractor" from across a field at a glance, which is how the
    /// player finds the thing they parked. Nothing here is a prefab or an imported
    /// mesh - it is all constructed at runtime, so a mod can add a vehicle without
    /// shipping art.
    /// </summary>
    public static class VehicleVisuals
    {
        public static void Build(Transform root, VehicleDefinition def)
        {
            GameObject vehicleModel;
            if (Core.ModelCatalogue.TryBuild(def.stringId, root, out vehicleModel)) return;

            var body = MaterialLibrary.Get(SurfaceFamily.Metal, def.tint, 0.25f, 0.55f);
            var rubber = MaterialLibrary.Get(SurfaceFamily.Metal, new Color(0.10f, 0.09f, 0.09f), 0.1f, 0.1f);
            var glass = MaterialLibrary.Get(SurfaceFamily.Metal, new Color(0.62f, 0.66f, 0.63f), 0.8f, 0.2f);

            Vector3 size = def.chassisSize;

            var hull = new GameObject("Hull");
            hull.transform.SetParent(root, false);

            // Bonnet forward, cab back: the player sits behind the engine, as they would.
            PrimitiveBuilder.Box(hull.transform, new Vector3(0f, size.y * 0.85f, size.z * 0.35f),
                new Vector3(size.x * 0.85f, size.y * 0.9f, size.z * 0.8f), body, "Bonnet");

            PrimitiveBuilder.Box(hull.transform, new Vector3(0f, size.y * 0.7f, -size.z * 0.15f),
                new Vector3(size.x * 1.05f, size.y * 0.6f, size.z * 0.7f), body, "Chassis");

            PrimitiveBuilder.Box(hull.transform, new Vector3(0f, size.y * 1.45f, -size.z * 0.25f),
                new Vector3(size.x * 0.8f, size.y * 0.5f, size.z * 0.4f), body, "Seat");

            // The exhaust stack is the one detail worth keeping: it is what tells you
            // which way the machine is pointing from behind.
            PrimitiveBuilder.Cylinder(hull.transform, new Vector3(size.x * 0.32f, size.y * 2.0f, size.z * 0.55f),
                new Vector3(0.12f, size.y * 1.2f, 0.12f), glass, "Stack");

            // Rear axle carries the work, so the rear wheels are half again as tall.
            float rear = size.y * 1.15f;
            float front = size.y * 0.7f;

            Wheel(hull.transform, new Vector3(size.x * 0.62f, rear * 0.5f, -size.z * 0.45f), rear, rubber, "WheelRL");
            Wheel(hull.transform, new Vector3(-size.x * 0.62f, rear * 0.5f, -size.z * 0.45f), rear, rubber, "WheelRR");
            Wheel(hull.transform, new Vector3(size.x * 0.5f, front * 0.5f, size.z * 0.6f), front, rubber, "WheelFL");
            Wheel(hull.transform, new Vector3(-size.x * 0.5f, front * 0.5f, size.z * 0.6f), front, rubber, "WheelFR");

            // A drawbar the implement visibly hangs off, so a hitched machine reads as
            // one object rather than two that happen to be near each other.
            PrimitiveBuilder.Box(hull.transform, new Vector3(0f, size.y * 0.55f, -size.z * 0.75f),
                new Vector3(0.2f, 0.16f, size.z * 0.5f), body, "Drawbar");
        }

        /// <summary>
        /// A wheel is a cylinder laid on its side: round in profile, narrow across. The
        /// half is not a typo - Unity's cylinder primitive is two units tall, so a scale
        /// of 0.08 gives the 0.16 m tyre width this wants.
        /// </summary>
        static void Wheel(Transform parent, Vector3 centre, float diameter, Material mat, string name)
        {
            var go = PrimitiveBuilder.Cylinder(parent, centre, new Vector3(diameter, 0.08f, diameter), mat, name);
            go.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        }
    }

    /// <summary>
    /// What hangs off the drawbar. Each kind gets a shape you can tell apart in a
    /// mirror-less game from the driver's seat: discs for a plough, tines for a
    /// cultivator, a tank for a seeder, a header for a harvester.
    /// </summary>
    public static class ImplementVisuals
    {
        public static void Build(Transform root, ImplementDefinition def)
        {
            GameObject implementModel;
            if (Core.ModelCatalogue.TryBuild(def.stringId, root, out implementModel)) return;

            var frame = MaterialLibrary.Get(SurfaceFamily.Metal, new Color(0.40f, 0.20f, 0.14f), 0.2f, 0.55f);
            var steel = MaterialLibrary.Get(SurfaceFamily.Metal, new Color(0.55f, 0.55f, 0.57f), 0.6f, 0.8f);

            float width = Mathf.Max(1f, def.workingWidth);

            PrimitiveBuilder.Box(root, new Vector3(0f, 0.55f, 0.25f),
                new Vector3(width * 0.9f, 0.16f, 0.2f), frame, "Toolbar");
            PrimitiveBuilder.Box(root, new Vector3(0f, 0.5f, 0.7f),
                new Vector3(0.16f, 0.14f, 0.9f), frame, "Drawbar");

            switch (def.kind)
            {
                case ImplementKind.Plow: BuildDiscs(root, width, steel); break;
                case ImplementKind.Cultivator: BuildTines(root, width, steel); break;
                case ImplementKind.Seeder: BuildTank(root, width, def, frame, steel); break;
                case ImplementKind.Spreader: BuildSpreader(root, width, frame, steel); break;
                default: BuildHeader(root, width, def, frame, steel); break;
            }
        }

        static void BuildDiscs(Transform root, float width, Material steel)
        {
            int count = Mathf.Max(3, Mathf.RoundToInt(width));
            for (int i = 0; i < count; i++)
            {
                float x = Mathf.Lerp(-width * 0.45f, width * 0.45f, count == 1 ? 0.5f : i / (float)(count - 1));
                var disc = PrimitiveBuilder.Cylinder(root, new Vector3(x, 0.26f, 0.1f),
                    new Vector3(0.52f, 0.05f, 0.52f), steel, "Disc" + i);
                // Angled, the way a disc plough actually cuts.
                disc.transform.localRotation = Quaternion.Euler(0f, 18f, 90f);
            }
        }

        static void BuildTines(Transform root, float width, Material steel)
        {
            int count = Mathf.Max(4, Mathf.RoundToInt(width * 1.5f));
            for (int i = 0; i < count; i++)
            {
                float x = Mathf.Lerp(-width * 0.45f, width * 0.45f, count == 1 ? 0.5f : i / (float)(count - 1));
                float z = i % 2 == 0 ? 0.05f : 0.35f;
                PrimitiveBuilder.Box(root, new Vector3(x, 0.3f, z),
                    new Vector3(0.07f, 0.5f, 0.07f), steel, "Tine" + i);
            }
        }

        static void BuildTank(Transform root, float width, ImplementDefinition def, Material frame, Material steel)
        {
            PrimitiveBuilder.Box(root, new Vector3(0f, 0.95f, 0.15f),
                new Vector3(width * 0.55f, 0.6f, 0.6f), frame, "Hopper");
            PrimitiveBuilder.Box(root, new Vector3(0f, 1.27f, 0.15f),
                new Vector3(width * 0.58f, 0.06f, 0.64f), steel, "Lid");

            int spouts = Mathf.Max(3, Mathf.RoundToInt(width));
            for (int i = 0; i < spouts; i++)
            {
                float x = Mathf.Lerp(-width * 0.45f, width * 0.45f, spouts == 1 ? 0.5f : i / (float)(spouts - 1));
                PrimitiveBuilder.Cylinder(root, new Vector3(x, 0.3f, 0f),
                    new Vector3(0.08f, 0.5f, 0.08f), steel, "Spout" + i);
            }
        }

        /// <summary>
        /// An open box on the back with two spinners under it. Readable at distance as
        /// "the wide one that throws muck" rather than as a drill, which matters when
        /// four implements are parked in a row and they all have to be told apart at a
        /// glance.
        /// </summary>
        static void BuildSpreader(Transform root, float width, Material frame, Material steel)
        {
            // Wider and shallower than the drill's hopper - it is a box, not a tank.
            PrimitiveBuilder.Box(root, new Vector3(0f, 0.85f, 0.2f),
                new Vector3(width * 0.72f, 0.5f, 0.75f), frame, "MuckBox");

            // Flared sides, so it does not read as a plain crate.
            PrimitiveBuilder.Box(root, new Vector3(-width * 0.37f, 1.05f, 0.2f),
                new Vector3(0.06f, 0.3f, 0.75f), steel, "SideL");
            PrimitiveBuilder.Box(root, new Vector3(width * 0.37f, 1.05f, 0.2f),
                new Vector3(0.06f, 0.3f, 0.75f), steel, "SideR");

            // The two spinning discs that do the throwing, low and behind.
            for (int i = 0; i < 2; i++)
            {
                float x = (i == 0 ? -1f : 1f) * width * 0.18f;
                PrimitiveBuilder.Cylinder(root, new Vector3(x, 0.3f, -0.3f),
                    new Vector3(0.62f, 0.05f, 0.62f), steel, "Spinner" + i);
            }
        }

        static void BuildHeader(Transform root, float width, ImplementDefinition def, Material frame, Material steel)
        {
            PrimitiveBuilder.Box(root, new Vector3(0f, 0.35f, -0.15f),
                new Vector3(width, 0.45f, 0.45f), frame, "Header");
            PrimitiveBuilder.Box(root, new Vector3(0f, 0.16f, -0.36f),
                new Vector3(width, 0.06f, 0.12f), steel, "Cutterbar");
            PrimitiveBuilder.Box(root, new Vector3(0f, 1.0f, 0.35f),
                new Vector3(width * 0.5f, 0.7f, 0.7f), frame, "Tank");
            PrimitiveBuilder.Cylinder(root, new Vector3(width * 0.28f, 1.15f, 0.35f),
                new Vector3(0.18f, 0.9f, 0.18f), steel, "Auger");
        }
    }
}
