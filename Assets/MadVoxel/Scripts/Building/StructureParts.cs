using System;
using MadVoxel.Core;
using MadVoxel.Inventory;
using UnityEngine;

namespace MadVoxel.Building
{

    /// <summary>A lootable crate. The UI listens for the open request rather than being called directly.</summary>
    public class StorageStructure : MonoBehaviour, IInteractable
    {
        public static event Action<StorageStructure> OpenRequested;

        public PlacedStructure Structure { get; private set; }
        public MadVoxel.Inventory.Inventory Contents { get; private set; }
        /// <summary>Set for death backpacks so the save layer can prune them.</summary>
        public bool IsDeathBackpack;

        public void Bind(PlacedStructure structure)
        {
            Structure = structure;
            Contents = new MadVoxel.Inventory.Inventory(Mathf.Max(1, structure.Definition.storageSlots));
        }

        public string InteractPrompt
        {
            get { return IsDeathBackpack ? "Recover backpack" : "Open " + Structure.Definition.displayName; }
        }

        public void Interact(GameObject interactor)
        {
            if (OpenRequested != null) OpenRequested(this);
        }
    }

    /// <summary>Workbench / forge. Enables the recipes gated behind that station.</summary>
    public class CraftStationStructure : MonoBehaviour, IInteractable
    {
        public static event Action<CraftStation> OpenRequested;

        public PlacedStructure Structure { get; private set; }
        public CraftStation Station { get { return Structure.Definition.craftStation; } }

        public void Bind(PlacedStructure structure)
        {
            Structure = structure;
        }

        public string InteractPrompt { get { return "Use " + Structure.Definition.displayName; } }

        public void Interact(GameObject interactor)
        {
            if (OpenRequested != null) OpenRequested(Station);
        }
    }

    /// <summary>Campfire: sodium-orange light, gentle flicker, and the cooking station.</summary>
    public class CampfireStructure : MonoBehaviour, IInteractable
    {
        public static event Action<CraftStation> OpenRequested;

        PlacedStructure _structure;
        Light _light;
        float _phase;

        public void Bind(PlacedStructure structure)
        {
            _structure = structure;
            _phase = UnityEngine.Random.value * 10f;

            var go = new GameObject("Firelight");
            go.transform.SetParent(structure.transform, false);
            go.transform.localPosition = new Vector3(0.5f, 0.55f, 0.5f);

            _light = go.AddComponent<Light>();
            _light.type = LightType.Point;
            _light.color = structure.Definition.lightColour;
            _light.range = structure.Definition.lightRange;
            _light.intensity = 2.1f;
            _light.shadows = LightShadows.None;

            PrimitiveBuilder.Box(structure.transform, new Vector3(0.5f, 0.3f, 0.5f), new Vector3(0.3f, 0.3f, 0.3f),
                MaterialLibrary.GetUnlit(new Color(1f, 0.55f, 0.16f)), "Flame");
        }

        void Update()
        {
            if (_light == null) return;
            _phase += Time.deltaTime * 6f;
            _light.intensity = 1.9f + Mathf.Sin(_phase) * 0.18f + Mathf.Sin(_phase * 2.37f) * 0.12f;
        }

        public string InteractPrompt { get { return "Cook at campfire"; } }

        public void Interact(GameObject interactor)
        {
            if (OpenRequested != null) OpenRequested(CraftStation.Campfire);
        }
    }

    /// <summary>Claims a radius around itself. Registers with the structure world on place.</summary>
    public class ToolCupboardStructure : MonoBehaviour
    {
        public PlacedStructure Structure { get; private set; }
        public LandClaim Claim { get; private set; }

        public void Bind(PlacedStructure structure)
        {
            Structure = structure;
            float radius = structure.Definition.claimRadius > 0f ? structure.Definition.claimRadius : 24f;
            Claim = new LandClaim
            {
                Cell = structure.Cell,
                Radius = radius,
                Stake = structure
            };
            structure.Owner.Claims.Register(Claim);
        }

        void OnDestroy()
        {
            if (Structure != null && Structure.Owner != null) Structure.Owner.Claims.Unregister(Claim);
        }
    }


    /// <summary>Sets the respawn point and lets the player sleep through to dawn.</summary>
    public class BedrollStructure : MonoBehaviour, IInteractable
    {
        public static event Action<BedrollStructure> UseRequested;

        public PlacedStructure Structure { get; private set; }

        public void Bind(PlacedStructure structure)
        {
            Structure = structure;
            var box = structure.GetComponent<BoxCollider>();
            if (box != null) box.isTrigger = true;
        }

        public string InteractPrompt { get { return "Set spawn / rest"; } }

        public void Interact(GameObject interactor)
        {
            if (UseRequested != null) UseRequested(this);
        }
    }
}
