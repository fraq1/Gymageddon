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
        // Range where fighters are visually represented as "throwing" instead of punching.
        private const float RANGED_VISUAL_THRESHOLD = 3.2f;
        private const float PUNCH_VISUAL_DURATION = 0.12f;
        private const float PUNCH_VISUAL_EXTENSION_X = 0.11f;
        private static readonly Vector3 PROJECTILE_SPAWN_OFFSET = new Vector3(0.25f, 0.12f, 0f);
        private static readonly Vector3 PROJECTILE_SCALE = new Vector3(0.14f, 0.14f, 1f);

        // Loaded from CharacterData
        public CharacterData Data { get; private set; }
        public string CharacterName => Data ? Data.characterName : "Unknown";

        // Effective stats (may be boosted by Trainer)
        public int   EffectiveAttackDamage { get; private set; }
        public float EffectiveAttackSpeed  { get; private set; }
        public float AttackRange           { get; private set; }

        private Lane   _lane;
        private bool   _attacking;
        private Transform _rightArm;
        private Vector3 _rightArmBasePos;
        private Vector3 _rightArmBaseScale;
        private bool _attackVisualInProgress;

        // ── Setup ─────────────────────────────────────────────────────
        public void Init(CharacterData data)
        {
            Data                   = data;
            EffectiveAttackDamage  = data.attackDamage;
            EffectiveAttackSpeed   = data.attackSpeed;
            AttackRange            = data.attackRange;
            InitHealth(data.maxHealth);
            ApplyVisual(data.bodyColor);
            CacheModelParts();
        }

        public void OnPlaced(Lane lane)
        {
            _lane     = lane;
            _attacking = false;
            _attackVisualInProgress = false;
            StartCoroutine(AttackRoutine());
        }

        public void OnRemoved()
        {
            StopAllCoroutines();
            _attackVisualInProgress = false;
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
                {
                    PlayAttackVisual(target.transform.position);
                    target.TakeDamage(EffectiveAttackDamage);
                }
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

        private void CacheModelParts()
        {
            _rightArm = transform.Find("RightArm");
            if (_rightArm == null) return;
            _rightArmBasePos = _rightArm.localPosition;
            _rightArmBaseScale = _rightArm.localScale;
        }

        private void PlayAttackVisual(Vector3 targetPosition)
        {
            if (_attackVisualInProgress) return;
            if (AttackRange >= RANGED_VISUAL_THRESHOLD)
                StartCoroutine(ThrowVisualRoutine(targetPosition));
            else
                StartCoroutine(PunchVisualRoutine());
        }

        private IEnumerator PunchVisualRoutine()
        {
            _attackVisualInProgress = true;
            if (_rightArm == null)
            {
                _attackVisualInProgress = false;
                yield break;
            }

            float t = 0f;
            float duration = PUNCH_VISUAL_DURATION;
            Vector3 endPos = _rightArmBasePos + new Vector3(PUNCH_VISUAL_EXTENSION_X, 0f, 0f);
            Vector3 endScale = new Vector3(_rightArmBaseScale.x * 1.25f, _rightArmBaseScale.y, _rightArmBaseScale.z);
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duration);
                _rightArm.localPosition = Vector3.Lerp(_rightArmBasePos, endPos, k);
                _rightArm.localScale = Vector3.Lerp(_rightArmBaseScale, endScale, k);
                yield return null;
            }

            t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duration);
                _rightArm.localPosition = Vector3.Lerp(endPos, _rightArmBasePos, k);
                _rightArm.localScale = Vector3.Lerp(endScale, _rightArmBaseScale, k);
                yield return null;
            }

            _rightArm.localPosition = _rightArmBasePos;
            _rightArm.localScale = _rightArmBaseScale;
            _attackVisualInProgress = false;
        }

        private IEnumerator ThrowVisualRoutine(Vector3 targetPosition)
        {
            _attackVisualInProgress = true;
            GameObject projectile = new GameObject("FighterProjectile");
            projectile.transform.position = transform.position + PROJECTILE_SPAWN_OFFSET;
            projectile.transform.localScale = PROJECTILE_SCALE;

            SpriteRenderer sr = projectile.AddComponent<SpriteRenderer>();
            SpriteRenderer sourceRenderer = _rightArm != null
                ? _rightArm.GetComponent<SpriteRenderer>()
                : GetComponentInChildren<SpriteRenderer>();
            sr.sprite = sourceRenderer != null ? sourceRenderer.sprite : null;
            sr.color = new Color(1f, 0.95f, 0.6f);
            sr.sortingOrder = 5;

            Vector3 start = projectile.transform.position;
            Vector3 end = targetPosition + new Vector3(0f, 0.1f, 0f);
            float t = 0f;
            float duration = 0.16f;
            while (t < duration && projectile != null)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duration);
                projectile.transform.position = Vector3.Lerp(start, end, k);
                yield return null;
            }

            if (projectile != null)
                Destroy(projectile);
            _attackVisualInProgress = false;
        }
    }
}
