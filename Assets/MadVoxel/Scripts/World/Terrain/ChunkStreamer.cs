using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using MadVoxel.Core;
using UnityEngine;

namespace MadVoxel.World.Terrain
{
    /// <summary>
    /// Loads, generates, meshes and unloads chunks around a tracked transform. Terrain
    /// generation, chunk file reads and greedy meshing all run on worker threads; only
    /// the mesh upload and the chunk bookkeeping happen on the main thread.
    /// </summary>
    public class ChunkStreamer : MonoBehaviour
    {
        struct GenResult
        {
            public ChunkCoord Coord;
            public ushort[] Blocks;
            public bool FromDisk;
        }

        struct MeshResult
        {
            public ChunkCoord Coord;
            public ChunkMeshData Data;
        }

        const int MaxConcurrentGenJobs = 32;
        const int MaxConcurrentMeshJobs = 8;
        const int ScanIntervalFrames = 10;

        TerrainWorld _world;
        GameConfig _config;
        IChunkStore _store;
        Transform _tracked;
        BlockMaterialCache _materials;
        BlockMeta[] _meta;
        Transform _chunkRoot;

        readonly Dictionary<ChunkCoord, ChunkView> _views = new Dictionary<ChunkCoord, ChunkView>();
        readonly Stack<ChunkView> _pool = new Stack<ChunkView>();

        readonly HashSet<ChunkCoord> _pendingGen = new HashSet<ChunkCoord>();
        readonly HashSet<ChunkCoord> _pendingMesh = new HashSet<ChunkCoord>();
        readonly HashSet<ChunkCoord> _meshRequests = new HashSet<ChunkCoord>();
        readonly List<ChunkCoord> _meshRequestScratch = new List<ChunkCoord>();

        readonly ConcurrentQueue<GenResult> _genDone = new ConcurrentQueue<GenResult>();
        readonly ConcurrentQueue<MeshResult> _meshDone = new ConcurrentQueue<MeshResult>();

        readonly List<Vector2Int> _ring = new List<Vector2Int>();
        readonly List<ChunkCoord> _unloadScratch = new List<ChunkCoord>();

        ChunkCoord _lastCentre;
        int _frameCounter;
        bool _scanQueued = true;

        public int PendingJobs { get { return _pendingGen.Count + _pendingMesh.Count + _meshRequests.Count; } }
        public int VisibleChunks { get { return _views.Count; } }

        public void Init(TerrainWorld world, GameConfig config, IChunkStore store, Transform tracked)
        {
            _world = world;
            _config = config;
            _store = store;
            _tracked = tracked;
            _materials = new BlockMaterialCache(world.Registry);
            _meta = BlockMeta.Snapshot(world.Registry);

            var rootGo = new GameObject("Chunks");
            rootGo.transform.SetParent(transform, false);
            _chunkRoot = rootGo.transform;

            BuildRing(config.viewDistanceChunks + config.generationPadding);

            _world.MeshInvalidated += OnMeshInvalidated;
            _lastCentre = new ChunkCoord(int.MinValue, 0, 0);
        }

        void OnDestroy()
        {
            if (_world != null) _world.MeshInvalidated -= OnMeshInvalidated;
        }

        void OnMeshInvalidated(ChunkCoord coord)
        {
            _meshRequests.Add(coord);
        }

        void BuildRing(int radius)
        {
            _ring.Clear();
            for (int z = -radius; z <= radius; z++)
            {
                for (int x = -radius; x <= radius; x++)
                {
                    if (x * x + z * z > radius * radius) continue;
                    _ring.Add(new Vector2Int(x, z));
                }
            }
            _ring.Sort((a, b) => (a.x * a.x + a.y * a.y).CompareTo(b.x * b.x + b.y * b.y));
        }

