using System.Collections;
using System.Threading;
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
        private const float DIAGONAL_VERTICAL_FACTOR = 0.55f;
        private static int _spawnCounter;

        public EnemyData Data        { get; private set; }
        public int       LaneIndex   { get; private set; }

        private float _moveSpeed;
        private float _leftBoundary = -8f; // x position of player base
        private float _laneCenterY;
        private float _laneHalfHeight;
        private Vector3 _moveDirection;
        private bool  _blocked;
        private Character _target;
        private float _retargetTimer;

        // ── Setup ─────────────────────────────────────────────────────
        public void Init(EnemyData data, int laneIndex, float leftBoundary, float laneCenterY, float laneHalfHeight)
        {
            Data          = data;
            LaneIndex     = laneIndex;
            _moveSpeed    = data.moveSpeed;
            _leftBoundary = leftBoundary;
            _laneCenterY  = laneCenterY;
            _laneHalfHeight = Mathf.Max(0.1f, laneHalfHeight);
            float verticalSign = (Interlocked.Increment(ref _spawnCounter) & 1) == 0 ? -1f : 1f;
            _moveDirection = new Vector3(-1f, verticalSign * DIAGONAL_VERTICAL_FACTOR, 0f).normalized;
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
            transform.position += _moveDirection * _moveSpeed * Time.deltaTime;
            KeepInsideLaneBounds();

            if (transform.position.x <= _leftBoundary)
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

                float dist = Vector2.Distance(transform.position, c.transform.position);
                if (dist < minDist) { minDist = dist; nearest = c; }
            }
            return nearest;
        }

        private bool IsTargetInMeleeRange(Character character)
        {
            if (character == null) return false;
            if (character.transform.position.x >= transform.position.x) return false;
            float dist = Vector2.Distance(transform.position, character.transform.position);
            return dist <= MELEE_RANGE;
        }

        private void KeepInsideLaneBounds()
        {
            float minY = _laneCenterY - _laneHalfHeight;
            float maxY = _laneCenterY + _laneHalfHeight;
            Vector3 pos = transform.position;

            if (pos.y < minY)
            {
                pos.y = minY;
                _moveDirection = new Vector3(_moveDirection.x, Mathf.Abs(_moveDirection.y), 0f);
            }
            else if (pos.y > maxY)
            {
                pos.y = maxY;
                _moveDirection = new Vector3(_moveDirection.x, -Mathf.Abs(_moveDirection.y), 0f);
            }

            transform.position = pos;
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
