using System;
using MadVoxel.Building;
using MadVoxel.Core;
using MadVoxel.World.Terrain;
using UnityEngine;

namespace MadVoxel.AI
{
    /// <summary>
    /// A wandering shambler. There is no NavMesh in a world that changes shape, so it
    /// steers straight at its target, steps up a voxel, and chews through whatever
    /// blocks the way when it stops making progress.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class Zombie : MonoBehaviour, IDamageable
    {
        enum Mode { Wander, Chase, Siege }

        const float Gravity = -20f;
        const float StuckThreshold = 0.45f;
        const float MemorySeconds = 9f;

        public ZombieDefinition Definition { get; private set; }
        public bool IsHordeUnit { get; set; }
        public bool HasSiegeTarget { get; set; }

        /// <summary>
        /// Where it is heading right now. A horde unit is given a way in first and the
        /// base second, so it funnels through the gap instead of walking at the nearest
        /// wall - which is the difference between a base being a shape and a health pool.
        /// </summary>
        public Vector3 SiegeTarget { get; set; }

        /// <summary>Where the lane leads. Taken up once the lane itself is reached.</summary>
        public Vector3 SiegeCentre { get; set; }

        /// <summary>Metres from a lane before it counts as reached.</summary>
        const float LaneReached = 3f;

        bool _throughLane;

        /// <summary>zombie, killer. The session awards XP and loot.</summary>
        public static event Action<Zombie, GameObject> Died;

        CharacterController _controller;
        TerrainWorld _voxels;
        StructureWorld _structures;
        BlockDamageTracker _blockDamage;
        LandClaimRegistry _claims;
        Transform _player;
        Core.Player.PlayerStats _playerStats;
        ZombieVisuals.Limbs _limbs;

        float _health;
        float _verticalVelocity;
        float _nextAttackTime;
        float _stuckTimer;
        float _animPhase;
        float _wanderRetargetTime;
        float _lastSeenTime = -999f;
        Vector3 _lastKnownPlayerPos;
        Vector3 _wanderTarget;
        Mode _mode = Mode.Wander;

        public bool IsAlive { get { return _health > 0f; } }
        public float Health { get { return _health; } }

        public void Init(ZombieDefinition def, TerrainWorld voxels, StructureWorld structures,
                         BlockDamageTracker blockDamage, LandClaimRegistry claims,
                         Transform player, Core.Player.PlayerStats playerStats)
        {
            Definition = def;
            _voxels = voxels;
            _structures = structures;
            _blockDamage = blockDamage;
            _claims = claims;
            _player = player;
            _playerStats = playerStats;
            _health = def.maxHealth;

            _controller = GetComponent<CharacterController>();
            _controller.height = def.height;
            _controller.radius = 0.32f;
            _controller.center = new Vector3(0f, def.height * 0.5f, 0f);
            _controller.stepOffset = 1.05f;
            _controller.slopeLimit = 60f;
            _controller.skinWidth = 0.04f;

            _limbs = ZombieVisuals.Build(transform, def);
            _wanderTarget = transform.position;
            name = def.displayName;
        }

        void Update()
        {
            if (!IsAlive || _voxels == null) return;

            float dt = Time.deltaTime;
            UpdatePerception();

            Vector3 target = ChooseTarget();
            Vector3 toTarget = target - transform.position;
            toTarget.y = 0f;
            float planarDistance = toTarget.magnitude;

            float speed = _mode == Mode.Wander ? Definition.walkSpeed : Definition.chaseSpeed;
            if (IsHordeUnit) speed *= 1.15f;

            if (TryAttackPlayer()) speed *= 0.2f;

            Vector3 before = transform.position;
            Vector3 move = Vector3.zero;

            if (planarDistance > 0.6f)
            {
                Vector3 dir = toTarget / planarDistance;
                move = dir * speed;
                FaceDirection(dir);
            }

            if (_controller.isGrounded)
            {
                _verticalVelocity = -2f;
            }
            else
            {
                _verticalVelocity += Gravity * dt;
            }
            move.y = _verticalVelocity;
            _controller.Move(move * dt);

            UpdateStuck(before, speed, dt);

            float moved = (transform.position - before).magnitude / Mathf.Max(0.001f, dt);
            _animPhase += dt * Mathf.Lerp(2.5f, 7f, Mathf.Clamp01(moved / 3f));
            ZombieVisuals.Animate(_limbs, _animPhase, Mathf.Clamp01(moved / 3f));

            if (!IsHordeUnit && _player != null)
            {
                float d = Vector3.Distance(transform.position, _player.position);
                if (d > 110f) UnityEngine.Object.Destroy(gameObject);
            }

            // Fell out of the world.
            if (transform.position.y < -4f) UnityEngine.Object.Destroy(gameObject);
        }

        void UpdatePerception()
        {
            if (_player == null || _playerStats == null || !_playerStats.IsAlive) return;

            Vector3 eye = transform.position + Vector3.up * (Definition.height * 0.85f);
            Vector3 playerCentre = _player.position + Vector3.up * 1.2f;
            float distance = Vector3.Distance(eye, playerCentre);

            float range = Definition.sightRange * (IsHordeUnit ? 1.8f : 1f);
            if (distance <= range && TerrainRay.HasLineOfSight(_voxels, eye, playerCentre))
            {
                _lastSeenTime = Time.time;
                _lastKnownPlayerPos = _player.position;
            }
        }

        Vector3 ChooseTarget()
        {
            if (Time.time - _lastSeenTime < MemorySeconds)
            {
                _mode = Mode.Chase;
                return Time.time - _lastSeenTime < 0.3f && _player != null ? _player.position : _lastKnownPlayerPos;
            }

            if (HasSiegeTarget)
            {
                // Through the gap, now for the base itself.
                if (!_throughLane)
                {
                    Vector3 toLane = SiegeTarget - transform.position;
                    toLane.y = 0f;

                    if (toLane.sqrMagnitude <= LaneReached * LaneReached)
                    {
                        _throughLane = true;
                        SiegeTarget = SiegeCentre;
                    }
                }

                _mode = Mode.Siege;
                return SiegeTarget;
            }

            _mode = Mode.Wander;
            if (Time.time >= _wanderRetargetTime || (transform.position - _wanderTarget).sqrMagnitude < 4f)
            {
                _wanderRetargetTime = Time.time + UnityEngine.Random.Range(5f, 11f);
                Vector2 circle = UnityEngine.Random.insideUnitCircle * 18f;
                _wanderTarget = transform.position + new Vector3(circle.x, 0f, circle.y);
            }
            return _wanderTarget;
        }

        void FaceDirection(Vector3 dir)
        {
            var look = Quaternion.LookRotation(new Vector3(dir.x, 0f, dir.z), Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, look, 280f * Time.deltaTime);
        }

        bool TryAttackPlayer()
        {
            if (_player == null || _playerStats == null || !_playerStats.IsAlive) return false;

            float distance = Vector3.Distance(transform.position, _player.position);
            if (distance > Definition.attackRange) return false;
            if (Time.time < _nextAttackTime) return true;

            _nextAttackTime = Time.time + Definition.attackInterval;
            _playerStats.ApplyDamage(new DamageInfo
            {
                Amount = Definition.meleeDamage,
                Kind = DamageKind.Zombie,
                Point = transform.position,
                Direction = (_player.position - transform.position).normalized,
                Source = gameObject
            });
            return true;
        }

        void UpdateStuck(Vector3 before, float desiredSpeed, float dt)
        {
            if (desiredSpeed <= 0.01f) { _stuckTimer = 0f; return; }

            Vector3 delta = transform.position - before;
            delta.y = 0f;
            float achieved = delta.magnitude / Mathf.Max(0.0001f, dt);

            if (achieved < desiredSpeed * StuckThreshold)
            {
                _stuckTimer += dt;
                if (_stuckTimer > 0.35f) Dig(dt);
            }
            else
            {
                _stuckTimer = 0f;
            }
        }

        /// <summary>Chew through whatever is in the way. Claims are safe outside a blood moon.</summary>
        void Dig(float dt)
        {
            Vector3 forward = transform.forward;
            float damage = Definition.digDamagePerSecond * dt;

            for (int i = 0; i < 2; i++)
            {
                float heightOffset = i == 0 ? 0.45f : 1.25f;
                Vector3 origin = transform.position + Vector3.up * heightOffset;
                Vector3 probe = origin + forward * 0.75f;

                if (!MayBreakAt(probe)) continue;

                // A raycast catches snap pieces, deployables and terrain colliders alike,
                // so walls, doors and foundations are all chewable.
                RaycastHit hit;
                if (Physics.Raycast(origin, forward, out hit, 1.1f, ~0, QueryTriggerInteraction.Ignore))
                {
                    var damageable = hit.collider.GetComponentInParent<IDamageable>();
                    if (damageable != null && !ReferenceEquals(damageable, this))
                    {
                        damageable.ApplyDamage(new DamageInfo
                        {
                            Amount = damage,
                            Kind = DamageKind.Zombie,
                            Point = hit.point,
                            Source = gameObject
                        });
                        return;
                    }
                }

                var structure = FindStructureAt(probe);
                if (structure != null)
                {
                    structure.ApplyDamage(new DamageInfo
                    {
                        Amount = damage,
                        Kind = DamageKind.Zombie,
                        Point = probe,
                        Source = gameObject
                    });
                    return;
                }

                var cell = Vector3Int.FloorToInt(probe);
                if (_voxels.IsSolid(cell.x, cell.y, cell.z))
                {
                    _blockDamage.Damage(cell, damage);
                    return;
                }
            }
        }

        bool MayBreakAt(Vector3 point)
        {
            if (IsHordeUnit) return true;
            return _claims == null || !_claims.IsProtected(point);
        }

        PlacedStructure FindStructureAt(Vector3 point)
        {
            if (_structures == null) return null;
            var cell = Vector3Int.FloorToInt(point);
            return _structures.GetAt(cell);
        }

        public void ApplyDamage(DamageInfo info)
        {
            if (!IsAlive) return;
            _health -= info.Amount;

            // Getting hit is as good as being seen.
            _lastSeenTime = Time.time;
            if (info.Source != null) _lastKnownPlayerPos = info.Source.transform.position;

            if (_health <= 0f) Kill(info.Source);
        }

        public void Kill(GameObject killer)
        {
            _health = 0f;
            if (Died != null) Died(this, killer);
            UnityEngine.Object.Destroy(gameObject);
        }
    }
}
