using MadVoxel.Core;
using MadVoxel.Core.Player;
using MadVoxel.Inventory;
using UnityEngine;

namespace MadVoxel.Combat
{
    /// <summary>
    /// An arrow in flight.
    ///
    /// It moves itself and sweeps for what it hit rather than using a rigidbody, for
    /// the same reason the tractor does not: the world is voxels the player has been
    /// digging, and a fast physics body tunnels through a one-block wall at forty
    /// metres a second. A swept raycast between last frame's position and this one
    /// cannot miss a wall it passed through.
    ///
    /// It lands and stays for a few seconds so a shot you missed is visible and
    /// recoverable, then gives up. An arrow that vanished the instant it landed would
    /// make aiming un-learnable.
    /// </summary>
    public class Arrow : MonoBehaviour
    {
        /// <summary>Seconds a spent arrow lies on the ground before it is cleaned up.</summary>
        const float LingerSeconds = 20f;

        /// <summary>How close you have to be to pick one back up.</summary>
        const float RecoverRange = 1.6f;

        /// <summary>A landed arrow is recoverable this often. A stuck one is not free ammo.</summary>
        public const float RecoverChance = 0.55f;

        float _damage;
        int _tier;
        GameObject _shooter;
        ItemDefinition _ammo;

        Vector3 _velocity;
        float _age;
        bool _landed;
        float _landedAt;
        bool _recoverable;

        public static Arrow Fire(Transform parent, Vector3 origin, Vector3 direction, float speed,
                                 float damage, int tier, ItemDefinition ammo, GameObject shooter)
        {
            var go = new GameObject("Arrow");
            go.transform.SetParent(parent, false);
            go.transform.position = origin;
            go.transform.rotation = Quaternion.LookRotation(direction);

            var shaft = MaterialLibrary.Get(SurfaceFamily.Plank, new Color(0.42f, 0.33f, 0.20f), 0.1f);
            var head = MaterialLibrary.Get(SurfaceFamily.Metal, new Color(0.62f, 0.62f, 0.64f), 0.6f, 0.8f);

            PrimitiveBuilder.Box(go.transform, new Vector3(0f, 0f, -0.06f), new Vector3(0.035f, 0.035f, 0.62f), shaft, "Shaft");
            PrimitiveBuilder.Box(go.transform, new Vector3(0f, 0f, 0.28f), new Vector3(0.06f, 0.06f, 0.14f), head, "Head");
            PrimitiveBuilder.Box(go.transform, new Vector3(0f, 0f, -0.32f), new Vector3(0.01f, 0.12f, 0.14f), shaft, "Fletching");

            var arrow = go.AddComponent<Arrow>();
            arrow._velocity = direction.normalized * speed;
            arrow._damage = damage;
            arrow._tier = tier;
            arrow._ammo = ammo;
            arrow._shooter = shooter;

            return arrow;
        }

        void Update()
        {
            if (_landed)
            {
                TryRecover();
                if (Time.time - _landedAt > LingerSeconds) Destroy(gameObject);
                return;
            }

            float dt = Time.deltaTime;
            _age += dt;

            if (_age > Ballistics.MaxFlightSeconds)
            {
                Destroy(gameObject);
                return;
            }

            var from = transform.position;
            var position = from;
            var velocity = _velocity;

            Ballistics.Step(ref position, ref velocity, dt);
            _velocity = velocity;

            // Swept, not teleported. At forty metres a second a frame is most of a
            // metre, and a point test would pass straight through a wall.
            Vector3 travel = position - from;
            float distance = travel.magnitude;

            if (distance > 0.0001f && Sweep(from, travel / distance, distance)) return;

            transform.position = position;
            if (velocity.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(velocity);
        }

        /// <summary>Returns true when the arrow stopped this frame.</summary>
        bool Sweep(Vector3 from, Vector3 direction, float distance)
        {
            var hits = Physics.RaycastAll(new Ray(from, direction), distance, ~0, QueryTriggerInteraction.Ignore);
            if (hits.Length == 0) return false;

            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            for (int i = 0; i < hits.Length; i++)
            {
                var hit = hits[i];
                if (hit.collider == null) continue;

                // Never your own bow arm.
                if (_shooter != null && hit.collider.transform.IsChildOf(_shooter.transform)) continue;

                var damageable = hit.collider.GetComponentInParent<IDamageable>();
                if (damageable != null && damageable.IsAlive)
                {
                    // Where on the body, and what that is worth. A wall has no zones,
                    // so it takes the shot at face value.
                    var body = damageable as IMeleeTarget;
                    var zone = HitZone.None;
                    float damage = _damage;

                    if (body != null)
                    {
                        zone = HitZones.Classify(hit.point.y, body.FootY, body.BodyHeight);
                        damage *= HitZones.Multiplier(zone);
                    }

                    damageable.ApplyDamage(new DamageInfo
                    {
                        Amount = damage,
                        Kind = DamageKind.Melee,
                        Point = hit.point,
                        Direction = direction,
                        Source = _shooter,
                        ToolTier = _tier
                    });

                    Audio.GameAudio.PlayAt(Audio.Sound.ArrowHit, hit.point,
                        zone == HitZone.Head ? 0.02f : 0.08f,
                        zone == HitZone.Head ? 1f : 0.7f);

                    CombatEvents.ReportHit(new HitReport
                    {
                        Victim = damageable,
                        Damage = damage,
                        Zone = zone,
                        Killed = !damageable.IsAlive,
                        Point = hit.point
                    });

                    // An arrow that hit something is in the something.
                    Destroy(gameObject);
                    return true;
                }

                Land(hit.point, direction);
                return true;
            }

            return false;
        }

        void Land(Vector3 point, Vector3 direction)
        {
            _landed = true;
            _landedAt = Time.time;

            // Rolled once, here. Re-rolling it while the player stands nearby would mean
            // waiting beside a snapped arrow until it became a whole one.
            _recoverable = Random.value <= RecoverChance;

            transform.position = point - direction * 0.18f;
            transform.rotation = Quaternion.LookRotation(direction);
        }

        /// <summary>
        /// Walk over a spent arrow and you get it back, about half the time. Certain
        /// recovery turns ammunition into a chore rather than a cost; none at all makes
        /// a bow unaffordable to shoot.
        ///
        /// Whether this particular arrow survives is decided once, when it lands, and
        /// not re-rolled while you stand near it - otherwise waiting beside a broken
        /// arrow eventually produces one.
        /// </summary>
        void TryRecover()
        {
            if (_ammo == null || _shooter == null || !_recoverable) return;
            if ((transform.position - _shooter.transform.position).sqrMagnitude > RecoverRange * RecoverRange) return;

            var inventory = _shooter.GetComponent<PlayerInventory>();
            if (inventory == null) return;

            // Asked before it is taken, not after. Destroying it on a full bag lost the
            // arrow; trying and failing instead retried every frame for the twenty
            // seconds it lies there, and Collect announces a full bag however quietly
            // you ask it to. Checking first does neither.
            if (!inventory.Bag.CanFit(_ammo, 1)) return;

            inventory.Collect(_ammo, 1, false);
            Destroy(gameObject);
        }
    }
}
