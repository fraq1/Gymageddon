using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Gymageddon.Core;
using Gymageddon.Data;
using Gymageddon.Entities;

namespace Gymageddon.Managers
{
    /// <summary>
    /// Spawns enemy waves. Enemies arrive from the right side of the board,
    /// one per lane (randomised), according to WaveData definitions.
    /// </summary>
    public class WaveManager : MonoBehaviour
    {
        [Header("Waves")]
        [SerializeField] private List<WaveData> _waves = new List<WaveData>();

        [Header("Spawn")]
        [SerializeField] private float _spawnX = 9f;     // right edge of board
        [SerializeField] private float _leftBoundary = -8f; // where enemy reaches base

        // Lane Y positions — set by GameBootstrap
        private float[] _laneYPositions = new float[GameBoard.LANE_COUNT];

        // Enemy prefab template (plain GO, script added at runtime)
        private GameObject _enemyTemplate;

        private int  _currentWave = -1;
        private bool _wavesStarted;

        // ── Injection ─────────────────────────────────────────────────
        public void SetLaneYPositions(float[] positions) => _laneYPositions = positions;
        public void SetEnemyTemplate(GameObject template) => _enemyTemplate = template;

        public void AddWave(WaveData wave) => _waves.Add(wave);

        // ── Entry point ───────────────────────────────────────────────
        public void StartWaves()
        {
            if (_wavesStarted) return;
            _wavesStarted = true;
            StartCoroutine(WaveSequence());
        }

        // ── Wave loop ─────────────────────────────────────────────────
        private IEnumerator WaveSequence()
        {
            for (int i = 0; i < _waves.Count; i++)
            {
                _currentWave = i;
                WaveData wave = _waves[i];

                GameEvents.RaiseWaveStarted(i + 1, _waves.Count);
                Debug.Log($"[WaveManager] Wave {i + 1}/{_waves.Count}: {wave.waveName} — waiting {wave.delayBeforeWave}s");

                yield return new WaitForSeconds(wave.delayBeforeWave);

                yield return StartCoroutine(SpawnWave(wave));

                // Wait until all enemies in this wave are dead before starting next
                yield return new WaitUntil(() => !AnyEnemiesAlive());
                yield return new WaitForSeconds(3f); // brief gap between waves
            }

            GameEvents.RaiseAllWavesComplete();
        }

        private IEnumerator SpawnWave(WaveData wave)
        {
            foreach (WaveData.EnemySpawn group in wave.enemyGroups)
            {
                if (group.enemyData == null) continue;
                yield return new WaitForSeconds(group.delayBeforeGroup);

                for (int i = 0; i < group.count; i++)
                {
                    SpawnEnemy(group.enemyData);
                    yield return new WaitForSeconds(group.spawnInterval);
                }
            }
        }

        private void SpawnEnemy(EnemyData data)
        {
            // Pick a random lane
            int lane = Random.Range(0, GameBoard.LANE_COUNT);
            float y  = _laneYPositions[lane];

            GameObject go = _enemyTemplate != null
                ? Instantiate(_enemyTemplate)
                : CreateEnemyGameObject(data.bodyColor);

            go.name = $"Enemy_{data.enemyName}_L{lane}";
            go.transform.position = new Vector3(_spawnX, y, 0f);

            Enemy enemy = go.AddComponent<Enemy>();
            enemy.Init(data, lane, _leftBoundary);
        }

        // ── Helpers ───────────────────────────────────────────────────
        private bool AnyEnemiesAlive()
        {
            return FindAnyObjectByType<Enemy>() != null;
        }

        private GameObject CreateEnemyGameObject(Color color)
        {
            GameObject go = new GameObject("Enemy");
            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = CreateColoredSprite(color);
            sr.sortingOrder = 2;
            go.transform.localScale = new Vector3(0.7f, 0.7f, 1f);
            return go;
        }

        private Sprite CreateColoredSprite(Color color)
        {
            Texture2D tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, color);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        }
    }
}
