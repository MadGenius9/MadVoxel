using System;
using System.Collections.Generic;
using System.IO;
using MadVoxel.World.Voxel;
using UnityEngine;

namespace MadVoxel.Save
{
    /// <summary>
    /// One file per edited chunk, run-length encoded against a per-chunk palette of
    /// block string ids. Unedited chunks are never written - they come back from the
    /// seed - which is what keeps an infinite world off the disk.
    /// Thread-safe: the streamer loads from worker threads.
    /// </summary>
    public class ChunkFileStore : IChunkStore
    {
        const int Magic = 0x4843564D; // "MVCH"
        const int Version = 1;

        readonly string _worldName;
        readonly BlockRegistry _registry;
        readonly object _gate = new object();
        readonly HashSet<ChunkCoord> _known = new HashSet<ChunkCoord>();

        public ChunkFileStore(string worldName, BlockRegistry registry)
        {
            _worldName = worldName;
            _registry = registry;
            SavePaths.EnsureWorldDirectories(worldName);
            IndexExistingChunks();
        }

        void IndexExistingChunks()
        {
            var dir = SavePaths.ChunkDirectory(_worldName);
            if (!Directory.Exists(dir)) return;

            var files = Directory.GetFiles(dir, "c.*.mvc");
            for (int i = 0; i < files.Length; i++)
            {
                ChunkCoord coord;
                if (TryParseName(Path.GetFileNameWithoutExtension(files[i]), out coord)) _known.Add(coord);
            }
        }

        static bool TryParseName(string name, out ChunkCoord coord)
        {
            coord = default(ChunkCoord);
            var parts = name.Split('.');
            if (parts.Length != 4 || parts[0] != "c") return false;

            int x, y, z;
            if (!int.TryParse(parts[1], out x)) return false;
            if (!int.TryParse(parts[2], out y)) return false;
            if (!int.TryParse(parts[3], out z)) return false;

            coord = new ChunkCoord(x, y, z);
            return true;
        }

        public bool TryLoad(ChunkCoord coord, ushort[] dest)
        {
            lock (_gate)
            {
                if (!_known.Contains(coord)) return false;
            }

            var path = SavePaths.ChunkFile(_worldName, coord.X, coord.Y, coord.Z);
            try
            {
                lock (_gate)
                {
                    if (!File.Exists(path)) return false;

                    using (var stream = File.OpenRead(path))
                    using (var reader = new BinaryReader(stream))
                    {
                        if (reader.ReadInt32() != Magic) return false;
                        int version = reader.ReadInt32();
                        if (version != Version) return false;

                        int paletteCount = reader.ReadInt32();
                        var palette = new ushort[paletteCount];
                        for (int i = 0; i < paletteCount; i++)
                        {
                            string stringId = reader.ReadString();
                            var def = _registry.ByStringId(stringId);
                            // Unknown blocks (removed content) fall back to air rather than corrupting the chunk.
                            palette[i] = def != null ? def.RuntimeId : (ushort)0;
                        }

                        int runCount = reader.ReadInt32();
                        int cursor = 0;
                        for (int i = 0; i < runCount; i++)
                        {
                            ushort paletteIndex = reader.ReadUInt16();
                            ushort length = reader.ReadUInt16();
                            ushort id = paletteIndex < palette.Length ? palette[paletteIndex] : (ushort)0;
                            for (int j = 0; j < length && cursor < dest.Length; j++) dest[cursor++] = id;
                        }

                        while (cursor < dest.Length) dest[cursor++] = 0;
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogErrorFormat("Failed to read chunk {0}: {1}", coord, ex.Message);
                return false;
            }
        }

        public void Save(ChunkCoord coord, ushort[] blocks)
        {
            var path = SavePaths.ChunkFile(_worldName, coord.X, coord.Y, coord.Z);

            // Build the palette and runs outside the lock; only the write is serialised.
            var paletteIds = new List<ushort>();
            var paletteIndex = new Dictionary<ushort, ushort>();
            var runValues = new List<ushort>();
            var runLengths = new List<ushort>();

            ushort currentIndex = LookupOrAdd(blocks[0], paletteIds, paletteIndex);
            ushort runLength = 1;

            for (int i = 1; i < blocks.Length; i++)
            {
                ushort index = LookupOrAdd(blocks[i], paletteIds, paletteIndex);
                if (index == currentIndex && runLength < ushort.MaxValue)
                {
                    runLength++;
                }
                else
                {
                    runValues.Add(currentIndex);
                    runLengths.Add(runLength);
                    currentIndex = index;
                    runLength = 1;
                }
            }
            runValues.Add(currentIndex);
            runLengths.Add(runLength);

            try
            {
                lock (_gate)
                {
                    Directory.CreateDirectory(SavePaths.ChunkDirectory(_worldName));
                    using (var stream = File.Create(path))
                    using (var writer = new BinaryWriter(stream))
                    {
                        writer.Write(Magic);
                        writer.Write(Version);

                        writer.Write(paletteIds.Count);
                        for (int i = 0; i < paletteIds.Count; i++)
                        {
                            var def = _registry.ById(paletteIds[i]);
                            writer.Write(def != null ? def.stringId : BlockIds.Air);
                        }

                        writer.Write(runValues.Count);
                        for (int i = 0; i < runValues.Count; i++)
                        {
                            writer.Write(runValues[i]);
                            writer.Write(runLengths[i]);
                        }
                    }
                    _known.Add(coord);
                }
            }
            catch (Exception ex)
            {
                Debug.LogErrorFormat("Failed to write chunk {0}: {1}", coord, ex.Message);
            }
        }

        static ushort LookupOrAdd(ushort blockId, List<ushort> paletteIds, Dictionary<ushort, ushort> index)
        {
            ushort existing;
            if (index.TryGetValue(blockId, out existing)) return existing;

            var next = (ushort)paletteIds.Count;
            paletteIds.Add(blockId);
            index.Add(blockId, next);
            return next;
        }

        public void Flush()
        {
            // Writes are synchronous, so there is nothing buffered to push.
        }

        public int KnownChunkCount
        {
            get { lock (_gate) { return _known.Count; } }
        }
    }
}
