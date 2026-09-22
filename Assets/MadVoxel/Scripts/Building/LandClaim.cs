using System.Collections.Generic;
using UnityEngine;

namespace MadVoxel.Building
{
    /// <summary>A claimed radius around a stake. In single player this is "your base".</summary>
    public class LandClaim
    {
        public Vector3Int Cell;
        public float Radius = 24f;
        public PlacedStructure Stake;

        public Vector3 Centre
        {
            get { return new Vector3(Cell.x + 0.5f, Cell.y + 0.5f, Cell.z + 0.5f); }
        }

        public bool Contains(Vector3 point)
        {
            Vector3 d = point - Centre;
            d.y *= 0.4f; // claims are wider than they are tall
            return d.sqrMagnitude <= Radius * Radius;
        }
    }

    /// <summary>
    /// Every active claim. Outside a horde night the AI leaves claimed ground alone; on
    /// a blood moon the claim is exactly what the horde walks towards.
    /// </summary>
    public class LandClaimRegistry
    {
        readonly List<LandClaim> _claims = new List<LandClaim>();

        public IReadOnlyList<LandClaim> Claims { get { return _claims; } }
        public bool Any { get { return _claims.Count > 0; } }

        public void Register(LandClaim claim)
        {
            if (claim != null && !_claims.Contains(claim)) _claims.Add(claim);
        }

        public void Unregister(LandClaim claim)
        {
            if (claim != null) _claims.Remove(claim);
        }

        public void Clear()
        {
            _claims.Clear();
        }

        public bool IsProtected(Vector3 point)
        {
            for (int i = 0; i < _claims.Count; i++)
            {
                if (_claims[i].Contains(point)) return true;
            }
            return false;
        }

        public LandClaim Nearest(Vector3 point)
        {
            LandClaim best = null;
            float bestDist = float.MaxValue;
            for (int i = 0; i < _claims.Count; i++)
            {
                float d = (_claims[i].Centre - point).sqrMagnitude;
                if (d < bestDist) { bestDist = d; best = _claims[i]; }
            }
            return best;
        }
    }
}
