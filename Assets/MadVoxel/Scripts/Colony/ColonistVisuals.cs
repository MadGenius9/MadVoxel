using MadVoxel.Core;
using UnityEngine;

namespace MadVoxel.Colony
{
    /// <summary>
    /// A person, built from boxes like everything else in this project. They read as
    /// working clothes over a survivor's frame rather than as a mannequin: the point is
    /// that you can tell a colonist from a zombie at fifty metres in bad light.
    /// </summary>
    public static class ColonistVisuals
    {
        static readonly Color Canvas = new Color(0.52f, 0.47f, 0.36f);
        static readonly Color Denim = new Color(0.28f, 0.31f, 0.36f);
        static readonly Color Skin = new Color(0.62f, 0.50f, 0.40f);
        static readonly Color Boots = new Color(0.22f, 0.19f, 0.16f);

        public static void Build(Transform parent)
        {
            var cloth = MaterialLibrary.Get(SurfaceFamily.Cloth, Canvas, 0.05f, 0f);
            var jeans = MaterialLibrary.Get(SurfaceFamily.Cloth, Denim, 0.05f, 0f);
            var skin = MaterialLibrary.Get(SurfaceFamily.Cloth, Skin, 0.1f, 0f);
            var leather = MaterialLibrary.Get(SurfaceFamily.Cloth, Boots, 0.1f, 0f);

            var body = new GameObject("Body");
            body.transform.SetParent(parent, false);

            PrimitiveBuilder.Box(body.transform, new Vector3(0f, 1.18f, 0f), new Vector3(0.46f, 0.62f, 0.26f), cloth, "Torso");
            PrimitiveBuilder.Box(body.transform, new Vector3(0f, 1.62f, 0f), new Vector3(0.24f, 0.26f, 0.24f), skin, "Head");

            // A brimmed hat, because a farmer in the sun is a silhouette you recognise.
            PrimitiveBuilder.Box(body.transform, new Vector3(0f, 1.77f, 0f), new Vector3(0.44f, 0.05f, 0.44f), cloth, "Brim");
            PrimitiveBuilder.Box(body.transform, new Vector3(0f, 1.81f, 0f), new Vector3(0.24f, 0.10f, 0.24f), cloth, "Crown");

            PrimitiveBuilder.Box(body.transform, new Vector3(-0.31f, 1.16f, 0f), new Vector3(0.14f, 0.56f, 0.16f), cloth, "ArmL");
            PrimitiveBuilder.Box(body.transform, new Vector3(0.31f, 1.16f, 0f), new Vector3(0.14f, 0.56f, 0.16f), cloth, "ArmR");

            PrimitiveBuilder.Box(body.transform, new Vector3(-0.13f, 0.54f, 0f), new Vector3(0.18f, 0.70f, 0.20f), jeans, "LegL");
            PrimitiveBuilder.Box(body.transform, new Vector3(0.13f, 0.54f, 0f), new Vector3(0.18f, 0.70f, 0.20f), jeans, "LegR");

            PrimitiveBuilder.Box(body.transform, new Vector3(-0.13f, 0.10f, 0.02f), new Vector3(0.20f, 0.16f, 0.28f), leather, "BootL");
            PrimitiveBuilder.Box(body.transform, new Vector3(0.13f, 0.10f, 0.02f), new Vector3(0.20f, 0.16f, 0.28f), leather, "BootR");
        }
    }
}
