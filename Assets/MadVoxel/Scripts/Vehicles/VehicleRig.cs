using MadVoxel.Core;
using MadVoxel.Core.Player;
using MadVoxel.Inventory;
using MadVoxel.Perks;
using UnityEngine;

namespace MadVoxel.Vehicles
{
    /// <summary>
    /// A drivable machine. Built on a CharacterController rather than a Rigidbody for
    /// the same reason the player is: the terrain is voxels the player has been
    /// digging, and a controller climbs a dug ramp predictably where a physics body
    /// catches on every step and flips.
    ///
    /// It is a tractor, not a car. Slow, heavy, turns on the spot when stationary, and
    /// the interesting decisions are about fuel and what is hitched to the back.
    /// </summary>
    public class VehicleRig : MonoBehaviour, IDamageable, IInteractable
    {
        public static event System.Action<VehicleRig> Mounted;
        public static event System.Action<VehicleRig> Dismounted;

        public VehicleDefinition Definition { get; private set; }
        public float FuelLitres { get; set; }
        public float Health { get; private set; }

        public bool IsAlive { get { return Health > 0f; } }
        public bool HasDriver { get { return _driver != null; } }

        /// <summary>Who is sitting on it, or null. The implement reads its perks through this.</summary>
        public PlayerRig Driver { get { return _driver; } }

        /// <summary>Metres per second, for the panel. Positive forward.</summary>
        public float Speed { get; private set; }

        /// <summary>What is hitched to the back, if anything.</summary>
        public ImplementController Implement { get; set; }

        CharacterController _controller;
        PlayerRig _driver;
        Transform _seat;
        Transform _cameraAnchor;

        float _verticalVelocity;
        float _throttle;
        int _mountedFrame = -1;

        public void Init(VehicleDefinition definition)
        {
            Definition = definition;
            Health = definition.maxHealth;
            FuelLitres = 0f;

            _controller = gameObject.AddComponent<CharacterController>();
            _controller.height = Mathf.Max(1f, definition.chassisSize.y * 2f);
            _controller.radius = Mathf.Max(0.5f, definition.chassisSize.x * 0.5f);
            _controller.center = new Vector3(0f, _controller.height * 0.5f, 0f);
            _controller.stepOffset = definition.climbHeight;

            var seat = new GameObject("Seat");
            seat.transform.SetParent(transform, false);
            seat.transform.localPosition = new Vector3(0f, definition.chassisSize.y + 0.9f, -0.2f);
            _seat = seat.transform;

            var anchor = new GameObject("CameraAnchor");
            anchor.transform.SetParent(_seat, false);
            _cameraAnchor = anchor.transform;

            VehicleVisuals.Build(transform, definition);
        }

        /// <summary>Puts a saved machine back as it was parked.</summary>
        public void RestoreState(float fuelLitres, float health)
        {
            FuelLitres = Mathf.Clamp(fuelLitres, 0f, Definition.fuelCapacity);
            Health = Mathf.Clamp(health, 0f, Definition.maxHealth);
        }

        // ----------------------------------------------------------------- driving

        void Update()
        {
            if (!IsAlive) return;

            if (_driver != null) Drive();
            else Settle();
        }

        void Drive()
        {
            float dt = Time.deltaTime;

            // The player's own interaction is switched off while seated, so the machine
            // reads its own keys. The frame guard stops the E that mounted from being
            // read again by the rig in the same frame and throwing the player straight off.
            if (Time.frameCount != _mountedFrame)
            {
                if (InputBridge.InteractDown) { Dismount(); return; }
                if (InputBridge.ImplementToggleDown && Implement != null) Implement.Toggle();
            }

            // No fuel is no engine. The machine coasts to a stop rather than locking,
            // so running dry halfway down a field is inconvenient and not a trap.
            bool hasFuel = FuelLitres > 0f;

            float steer = InputBridge.Move.x;
            _throttle = InputBridge.Move.y;

            if (!hasFuel) _throttle = 0f;

            float top = Definition.maxSpeed;
            if (Implement != null && Implement.Engaged) top *= Implement.SpeedMultiplier;

            Speed = Mathf.MoveTowards(Speed, _throttle * top, Definition.acceleration * dt);

            // A tractor turns about its back axle, so it steers while stationary but
            // far more slowly than when rolling.
            float steerAuthority = Mathf.Lerp(0.25f, 1f, Mathf.Clamp01(Mathf.Abs(Speed) / Mathf.Max(0.1f, top)));
            transform.Rotate(0f, steer * Definition.turnRate * steerAuthority * dt, 0f);

            Vector3 move = transform.forward * Speed;

            _verticalVelocity = _controller.isGrounded ? -2f : _verticalVelocity - 20f * dt;
            move.y = _verticalVelocity;
            _controller.Move(move * dt);

            if (hasFuel && Mathf.Abs(Speed) > 0.1f)
            {
                float burn = Definition.fuelPerSecond;
                if (Implement != null && Implement.Engaged) burn += Implement.FuelPerSecond;

                // The Economiser perk lands here. It divides rather than multiplies: the
                // effect is written as "fuel economy", and better economy is less burn.
                if (_driver != null && _driver.Progression != null)
                {
                    burn /= _driver.Progression.Effects.Multiplier(PerkEffectType.VehicleFuelEfficiency);
                }

                FuelLitres = Mathf.Max(0f, FuelLitres - burn * dt);
                if (FuelLitres <= 0f) Notifications.Post("Out of fuel");
            }

            if (_driver != null) _driver.transform.position = _seat.position;
        }

