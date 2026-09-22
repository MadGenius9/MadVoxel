using MadVoxel.Building;
using MadVoxel.Core;
using MadVoxel.Farming.Plots;
using MadVoxel.Inventory;
using UnityEngine;

namespace MadVoxel.Colony
{
    /// <summary>
    /// One person. A small state machine with four needs and one job - not a planner,
    /// not a utility AI. They walk to a thing, do the thing, and go to bed.
    ///
    /// The deliberate limit is that a colonist never reasons about the future. They do
    /// not stockpile, they do not anticipate a blood moon, and they will happily work a
    /// field while the stores run out. That is what makes the board worth reading: the
    /// planning is yours, and they are only hands.
    /// </summary>
    public class Colonist : MonoBehaviour, IDamageable
    {
        public static event System.Action<Colonist> Died;

        const float ArriveDistance = 1.4f;
        const float ThinkInterval = 0.5f;

        public ColonistDefinition Definition { get; private set; }
        public ColonyWorld Colony { get; private set; }

        public string Name = "Survivor";
        public ColonyJob Job = ColonyJob.Idle;
        public ColonyState State { get; private set; }

        /// <summary>0 is starving, 100 is full.</summary>
        public float Food = 80f;
        public float Water = 80f;
        public float Morale = 65f;
        public float Health { get; private set; }

        /// <summary>The bed they were assigned, or null while they have none.</summary>
        public PlacedStructure Bed;

        public bool IsAlive { get { return Health > 0f; } }
        public bool Sheltering { get; set; }

        CharacterController _controller;
        Vector3 _target;
        bool _hasTarget;
        float _nextThink;
        float _nextSwing;
        float _verticalVelocity;

        public void Init(ColonyWorld colony, ColonistDefinition definition, string name)
        {
            Colony = colony;
            Definition = definition;
            Name = name;
            Health = definition.maxHealth;
            Morale = definition.startingMorale;
            State = ColonyState.Working;

            _controller = GetComponent<CharacterController>();
        }

        // ------------------------------------------------------------------- needs

        /// <summary>Called by the colony once per sampled game hour.</summary>
        public void TickNeeds(float gameHours, MoraleContext context)
        {
            if (!IsAlive) return;

            Food = Mathf.Max(0f, Food - Definition.hungerPerHour * gameHours);
            Water = Mathf.Max(0f, Water - Definition.thirstPerHour * gameHours);

            Morale = ColonyMorale.Step(Morale, context, Definition, gameHours);

            // Starving and parched do real damage, slowly. Nobody dies of a bad day.
            if (Food <= 0f) Health -= 1.5f * gameHours;
            if (Water <= 0f) Health -= 2.5f * gameHours;

            if (Health <= 0f) Kill();
        }

        public bool IsHungry { get { return Food < 35f; } }
        public bool IsThirsty { get { return Water < 35f; } }

        public void Eat(float amount)
        {
            Food = Mathf.Min(100f, Food + amount);
        }

        public void Drink(float amount)
        {
            Water = Mathf.Min(100f, Water + amount);
        }

        // ------------------------------------------------------------------ acting

        void Update()
        {
            if (!IsAlive || Colony == null) return;

            if (Time.time >= _nextThink)
            {
                _nextThink = Time.time + ThinkInterval;
                Think();
            }

            Step();
        }

        /// <summary>
        /// The whole brain. Order matters and is the design: shelter beats everything,
        /// then thirst, then hunger, then sleep, then work. A miserable colonist simply
        /// stops at the work step and stands about, which is visible and diagnosable.
        /// </summary>
        void Think()
        {
            if (Sheltering)
            {
                State = ColonyState.Sheltering;
                GoTo(Colony.ShelterPoint(this));
                return;
            }

            if (ColonyMorale.IsLeaving(Morale, Definition))
            {
                State = ColonyState.Leaving;
                GoTo(Colony.ExitPoint());
                return;
            }

            if (IsThirsty && Colony.TryDrink(this)) { State = ColonyState.Drinking; return; }
            if (IsHungry && Colony.TryEat(this)) { State = ColonyState.Eating; return; }

            if (Colony.IsNight && Bed != null)
            {
                State = ColonyState.Sleeping;
                GoTo(Bed.transform.position);
                return;
            }

            if (ColonyMorale.IsSulking(Morale, Definition))
            {
                // Down tools. They are still here, and still eating.
                State = ColonyState.Working;
                _hasTarget = false;
                return;
            }

            State = ColonyState.Working;
            DoJob();
        }

