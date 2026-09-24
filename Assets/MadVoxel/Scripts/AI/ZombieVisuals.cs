using MadVoxel.Core;
using UnityEngine;

namespace MadVoxel.AI
{
    /// <summary>Blocky placeholder body with readable limbs at 50-100m.</summary>
    public static class ZombieVisuals
    {
        public class Limbs
        {
            public Transform ArmL, ArmR, LegL, LegR, Torso;
        }

        public static Limbs Build(Transform root, ZombieDefinition def)
        {
            // A real model, if one has been installed for this zombie. It brings its
            // own rig and its own animation, so there are no limbs to swing - Animate
            // already does nothing when handed a null, which is the whole reason this
            // returns one rather than an empty set.
            GameObject model;
            if (Core.ModelCatalogue.TryBuild(def.stringId, root, out model)) return null;

            float h = def.height;
            var flesh = MaterialLibrary.Get(SurfaceFamily.Flesh, def.tint, 0.05f);
            var cloth = MaterialLibrary.Get(SurfaceFamily.Cloth, def.tint * 0.55f, 0.05f);
            var head = MaterialLibrary.Get(SurfaceFamily.Flesh, Color.Lerp(def.tint, new Color(0.62f, 0.60f, 0.50f), 0.45f), 0.05f);

            var body = new GameObject("Body");
            body.transform.SetParent(root, false);

            var limbs = new Limbs();

            var torso = PrimitiveBuilder.Box(body.transform, new Vector3(0f, h * 0.62f, 0f),
                new Vector3(0.52f, h * 0.36f, 0.28f), cloth, "Torso");
            limbs.Torso = torso.transform;

            PrimitiveBuilder.Box(body.transform, new Vector3(0f, h * 0.90f, 0f),
                new Vector3(0.26f, 0.26f, 0.26f), head, "Head");

            limbs.ArmL = MakePivot(body.transform, new Vector3(-0.35f, h * 0.76f, 0f), "ArmL");
            PrimitiveBuilder.Box(limbs.ArmL, new Vector3(0f, -h * 0.17f, 0.1f), new Vector3(0.16f, h * 0.34f, 0.16f), flesh, "ArmMesh");

            limbs.ArmR = MakePivot(body.transform, new Vector3(0.35f, h * 0.76f, 0f), "ArmR");
            PrimitiveBuilder.Box(limbs.ArmR, new Vector3(0f, -h * 0.17f, 0.1f), new Vector3(0.16f, h * 0.34f, 0.16f), flesh, "ArmMesh");

            limbs.LegL = MakePivot(body.transform, new Vector3(-0.14f, h * 0.44f, 0f), "LegL");
            PrimitiveBuilder.Box(limbs.LegL, new Vector3(0f, -h * 0.22f, 0f), new Vector3(0.18f, h * 0.44f, 0.18f), cloth, "LegMesh");

            limbs.LegR = MakePivot(body.transform, new Vector3(0.14f, h * 0.44f, 0f), "LegR");
            PrimitiveBuilder.Box(limbs.LegR, new Vector3(0f, -h * 0.22f, 0f), new Vector3(0.18f, h * 0.44f, 0.18f), cloth, "LegMesh");

            return limbs;
        }

        static Transform MakePivot(Transform parent, Vector3 localPosition, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            return go.transform;
        }

        /// <summary>Crude shamble: arms out front, legs counter-swinging.</summary>
        public static void Animate(Limbs limbs, float phase, float speed01)
        {
            if (limbs == null) return;
            float swing = Mathf.Sin(phase) * Mathf.Lerp(6f, 42f, speed01);

            if (limbs.LegL != null) limbs.LegL.localRotation = Quaternion.Euler(swing, 0f, 0f);
            if (limbs.LegR != null) limbs.LegR.localRotation = Quaternion.Euler(-swing, 0f, 0f);
            if (limbs.ArmL != null) limbs.ArmL.localRotation = Quaternion.Euler(-72f + swing * 0.3f, 0f, 0f);
            if (limbs.ArmR != null) limbs.ArmR.localRotation = Quaternion.Euler(-72f - swing * 0.3f, 0f, 0f);
            if (limbs.Torso != null) limbs.Torso.localRotation = Quaternion.Euler(6f, Mathf.Sin(phase * 0.5f) * 3f, 0f);
        }
    }
}
