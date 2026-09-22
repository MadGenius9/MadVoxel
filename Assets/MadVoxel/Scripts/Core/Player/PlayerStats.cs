using System;
using MadVoxel.Core;
using UnityEngine;

namespace MadVoxel.Core.Player
{
    /// <summary>Health, stamina, food and water. Owns death, not respawn.</summary>
    public class PlayerStats : MonoBehaviour, IDamageable
    {
        GameConfig _config;

        public float Health { get; private set; }
        public float Stamina { get; private set; }
        public float Food { get; private set; }
        public float Water { get; private set; }

        public float MaxHealth { get { return _config.maxHealth; } }
        public float MaxStamina { get { return _config.maxStamina; } }
        public float MaxFood { get { return _config.maxFood; } }
        public float MaxWater { get { return _config.maxWater; } }

        public bool IsAlive { get { return Health > 0f; } }

        public event Action<DamageInfo> Damaged;
        public event Action Died;

        float _invulnerableUntil;
        float _staminaBlockedUntil;
        float _regenTick;

        public void Init(GameConfig config)
        {
            _config = config;
            ResetToFull();
        }

        public void ResetToFull()
        {
            Health = _config.maxHealth;
            Stamina = _config.maxStamina;
            Food = _config.maxFood;
            Water = _config.maxWater;
            _invulnerableUntil = Time.time + _config.respawnInvulnerableSeconds;
        }

        public void LoadState(float health, float stamina, float food, float water)
        {
            Health = Mathf.Clamp(health, 0f, _config.maxHealth);
            Stamina = Mathf.Clamp(stamina, 0f, _config.maxStamina);
            Food = Mathf.Clamp(food, 0f, _config.maxFood);
            Water = Mathf.Clamp(water, 0f, _config.maxWater);
        }

        void Update()
        {
            if (_config == null || !IsAlive) return;

            float dt = Time.deltaTime;
            Food = Mathf.Max(0f, Food - _config.foodDrainPerMinute / 60f * dt);
            Water = Mathf.Max(0f, Water - _config.waterDrainPerMinute / 60f * dt);

            if (Time.time >= _staminaBlockedUntil)
            {
                float regen = _config.staminaRegenPerSecond * (Food > 0f ? 1f : 0.35f);
                Stamina = Mathf.Min(_config.maxStamina, Stamina + regen * dt);
            }

            // Starvation and thirst bite once a second so the numbers stay readable.
            _regenTick += dt;
            if (_regenTick >= 1f)
            {
                _regenTick -= 1f;
                if (Food <= 0f) ApplyDamage(DamageInfo.Simple(2f, DamageKind.Starvation));
                if (Water <= 0f) ApplyDamage(DamageInfo.Simple(3f, DamageKind.Thirst));

                if (Food > 35f && Water > 35f && Health < _config.maxHealth)
                {
                    Health = Mathf.Min(_config.maxHealth, Health + 0.6f);
                }
            }
        }

        public bool TrySpendStamina(float amount)
        {
            if (Stamina < amount) return false;
            Stamina -= amount;
            _staminaBlockedUntil = Time.time + 1.1f;
            return true;
        }

        public void DrainStamina(float amountPerSecond, float dt)
        {
            Stamina = Mathf.Max(0f, Stamina - amountPerSecond * dt);
            _staminaBlockedUntil = Time.time + 0.9f;
        }

        public void ApplyDamage(DamageInfo info)
        {
            if (!IsAlive) return;
            if (info.Kind != DamageKind.Starvation && info.Kind != DamageKind.Thirst && Time.time < _invulnerableUntil) return;

            Health = Mathf.Max(0f, Health - info.Amount);
            if (Damaged != null) Damaged(info);

            if (Health <= 0f && Died != null) Died();
        }

        public void Consume(float food, float water, float health)
        {
            Food = Mathf.Min(_config.maxFood, Food + food);
            Water = Mathf.Min(_config.maxWater, Water + water);
            Health = Mathf.Min(_config.maxHealth, Health + health);
        }

        public void GrantInvulnerability(float seconds)
        {
            _invulnerableUntil = Mathf.Max(_invulnerableUntil, Time.time + seconds);
        }
    }
}