        void DoJob()
        {
            switch (Job)
            {
                case ColonyJob.Farm: DoFarm(); break;
                case ColonyJob.Guard: DoGuard(); break;
                case ColonyJob.Repair: DoRepair(); break;
                case ColonyJob.Cook: DoCook(); break;
                default: _hasTarget = false; break;
            }
        }

        void DoFarm()
        {
            var plot = Colony.FindReadyPlot(transform.position);
            if (plot == null) { _hasTarget = false; return; }

            GoTo(plot.transform.position);
            if (!AtTarget(plot.transform.position)) return;

            Colony.HarvestInto(plot, this);
        }

        void DoGuard()
        {
            var post = Colony.GuardPost(this);
            GoTo(post);

            var threat = Colony.NearestThreat(transform.position, Definition.guardReach + 1.5f);
            if (threat == null || Time.time < _nextSwing) return;

            _nextSwing = Time.time + Definition.guardCooldown;
            threat.ApplyDamage(new DamageInfo
            {
                Amount = Definition.guardDamage,
                Kind = DamageKind.Melee,
                Point = transform.position,
                Direction = transform.forward,
                Source = gameObject,
                ToolTier = 1
            });
        }

        void DoRepair()
        {
            var piece = Colony.FindDamagedPiece(transform.position);
            if (piece == null) { _hasTarget = false; return; }

            GoTo(piece.transform.position);
            if (!AtTarget(piece.transform.position)) return;

            Colony.RepairA(piece, this);
        }

        void DoCook()
        {
            var fire = Colony.CookingFire();
            if (fire == null) { _hasTarget = false; return; }

            GoTo(fire.transform.position);
            if (!AtTarget(fire.transform.position)) return;

            Colony.CookOnce(this);
        }

        // ---------------------------------------------------------------- movement

        void GoTo(Vector3 position)
        {
            _target = position;
            _hasTarget = true;
        }

        bool AtTarget(Vector3 position)
        {
            Vector3 flat = position - transform.position;
            flat.y = 0f;
            return flat.sqrMagnitude <= ArriveDistance * ArriveDistance;
        }

        /// <summary>
        /// Walking, and nothing cleverer. They follow the ground the player dug because
        /// the character controller does, not because anything is pathfinding.
        /// </summary>
        void Step()
        {
            if (_controller == null) return;

            Vector3 move = Vector3.zero;

            if (_hasTarget)
            {
                Vector3 flat = _target - transform.position;
                flat.y = 0f;

                if (flat.sqrMagnitude > ArriveDistance * ArriveDistance)
                {
                    move = flat.normalized * Definition.moveSpeed;
                    transform.rotation = Quaternion.Slerp(transform.rotation,
                        Quaternion.LookRotation(flat.normalized), Time.deltaTime * 6f);
                }
                else
                {
                    _hasTarget = false;
                }
            }

            _verticalVelocity = _controller.isGrounded ? -2f : _verticalVelocity - 18f * Time.deltaTime;
            move.y = _verticalVelocity;

            _controller.Move(move * Time.deltaTime);
        }

        // ------------------------------------------------------------------- death

        public void ApplyDamage(DamageInfo info)
        {
            if (!IsAlive) return;

            Health = Mathf.Max(0f, Health - info.Amount);
            if (Health <= 0f) Kill();
        }

        public void Kill()
        {
            if (State == ColonyState.Dead) return;

            Health = 0f;
            State = ColonyState.Dead;

            Notifications.PostFormat("{0} died", Name);
            if (Died != null) Died(this);
        }

        /// <summary>The Claim Slate look-at line: "JULES  FARM  HUNGRY".</summary>
        public string Readout()
        {
            string mood = IsHungry ? "HUNGRY"
                        : IsThirsty ? "THIRSTY"
                        : ColonyMorale.Describe(Morale, Definition);

            return string.Format("{0}   {1}   {2}",
                Name.ToUpperInvariant(), Job.ToString().ToUpperInvariant(), mood);
        }
    }
}