        void Update()
        {
            if (_world == null || _tracked == null) return;

            DrainGeneration();
            DrainMeshes();

            var centre = ChunkCoord.FromWorld(_tracked.position);
            if (!centre.Equals(_lastCentre))
            {
                _lastCentre = centre;
                _scanQueued = true;
            }

            _frameCounter++;
            bool periodic = _frameCounter % ScanIntervalFrames == 0;
            if (_scanQueued || periodic)
            {
                _scanQueued = false;
                Scan(centre);
            }

            // Unloading walks every loaded chunk, so it stays on the slow tick even
            // while generation is running flat out.
            if (periodic) Unload(centre);

            ServiceMeshRequests();
        }

        // ---------------------------------------------------------------- generation

        void Scan(ChunkCoord centre)
        {
            int genRadius = _config.viewDistanceChunks + _config.generationPadding;
            int meshRadius = _config.viewDistanceChunks;

            for (int i = 0; i < _ring.Count; i++)
            {
                if (_pendingGen.Count >= MaxConcurrentGenJobs) break;

                var offset = _ring[i];
                int dist2 = offset.x * offset.x + offset.y * offset.y;
                if (dist2 > genRadius * genRadius) continue;

                if (!_world.InBounds(centre.X + offset.x, centre.Z + offset.y)) continue;

                for (int cy = 0; cy < TerrainWorld.WorldHeightChunks; cy++)
                {
                    var coord = new ChunkCoord(centre.X + offset.x, cy, centre.Z + offset.y);
                    var chunk = _world.GetOrCreateChunk(coord);
                    if (chunk == null) break;
                    if (!chunk.Generated && !_pendingGen.Contains(coord))
                    {
                        DispatchGeneration(coord);
                        if (_pendingGen.Count >= MaxConcurrentGenJobs) break;
                    }
                }
            }

            // Second pass: anything generated and inside the mesh radius that still needs a mesh.
            for (int i = 0; i < _ring.Count; i++)
            {
                var offset = _ring[i];
                int dist2 = offset.x * offset.x + offset.y * offset.y;
                if (dist2 > meshRadius * meshRadius) continue;

                for (int cy = 0; cy < TerrainWorld.WorldHeightChunks; cy++)
                {
                    var coord = new ChunkCoord(centre.X + offset.x, cy, centre.Z + offset.y);
                    Chunk chunk;
                    if (!_world.TryGetChunk(coord, out chunk)) continue;
                    if (chunk.Generated && chunk.MeshDirty) _meshRequests.Add(coord);
                }
            }
        }

        void DispatchGeneration(ChunkCoord coord)
        {
            _pendingGen.Add(coord);
            var store = _store;
            var terrain = _world.Terrain;

            Task.Run(() =>
            {
                var blocks = new ushort[Chunk.Volume];
                bool fromDisk = store != null && store.TryLoad(coord, blocks);
                if (!fromDisk) terrain.Generate(coord, blocks);
                _genDone.Enqueue(new GenResult { Coord = coord, Blocks = blocks, FromDisk = fromDisk });
            });
        }

        void DrainGeneration()
        {
            GenResult result;
            while (_genDone.TryDequeue(out result))
            {
                _pendingGen.Remove(result.Coord);
                _scanQueued = true;

                Chunk chunk;
                if (!_world.TryGetChunk(result.Coord, out chunk)) continue; // unloaded while we worked
                if (chunk.Generated) continue;

                chunk.AdoptBlocks(result.Blocks);
                chunk.FromDisk = result.FromDisk;
                _meshRequests.Add(result.Coord);

                // A new chunk changes what its neighbours' border faces look like.
                _meshRequests.Add(result.Coord.Offset(1, 0, 0));
                _meshRequests.Add(result.Coord.Offset(-1, 0, 0));
                _meshRequests.Add(result.Coord.Offset(0, 1, 0));
                _meshRequests.Add(result.Coord.Offset(0, -1, 0));
                _meshRequests.Add(result.Coord.Offset(0, 0, 1));
                _meshRequests.Add(result.Coord.Offset(0, 0, -1));
            }
        }

