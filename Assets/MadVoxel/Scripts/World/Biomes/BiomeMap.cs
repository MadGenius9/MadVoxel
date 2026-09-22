using MadVoxel.Core;
using UnityEngine;

namespace MadVoxel.World.Biomes
{
    /// <summary>
    /// Paints the five biomes onto the finite map. Deterministic from the seed, pure,
    /// and safe to call from the chunk workers - it touches no Unity object and holds
    /// no state beyond the seed.
    ///
    /// It is a warped Worley diagram rather than a noise threshold: sites sit on a
    /// jittered grid, a query takes the nearest one, and the query point is pushed
    /// around by low-frequency noise first so the borders wander instead of drawing
    /// straight bisectors. Only the nine grid cells around a query are ever tested, so
    /// the cost does not grow with the map.
    ///
    /// Two placements are forced rather than random, because they are design, not
    /// scenery: the cell containing the origin is always Farmland (you wake up on good
    /// dirt), and everything past the far edge band is Frost Shelf (small, and at the
    /// edge where you have to choose to go).
    /// </summary>
    public class BiomeMap
    {
        /// <summary>Metres across one site cell. A blob ends up roughly this wide.</summary>
        public const float SiteSpacing = 600f;

        /// <summary>How far the border smears. The brief asks for 30-50 m.</summary>
        public const float FadeMetres = 40f;

        /// <summary>How hard the noise pushes a query off the straight bisector.</summary>
        const float WarpMetres = 105f;

        /// <summary>Past this fraction of the map extent, the shelf takes over.</summary>
        const float FrostBand = 0.74f;

        /// <summary>
        /// Every site this close to the origin is farmland, whichever cell it belongs
        /// to. Forcing only the origin's own cell is not enough: sites are jittered
        /// inside their cells, so a neighbour's site can easily end up nearer to spawn
        /// than the origin cell's own - and then you wake up on hardpan.
        /// </summary>
        const float StarterRadius = 340f;

        readonly int _seed;
        readonly float _extent;

        public BiomeMap(int seed, int worldExtentMetres)
        {
            _seed = seed;
            _extent = Mathf.Max(SiteSpacing, worldExtentMetres);
        }

        /// <summary>The biome at a world column.</summary>
        public BiomeId At(int wx, int wz)
        {
            BiomeId second;
            float weight;
            return Sample(wx, wz, out second, out weight);
        }

        /// <summary>
        /// The biome at a column, plus the one it is fading into and how far through
        /// the fade this column is. <paramref name="weight"/> is 1 deep inside a biome
        /// and falls towards 0.5 at the exact border, so a caller can lerp block
        /// palettes and yields across the seam instead of stepping.
        /// </summary>
        public BiomeId Sample(int wx, int wz, out BiomeId second, out float weight)
        {
            float px = wx, pz = wz;
            Warp(ref px, ref pz);

            int cellX = Mathf.FloorToInt(px / SiteSpacing);
            int cellZ = Mathf.FloorToInt(pz / SiteSpacing);

            float bestSq = float.MaxValue, secondSq = float.MaxValue;
            BiomeId best = BiomeId.Farmland, runnerUp = BiomeId.Farmland;

            for (int dz = -1; dz <= 1; dz++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    int sx = cellX + dx, sz = cellZ + dz;

                    float siteX, siteZ;
                    SitePosition(sx, sz, out siteX, out siteZ);

                    float ddx = siteX - px, ddz = siteZ - pz;
                    float distSq = ddx * ddx + ddz * ddz;
                    if (distSq >= secondSq) continue;

                    var kind = KindOf(sx, sz, siteX, siteZ);
                    if (distSq < bestSq)
                    {
                        secondSq = bestSq;
                        runnerUp = best;
                        bestSq = distSq;
                        best = kind;
                    }
                    else
                    {
                        secondSq = distSq;
                        runnerUp = kind;
                    }
                }
            }

            second = runnerUp;

            // Distance to the bisector, not to the site: two sites 40 m apart should not
            // read as a 40 m fade just because one of them is far away.
            float gap = Mathf.Sqrt(secondSq) - Mathf.Sqrt(bestSq);
            weight = Mathf.Clamp01(0.5f + 0.5f * Mathf.Clamp01(gap / FadeMetres));
            return best;
        }

        /// <summary>True when a column sits inside the smeared border between two biomes.</summary>
        public bool IsInFade(int wx, int wz)
        {
            BiomeId second;
            float weight;
            var here = Sample(wx, wz, out second, out weight);
            return second != here && weight < 0.999f;
        }

        void Warp(ref float x, ref float z)
        {
            float wx = Noise.Fbm2D(x * 0.00085f, z * 0.00085f, _seed + 8821, 3) - 0.5f;
            float wz = Noise.Fbm2D(x * 0.00085f, z * 0.00085f, _seed + 9137, 3) - 0.5f;
            x += wx * 2f * WarpMetres;
            z += wz * 2f * WarpMetres;
        }

        void SitePosition(int cellX, int cellZ, out float x, out float z)
        {
            // The origin's site sits exactly on spawn rather than somewhere inside its
            // cell, so the ground you wake up on is the middle of a blob and not its
            // ragged edge.
            if (cellX == 0 && cellZ == 0)
            {
                x = 0f;
                z = 0f;
                return;
            }

            // Jitter is kept off the cell edges so two sites cannot land on top of each
            // other and produce a sliver biome nobody can find.
            float jx = Noise.Hash01(cellX, 71, cellZ, _seed + 5501) * 0.62f + 0.19f;
            float jz = Noise.Hash01(cellX, 73, cellZ, _seed + 5503) * 0.62f + 0.19f;

            x = (cellX + jx) * SiteSpacing;
            z = (cellZ + jz) * SiteSpacing;
        }

        /// <summary>Which biome a site carries. Two cases are forced; the rest is weighted.</summary>
        BiomeId KindOf(int cellX, int cellZ, float siteX, float siteZ)
        {
            // You wake up on good dirt, and so does everything within a walk of it.
            // Non-negotiable: the first garden has to go somewhere.
            if (siteX * siteX + siteZ * siteZ <= StarterRadius * StarterRadius) return BiomeId.Farmland;

            // The shelf is a band at one edge, not a blob in the middle of your commute.
            if (siteZ > _extent * FrostBand) return BiomeId.FrostShelf;

            float roll = Noise.Hash01(cellX, 17, cellZ, _seed + 6607);

            if (roll < 0.34f) return BiomeId.PineScrub;
            if (roll < 0.62f) return BiomeId.ClayHills;
            if (roll < 0.90f) return BiomeId.DryFlats;
            return BiomeId.Farmland;
        }
    }
}
