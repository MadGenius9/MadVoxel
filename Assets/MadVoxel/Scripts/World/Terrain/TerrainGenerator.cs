using System.Collections.Generic;
using MadVoxel.Core;
using MadVoxel.World.Biomes;
using UnityEngine;

namespace MadVoxel.World.Terrain
{
    /// <summary>
    /// Deterministic, thread-safe terrain. Given a seed it produces the same world on
    /// every machine, and it never needs the rest of the game to be loaded - that is
    /// what lets chunk generation run off the main thread.
    /// </summary>
    public class TerrainGenerator
    {
        public const int SeaLevel = 48;
        const int TreeCell = 8;

        readonly int _seed;

        // Block ids are resolved once, on the main thread, so the worker touches no Unity objects.
        readonly ushort _air, _bedrock, _stone, _dirt, _grass, _sand, _gravel;
        readonly ushort _coalOre, _ironOre, _log, _leaves, _scrap, _clay;
        readonly ushort _concrete, _planks, _ironBlock, _cobble;
        readonly ushort _wildYucca, _wildGrain, _wildCorn;

        /// <summary>Points of interest. Read-only after construction, so workers can use it.</summary>
        public PoiPlanner Pois { get; private set; }

        /// <summary>Which of the five regions a column belongs to. Pure and thread-safe.</summary>
        public BiomeMap Biomes { get; private set; }

        /// <summary>
        /// A biome's terrain numbers flattened to plain values. The definitions are
        /// ScriptableObjects and the chunk workers must not touch Unity objects, so
        /// everything the generator needs is copied out on the main thread, once.
        /// </summary>
        struct BiomeProfile
        {
            public ushort Surface;
            public ushort SubSurface;
            public float ClayChance;
            public float BareDirtChance;
            public int SoilDepth;
            public float OreDensity;
            public int OreDepthBonus;
            public float TreeDensity;
            public float ForageDensity;
            public float ScrapDensity;
        }

        readonly BiomeProfile[] _profiles = new BiomeProfile[BiomeIds.Count];

        public TerrainGenerator(int seed, BlockRegistry registry, int worldExtentMetres = 1536,
                                BiomeTable biomes = null)
        {
            _seed = seed;
            registry.Build();
            _air = registry.IdOf(BlockIds.Air);
            _bedrock = registry.IdOf(BlockIds.Bedrock);
            _stone = registry.IdOf(BlockIds.Stone);
            _dirt = registry.IdOf(BlockIds.Dirt);
            _grass = registry.IdOf(BlockIds.Grass);
            _sand = registry.IdOf(BlockIds.Sand);
            _gravel = registry.IdOf(BlockIds.Gravel);
            _coalOre = registry.IdOf(BlockIds.CoalOre);
            _ironOre = registry.IdOf(BlockIds.IronOre);
            _log = registry.IdOf(BlockIds.PineLog);
            _leaves = registry.IdOf(BlockIds.PineNeedles);
            _scrap = registry.IdOf(BlockIds.ScrapHeap);
            _clay = registry.IdOf(BlockIds.Clay);
            _concrete = registry.IdOf(BlockIds.Concrete);
            _planks = registry.IdOf(BlockIds.Planks);
            _ironBlock = registry.IdOf(BlockIds.IronBlock);
            _cobble = registry.IdOf(BlockIds.Cobblestone);
            _wildYucca = registry.IdOf(BlockIds.WildYucca);
            _wildGrain = registry.IdOf(BlockIds.WildGrain);
            _wildCorn = registry.IdOf(BlockIds.WildCorn);

            Biomes = new BiomeMap(seed, worldExtentMetres);
            BuildBiomeProfiles(registry, biomes);

            // The planner samples the raw height field, so it must be built from
            // BaseSurfaceHeight rather than the pad-aware SurfaceHeight below.
            // It also needs the biome map: the two trader outposts are placed in
            // different regions on purpose.
            Pois = new PoiPlanner(seed, worldExtentMetres, BaseSurfaceHeight, Biomes);
        }

