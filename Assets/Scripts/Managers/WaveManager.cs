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
    /// random unit cards are offered to the player, and enemies only spawn
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
        [SerializeField] private int   _cardsPerWave    = 5;

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
            enemy.Init(data, lane, _leftBoundary, y);
        }

        private (Queue<int> spawnPlan, List<int> previewLanes) BuildSpawnLanePlan(WaveData wave, int waveIndex)
        {
            Queue<int> spawnPlan = new Queue<int>();
            HashSet<int> preview = new HashSet<int>();

            int maxDirections = Mathf.Clamp(waveIndex + 2, 2, GameBoard.LANE_COUNT);

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
            List<UnitCard> characterCards = new List<UnitCard>();
            List<UnitCard> trainerCards   = new List<UnitCard>();

            foreach (CharacterData c in _characterPool) characterCards.Add(new UnitCard(c));
            foreach (TrainerData   t in _trainerPool)   trainerCards.Add(new UnitCard(t));

            List<UnitCard> result = new List<UnitCard>();
            int targetCount = Mathf.Max(1, count);

            int guaranteedCharacters = characterCards.Count > 0 && trainerCards.Count > 0
                ? Mathf.Min(characterCards.Count, Mathf.Max(1, targetCount / 2))
                : 0;

            TakeRandomCards(characterCards, guaranteedCharacters, result);
            targetCount -= result.Count;

            if (targetCount > 0)
                TakeRandomCards(trainerCards, Mathf.Min(targetCount, trainerCards.Count), result);

            targetCount = Mathf.Max(0, count - result.Count);
            if (targetCount > 0)
            {
                List<UnitCard> remaining = new List<UnitCard>();
                remaining.AddRange(characterCards);
                remaining.AddRange(trainerCards);
                TakeRandomCards(remaining, Mathf.Min(targetCount, remaining.Count), result);
            }

            return result;
        }

        private void TakeRandomCards(List<UnitCard> source, int count, List<UnitCard> destination)
        {
            for (int i = source.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (source[i], source[j]) = (source[j], source[i]);
            }

            int take = Mathf.Clamp(count, 0, source.Count);
            for (int i = 0; i < take; i++)
                destination.Add(source[i]);

            if (take > 0)
                source.RemoveRange(0, take);
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
            go.transform.rotation = Quaternion.identity;
            go.transform.localScale = new Vector3(0.72f, 0.72f, 1f);
            CreateEnemyFace(go.transform);
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

        private void CreateEnemyFace(Transform parent)
        {
            CreateFacePart(parent, "LeftEye", new Vector3(-0.22f, 0.12f, -0.05f), new Vector3(0.18f, 0.18f, 1f), Color.white, 3);
            CreateFacePart(parent, "RightEye", new Vector3(0.22f, 0.12f, -0.05f), new Vector3(0.18f, 0.18f, 1f), Color.white, 3);
            CreateFacePart(parent, "LeftPupil", new Vector3(-0.22f, 0.12f, -0.08f), new Vector3(0.08f, 0.08f, 1f), Color.black, 4);
            CreateFacePart(parent, "RightPupil", new Vector3(0.22f, 0.12f, -0.08f), new Vector3(0.08f, 0.08f, 1f), Color.black, 4);
            CreateFacePart(parent, "Mouth", new Vector3(0f, -0.2f, -0.05f), new Vector3(0.42f, 0.10f, 1f), new Color(0.35f, 0f, 0f, 0.95f), 3);
        }

        private void CreateFacePart(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Color color, int sortingOrder)
        {
            GameObject part = new GameObject(name);
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = localScale;

            SpriteRenderer sr = part.AddComponent<SpriteRenderer>();
            sr.sprite = CreateColoredSprite(color);
            sr.sortingOrder = sortingOrder;
        }

        private void CreateEnemyBadge(Transform parent, string text)
        {
            GameObject badge = new GameObject("EnemyBadge");
            badge.transform.SetParent(parent, false);
            badge.transform.localPosition = new Vector3(0f, 0.36f, -0.1f);
            badge.transform.localRotation = Quaternion.identity;
            badge.transform.localScale = Vector3.one * 0.12f;

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
