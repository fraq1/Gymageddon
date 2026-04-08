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
        private const float ENEMY_ROTATION_DEGREES = 45f;

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
        private Queue<int> _pendingSpawnLanes = new Queue<int>();

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
                (Queue<int> spawnPlan, List<int> previewLanes) = BuildSpawnLanePlan(wave, i);
                _pendingSpawnLanes = spawnPlan;

                // ── Preparation phase: offer cards, wait for player ────
                _preparationEnded = false;
                List<UnitCard> cards = PickRandomCards(_cardsPerWave);
                GameEvents.RaiseWaveDirectionsPreviewed(i + 1, previewLanes);
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
                    int lane = _pendingSpawnLanes.Count > 0
                        ? _pendingSpawnLanes.Dequeue()
                        : Random.Range(0, GameBoard.LANE_COUNT);
                    SpawnEnemy(group.enemyData, lane);
                    yield return new WaitForSeconds(group.spawnInterval);
                }
            }
        }

        private void SpawnEnemy(EnemyData data, int lane)
        {
            float y  = _laneYPositions[lane];

            GameObject go = _enemyTemplate != null
                ? Instantiate(_enemyTemplate)
                : CreateEnemyGameObject(data.bodyColor, lane);

            go.name = $"Enemy_{data.enemyName}_L{lane}";
            go.transform.position = new Vector3(_spawnX, y, 0f);

            Enemy enemy = go.AddComponent<Enemy>();
            enemy.Init(data, lane, _leftBoundary);
        }

        private (Queue<int> spawnPlan, List<int> previewLanes) BuildSpawnLanePlan(WaveData wave, int waveIndex)
        {
            Queue<int> spawnPlan = new Queue<int>();
            HashSet<int> preview = new HashSet<int>();

            int maxDirections = waveIndex == 0
                ? Mathf.Min(2, GameBoard.LANE_COUNT) // first wave (zero-based index 0) uses at most 2 lanes
                : GameBoard.LANE_COUNT;

            List<int> allowedLanes = PickRandomLanes(maxDirections);
            if (allowedLanes.Count == 0)
                allowedLanes.Add(0);

            foreach (WaveData.EnemySpawn group in wave.enemyGroups)
            {
                if (group.enemyData == null || group.count <= 0) continue;

                for (int i = 0; i < group.count; i++)
                {
                    int lane = allowedLanes[Random.Range(0, allowedLanes.Count)];
                    spawnPlan.Enqueue(lane);
                    preview.Add(lane);
                }
            }

            List<int> previewLanes = new List<int>(preview);
            previewLanes.Sort();
            return (spawnPlan, previewLanes);
        }

        private List<int> PickRandomLanes(int count)
        {
            List<int> allLanes = new List<int>();
            for (int i = 0; i < GameBoard.LANE_COUNT; i++)
                allLanes.Add(i);

            for (int i = allLanes.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (allLanes[i], allLanes[j]) = (allLanes[j], allLanes[i]);
            }

            int take = Mathf.Clamp(count, 0, allLanes.Count);
            return allLanes.GetRange(0, take);
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

        private GameObject CreateEnemyGameObject(Color color, int lane)
        {
            GameObject go = new GameObject("Enemy");
            CreateEnemyOutline(go.transform, new Color(0.35f, 0.05f, 0.05f, 0.9f));

            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = CreateColoredSprite(color);
            sr.sortingOrder = 2;
            go.transform.rotation = Quaternion.Euler(0f, 0f, ENEMY_ROTATION_DEGREES);
            go.transform.localScale = new Vector3(0.7f, 0.7f, 1f);
            CreateEnemyBadge(go.transform, $"M{lane + 1}");
            return go;
        }

        private void CreateEnemyOutline(Transform parent, Color color)
        {
            GameObject outline = new GameObject("EnemyOutline");
            outline.transform.SetParent(parent, false);
            outline.transform.localPosition = Vector3.zero;
            outline.transform.localScale = new Vector3(0.9f, 0.9f, 1f);

            SpriteRenderer sr = outline.AddComponent<SpriteRenderer>();
            sr.sprite = CreateColoredSprite(color);
            sr.sortingOrder = 1;
        }

        private void CreateEnemyBadge(Transform parent, string text)
        {
            GameObject badge = new GameObject("EnemyBadge");
            badge.transform.SetParent(parent, false);
            badge.transform.localPosition = new Vector3(0f, 0f, -0.1f);
            badge.transform.localRotation = Quaternion.Euler(0f, 0f, -45f);
            badge.transform.localScale = Vector3.one * 0.16f;

            TextMesh tm = badge.AddComponent<TextMesh>();
            tm.text = text;
            tm.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            tm.fontSize = 80;
            tm.characterSize = 0.1f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = new Color(1f, 0.95f, 0.85f);

            MeshRenderer mr = badge.GetComponent<MeshRenderer>();
            if (mr != null)
                mr.sortingOrder = 3;
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