        /// <summary>
        /// Flattens the biome table into plain values. Without a table every region
        /// falls back to the pre-biome behaviour, which is what keeps the headless
        /// terrain checks and any mod-free boot working unchanged.
        /// </summary>
        void BuildBiomeProfiles(BlockRegistry registry, BiomeTable table)
        {
            for (int i = 0; i < _profiles.Length; i++)
            {
                _profiles[i] = new BiomeProfile
                {
                    Surface = _grass,
                    SubSurface = _dirt,
                    ClayChance = 0.035f,
                    BareDirtChance = 0.15f,
                    SoilDepth = 4,
                    OreDensity = 1f,
                    OreDepthBonus = 0,
                    TreeDensity = 1f,
                    ForageDensity = 1f,
                    ScrapDensity = 1f
                };
            }

            if (table == null) return;

            for (int i = 0; i < table.biomes.Count; i++)
            {
                var def = table.biomes[i];
                if (def == null) continue;

                int index = (int)def.id;
                if (index < 0 || index >= _profiles.Length) continue;

                _profiles[index] = new BiomeProfile
                {
                    Surface = registry.IdOf(def.surfaceBlockId),
                    SubSurface = registry.IdOf(def.subSurfaceBlockId),
                    ClayChance = def.clayChance,
                    BareDirtChance = def.bareDirtChance,
                    SoilDepth = Mathf.Max(1, def.soilDepth),
                    OreDensity = Mathf.Max(0f, def.oreDensityMultiplier),
                    OreDepthBonus = def.oreDepthBonus,
                    TreeDensity = Mathf.Max(0f, def.treeDensityMultiplier),
                    ForageDensity = Mathf.Max(0f, def.forageDensityMultiplier),
                    ScrapDensity = Mathf.Max(0f, def.scrapDensityMultiplier)
                };
            }
        }

        /// <summary>The biome a column belongs to. Cheap enough to call per column.</summary>
        public BiomeId BiomeAt(int wx, int wz)
        {
            return Biomes.At(wx, wz);
        }

        /// <summary>The biome of a chunk, taken at its centre. This is what gets saved.</summary>
        public BiomeId BiomeOfChunk(ChunkCoord coord)
        {
            var origin = coord.Origin;
            return Biomes.At(origin.x + Chunk.Size / 2, origin.z + Chunk.Size / 2);
        }

