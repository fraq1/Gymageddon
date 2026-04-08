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
        public EnemyData Data        { get; private set; }
        public int       LaneIndex   { get; private set; }

        private float _moveSpeed;
        private float _leftBoundary = -8f; // x position of player base
        private bool  _blocked;
        private Character _target;

        // ── Setup ─────────────────────────────────────────────────────
        public void Init(EnemyData data, int laneIndex, float leftBoundary)
        {
            Data          = data;
            LaneIndex     = laneIndex;
            _moveSpeed    = data.moveSpeed;
            _leftBoundary = leftBoundary;

            InitHealth(data.maxHealth);
            ApplyVisual(data.bodyColor);
            StartCoroutine(AttackRoutine());
        }

        // ── Movement ──────────────────────────────────────────────────
        private void Update()
        {
            if (_blocked) return;
            transform.Translate(Vector3.left * _moveSpeed * Time.deltaTime);

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
                if (_target == null || _target.IsDead)
                {
                    _target  = FindBlockingCharacter();
                    _blocked = (_target != null);
                }

                if (_target != null && !_target.IsDead)
                    _target.TakeDamage(Data.attackDamage);
                else
                    _blocked = false;
            }
        }

        private Character FindBlockingCharacter()
        {
            // Look for a character in the same lane that is ahead (to the left)
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

        // ── Death ─────────────────────────────────────────────────────
        protected override void OnDied()
        {
            StopAllCoroutines();
            if (Managers.ResourceManager.Instance != null)
                Managers.ResourceManager.Instance.AddEnergy(Data.energyReward);
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
