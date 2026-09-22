namespace MadVoxel.World.Terrain
{
    /// <summary>
    /// Persistence for edited chunks. Implementations must be thread-safe: the streamer
    /// calls TryLoad from worker threads.
    /// </summary>
    public interface IChunkStore
    {
        bool TryLoad(ChunkCoord coord, ushort[] dest);
        void Save(ChunkCoord coord, ushort[] blocks);
        void Flush();
    }
}