        // --------------------------------------------------------------------- mesh

        void ServiceMeshRequests()
        {
            if (_meshRequests.Count == 0) return;
            if (_pendingMesh.Count >= MaxConcurrentMeshJobs) return;

            _meshRequestScratch.Clear();
            _meshRequestScratch.AddRange(_meshRequests);

            // Nearest first so an edit under the player's feet updates immediately.
            var centre = _lastCentre;
            _meshRequestScratch.Sort((a, b) => Dist2(a, centre).CompareTo(Dist2(b, centre)));

            int meshRadius = _config.viewDistanceChunks;
            for (int i = 0; i < _meshRequestScratch.Count; i++)
            {
                if (_pendingMesh.Count >= MaxConcurrentMeshJobs) break;

                var coord = _meshRequestScratch[i];
                if (_pendingMesh.Contains(coord)) continue;

                int dx = coord.X - centre.X;
                int dz = coord.Z - centre.Z;
                if (dx * dx + dz * dz > meshRadius * meshRadius)
                {
                    _meshRequests.Remove(coord);
                    continue;
                }

                Chunk chunk;
                if (!_world.TryGetChunk(coord, out chunk) || !chunk.Generated)
                {
                    _meshRequests.Remove(coord);
                    continue;
                }

                if (!_world.NeighboursGenerated(coord)) continue; // retry next frame

                _meshRequests.Remove(coord);
                chunk.MeshDirty = false;

                if (CanSkipMesh(chunk))
                {
                    ReleaseView(coord);
                    continue;
                }

                DispatchMesh(coord);
            }
        }

        static int Dist2(ChunkCoord a, ChunkCoord b)
        {
            int dx = a.X - b.X, dy = a.Y - b.Y, dz = a.Z - b.Z;
            return dx * dx + dy * dy + dz * dz;
        }

        /// <summary>
        /// A uniform chunk surrounded by chunks of the same opacity has no visible faces:
        /// open sky and deep stone cost nothing.
        /// </summary>
        bool CanSkipMesh(Chunk chunk)
        {
            if (!chunk.IsUniform) return false;

            bool centreOpaque = _world.Registry.IsOpaque(chunk.UniformBlock);
            for (int axis = 0; axis < 3; axis++)
            {
                for (int sign = -1; sign <= 1; sign += 2)
                {
                    var n = chunk.Coord.Offset(axis == 0 ? sign : 0, axis == 1 ? sign : 0, axis == 2 ? sign : 0);
                    if (n.Y < 0 || n.Y >= TerrainWorld.WorldHeightChunks)
                    {
                        // Outside the vertical world counts as air.
                        if (centreOpaque) return false;
                        continue;
                    }

                    Chunk neighbour;
                    if (!_world.TryGetChunk(n, out neighbour) || !neighbour.Generated) return false;
                    if (!neighbour.IsUniform) return false;
                    if (_world.Registry.IsOpaque(neighbour.UniformBlock) != centreOpaque) return false;
                }
            }
            return true;
        }

        void DispatchMesh(ChunkCoord coord)
        {
            var padded = _world.BuildPaddedSnapshot(coord);
            var meta = _meta;
            _pendingMesh.Add(coord);

            Task.Run(() =>
            {
                var data = ChunkMesher.Build(padded, meta, coord.Origin);
                _meshDone.Enqueue(new MeshResult { Coord = coord, Data = data });
            });
        }

        void DrainMeshes()
        {
            int budget = Mathf.Max(1, _config.maxChunkBuildsPerFrame);
            MeshResult result;
            while (budget > 0 && _meshDone.TryDequeue(out result))
            {
                _pendingMesh.Remove(result.Coord);

                Chunk chunk;
                if (!_world.TryGetChunk(result.Coord, out chunk)) continue;

                if (result.Data.IsEmpty)
                {
                    ReleaseView(result.Coord);
                    continue;
                }

                var view = GetOrCreateView(result.Coord);
                view.Apply(result.Data, _materials);
                budget--;
            }
        }

