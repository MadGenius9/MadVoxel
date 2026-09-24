using System.Collections.Generic;
using UnityEngine;

namespace MadVoxel.Core
{
    /// <summary>
    /// Real models, when there are any, and boxes when there are not.
    ///
    /// Every prop in this game is built out of primitives at runtime because the
    /// project could not ship art. That was always meant to be temporary - the note
    /// on <see cref="PrimitiveBuilder"/> has said "swapping in real meshes later means
    /// replacing these calls, nothing else" since the beginning - but "replacing these
    /// calls" is a hundred and sixty-eight of them across ten files, and doing that in
    /// one go, to a build nobody has run, would be a bad trade.
    ///
    /// So the swap happens here instead. A visual builder asks for a model by id
    /// before it starts stacking boxes; if one exists it is used and the box code is
    /// skipped, and if not the box code runs exactly as it always has. Nothing breaks
    /// when there are no assets, the headless tests never touch it, and art can be
    /// dropped in one prop at a time rather than all at once.
    ///
    /// To add a model: put a prefab at
    /// <c>Assets/MadVoxel/Resources/MadVoxel/Models/&lt;id&gt;.prefab</c>, where the id
    /// is the definition's string id with the namespace stripped - so
    /// <c>madvoxel:zombie_shambler</c> becomes <c>zombie_shambler</c>. Nothing needs
    /// recompiling and nothing needs registering.
    /// </summary>
    public static class ModelCatalogue
    {
        const string Root = "MadVoxel/Models/";

        /// <summary>
        /// Prefab per id, including the misses.
        ///
        /// Caching the failures matters more than caching the hits: a horde night
        /// spawns hundreds of bodies, and without this every one of them would hit the
        /// filesystem for a model that has never existed.
        /// </summary>
        static readonly Dictionary<string, GameObject> Cache = new Dictionary<string, GameObject>();

        /// <summary>Turns a definition's string id into a model id. Namespace-free.</summary>
        public static string IdFor(string stringId)
        {
            if (string.IsNullOrEmpty(stringId)) return "";

            int colon = stringId.IndexOf(':');
            return colon >= 0 ? stringId.Substring(colon + 1) : stringId;
        }

        /// <summary>The prefab for an id, or null when nothing is installed for it.</summary>
        public static GameObject Find(string modelId)
        {
            if (string.IsNullOrEmpty(modelId)) return null;

            GameObject prefab;
            if (Cache.TryGetValue(modelId, out prefab)) return prefab;

            prefab = Resources.Load<GameObject>(Root + modelId);
            Cache[modelId] = prefab;
            return prefab;
        }

        /// <summary>Is there a model for this definition?</summary>
        public static bool Has(string stringId)
        {
            return Find(IdFor(stringId)) != null;
        }

        /// <summary>
        /// Builds the model for a definition under <paramref name="parent"/>, or
        /// returns false if there is none and the caller should draw its own boxes.
        ///
        /// Instantiated at the parent's origin with no rotation, which is the same
        /// contract the primitive builders work to - a model that needs an offset
        /// should carry it in the prefab, where an artist can see it, rather than in a
        /// table of magic numbers here.
        /// </summary>
        public static bool TryBuild(string stringId, Transform parent, out GameObject instance)
        {
            instance = null;

            var prefab = Find(IdFor(stringId));
            if (prefab == null || parent == null) return false;

            instance = Object.Instantiate(prefab, parent, false);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            return true;
        }

        /// <summary>
        /// Forgets what has been looked up.
        ///
        /// Only useful in the editor, where art is added while the game is running and
        /// a cached miss would hide it until a domain reload.
        /// </summary>
        public static void Forget()
        {
            Cache.Clear();
        }
    }
}
