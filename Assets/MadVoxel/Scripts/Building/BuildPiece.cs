using MadVoxel.Core;
using UnityEngine;

namespace MadVoxel.Building
{
    /// <summary>
    /// A placed snap piece. Doors and hatches are the same class with a hinge child, so
    /// the grid only ever has one kind of occupant per slot.
    /// </summary>
    public class BuildPiece : MonoBehaviour, IDamageable, IInteractable
    {
        public BuildPieceDefinition Definition { get; private set; }
        public BuildAddress Address { get; private set; }
        public BuildingWorld Owner { get; private set; }
        public float Health { get; private set; }
        public bool IsOpen { get; private set; }

        Transform _hinge;

        public bool IsAlive { get { return Health > 0f; } }
        public float HealthFraction { get { return Definition != null ? Mathf.Clamp01(Health / Definition.maxHealth) : 0f; } }
        public BuildTier Tier { get { return Definition != null ? Definition.tier : BuildTier.Twig; } }

        public bool IsOpenable
        {
            get
            {
                return Definition != null &&
                       (Definition.kind == BuildPieceKind.Door || Definition.kind == BuildPieceKind.Hatch);
            }
        }

        public void Initialise(BuildingWorld owner, BuildPieceDefinition definition, BuildAddress address, float health)
        {
            Owner = owner;
            Definition = definition;
            Address = address;
            Health = health > 0f ? health : definition.maxHealth;

            name = definition.displayName + " " + address;
            transform.position = BuildGrid.CellOrigin(address.X, address.Y, address.Z);
            transform.rotation = Quaternion.identity;

            BuildPieceVisuals.Build(this);
            _hinge = FindHinge();
            ApplyOpenState();
        }

        Transform FindHinge()
        {
            var all = GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].name == BuildPieceVisuals.DoorHingeName || all[i].name == BuildPieceVisuals.HatchHingeName)
                    return all[i];
            }
            return null;
        }

        /// <summary>Swaps the definition in place, keeping the slot and the damage ratio.</summary>
        public void ReplaceDefinition(BuildPieceDefinition definition)
        {
            float ratio = HealthFraction;

            // Rebuild the visuals and collider from scratch rather than trying to patch them.
            var children = new System.Collections.Generic.List<GameObject>();
            for (int i = 0; i < transform.childCount; i++) children.Add(transform.GetChild(i).gameObject);
            for (int i = 0; i < children.Count; i++) DestroyImmediate(children[i]);

            var colliders = GetComponents<BoxCollider>();
            for (int i = 0; i < colliders.Length; i++) DestroyImmediate(colliders[i]);

            Definition = definition;
            Health = Mathf.Max(1f, definition.maxHealth * Mathf.Max(ratio, 0.5f));
            name = definition.displayName + " " + Address;

            BuildPieceVisuals.Build(this);
            _hinge = FindHinge();
            ApplyOpenState();
        }

        public string InteractPrompt
        {
            get
            {
                if (!IsOpenable) return "";
                return (IsOpen ? "Close " : "Open ") + Definition.displayName.ToLowerInvariant();
            }
        }

        public void Interact(GameObject interactor)
        {
            if (!IsOpenable) return;
            SetOpen(!IsOpen);
        }

        public void SetOpen(bool open)
        {
            if (!IsOpenable) return;
            IsOpen = open;
            ApplyOpenState();
        }

        void ApplyOpenState()
        {
            if (_hinge == null) return;

            if (Definition.kind == BuildPieceKind.Door)
            {
                _hinge.localRotation = Quaternion.Euler(0f, IsOpen ? -95f : 0f, 0f);
            }
            else
            {
                _hinge.localRotation = Quaternion.Euler(0f, 0f, IsOpen ? -85f : 0f);
            }

            // An open door or hatch must stop blocking movement.
            var colliders = GetComponentsInChildren<BoxCollider>();
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i].gameObject == gameObject) colliders[i].isTrigger = IsOpen || !Definition.blocksMovement;
            }
        }

        public void ApplyDamage(DamageInfo info)
        {
            if (!IsAlive) return;

            float resistance = Mathf.Max(0.05f, Definition.damageResistance);
            Health -= info.Amount / resistance;

            BuildPieceVisuals.ShowDamage(this);
            if (Health <= 0f && Owner != null) Owner.Remove(this, false);
        }

        public void Repair(float amount)
        {
            Health = Mathf.Min(Definition.maxHealth, Health + amount);
            BuildPieceVisuals.ShowDamage(this);
        }
    }
}
