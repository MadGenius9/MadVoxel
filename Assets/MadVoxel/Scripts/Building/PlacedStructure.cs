using System.Collections.Generic;
using MadVoxel.Core;
using UnityEngine;

namespace MadVoxel.Building
{
    /// <summary>
    /// A built snap piece in the world. Owns its cells, health and placeholder visuals;
    /// the behaviour-specific bits live in small sibling components.
    /// </summary>
    public class PlacedStructure : MonoBehaviour, IDamageable
    {
        public StructureDefinition Definition { get; private set; }
        public Vector3Int Cell { get; private set; }
        public int RotationSteps { get; private set; }
        public float Health { get; private set; }
        public StructureWorld Owner { get; private set; }

        readonly List<Vector3Int> _cells = new List<Vector3Int>();

        public IReadOnlyList<Vector3Int> OccupiedCells { get { return _cells; } }
        public bool IsAlive { get { return Health > 0f; } }
        public float HealthFraction { get { return Definition != null ? Mathf.Clamp01(Health / Definition.maxHealth) : 0f; } }

        public void Initialise(StructureWorld owner, StructureDefinition def, Vector3Int cell, int rotationSteps, float health)
        {
            Owner = owner;
            Definition = def;
            Cell = cell;
            RotationSteps = ((rotationSteps % 4) + 4) % 4;
            Health = health > 0f ? health : def.maxHealth;

            name = def.displayName + " " + cell;
            // The root stays axis-aligned and anchored to the cell corner; rotation is
            // applied about the footprint centre by the visual pivot, so a rotated piece
            // still occupies exactly the cells it reserved.
            transform.position = new Vector3(cell.x, cell.y, cell.z);
            transform.rotation = Quaternion.identity;

            _cells.Clear();
            var size = RotatedFootprint(def.footprint, RotationSteps);
            for (int y = 0; y < Mathf.Max(1, size.y); y++)
            for (int z = 0; z < Mathf.Max(1, size.z); z++)
            for (int x = 0; x < Mathf.Max(1, size.x); x++)
            {
                _cells.Add(new Vector3Int(cell.x + x, cell.y + y, cell.z + z));
            }

            StructureVisuals.Build(this);
            AttachBehaviour();
        }

        public static Vector3Int RotatedFootprint(Vector3Int footprint, int rotationSteps)
        {
            if (rotationSteps % 2 == 0) return footprint;
            return new Vector3Int(footprint.z, footprint.y, footprint.x);
        }

        void AttachBehaviour()
        {
            switch (Definition.kind)
            {
                case StructureKind.Door: gameObject.AddComponent<DoorStructure>().Bind(this); break;
                case StructureKind.Storage: gameObject.AddComponent<StorageStructure>().Bind(this); break;
                case StructureKind.CraftStation: gameObject.AddComponent<CraftStationStructure>().Bind(this); break;
                case StructureKind.Campfire: gameObject.AddComponent<CampfireStructure>().Bind(this); break;
                case StructureKind.ClaimStake: gameObject.AddComponent<ClaimStakeStructure>().Bind(this); break;
                case StructureKind.Ladder: gameObject.AddComponent<LadderStructure>().Bind(this); break;
                case StructureKind.Bedroll: gameObject.AddComponent<BedrollStructure>().Bind(this); break;
            }
        }

        public void ApplyDamage(DamageInfo info)
        {
            if (!IsAlive) return;
            Health -= info.Amount;
            StructureVisuals.ShowDamage(this);
            if (Health <= 0f && Owner != null) Owner.Destroy(this, true);
        }

        public void Repair(float amount)
        {
            Health = Mathf.Min(Definition.maxHealth, Health + amount);
            StructureVisuals.ShowDamage(this);
        }

        public void SetHealthDirect(float health)
        {
            Health = health;
        }
    }
}
