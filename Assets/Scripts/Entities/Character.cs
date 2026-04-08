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
        private const float MIN_ATTACK_SPEED = 0.05f;
        private const float EVOLUTION_DAMAGE_MULTIPLIER = 1.9f;
        private const float EVOLUTION_SPEED_MULTIPLIER = 1.5f;
        private const float EVOLUTION_HEALTH_MULTIPLIER = 2.0f;
        private const int EVOLUTION_VISUAL_SORTING_ORDER = 4;
        private static readonly Vector3 PROJECTILE_SPAWN_OFFSET = new Vector3(0.25f, 0.12f, 0f);
        private static readonly Vector3 PROJECTILE_SCALE = new Vector3(0.14f, 0.14f, 1f);

        // Loaded from CharacterData
        public CharacterData Data { get; private set; }
        public string CharacterName => Data ? Data.characterName : "Unknown";

        // Effective stats (may be boosted by Trainer)
        public int   EffectiveAttackDamage { get; private set; }
        public float EffectiveAttackSpeed  { get; private set; }
        public float AttackRange           { get; private set; }
        public bool IsEvolved { get; private set; }
        public int LaneIndex => _lane != null ? _lane.LaneIndex : -1;

        private Lane   _lane;
        private float _attackCooldown;
        private Transform _rightArm;
        private Vector3 _rightArmBasePos;
        private Vector3 _rightArmBaseScale;
        private bool _attackVisualInProgress;
        private float _trainerDamageMultiplier = 1f;
        private float _trainerSpeedMultiplier = 1f;
        private float _trainerHealthMultiplier = 1f;
        private float _evolutionDamageMultiplier = 1f;
        private float _evolutionSpeedMultiplier = 1f;
        private float _evolutionHealthMultiplier = 1f;
        private bool _hasEvolutionVisual;
        private static Sprite _whiteSprite;

        // ── Setup ─────────────────────────────────────────────────────
        public void Init(CharacterData data)
        {
            Data                   = data;
            AttackRange            = data.attackRange;
            IsEvolved              = false;
            _trainerDamageMultiplier = 1f;
            _trainerSpeedMultiplier = 1f;
            _trainerHealthMultiplier = 1f;
            _evolutionDamageMultiplier = 1f;
            _evolutionSpeedMultiplier = 1f;
            _evolutionHealthMultiplier = 1f;
            _hasEvolutionVisual = false;
            InitHealth(data.maxHealth);
            RecalculateCombatStats();
            RecalculateHealthKeepingRatio();
            ApplyVisual(data.bodyColor);
            CacheModelParts();
        }

        public void OnPlaced(Lane lane)
        {
            _lane     = lane;
            _attackCooldown = EffectiveAttackSpeed > 0f
                ? 1f / EffectiveAttackSpeed
                : float.MaxValue;
            _attackVisualInProgress = false;
        }

        public void OnRemoved()
        {
            StopAllCoroutines();
            _attackVisualInProgress = false;
            _lane = null;
        }

        // ── Trainer buffs ─────────────────────────────────────────────
        public void ApplyDamageBoost(float multiplier)
        {
            _trainerDamageMultiplier = 1f + multiplier;
            RecalculateCombatStats();
        }

        public void ApplySpeedBoost(float multiplier)
        {
            _trainerSpeedMultiplier = 1f + multiplier;
            RecalculateCombatStats();
        }

        public void ApplyHealthBoost(float multiplier)
        {
            _trainerHealthMultiplier = 1f + multiplier;
            RecalculateHealthKeepingRatio();
        }

        public void RemoveDamageBoost()
        {
            _trainerDamageMultiplier = 1f;
            RecalculateCombatStats();
        }

        public void RemoveSpeedBoost()
        {
            _trainerSpeedMultiplier = 1f;
            RecalculateCombatStats();
        }

        public void RemoveHealthBoost()
        {
            _trainerHealthMultiplier = 1f;
            RecalculateHealthKeepingRatio();
        }

        public bool TryEvolveWith(CharacterData incomingData)
        {
            if (IsEvolved || incomingData == null || Data == null) return false;
            if (!string.Equals(incomingData.characterName, Data.characterName, System.StringComparison.OrdinalIgnoreCase))
                return false;

            IsEvolved = true;
            _evolutionDamageMultiplier = EVOLUTION_DAMAGE_MULTIPLIER;
            _evolutionSpeedMultiplier = EVOLUTION_SPEED_MULTIPLIER;
            _evolutionHealthMultiplier = EVOLUTION_HEALTH_MULTIPLIER;
            RecalculateCombatStats();
            RecalculateHealthKeepingRatio();
            AddEvolutionVisual();
            return true;
        }

        private void Update()
        {
            if (_lane == null || IsDead) return;

            _attackCooldown -= Time.deltaTime;
            if (_attackCooldown > 0f) return;

            Enemy target = FindNearestEnemy();
            if (target != null)
            {
                PlayAttackVisual(target.transform.position);
                target.TakeDamage(EffectiveAttackDamage);
            }

            _attackCooldown = EffectiveAttackSpeed > 0f
                ? 1f / EffectiveAttackSpeed
                : float.MaxValue;
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

        private void RecalculateCombatStats()
        {
            if (Data == null) return;
            EffectiveAttackDamage = Mathf.Max(1, Mathf.RoundToInt(
                Data.attackDamage * _evolutionDamageMultiplier * _trainerDamageMultiplier));
            EffectiveAttackSpeed = Mathf.Max(MIN_ATTACK_SPEED, Data.attackSpeed * _evolutionSpeedMultiplier * _trainerSpeedMultiplier);
        }

        private void RecalculateHealthKeepingRatio()
        {
            if (Data == null) return;
            int oldMax = Mathf.Max(MaxHealth, 1);
            float ratio = Mathf.Clamp01((float)CurrentHealth / oldMax);
            MaxHealth = Mathf.Max(1, Mathf.RoundToInt(
                Data.maxHealth * _evolutionHealthMultiplier * _trainerHealthMultiplier));
            CurrentHealth = Mathf.Clamp(Mathf.RoundToInt(ratio * MaxHealth), 0, MaxHealth);
        }

        private void AddEvolutionVisual()
        {
            if (_hasEvolutionVisual || transform.Find("EvolutionDumbbells") != null) return;

            GameObject root = new GameObject("EvolutionDumbbells");
            root.transform.SetParent(transform, false);
            root.transform.localPosition = new Vector3(0f, 0.44f, -0.02f);

            CreateDumbbell(root.transform, new Vector3(-0.18f, 0f, 0f));
            CreateDumbbell(root.transform, new Vector3(0.18f, 0f, 0f));
            _hasEvolutionVisual = true;
        }

        private void CreateDumbbell(Transform parent, Vector3 localPosition)
        {
            GameObject dumbbell = new GameObject("Dumbbell");
            dumbbell.transform.SetParent(parent, false);
            dumbbell.transform.localPosition = localPosition;

            CreateVisualPart(dumbbell.transform, "Handle", Vector3.zero, new Vector3(0.07f, 0.018f, 1f), new Color(0.80f, 0.60f, 0.10f));
            CreateVisualPart(dumbbell.transform, "PlateL", new Vector3(-0.04f, 0f, 0f), new Vector3(0.03f, 0.04f, 1f), new Color(1f, 0.84f, 0.20f));
            CreateVisualPart(dumbbell.transform, "PlateR", new Vector3(0.04f, 0f, 0f), new Vector3(0.03f, 0.04f, 1f), new Color(1f, 0.84f, 0.20f));
        }

        private void CreateVisualPart(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Color color)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = localScale;

            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = GetWhiteSprite();
            sr.color = color;
            sr.sortingOrder = EVOLUTION_VISUAL_SORTING_ORDER;
        }

        private static Sprite GetWhiteSprite()
        {
            if (_whiteSprite != null) return _whiteSprite;
            _whiteSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
            return _whiteSprite;
        }
    }
}
