using System.Collections;
using UnityEngine;
using Gymageddon.Core;
using Gymageddon.Data;

namespace Gymageddon.Entities
{
    /// <summary>
    /// A gym fighter placed in a lane to attack incoming enemies.
    /// Stat boosts from Trainers are applied at placement time.
    /// </summary>
    public class Character : Unit
    {
        // Loaded from CharacterData
        public CharacterData Data { get; private set; }
        public string CharacterName => Data ? Data.characterName : "Unknown";

        // Effective stats (may be boosted by Trainer)
        public int   EffectiveAttackDamage { get; private set; }
        public float EffectiveAttackSpeed  { get; private set; }
        public float AttackRange           { get; private set; }

        private Lane   _lane;
        private bool   _attacking;

        // ── Setup ─────────────────────────────────────────────────────
        public void Init(CharacterData data)
        {
            Data                   = data;
            EffectiveAttackDamage  = data.attackDamage;
            EffectiveAttackSpeed   = data.attackSpeed;
            AttackRange            = data.attackRange;
            InitHealth(data.maxHealth);
            ApplyVisual(data.bodyColor);
        }

        public void OnPlaced(Lane lane)
        {
            _lane     = lane;
            _attacking = false;
            StartCoroutine(AttackRoutine());
        }

        public void OnRemoved()
        {
            StopAllCoroutines();
        }

        // ── Trainer buffs ─────────────────────────────────────────────
        public void ApplyDamageBoost(float multiplier)   => EffectiveAttackDamage = Mathf.RoundToInt(Data.attackDamage * (1f + multiplier));
        public void ApplySpeedBoost(float multiplier)    => EffectiveAttackSpeed  = Data.attackSpeed * (1f + multiplier);
        public void ApplyHealthBoost(float multiplier)
        {
            int oldMax = MaxHealth;
            MaxHealth = Mathf.RoundToInt(Data.maxHealth * (1f + multiplier));
            // Preserve the current health percentage so placement mid-combat doesn't fully heal.
            CurrentHealth = Mathf.Clamp(
                Mathf.RoundToInt((float)CurrentHealth / oldMax * MaxHealth), 0, MaxHealth);
        }

        public void RemoveDamageBoost()  => EffectiveAttackDamage = Data.attackDamage;
        public void RemoveSpeedBoost()   => EffectiveAttackSpeed  = Data.attackSpeed;
        public void RemoveHealthBoost()  { MaxHealth = Data.maxHealth; CurrentHealth = Mathf.Min(CurrentHealth, MaxHealth); }

        // ── Combat loop ───────────────────────────────────────────────
        private IEnumerator AttackRoutine()
        {
            while (true)
            {
                float interval = EffectiveAttackSpeed > 0f ? 1f / EffectiveAttackSpeed : float.MaxValue;
                yield return new WaitForSeconds(interval);

                Enemy target = FindNearestEnemy();
                if (target != null)
                    target.TakeDamage(EffectiveAttackDamage);
            }
        }

        private Enemy FindNearestEnemy()
        {
            Enemy nearest  = null;
            float minDist  = AttackRange;

            // Only target enemies in the same lane
            Enemy[] all = FindObjectsByType<Enemy>(FindObjectsSortMode.None);
            foreach (Enemy e in all)
            {
                if (e.LaneIndex != _lane.LaneIndex) continue;
                float dist = Vector3.Distance(transform.position, e.transform.position);
                if (dist < minDist) { minDist = dist; nearest = e; }
            }
            return nearest;
        }

        // ── Death ─────────────────────────────────────────────────────
        protected override void OnDied()
        {
            StopAllCoroutines();
            _lane?.RemoveCharacter();
            Debug.Log($"[Character] {CharacterName} in lane {_lane?.LaneIndex} has died.");
        }

        // ── Visual ────────────────────────────────────────────────────
        private void ApplyVisual(Color color)
        {
            SpriteRenderer sr = GetComponent<SpriteRenderer>();
            if (sr != null) sr.color = color;
        }
    }
}
