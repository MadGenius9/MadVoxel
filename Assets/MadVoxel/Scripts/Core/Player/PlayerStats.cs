using System;
using MadVoxel.Core;
using MadVoxel.Perks;
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

        /// <summary>Base pool plus whatever Iron Lungs and friends have added.</summary>
        public float MaxStamina { get { return _config.maxStamina + _perks.Bonus(PerkEffectType.MaxStaminaBonus); } }
        public float MaxFood { get { return _config.maxFood; } }
        public float MaxWater { get { return _config.maxWater; } }

        public bool IsAlive { get { return Health > 0f; } }

        /// <summary>Developer tools toggle. Never set by gameplay.</summary>
        public bool Invulnerable { get; set; }

        public event Action<DamageInfo> Damaged;
        public event Action Died;

        // Never null: the rig binds the real set once progression exists, and an empty
        // set reads as "no perks" rather than forcing a null check on every drain.
        PerkEffects _perks = new PerkEffects();

        float _invulnerableUntil;
        float _staminaBlockedUntil;
        float _regenTick;

        public void Init(GameConfig config)
        {
            _config = config;
            ResetToFull();
        }

        public void BindPerks(PerkEffects effects)
        {
            if (effects != null) _perks = effects;
        }

        public void ResetToFull()
        {
            Health = _config.maxHealth;
            Stamina = MaxStamina;
            Food = _config.maxFood;
            Water = _config.maxWater;
            _invulnerableUntil = Time.time + _config.respawnInvulnerableSeconds;
        }

        public void LoadState(float health, float stamina, float food, float water)
        {
            Health = Mathf.Clamp(health, 0f, _config.maxHealth);
            Stamina = Mathf.Clamp(stamina, 0f, MaxStamina);
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
                Stamina = Mathf.Min(MaxStamina, Stamina + regen * dt);
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
            amount *= _perks.Multiplier(PerkEffectType.StaminaDrainMultiplier);
            if (Stamina < amount) return false;
            Stamina -= amount;
            _staminaBlockedUntil = Time.time + 1.1f;
            return true;
        }

        public void DrainStamina(float amountPerSecond, float dt)
        {
            amountPerSecond *= _perks.Multiplier(PerkEffectType.StaminaDrainMultiplier);
            Stamina = Mathf.Max(0f, Stamina - amountPerSecond * dt);
            _staminaBlockedUntil = Time.time + 0.9f;
        }

        public void ApplyDamage(DamageInfo info)
        {
            if (!IsAlive || Invulnerable) return;
            if (info.Kind != DamageKind.Starvation && info.Kind != DamageKind.Thirst && Time.time < _invulnerableUntil) return;

            Health = Mathf.Max(0f, Health - info.Amount);
            if (Damaged != null) Damaged(info);

            if (Health <= 0f && Died != null) Died();
        }

        /// <summary>
        /// Eating and bandaging. Medicine perks scale what the item gives back, which is
        /// why this takes the raw item numbers rather than pre-scaled ones.
        /// </summary>
        public void Consume(float food, float water, float health, float stamina = 0f)
        {
            float scale = _perks.Multiplier(PerkEffectType.HealingMultiplier);

            Food = Mathf.Min(_config.maxFood, Food + food * scale);
            Water = Mathf.Min(_config.maxWater, Water + water * scale);
            Health = Mathf.Min(_config.maxHealth, Health + health * scale);
            if (stamina > 0f) Stamina = Mathf.Min(MaxStamina, Stamina + stamina * scale);
        }

        public void GrantInvulnerability(float seconds)
        {
            _invulnerableUntil = Mathf.Max(_invulnerableUntil, Time.time + seconds);
        }
    }
}