        // ------------------------------------------------------------------- views

        ChunkView GetOrCreateView(ChunkCoord coord)
        {
            ChunkView view;
            if (_views.TryGetValue(coord, out view) && view != null) return view;

            if (_pool.Count > 0)
            {
                view = _pool.Pop();
                view.gameObject.SetActive(true);
            }
            else
            {
                var go = new GameObject("Chunk");
                go.transform.SetParent(_chunkRoot, false);
                view = go.AddComponent<ChunkView>();
            }

            view.Bind(coord);
            _views[coord] = view;
            return view;
        }

        void ReleaseView(ChunkCoord coord)
        {
            ChunkView view;
            if (!_views.TryGetValue(coord, out view)) return;
            _views.Remove(coord);
            if (view == null) return;
            view.ClearMesh();
            view.gameObject.SetActive(false);
            _pool.Push(view);
        }

        // ------------------------------------------------------------------ unload

        void Unload(ChunkCoord centre)
        {
            int keepRadius = _config.viewDistanceChunks + _config.generationPadding + 2;
            int keep2 = keepRadius * keepRadius;

            _unloadScratch.Clear();
            foreach (var chunk in _world.LoadedChunks)
            {
                int dx = chunk.Coord.X - centre.X;
                int dz = chunk.Coord.Z - centre.Z;
                if (dx * dx + dz * dz > keep2) _unloadScratch.Add(chunk.Coord);
            }

            for (int i = 0; i < _unloadScratch.Count; i++)
            {
                var coord = _unloadScratch[i];
                if (_pendingGen.Contains(coord) || _pendingMesh.Contains(coord)) continue;

                Chunk chunk;
                if (_world.TryGetChunk(coord, out chunk))
                {
                    if (chunk.Dirty && _store != null)
                    {
                        _store.Save(coord, chunk.CopyBlocks());
                        chunk.Dirty = false;
                    }
                }

                ReleaseView(coord);
                _meshRequests.Remove(coord);
                _world.RemoveChunk(coord);
            }

            // Drop view distance changes cleanly.
            int viewKeep = _config.viewDistanceChunks * _config.viewDistanceChunks;
            _unloadScratch.Clear();
            foreach (var kv in _views)
            {
                int dx = kv.Key.X - centre.X;
                int dz = kv.Key.Z - centre.Z;
                if (dx * dx + dz * dz > viewKeep + 4) _unloadScratch.Add(kv.Key);
            }
            for (int i = 0; i < _unloadScratch.Count; i++) ReleaseView(_unloadScratch[i]);
        }

        /// <summary>Writes every edited chunk currently in memory. Called on save and quit.</summary>
        public void SaveDirtyChunks()
        {
            if (_store == null) return;
            foreach (var chunk in _world.LoadedChunks)
            {
                if (!chunk.Dirty) continue;
                _store.Save(chunk.Coord, chunk.CopyBlocks());
                chunk.Dirty = false;
            }
            _store.Flush();
        }

        /// <summary>True once the column under a position has terrain, so it is safe to drop an entity in.</summary>
        public bool IsColumnReady(Vector3 worldPos)
        {
            var centre = ChunkCoord.FromWorld(worldPos);
            for (int cy = 0; cy < TerrainWorld.WorldHeightChunks; cy++)
            {
                Chunk chunk;
                if (!_world.TryGetChunk(new ChunkCoord(centre.X, cy, centre.Z), out chunk)) return false;
                if (!chunk.Generated) return false;
            }
            return true;
        }

        /// <summary>Terrain in a 3x3 column block around a position has finished generating.</summary>
        public bool IsAreaReady(Vector3 worldPos)
        {
            for (int dz = -1; dz <= 1; dz++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (!IsColumnReady(worldPos + new Vector3(dx * Chunk.Size, 0f, dz * Chunk.Size))) return false;
                }
            }
            return true;
        }
    }
}
