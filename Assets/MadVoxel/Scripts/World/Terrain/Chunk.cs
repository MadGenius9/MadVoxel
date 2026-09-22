namespace MadVoxel.World.Terrain
{
    /// <summary>
    /// A 16^3 block of voxels. Chunks that are entirely one block type (deep stone,
    /// open sky) keep no array at all, which is what makes streaming a tall world
    /// affordable.
    /// </summary>
    public sealed class Chunk
    {
        public const int Size = 16;
        public const int Mask = Size - 1;
        public const int Shift = 4;
        public const int Volume = Size * Size * Size;

        public readonly ChunkCoord Coord;

        ushort[] _blocks;
        ushort _uniform;

        /// <summary>Terrain generation has run.</summary>
        public bool Generated;
        /// <summary>Player edits since load; only dirty chunks are written to disk.</summary>
        public bool Dirty;
        /// <summary>Mesh needs rebuilding.</summary>
        public bool MeshDirty = true;
        /// <summary>Loaded from a save file rather than generated, so never regenerate it.</summary>
        public bool FromDisk;

        public Chunk(ChunkCoord coord)
        {
            Coord = coord;
            _uniform = 0;
        }

        public bool IsUniform { get { return _blocks == null; } }
        public ushort UniformBlock { get { return _uniform; } }
        public ushort[] RawBlocks { get { return _blocks; } }

        public static int Index(int x, int y, int z)
        {
            return (y << (Shift + Shift)) | (z << Shift) | x;
        }

        public ushort Get(int x, int y, int z)
        {
            if (_blocks == null) return _uniform;
            return _blocks[Index(x, y, z)];
        }

        public bool Set(int x, int y, int z, ushort id)
        {
            if (_blocks == null)
            {
                if (_uniform == id) return false;
                Materialise();
            }
            int i = Index(x, y, z);
            if (_blocks[i] == id) return false;
            _blocks[i] = id;
            return true;
        }

        /// <summary>Hands the chunk a freshly generated array (from a worker thread).</summary>
        public void AdoptBlocks(ushort[] blocks)
        {
            _blocks = blocks;
            Compact();
            Generated = true;
            MeshDirty = true;
        }

        public void SetUniform(ushort id)
        {
            _blocks = null;
            _uniform = id;
            Generated = true;
            MeshDirty = true;
        }

        void Materialise()
        {
            var arr = new ushort[Volume];
            if (_uniform != 0)
            {
                for (int i = 0; i < Volume; i++) arr[i] = _uniform;
            }
            _blocks = arr;
        }

        /// <summary>Drops the array when every voxel matches, reclaiming 8 KB.</summary>
        public void Compact()
        {
            if (_blocks == null) return;
            ushort first = _blocks[0];
            for (int i = 1; i < Volume; i++)
            {
                if (_blocks[i] != first) return;
            }
            _uniform = first;
            _blocks = null;
        }

        /// <summary>Copies the voxels out for a worker thread or the save writer.</summary>
        public ushort[] CopyBlocks()
        {
            var arr = new ushort[Volume];
            if (_blocks == null)
            {
                for (int i = 0; i < Volume; i++) arr[i] = _uniform;
            }
            else
            {
                System.Array.Copy(_blocks, arr, Volume);
            }
            return arr;
        }
    }
}
