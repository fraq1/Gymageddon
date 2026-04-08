using System.Collections.Generic;
using UnityEngine;
using Gymageddon.Data;

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
            GameEvents.OnEnemyReachedBase        += HandleEnemyReachedBase;
            GameEvents.OnAllWavesComplete        += HandleAllWavesComplete;
            GameEvents.OnPreparationPhaseStarted += HandlePreparationPhaseStarted;
        }

        private void OnDestroy()
        {
            GameEvents.OnEnemyReachedBase        -= HandleEnemyReachedBase;
            GameEvents.OnAllWavesComplete        -= HandleAllWavesComplete;
            GameEvents.OnPreparationPhaseStarted -= HandlePreparationPhaseStarted;
        }

        // ── Start / flow ──────────────────────────────────────────────
        private void Start()
        {
            // State starts as Preparing (default). WaveManager will raise
            // OnPreparationPhaseStarted before the first wave, which keeps
            // us in the Preparing state and shows the card selection UI.
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

        // ── Preparation phase ─────────────────────────────────────────
        private void HandlePreparationPhaseStarted(int waveNumber, int totalWaves,
            List<UnitCard> cards, float timeLimit)
        {
            SetState(GameState.Preparing);
        }

        /// <summary>
        /// Called by the UI when the player presses "Start Wave" or the timer runs out.
        /// Transitions to Playing and signals WaveManager to spawn enemies.
        /// </summary>
        public void EndPreparationPhase()
        {
            if (CurrentState != GameState.Preparing) return;
            SetState(GameState.Playing);
            GameEvents.RaisePreparationPhaseEnded();
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