        /// <summary>0 = ruined farmland (flat, tilled, few trees), 1 = pine scrub (hilly, wooded).</summary>
        public float ScrubFactor(int wx, int wz)
        {
            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.42f, 0.62f,
                Noise.Fbm2D(wx * 0.0021f, wz * 0.0021f, _seed + 991, 3)));
        }

        /// <summary>
        /// Ground height including any flattened POI pad. Everything outside terrain
        /// generation should use this.
        /// </summary>
        public int SurfaceHeight(int wx, int wz)
        {
            Poi poi;
            if (Pois != null && Pois.TryGetAt(wx, wz, out poi)) return poi.PadY;
            return BaseSurfaceHeight(wx, wz);
        }

        /// <summary>The raw noise height, before POIs level anything.</summary>
        public int BaseSurfaceHeight(int wx, int wz)
        {
            float continent = Noise.Fbm2D(wx * 0.0026f, wz * 0.0026f, _seed + 17, 4);
            float hills = Noise.Fbm2D(wx * 0.0130f, wz * 0.0130f, _seed + 233, 4);
            float detail = Noise.Fbm2D(wx * 0.041f, wz * 0.041f, _seed + 457, 2);

            float scrub = ScrubFactor(wx, wz);
            float relief = Mathf.Lerp(9f, 30f, scrub);

            float h = SeaLevel - 6f
                      + continent * 30f
                      + (hills - 0.5f) * relief
                      + (detail - 0.5f) * Mathf.Lerp(1.5f, 4f, scrub);

            return Mathf.Clamp(Mathf.RoundToInt(h), 4, TerrainWorld.WorldHeight - 40);
        }

        /// <summary>Fills a chunk-sized array. Safe to call from a worker thread.</summary>
        public void Generate(ChunkCoord coord, ushort[] blocks)
        {
            var origin = coord.Origin;
            int ox = origin.x, oy = origin.y, oz = origin.z;

            // Cheap reject: chunks far above the tallest possible column are pure air.
            for (int lz = 0; lz < Chunk.Size; lz++)
            {
                int wz = oz + lz;
                for (int lx = 0; lx < Chunk.Size; lx++)
                {
                    int wx = ox + lx;
                    int surface = SurfaceHeight(wx, wz);
                    float scrub = ScrubFactor(wx, wz);

                    // The biome decides the palette; the fade decides how sharply. Two
                    // biomes meeting blend their surface blocks over the border band
                    // rather than drawing a line you could follow on a map.
                    BiomeId fadeInto;
                    float ownWeight;
                    var biome = Biomes.Sample(wx, wz, out fadeInto, out ownWeight);

                    var profile = _profiles[(int)biome];
                    if (fadeInto != biome && Noise.Hash01(wx, 11, wz, _seed + 7717) > ownWeight)
                    {
                        profile = _profiles[(int)fadeInto];
                    }

                    int soilDepth = profile.SoilDepth - 1 + (int)(Noise.Hash01(wx, 7, wz, _seed + 61) * 3f);

                    for (int ly = 0; ly < Chunk.Size; ly++)
                    {
                        int wy = oy + ly;
                        ushort id = _air;

                        if (wy <= 1)
                        {
                            id = _bedrock;
                        }
                        else if (wy > surface)
                        {
                            id = _air;
                        }
                        else if (wy == surface)
                        {
                            Poi pad;
                            if (Pois != null && Pois.TryGetAt(wx, wz, out pad))
                            {
                                // Made ground: concrete hardstanding at built-up sites,
                                // beaten dirt around the farm ruins.
                                id = pad.Kind == PoiKind.FarmRuin ? _dirt : _concrete;
                            }
                            else
                            {
                                // A shoreline still beats the biome: sand where the land
                                // dips under the water line, whatever region it is in.
                                id = surface <= SeaLevel - 10 ? _sand : profile.Surface;

                                // Worked or weathered ground shows through in patches.
                                if (id == profile.Surface && profile.BareDirtChance > 0f
                                    && Noise.Fbm2D(wx * 0.06f, wz * 0.06f, _seed + 771, 2) > 1f - profile.BareDirtChance)
                                {
                                    id = profile.SubSurface;
                                }
                            }
                        }
                        else if (wy > surface - soilDepth)
                        {
                            id = surface <= SeaLevel - 10 ? _sand : profile.SubSurface;
                            if (Noise.Hash01(wx, wy, wz, _seed + 88) > 1f - profile.ClayChance) id = _clay;
                        }
                        else
                        {
                            id = _stone;
                            if (Noise.Hash01(wx, wy, wz, _seed + 121) > 0.985f) id = _gravel;

                            // A denser biome lowers the threshold rather than sampling
                            // more noise, so ore stays in the same veins and only their
                            // thickness changes. Dry flats also lift the whole band, which
                            // is what "ore at the surface" means here.
                            int coalCeiling = 74 + profile.OreDepthBonus;
                            int ironCeiling = 46 + profile.OreDepthBonus;

                            float coalCut = 1f - (1f - 0.795f) * profile.OreDensity;
                            float ironCut = 1f - (1f - 0.820f) * profile.OreDensity;

                            if (wy < coalCeiling && Noise.Fbm3D(wx * 0.055f, wy * 0.075f, wz * 0.055f, _seed + 313, 2) > coalCut)
                                id = _coalOre;
                            if (wy < ironCeiling && Noise.Fbm3D(wx * 0.062f, wy * 0.080f, wz * 0.062f, _seed + 509, 2) > ironCut)
                                id = _ironOre;
                        }

                        // Caves. Two ridged sheets intersecting gives tunnels rather than blobs.
                        if (id != _air && id != _bedrock && wy > 3 && wy < surface - 1)
                        {
                            float a = Noise.Ridged3D(wx * 0.017f, wy * 0.030f, wz * 0.017f, _seed + 1201, 2);
                            float b = Noise.Ridged3D(wx * 0.017f, wy * 0.030f, wz * 0.017f, _seed + 1303, 2);
                            float taper = Mathf.InverseLerp(surface - 1f, surface - 9f, wy);
                            if (a > 0.945f && b > 0.925f && taper > 0.05f) id = _air;
                        }

                        blocks[Chunk.Index(lx, ly, lz)] = id;
                    }
                }
            }

            ApplySurfaceScrap(coord, blocks);
            ApplyForage(coord, blocks);
            ApplyPois(coord, blocks);
            ApplyTrees(coord, blocks);
        }

        /// <summary>Rusted heaps poking out of the farmland: the early metal source.</summary>
        void ApplySurfaceScrap(ChunkCoord coord, ushort[] blocks)
        {
            var origin = coord.Origin;
            for (int lz = 0; lz < Chunk.Size; lz++)
            {
                int wz = origin.z + lz;
                for (int lx = 0; lx < Chunk.Size; lx++)
                {
                    int wx = origin.x + lx;

                    float scrapCut = 1f - (1f - 0.9955f) * _profiles[(int)Biomes.At(wx, wz)].ScrapDensity;
                    if (Noise.Hash01(wx, 3, wz, _seed + 4409) < scrapCut) continue;

                    Poi ignored;
                    if (Pois != null && Pois.TryGetAt(wx, wz, out ignored)) continue;

                    int surface = SurfaceHeight(wx, wz);
                    int height = 1 + (int)(Noise.Hash01(wx, 4, wz, _seed + 4410) * 2.99f);
                    for (int i = 0; i < height; i++)
                    {
                        Put(blocks, coord, wx, surface + i, wz, _scrap, true);
                    }
                }
            }
        }

        void ApplyTrees(ChunkCoord coord, ushort[] blocks)
        {
            var origin = coord.Origin;
            int minCellX = ChunkCoord.FloorDiv(origin.x - 3, TreeCell);
            int maxCellX = ChunkCoord.FloorDiv(origin.x + Chunk.Size + 3, TreeCell);
            int minCellZ = ChunkCoord.FloorDiv(origin.z - 3, TreeCell);
            int maxCellZ = ChunkCoord.FloorDiv(origin.z + Chunk.Size + 3, TreeCell);

            for (int cz = minCellZ; cz <= maxCellZ; cz++)
            {
                for (int cx = minCellX; cx <= maxCellX; cx++)
                {
                    uint h = Noise.Hash(cx, 5, cz, _seed + 6607);
                    int tx = cx * TreeCell + (int)(h % TreeCell);
                    int tz = cz * TreeCell + (int)((h >> 8) % TreeCell);

                    Poi onPoi;
                    if (Pois != null && Pois.TryGetAt(tx, tz, out onPoi)) continue;

                    float scrub = ScrubFactor(tx, tz);
                    float density = Mathf.Lerp(0.06f, 0.55f, scrub)
                                  * _profiles[(int)Biomes.At(tx, tz)].TreeDensity;
                    if (Noise.Hash01(tx, 6, tz, _seed + 6608) > density) continue;

                    int surface = SurfaceHeight(tx, tz);
                    if (surface <= SeaLevel - 8 || surface > TerrainWorld.WorldHeight - 48) continue;

                    // Skip trees on steep ground so they do not hang off cliffs.
                    if (Mathf.Abs(SurfaceHeight(tx + 1, tz) - surface) > 2) continue;
                    if (Mathf.Abs(SurfaceHeight(tx, tz + 1) - surface) > 2) continue;

                    BuildPine(blocks, coord, tx, surface + 1, tz, (int)((h >> 16) % 4u));
                }
            }
        }

        void BuildPine(ushort[] blocks, ChunkCoord coord, int wx, int baseY, int wz, int variant)
        {
            int trunk = 4 + variant;
            for (int i = 0; i < trunk; i++)
            {
                Put(blocks, coord, wx, baseY + i, wz, _log, false);
            }

            // Tapered needle cone, widest two blocks below the tip.
            int top = baseY + trunk;
            for (int layer = 0; layer < 4; layer++)
            {
                int y = top - layer;
                int radius = layer == 0 ? 0 : (layer == 3 ? 2 : 1);
                for (int dz = -radius; dz <= radius; dz++)
                {
                    for (int dx = -radius; dx <= radius; dx++)
                    {
                        if (Mathf.Abs(dx) == radius && Mathf.Abs(dz) == radius && radius > 1) continue;
                        Put(blocks, coord, wx + dx, y, wz + dz, _leaves, true);
                    }
                }
            }
            Put(blocks, coord, wx, top + 1, wz, _leaves, true);
        }


        // ------------------------------------------------------------------- POIs

        [System.ThreadStatic] static List<Poi> _poiScratch;

        /// <summary>
        /// Stamps any point of interest overlapping this chunk. The surface under a POI
        /// is already flat because SurfaceHeight returns the pad level there, so these
        /// only have to build upward.
        /// </summary>
        /// <summary>
        /// Wild yucca, grain and corn on open ground. These are the only seed source
        /// before a trader, so they have to be common enough to find on a first walk -
        /// but they are the ruined-farmland flavour, so they cluster where the scrub
        /// thins out.
        /// </summary>
        void ApplyForage(ChunkCoord coord, ushort[] blocks)
        {
            var origin = coord.Origin;

            for (int lz = 0; lz < Chunk.Size; lz++)
            {
                int wz = origin.z + lz;
                for (int lx = 0; lx < Chunk.Size; lx++)
                {
                    int wx = origin.x + lx;

                    Poi ignored;
                    if (Pois != null && Pois.TryGetAt(wx, wz, out ignored)) continue;

                    float scrub = ScrubFactor(wx, wz);
                    // Old fields grow food; deep pine scrub mostly does not.
                    float density = Mathf.Lerp(0.030f, 0.006f, scrub)
                                  * _profiles[(int)Biomes.At(wx, wz)].ForageDensity;
                    if (Noise.Hash01(wx, 9, wz, _seed + 5107) > density) continue;

                    int surface = SurfaceHeight(wx, wz);
                    if (surface <= SeaLevel - 6) continue;

                    // Patches rather than confetti: the local hash picks the species.
                    uint pick = Noise.Hash(wx / 6, 11, wz / 6, _seed + 5113) % 3u;
                    ushort plant = pick == 0u ? _wildYucca : (pick == 1u ? _wildGrain : _wildCorn);
                    Put(blocks, coord, wx, surface + 1, wz, plant, true);
                }
            }
        }

        void ApplyPois(ChunkCoord coord, ushort[] blocks)
        {
            if (Pois == null) return;

            if (_poiScratch == null) _poiScratch = new List<Poi>(4);
            _poiScratch.Clear();

            var origin = coord.Origin;
            Pois.Overlapping(origin.x, origin.z, origin.x + Chunk.Size - 1, origin.z + Chunk.Size - 1, _poiScratch);
            if (_poiScratch.Count == 0) return;

            for (int i = 0; i < _poiScratch.Count; i++)
            {
                var poi = _poiScratch[i];
                switch (poi.Kind)
                {
                    case PoiKind.FarmRuin: StampFarmRuin(coord, blocks, poi); break;
                    case PoiKind.TownFragment: StampTownFragment(coord, blocks, poi); break;
                    case PoiKind.TraderOutpost: StampTraderOutpost(coord, blocks, poi); break;
                }
            }
        }

        /// <summary>A collapsed barn: plank walls with holes, a scrap pile, salvage.</summary>
        void StampFarmRuin(ChunkCoord coord, ushort[] blocks, Poi poi)
        {
            int y = poi.PadY;

            // Perimeter, eaten away so it reads as ruined rather than built.
            for (int wz = poi.MinZ; wz <= poi.MaxZ; wz++)
            {
                for (int wx = poi.MinX; wx <= poi.MaxX; wx++)
                {
                    bool edge = wx == poi.MinX || wx == poi.MaxX || wz == poi.MinZ || wz == poi.MaxZ;
                    if (!edge) continue;

                    int height = 4;
                    for (int h = 1; h <= height; h++)
                    {
                        // Higher courses fall away first.
                        float survive = 1f - h * 0.16f;
                        if (Noise.Hash01(wx, h, wz, _seed + 3301) > survive) continue;
                        Put(blocks, coord, wx, y + h, wz, _planks, false);
                    }
                }
            }

            // Corner posts hold the silhouette together.
            PutColumn(coord, blocks, poi.MinX, y + 1, poi.MinZ, 5, _log);
            PutColumn(coord, blocks, poi.MaxX, y + 1, poi.MinZ, 5, _log);
            PutColumn(coord, blocks, poi.MinX, y + 1, poi.MaxZ, 5, _log);
            PutColumn(coord, blocks, poi.MaxX, y + 1, poi.MaxZ, 5, _log);

            // Salvage worth the walk.
            for (int i = 0; i < 6; i++)
            {
                uint h = Noise.Hash(poi.CentreX + i, 41, poi.CentreZ - i, _seed + 3307);
                int sx = poi.MinX + 2 + (int)(h % (uint)Mathf.Max(1, poi.HalfX * 2 - 3));
                int sz = poi.MinZ + 2 + (int)((h >> 8) % (uint)Mathf.Max(1, poi.HalfZ * 2 - 3));
                PutColumn(coord, blocks, sx, y + 1, sz, 1 + (int)((h >> 16) % 2u), _scrap);
            }
        }

        /// <summary>Three concrete shells and a strip of road.</summary>
        void StampTownFragment(ChunkCoord coord, ushort[] blocks, Poi poi)
        {
            int y = poi.PadY;

            // Road down the middle, one block proud so it reads from a distance.
            for (int wz = poi.MinZ; wz <= poi.MaxZ; wz++)
            {
                for (int wx = poi.CentreX - 3; wx <= poi.CentreX + 3; wx++)
                {
                    Put(blocks, coord, wx, y, wz, _cobble, false);
                }
            }

            // Shells either side of the road.
            StampShell(coord, blocks, poi, poi.CentreX - 13, poi.CentreZ - 12, 9, 9, 7, y, 0);
            StampShell(coord, blocks, poi, poi.CentreX + 5, poi.CentreZ - 8, 10, 11, 9, y, 1);
            StampShell(coord, blocks, poi, poi.CentreX - 12, poi.CentreZ + 3, 11, 10, 6, y, 2);

            // Rubble.
            for (int i = 0; i < 14; i++)
            {
                uint h = Noise.Hash(poi.CentreX - i, 57, poi.CentreZ + i, _seed + 3407);
                int sx = poi.MinX + (int)(h % (uint)(poi.HalfX * 2));
                int sz = poi.MinZ + (int)((h >> 8) % (uint)(poi.HalfZ * 2));
                Put(blocks, coord, sx, y + 1, sz, (h >> 16) % 3u == 0u ? _scrap : _gravel, true);
            }
        }

        /// <summary>A walled compound with a gate and a strongroom. Phase 1 puts a trader inside.</summary>
        void StampTraderOutpost(ChunkCoord coord, ushort[] blocks, Poi poi)
        {
            int y = poi.PadY;
            const int wallHeight = 5;

            for (int wz = poi.MinZ; wz <= poi.MaxZ; wz++)
            {
                for (int wx = poi.MinX; wx <= poi.MaxX; wx++)
                {
                    bool edge = wx == poi.MinX || wx == poi.MaxX || wz == poi.MinZ || wz == poi.MaxZ;
                    if (!edge) continue;

                    // Gate on the south wall.
                    bool gate = wz == poi.MaxZ && wx >= poi.CentreX - 2 && wx <= poi.CentreX + 2;
                    int height = gate ? 0 : wallHeight;
                    for (int h = 1; h <= height; h++) Put(blocks, coord, wx, y + h, wz, _concrete, false);
                }
            }

            // Corner towers, so the outpost is visible over the treeline.
            PutColumn(coord, blocks, poi.MinX, y + 1, poi.MinZ, wallHeight + 4, _concrete);
            PutColumn(coord, blocks, poi.MaxX, y + 1, poi.MinZ, wallHeight + 4, _concrete);
            PutColumn(coord, blocks, poi.MinX, y + 1, poi.MaxZ, wallHeight + 4, _concrete);
            PutColumn(coord, blocks, poi.MaxX, y + 1, poi.MaxZ, wallHeight + 4, _concrete);

            // Strongroom: iron walls, a door gap facing the gate.
            int hx = 5, hz = 4;
            for (int wz = poi.CentreZ - hz; wz <= poi.CentreZ + hz; wz++)
            {
                for (int wx = poi.CentreX - hx; wx <= poi.CentreX + hx; wx++)
                {
                    bool edge = wx == poi.CentreX - hx || wx == poi.CentreX + hx
                             || wz == poi.CentreZ - hz || wz == poi.CentreZ + hz;
                    if (!edge) continue;

                    bool doorway = wz == poi.CentreZ + hz && wx >= poi.CentreX - 1 && wx <= poi.CentreX + 1;
                    int height = doorway ? 0 : 4;
                    for (int h = 1; h <= height; h++) Put(blocks, coord, wx, y + h, wz, _ironBlock, false);
                }
            }

            // Roof over the strongroom.
            for (int wz = poi.CentreZ - hz; wz <= poi.CentreZ + hz; wz++)
            {
                for (int wx = poi.CentreX - hx; wx <= poi.CentreX + hx; wx++)
                {
                    Put(blocks, coord, wx, y + 5, wz, _ironBlock, false);
                }
            }
        }

        /// <summary>A hollow building shell with one doorway.</summary>
        void StampShell(ChunkCoord coord, ushort[] blocks, Poi poi,
                        int minX, int minZ, int sizeX, int sizeZ, int height, int y, int variant)
        {
            int maxX = minX + sizeX;
            int maxZ = minZ + sizeZ;
            int doorX = minX + sizeX / 2;

            for (int wz = minZ; wz <= maxZ; wz++)
            {
                for (int wx = minX; wx <= maxX; wx++)
                {
                    bool edge = wx == minX || wx == maxX || wz == minZ || wz == maxZ;
                    if (!edge) continue;

                    for (int h = 1; h <= height; h++)
                    {
                        // Doorway on the south face.
                        if (wz == maxZ && wx >= doorX - 1 && wx <= doorX + 1 && h <= 3) continue;

                        // Window band, punched every third block.
                        if (h == 3 && wx % 3 == 0 && !(wx == minX || wx == maxX)) continue;

                        // The top courses are partly collapsed.
                        if (h > height - 2 && Noise.Hash01(wx, h, wz, _seed + 3511 + variant) > 0.55f) continue;

                        Put(blocks, coord, wx, y + h, wz, _concrete, false);
                    }
                }
            }
        }

        void PutColumn(ChunkCoord coord, ushort[] blocks, int wx, int y, int wz, int height, ushort id)
        {
            for (int i = 0; i < height; i++) Put(blocks, coord, wx, y + i, wz, id, false);
        }

        void Put(ushort[] blocks, ChunkCoord coord, int wx, int wy, int wz, ushort id, bool onlyReplaceAir)
        {
            var origin = coord.Origin;
            int lx = wx - origin.x;
            int ly = wy - origin.y;
            int lz = wz - origin.z;
            if (lx < 0 || lx >= Chunk.Size || ly < 0 || ly >= Chunk.Size || lz < 0 || lz >= Chunk.Size) return;

            int i = Chunk.Index(lx, ly, lz);
            if (onlyReplaceAir && blocks[i] != _air) return;
            blocks[i] = id;
        }

        /// <summary>Safe standing height for spawning the player or a trader.</summary>
        public int SpawnHeight(int wx, int wz)
        {
            return SurfaceHeight(wx, wz) + 1;
        }
    }
}
