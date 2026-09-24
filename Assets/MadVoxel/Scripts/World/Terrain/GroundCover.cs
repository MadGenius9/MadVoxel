using System.Collections.Generic;
using UnityEngine;

namespace MadVoxel.World.Terrain
{
    /// <summary>One tuft the cover mesher has been asked to draw.</summary>
    public struct CoverTuft
    {
        public float X, Y, Z;
        /// <summary>Metres. Already bucketed by the caller so a patch rebuilds rarely.</summary>
        public float Height;
        /// <summary>Which palette the tuft draws from - grass, scrub, dry.</summary>
        public byte Kind;
    }

    /// <summary>
    /// The grass.
    ///
    /// Nothing in this project moved the look as far as this will, and it is worth
    /// being clear why: in every screenshot of the game this one is chasing, the thing
    /// doing the work is not the terrain mesh or the lighting - it is that the ground
    /// is covered in small plants. A bare mesh reads as a level. The same mesh with
    /// grass on it reads as a place. The geometry underneath is identical.
    ///
    /// Deliberately not Unity's terrain detail system, which cannot be used here: that
    /// paints onto a heightmap, and this world is a destructible voxel volume with no
    /// heightmap to paint on. So tufts are scattered from a hash of world position -
    /// no stored state, nothing to save, and a patch dug up and refilled comes back
    /// exactly as it was.
    ///
    /// Pure and engine-free. Whether grass looks good is a question for a playtest,
    /// but whether it is stable - whether the same ground produces the same grass
    /// every time it is meshed, which is the difference between a field and a field
    /// that crawls - is arithmetic.
    /// </summary>
    public static class GroundCover
    {
        /// <summary>Metres square. Matches the field cover's patches, and a chunk's footprint.</summary>
        public const int PatchSize = 16;

        /// <summary>
        /// Tufts per square metre, before anything thins them.
        ///
        /// Three is enough to read as cover at a distance without turning a hillside
        /// into a wall of quads: a patch is 16m square, so this is around 750 tufts
        /// and 1,500 quads for a patch, and a patch is only built when it is close.
        /// </summary>
        public const int PerMetre = 3;

        /// <summary>Metres. Past this nothing is scattered at all.</summary>
        public const float DrawDistance = 56f;

        /// <summary>Where the tufts thin out towards the edge of the draw distance.</summary>
        public const float FadeStart = 40f;

        public const byte KindGrass = 0;
        public const byte KindScrub = 1;
        public const byte KindDry = 2;

        /// <summary>Deterministic 0..1 from a position and a channel.</summary>
        public static float Hash(int x, int z, int channel)
        {
            unchecked
            {
                uint h = (uint)(x * 73856093) ^ (uint)(z * 19349663) ^ (uint)(channel * 83492791);
                h ^= h >> 13;
                h *= 0x85EBCA6B;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / (float)0xFFFFFF;
            }
        }

        /// <summary>
        /// How much of the full density survives at this range.
        ///
        /// Thinning with distance rather than cutting off at it: a hard edge sweeping
        /// across a hillside as the player walks is far more noticeable than sparse
        /// grass in the distance, which just looks like distance.
        /// </summary>
        public static float DensityAt(float distance)
        {
            if (distance <= FadeStart) return 1f;
            if (distance >= DrawDistance) return 0f;
            return 1f - Mathf.InverseLerp(FadeStart, DrawDistance, distance);
        }

        /// <summary>
        /// Scatters tufts across one square metre of ground.
        ///
        /// <paramref name="surfaceY"/> is the top of the ground at this cell, and
        /// <paramref name="kind"/> what is growing there. Returns how many were added,
        /// which is zero for bare rock and for ground the caller has thinned away.
        /// </summary>
        public static int Scatter(int cellX, int cellZ, float surfaceY, byte kind, float density,
                                  IList<CoverTuft> into)
        {
            if (into == null || density <= 0f) return 0;

            int wanted = Mathf.RoundToInt(PerMetre * Mathf.Clamp01(density));
            if (wanted <= 0) return 0;

            int added = 0;
            for (int i = 0; i < wanted; i++)
            {
                // Each blade gets its own channel, so thinning removes the same blades
                // every time rather than reshuffling the whole tuft as you walk.
                float fx = Hash(cellX, cellZ, 11 + i * 3);
                float fz = Hash(cellX, cellZ, 12 + i * 3);
                float fh = Hash(cellX, cellZ, 13 + i * 3);

                into.Add(new CoverTuft
                {
                    X = cellX + fx,
                    Y = surfaceY,
                    Z = cellZ + fz,
                    Height = HeightFor(kind, fh),
                    Kind = kind
                });
                added++;
            }

            return added;
        }

        /// <summary>
        /// Blade height, bucketed to eight steps.
        ///
        /// Bucketed because the height is part of a patch's rebuild signature, and a
        /// continuous value would mean any change anywhere rebuilt the patch. Eight
        /// steps is more variation than the eye can count at ten metres.
        /// </summary>
        public static float HeightFor(byte kind, float roll)
        {
            float lo, hi;
            switch (kind)
            {
                case KindScrub: lo = 0.35f; hi = 0.8f; break;
                case KindDry: lo = 0.12f; hi = 0.3f; break;
                default: lo = 0.18f; hi = 0.5f; break;
            }

            int bucket = Mathf.Clamp(Mathf.FloorToInt(roll * 8f), 0, 7);
            return Mathf.Lerp(lo, hi, bucket / 7f);
        }

        /// <summary>The patch a cell belongs to.</summary>
        public static Vector2Int PatchOf(int cellX, int cellZ)
        {
            return new Vector2Int(FloorDiv(cellX, PatchSize), FloorDiv(cellZ, PatchSize));
        }

        static int FloorDiv(int value, int size)
        {
            return value >= 0 ? value / size : ((value + 1) / size) - 1;
        }
    }
}
