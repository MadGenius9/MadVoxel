using MadVoxel.Building;
using MadVoxel.Perks;
using MadVoxel.World.Fields;
using MadVoxel.World.Terrain;
using UnityEngine;

namespace MadVoxel.Core.Player
{
    /// <summary>
    /// Holds the player's components together. Pure references - all behaviour lives in
    /// the components themselves.
    /// </summary>
    public class PlayerRig : MonoBehaviour
    {
        public PlayerMotor Motor;
        public PlayerLook Look;
        public PlayerStats Stats;
        public PlayerInventory Inventory;
        public PlayerInteraction Interaction;
        public PlayerProgression Progression;

        /// <summary>
        /// Contracts taken and finished. Plain state on the rig rather than a component,
        /// because every rule in it is engine-free and worth testing that way.
        /// </summary>
        public readonly Quests.QuestLog Quests = new Quests.QuestLog();
        public Camera Camera;
        public Transform CameraPivot;

        public Vector3 RespawnPoint;
        public bool HasRespawnPoint;

        public Vector3 EyePosition
        {
            get { return CameraPivot != null ? CameraPivot.position : transform.position + Vector3.up * 1.6f; }
        }
    }

    /// <summary>Builds the first-person rig from primitives. No prefab required.</summary>
    public static class PlayerFactory
    {
        public static PlayerRig Create(GameConfig config, TerrainWorld voxels, StructureWorld structures,
                                       BuildingWorld buildings, FieldWorld fields, Vector3 position)
        {
            var go = new GameObject("Player");
            go.transform.position = position;
            go.tag = "Player";

            var controller = go.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = 0.35f;
            controller.center = new Vector3(0f, 0.9f, 0f);

            var rig = go.AddComponent<PlayerRig>();

            var pivotGo = new GameObject("CameraPivot");
            pivotGo.transform.SetParent(go.transform, false);
            pivotGo.transform.localPosition = new Vector3(0f, 1.62f, 0f);
            rig.CameraPivot = pivotGo.transform;

            var camGo = new GameObject("MainCamera");
            camGo.tag = "MainCamera";
            camGo.transform.SetParent(pivotGo.transform, false);
            var camera = camGo.AddComponent<Camera>();
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 420f;
            camera.fieldOfView = 78f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.44f, 0.47f, 0.48f);
            camGo.AddComponent<AudioListener>();
            rig.Camera = camera;

            rig.Stats = go.AddComponent<PlayerStats>();
            rig.Stats.Init(config);

            rig.Motor = go.AddComponent<PlayerMotor>();
            rig.Motor.Init(config, rig.Stats, structures);

            rig.Look = go.AddComponent<PlayerLook>();
            rig.Look.Init(config, rig.CameraPivot);

            rig.Inventory = go.AddComponent<PlayerInventory>();
            rig.Progression = go.AddComponent<PlayerProgression>();
            rig.Stats.BindPerks(rig.Progression.Effects);

            rig.Interaction = go.AddComponent<PlayerInteraction>();
            rig.Interaction.Init(config, voxels, structures, buildings, fields, rig.Inventory, rig.Stats, rig.Progression, camera);

            // A small headlamp keeps the first metres readable on a moonless night.
            var lampGo = new GameObject("Headlamp");
            lampGo.transform.SetParent(pivotGo.transform, false);
            var lamp = lampGo.AddComponent<Light>();
            lamp.type = LightType.Point;
            lamp.range = 6.5f;
            lamp.intensity = 0.55f;
            lamp.color = new Color(0.95f, 0.92f, 0.82f);
            lamp.shadows = LightShadows.None;

            return rig;
        }
    }
}
