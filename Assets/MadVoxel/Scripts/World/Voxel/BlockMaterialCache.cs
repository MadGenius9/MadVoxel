using System.Collections.Generic;
using MadVoxel.Core;
using UnityEngine;

namespace MadVoxel.World.Voxel
{
    /// <summary>Block runtime id to material, built once from the registry.</summary>
    public class BlockMaterialCache
    {
        readonly Dictionary<ushort, Material> _materials = new Dictionary<ushort, Material>();
        readonly BlockRegistry _registry;

        public BlockMaterialCache(BlockRegistry registry)
        {
            _registry = registry;
        }

        public Material Get(ushort blockId)
        {
            Material mat;
            if (_materials.TryGetValue(blockId, out mat) && mat != null) return mat;

            var def = _registry.ById(blockId);
            if (def == null)
            {
                mat = MaterialLibrary.Get(SurfaceFamily.Stone, Color.magenta);
            }
            else
            {
                mat = MaterialLibrary.Get(def.surfaceFamily, def.tint, def.smoothness, def.metallic, def.transparent);
            }
            _materials[blockId] = mat;
            return mat;
        }
    }
}
