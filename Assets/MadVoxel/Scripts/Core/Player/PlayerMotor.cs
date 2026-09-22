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

        readonly Collider[] _ladderProbe = new Collider[8];

        Vector3 _velocity;
        bool _crouching;
        float _lowestSpeedThisFall;

        /// <summary>Developer tools toggle: ignores gravity and collision-driven falling.</summary>
        public bool FlyMode { get; set; }

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

            if (FlyMode)
            {
                Fly(wish, speed, dt);
                return;
            }

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

        void Fly(Vector3 wish, float speed, float dt)
        {
            _velocity = Vector3.zero;
            _lowestSpeedThisFall = 0f;

            float vertical = 0f;
            if (InputBridge.Jump) vertical += 1f;
            if (InputBridge.Crouch) vertical -= 1f;

            Vector3 motion = wish * (speed * 2.5f);
            motion.y = vertical * speed * 2.5f;
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

        /// <summary>
        /// Ladders are snap pieces spanning a 3 m cell edge, so an overlap probe around
        /// the chest is both simpler and more accurate than a grid lookup.
        /// </summary>
        void UpdateLadderState()
        {
            IsOnLadder = false;

            Vector3 chest = transform.position + Vector3.up * 1.1f;
            int count = Physics.OverlapSphereNonAlloc(chest, 0.6f, _ladderProbe, ~0, QueryTriggerInteraction.Collide);

            for (int i = 0; i < count; i++)
            {
                if (_ladderProbe[i] == null) continue;
                var piece = _ladderProbe[i].GetComponentInParent<BuildPiece>();
                if (piece != null && piece.Definition.kind == BuildPieceKind.Ladder)
                {
                    IsOnLadder = true;
                    return;
                }
            }
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
