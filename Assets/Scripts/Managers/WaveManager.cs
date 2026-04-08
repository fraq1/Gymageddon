using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Gymageddon.Core;
using Gymageddon.Data;
using Gymageddon.Entities;

namespace Gymageddon.Managers
{
    /// <summary>
    /// Spawns enemy waves. Before each wave a preparation phase is triggered:
    /// 3 random unit cards are offered to the player, and enemies only spawn
    /// after the player presses "Start Wave" or the preparation timer expires.
    /// </summary>
    public class WaveManager : MonoBehaviour
    {
        [Header("Waves")]
        [SerializeField] private List<WaveData> _waves = new List<WaveData>();

        [Header("Spawn")]
        [SerializeField] private float _spawnX = 9f;
        [SerializeField] private float _leftBoundary = -8f;

        [Header("Preparation Phase")]
        [SerializeField] private float _preparationTime = 30f; // seconds to place units before wave
        [SerializeField] private int   _cardsPerWave    = 3;

        // Lane Y positions — set by GameBootstrap
        private float[] _laneYPositions = new float[GameBoard.LANE_COUNT];

        // Card pool — set by GameBootstrap
        private List<CharacterData> _characterPool = new List<CharacterData>();
        private List<TrainerData>   _trainerPool   = new List<TrainerData>();

        // Enemy prefab template (plain GO, script added at runtime)
        private GameObject _enemyTemplate;

        private int  _currentWave = -1;
        private bool _wavesStarted;
        private bool _preparationEnded;

        // ── Lifecycle ─────────────────────────────────────────────────
        private void Awake()
        {
            GameEvents.OnPreparationPhaseEnded += OnPreparationEnded;
        }

        private void OnDestroy()
        {
            GameEvents.OnPreparationPhaseEnded -= OnPreparationEnded;
        }

        private void OnPreparationEnded() => _preparationEnded = true;

        // ── Injection ─────────────────────────────────────────────────
        public void SetLaneYPositions(float[] positions) => _laneYPositions = positions;
        public void SetEnemyTemplate(GameObject template) => _enemyTemplate = template;
        public void AddWave(WaveData wave) => _waves.Add(wave);

        public void SetCardPool(List<CharacterData> chars, List<TrainerData> trainers)
        {
            _characterPool = new List<CharacterData>(chars);
            _trainerPool   = new List<TrainerData>(trainers);
        }

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
            // Wait one frame so all MonoBehaviour.Start() methods have run
            // (GameUI subscribes to events in its Start).
            yield return null;

            for (int i = 0; i < _waves.Count; i++)
            {
                _currentWave = i;
                WaveData wave = _waves[i];

                // ── Preparation phase: offer cards, wait for player ────
                _preparationEnded = false;
                List<UnitCard> cards = PickRandomCards(_cardsPerWave);
                GameEvents.RaisePreparationPhaseStarted(i + 1, _waves.Count, cards, _preparationTime);
                Debug.Log($"[WaveManager] Preparation for wave {i + 1}/{_waves.Count} — {cards.Count} cards offered");

                yield return new WaitUntil(() => _preparationEnded);

                // ── Wave starts ───────────────────────────────────────
                GameEvents.RaiseWaveStarted(i + 1, _waves.Count);
                Debug.Log($"[WaveManager] Wave {i + 1}/{_waves.Count}: {wave.waveName} — spawning enemies");

                yield return StartCoroutine(SpawnWave(wave));

                // Wait until all enemies in this wave are dead before next
                yield return new WaitUntil(() => !AnyEnemiesAlive());
                yield return new WaitForSeconds(3f);
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

        // ── Card picking ──────────────────────────────────────────────
        private List<UnitCard> PickRandomCards(int count)
        {
            var pool = new List<UnitCard>();
            foreach (CharacterData c in _characterPool) pool.Add(new UnitCard(c));
            foreach (TrainerData   t in _trainerPool)   pool.Add(new UnitCard(t));

            // Fisher-Yates shuffle
            for (int i = pool.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (pool[i], pool[j]) = (pool[j], pool[i]);
            }

            return pool.Count <= count ? pool : pool.GetRange(0, count);
        }

        // ── Helpers ───────────────────────────────────────────────────
        private bool AnyEnemiesAlive() => FindAnyObjectByType<Enemy>() != null;

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

