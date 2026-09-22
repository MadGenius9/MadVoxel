using System;
using UnityEngine;

namespace MadVoxel.World.Terrain
{
    [Serializable]
    public struct ChunkCoord : IEquatable<ChunkCoord>
    {
        public int X;
        public int Y;
        public int Z;

        public ChunkCoord(int x, int y, int z)
        {
            X = x; Y = y; Z = z;
        }

        public Vector3Int Origin
        {
            get { return new Vector3Int(X * Chunk.Size, Y * Chunk.Size, Z * Chunk.Size); }
        }

        public Vector3 WorldCentre
        {
            get { return new Vector3((X + 0.5f) * Chunk.Size, (Y + 0.5f) * Chunk.Size, (Z + 0.5f) * Chunk.Size); }
        }

        public static ChunkCoord FromWorld(int wx, int wy, int wz)
        {
            return new ChunkCoord(FloorDiv(wx, Chunk.Size), FloorDiv(wy, Chunk.Size), FloorDiv(wz, Chunk.Size));
        }

        public static ChunkCoord FromWorld(Vector3 p)
        {
            return FromWorld(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.y), Mathf.FloorToInt(p.z));
        }

        public static int FloorDiv(int a, int b)
        {
            int q = a / b;
            if ((a % b != 0) && ((a < 0) != (b < 0))) q--;
            return q;
        }

        public static int FloorMod(int a, int b)
        {
            int r = a % b;
            if (r != 0 && ((r < 0) != (b < 0))) r += b;
            return r;
        }

        public ChunkCoord Offset(int dx, int dy, int dz)
        {
            return new ChunkCoord(X + dx, Y + dy, Z + dz);
        }

        public bool Equals(ChunkCoord other)
        {
            return X == other.X && Y == other.Y && Z == other.Z;
        }

        public override bool Equals(object obj)
        {
            return obj is ChunkCoord && Equals((ChunkCoord)obj);
        }

        public override int GetHashCode()
        {
            // A plain XOR of per-axis products collides structurally for neighbouring
            // chunks, which is exactly the access pattern here, so mix properly.
            unchecked
            {
                uint h = (uint)X * 2654435761u;
                h ^= (uint)Y * 2246822519u;
                h = (h << 13) | (h >> 19);
                h ^= (uint)Z * 3266489917u;
                h ^= h >> 15;
                h *= 2654435761u;
                h ^= h >> 13;
                return (int)h;
            }
        }

        public override string ToString()
        {
            return string.Format("({0},{1},{2})", X, Y, Z);
        }
    }
}
