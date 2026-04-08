using System.Collections;
using UnityEngine;
using Gymageddon.Core;
using Gymageddon.Data;

namespace Gymageddon.Entities
{
    /// <summary>
    /// An enemy that moves along a lane from right to left.
    /// When it reaches the left boundary it damages the player's base.
    /// </summary>
    public class Enemy : Unit
    {
        private const float MELEE_RANGE = 0.75f;
        private const float RETARGET_INTERVAL = 0.2f;

        public EnemyData Data        { get; private set; }
        public int       LaneIndex   { get; private set; }

        private float _moveSpeed;
        private float _leftBoundary = -8f; // x position of player base
        private float _laneY;
        private bool  _blocked;
        private Character _target;
        private float _retargetTimer;

        // ── Setup ─────────────────────────────────────────────────────
        public void Init(EnemyData data, int laneIndex, float leftBoundary)
        {
            Data          = data;
            LaneIndex     = laneIndex;
            _moveSpeed    = data.moveSpeed;
            _leftBoundary = leftBoundary;
            _laneY        = transform.position.y;
            _retargetTimer = 0f;

            InitHealth(data.maxHealth);
            ApplyVisual(data.bodyColor);
            StartCoroutine(AttackRoutine());
        }

        // ── Movement ──────────────────────────────────────────────────
        private void Update()
        {
            _retargetTimer -= Time.deltaTime;
            if (_retargetTimer <= 0f || _target == null || _target.IsDead)
            {
                _target = FindNearestCharacterInLane();
                _retargetTimer = RETARGET_INTERVAL;
            }

            _blocked = _target != null && !_target.IsDead && IsTargetInMeleeRange(_target);
            if (_blocked) return;
            Vector3 position = transform.position;
            position.x -= _moveSpeed * Time.deltaTime;
            position.y = _laneY;
            transform.position = position;

            if (position.x <= _leftBoundary)
            {
                GameEvents.RaiseEnemyReachedBase(LaneIndex);
                Destroy(gameObject);
            }
        }

        // ── Combat ────────────────────────────────────────────────────
        private IEnumerator AttackRoutine()
        {
            while (true)
            {
                float interval = Data.attackSpeed > 0f ? 1f / Data.attackSpeed : float.MaxValue;
                yield return new WaitForSeconds(interval);

                // Find the character blocking this lane
                if (_target == null || _target.IsDead || !IsTargetInMeleeRange(_target))
                {
                    _target = FindNearestCharacterInLane();
                    _retargetTimer = RETARGET_INTERVAL;
                }

                if (_target != null && !_target.IsDead && IsTargetInMeleeRange(_target))
                    _target.TakeDamage(Data.attackDamage);
            }
        }

        private Character FindNearestCharacterInLane()
        {
            // Look for the nearest character in the same lane that is ahead (to the left)
            Character[] all = FindObjectsByType<Character>(FindObjectsSortMode.None);
            Character nearest = null;
            float minDist = float.MaxValue;
            foreach (Character c in all)
            {
                // Same-lane check via lane index stored on the Character's Lane reference
                var lane = c.GetComponentInParent<Lane>();
                if (lane == null || lane.LaneIndex != LaneIndex) continue;
                if (c.transform.position.x > transform.position.x) continue; // skip those behind the enemy

                float dist = transform.position.x - c.transform.position.x;
                if (dist < minDist) { minDist = dist; nearest = c; }
            }
            return nearest;
        }

        private bool IsTargetInMeleeRange(Character character)
        {
            if (character == null) return false;
            if (character.transform.position.x >= transform.position.x) return false;
            float dist = transform.position.x - character.transform.position.x;
            return dist <= MELEE_RANGE;
        }

        // ── Death ─────────────────────────────────────────────────────
        protected override void OnDied()
        {
            StopAllCoroutines();
            GameEvents.RaiseEnemyKilled(this);
            Debug.Log($"[Enemy] {Data.enemyName} in lane {LaneIndex} killed.");
        }

        // ── Visual ────────────────────────────────────────────────────
        private void ApplyVisual(Color color)
        {
            SpriteRenderer sr = GetComponent<SpriteRenderer>();
            if (sr != null) sr.color = color;
        }
    }
}
