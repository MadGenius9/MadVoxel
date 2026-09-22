using MadVoxel.Building;
using UnityEngine;

namespace MadVoxel.Core.Player
{
    /// <summary>
    /// Character-controller movement: walk, sprint, crouch, jump, ladders and fall
    /// damage. No rigidbody, so it never fights the chunk colliders while they rebake.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerMotor : MonoBehaviour
    {
        const float StandHeight = 1.8f;
        const float CrouchHeight = 1.15f;
        const float JumpStaminaCost = 6f;
        const float FallDamageSpeed = 13f;
        const float FallDamagePerUnit = 5.5f;

        CharacterController _controller;
        GameConfig _config;
        PlayerStats _stats;
        StructureWorld _structures;

        Vector3 _velocity;
        bool _crouching;
        float _lowestSpeedThisFall;

        public bool IsSprinting { get; private set; }
        public bool IsOnLadder { get; private set; }
        public bool IsGrounded { get { return _controller.isGrounded; } }
        public Vector3 Velocity { get { return _velocity; } }

        public void Init(GameConfig config, PlayerStats stats, StructureWorld structures)
        {
            _config = config;
            _stats = stats;
            _structures = structures;

            _controller = GetComponent<CharacterController>();
            _controller.height = StandHeight;
            _controller.radius = 0.35f;
            _controller.center = new Vector3(0f, StandHeight * 0.5f, 0f);
            _controller.slopeLimit = 55f;
            _controller.stepOffset = 1.05f; // one voxel step up, like Minecraft
            _controller.skinWidth = 0.04f;
            _controller.minMoveDistance = 0f;
        }

        public void Teleport(Vector3 position)
        {
            _controller.enabled = false;
            transform.position = position;
            _controller.enabled = true;
            _velocity = Vector3.zero;
            _lowestSpeedThisFall = 0f;
        }

        void Update()
        {
            if (_config == null || _stats == null || !_stats.IsAlive) return;

            float dt = Time.deltaTime;
            UpdateLadderState();

            Vector2 input = InputBridge.Move;
            Vector3 wish = transform.right * input.x + transform.forward * input.y;
            if (wish.sqrMagnitude > 1f) wish.Normalize();

            bool wantsCrouch = InputBridge.Crouch;
            if (wantsCrouch != _crouching) SetCrouch(wantsCrouch);

            IsSprinting = InputBridge.Sprint && !_crouching && input.y > 0.1f && _stats.Stamina > 1f;
            if (IsSprinting) _stats.DrainStamina(_config.sprintStaminaPerSecond, dt);

            float speed = _crouching ? _config.crouchSpeed : (IsSprinting ? _config.sprintSpeed : _config.walkSpeed);
            if (_stats.Food <= 0f || _stats.Water <= 0f) speed *= 0.72f;

            if (IsOnLadder)
            {
                MoveOnLadder(wish, speed, dt);
                return;
            }

            if (_controller.isGrounded)
            {
                ResolveFallDamage();
                if (_velocity.y < 0f) _velocity.y = -2f;

                if (InputBridge.Jump && _stats.TrySpendStamina(JumpStaminaCost))
                {
                    _velocity.y = Mathf.Sqrt(_config.jumpHeight * -2f * _config.gravity);
                }
            }
            else
            {
                _velocity.y += _config.gravity * dt;
                _lowestSpeedThisFall = Mathf.Min(_lowestSpeedThisFall, _velocity.y);
            }

            Vector3 motion = wish * speed;
            motion.y = _velocity.y;
            _controller.Move(motion * dt);
        }

        void MoveOnLadder(Vector3 wish, float speed, float dt)
        {
            _lowestSpeedThisFall = 0f;
            _velocity.y = 0f;

            float climb = 0f;
            if (InputBridge.Move.y > 0.1f) climb = 1f;
            else if (InputBridge.Move.y < -0.1f) climb = -1f;
            if (InputBridge.Jump) climb = 1f;

            Vector3 motion = wish * (speed * 0.5f);
            motion.y = climb * 3.2f;
            _controller.Move(motion * dt);
        }

        void UpdateLadderState()
        {
            IsOnLadder = false;
            if (_structures == null) return;

            Vector3 p = transform.position;
            var feet = new Vector3Int(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.y + 0.2f), Mathf.FloorToInt(p.z));
            var chest = new Vector3Int(feet.x, Mathf.FloorToInt(p.y + 1.2f), feet.z);

            if (_structures.IsLadder(feet) || _structures.IsLadder(chest))
            {
                IsOnLadder = true;
                return;
            }

            // Also count a ladder in the cell we are pressed against.
            Vector3 ahead = p + transform.forward * 0.45f + Vector3.up * 1.0f;
            var aheadCell = new Vector3Int(Mathf.FloorToInt(ahead.x), Mathf.FloorToInt(ahead.y), Mathf.FloorToInt(ahead.z));
            if (_structures.IsLadder(aheadCell)) IsOnLadder = true;
        }

        void ResolveFallDamage()
        {
            if (_lowestSpeedThisFall < -FallDamageSpeed)
            {
                float excess = -_lowestSpeedThisFall - FallDamageSpeed;
                _stats.ApplyDamage(new DamageInfo
                {
                    Amount = excess * FallDamagePerUnit,
                    Kind = DamageKind.Fall
                });
            }
            _lowestSpeedThisFall = 0f;
        }

        void SetCrouch(bool crouch)
        {
            if (!crouch)
            {
                // Refuse to stand up under a block.
                Vector3 head = transform.position + Vector3.up * (CrouchHeight + 0.1f);
                if (Physics.SphereCast(head, 0.3f, Vector3.up, out _, StandHeight - CrouchHeight, ~0, QueryTriggerInteraction.Ignore))
                    return;
            }

            _crouching = crouch;
            float height = crouch ? CrouchHeight : StandHeight;
            _controller.height = height;
            _controller.center = new Vector3(0f, height * 0.5f, 0f);
        }

        public float EyeHeight
        {
            get { return (_crouching ? CrouchHeight : StandHeight) - 0.18f; }
        }
    }
}
