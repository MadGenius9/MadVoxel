using System.Collections.Generic;
using UnityEngine;

namespace MadVoxel.World.Voxel
{
    /// <summary>
    /// Assigns runtime ids to blocks and maps them back to the stable string ids used
    /// on disk, so chunk files survive re-ordering this list.
    /// </summary>
    [CreateAssetMenu(menuName = "MadVoxel/Block Registry", fileName = "BlockRegistry")]
    public class BlockRegistry : ScriptableObject
    {
        [Tooltip("Index 0 must be air.")]
        public List<BlockDefinition> blocks = new List<BlockDefinition>();

        readonly Dictionary<string, BlockDefinition> _byStringId = new Dictionary<string, BlockDefinition>();
        bool _built;

        public int Count { get { return blocks.Count; } }

        public void Build()
        {
            _byStringId.Clear();
            for (int i = 0; i < blocks.Count; i++)
            {
                var b = blocks[i];
                if (b == null)
                {
                    Debug.LogErrorFormat("BlockRegistry has a null entry at index {0}.", i);
                    continue;
                }
                b.RuntimeId = (ushort)i;
                if (_byStringId.ContainsKey(b.stringId))
                {
                    Debug.LogErrorFormat("Duplicate block string id '{0}'.", b.stringId);
                    continue;
                }
                _byStringId.Add(b.stringId, b);
            }
            _built = true;
        }

        public BlockDefinition ById(ushort id)
        {
            if (!_built) Build();
            if (id >= blocks.Count) return blocks.Count > 0 ? blocks[0] : null;
            return blocks[id];
        }

        public BlockDefinition ByStringId(string stringId)
        {
            if (!_built) Build();
            BlockDefinition def;
            return _byStringId.TryGetValue(stringId, out def) ? def : null;
        }

        public ushort IdOf(string stringId)
        {
            var def = ByStringId(stringId);
            return def != null ? def.RuntimeId : (ushort)0;
        }

        public bool IsOpaque(ushort id)
        {
            var def = ById(id);
            return def != null && !def.isAir && def.opaque;
        }

        public bool IsSolid(ushort id)
        {
            var def = ById(id);
            return def != null && !def.isAir && def.solid;
        }

        public bool IsAir(ushort id)
        {
            var def = ById(id);
            return def == null || def.isAir;
        }
    }
}
