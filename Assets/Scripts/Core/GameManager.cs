using UnityEngine;

namespace Gymageddon.Core
{
    /// <summary>
    /// Central game controller — manages state transitions and wires
    /// references between sub-systems.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public GameState CurrentState { get; private set; } = GameState.Preparing;

        [Header("Sub-systems (auto-wired by GameBootstrap)")]
        public GameBoard        Board;
        public Managers.ResourceManager  Resources;
        public Managers.WaveManager      Waves;
        public Managers.PlacementManager Placement;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            // Subscribe to events
            GameEvents.OnEnemyReachedBase   += HandleEnemyReachedBase;
            GameEvents.OnAllWavesComplete   += HandleAllWavesComplete;
        }

        private void OnDestroy()
        {
            GameEvents.OnEnemyReachedBase   -= HandleEnemyReachedBase;
            GameEvents.OnAllWavesComplete   -= HandleAllWavesComplete;
        }

        // ── Start / flow ──────────────────────────────────────────────
        private void Start()
        {
            SetState(GameState.Playing);
            Waves.StartWaves();
        }

        public void PauseGame()
        {
            if (CurrentState != GameState.Playing) return;
            SetState(GameState.Paused);
            Time.timeScale = 0f;
        }

        public void ResumeGame()
        {
            if (CurrentState != GameState.Paused) return;
            SetState(GameState.Playing);
            Time.timeScale = 1f;
        }

        // ── Internal handlers ─────────────────────────────────────────
        private int _baseLivesLost = 0;
        private const int MAX_BASE_HITS = 5; // lose after 5 enemies reach base

        private void HandleEnemyReachedBase(int laneIndex)
        {
            _baseLivesLost++;
            Debug.Log($"[GameManager] Enemy reached base via lane {laneIndex}. Lives lost: {_baseLivesLost}/{MAX_BASE_HITS}");

            if (_baseLivesLost >= MAX_BASE_HITS)
                SetState(GameState.Defeat);
        }

        private void HandleAllWavesComplete()
        {
            SetState(GameState.Victory);
        }

        private void SetState(GameState newState)
        {
            CurrentState = newState;
            GameEvents.RaiseGameStateChanged(newState);
            Debug.Log($"[GameManager] State -> {newState}");
        }
    }
}