        /// <summary>Unmanned: bleed off speed and stay put rather than rolling away.</summary>
        void Settle()
        {
            if (Mathf.Abs(Speed) < 0.01f && _controller.isGrounded) return;

            Speed = Mathf.MoveTowards(Speed, 0f, Definition.acceleration * 2f * Time.deltaTime);

            Vector3 move = transform.forward * Speed;
            _verticalVelocity = _controller.isGrounded ? -2f : _verticalVelocity - 20f * Time.deltaTime;
            move.y = _verticalVelocity;
            _controller.Move(move * Time.deltaTime);
        }

        // -------------------------------------------------------------- mount

        public bool Mount(PlayerRig player)
        {
            if (player == null || _driver != null || !IsAlive) return false;

            _driver = player;
            _mountedFrame = Time.frameCount;
            player.Motor.enabled = false;

            // Seated, the hands are on the wheel: no mining, no placing, no swinging.
            if (player.Interaction != null) player.Interaction.enabled = false;

            if (player.Camera != null)
            {
                player.Camera.transform.SetParent(_cameraAnchor, false);
                player.Camera.transform.localPosition = new Vector3(0f, 0.4f, -4.5f);
                player.Camera.transform.localRotation = Quaternion.Euler(12f, 0f, 0f);
            }

            player.transform.position = _seat.position;
            Notifications.PostFormat("{0} - WASD to drive, F to work, G to hitch, E to get off",
                Definition.displayName);

            if (Mounted != null) Mounted(this);
            return true;
        }

        public void Dismount()
        {
            if (_driver == null) return;

            var player = _driver;
            _driver = null;
            Speed = 0f;

            if (player.Camera != null && player.CameraPivot != null)
            {
                player.Camera.transform.SetParent(player.CameraPivot, false);
                player.Camera.transform.localPosition = Vector3.zero;
                player.Camera.transform.localRotation = Quaternion.identity;
            }

            // Step off to the left, clear of the wheels.
            player.transform.position = transform.position - transform.right * 2f + Vector3.up * 0.5f;
            player.Motor.enabled = true;
            if (player.Interaction != null) player.Interaction.enabled = true;

            if (Dismounted != null) Dismounted(this);
        }

        // ---------------------------------------------------------------- fuelling

        public bool TryRefuel(PlayerInventory inventory)
        {
            if (inventory == null || Definition.fuelItem == null) return false;

            var held = inventory.SelectedItem;
            if (held != Definition.fuelItem) return false;

            float room = Definition.fuelCapacity - FuelLitres;
            if (room <= 0.01f)
            {
                Notifications.Post("The tank is full");
                return true;
            }

            FuelLitres = Mathf.Min(Definition.fuelCapacity, FuelLitres + Definition.fuelPerItem);
            inventory.ConsumeSelected(1);

            Notifications.PostFormat("{0} - {1} L", Definition.displayName, Mathf.FloorToInt(FuelLitres));
            return true;
        }

        public void ApplyDamage(DamageInfo info)
        {
            if (!IsAlive) return;

            Health = Mathf.Max(0f, Health - info.Amount);
            if (Health > 0f) return;

            Dismount();
            Notifications.PostFormat("{0} is wrecked", Definition.displayName);
        }

        /// <summary>Kilometres per hour, for the panel.</summary>
        public float SpeedKph { get { return Speed * 3.6f; } }

        // ------------------------------------------------------------- interacting

        public string InteractPrompt
        {
            get
            {
                if (!IsAlive) return Definition.displayName + " - wrecked";
                if (_driver != null) return "";
                return string.Format("{0} - {1:0} L fuel", Definition.displayName, FuelLitres);
            }
        }

        public void Interact(GameObject interactor)
        {
            if (interactor == null) return;

            var player = interactor.GetComponent<PlayerRig>();
            if (player == null) return;

            // A can of fuel in hand means you came to fill it, not to drive it.
            if (TryRefuel(player.Inventory)) return;

            if (!IsAlive)
            {
                Notifications.PostFormat("{0} is wrecked", Definition.displayName);
                return;
            }

            Mount(player);
        }
    }
}
