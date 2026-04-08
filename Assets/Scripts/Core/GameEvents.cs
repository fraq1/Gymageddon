using System;
using UnityEngine;

namespace Gymageddon.Core
{
    /// <summary>
    /// Central static event bus used by all systems to communicate without tight coupling.
    /// </summary>
    public static class GameEvents
    {
        // ── Resource ──────────────────────────────────────────────────
        public static event Action<int> OnEnergyChanged;
        public static void RaiseEnergyChanged(int newAmount) => OnEnergyChanged?.Invoke(newAmount);

        // ── Placement ─────────────────────────────────────────────────
        public static event Action<int, Entities.Character> OnCharacterPlaced;
        public static void RaiseCharacterPlaced(int laneIndex, Entities.Character ch) =>
            OnCharacterPlaced?.Invoke(laneIndex, ch);

        public static event Action<int, Entities.Trainer> OnTrainerPlaced;
        public static void RaiseTrainerPlaced(int laneIndex, Entities.Trainer t) =>
            OnTrainerPlaced?.Invoke(laneIndex, t);

        // ── Combat ────────────────────────────────────────────────────
        public static event Action<int> OnEnemyReachedBase;   // laneIndex
        public static void RaiseEnemyReachedBase(int laneIndex) => OnEnemyReachedBase?.Invoke(laneIndex);

        public static event Action<Entities.Enemy> OnEnemyKilled;
        public static void RaiseEnemyKilled(Entities.Enemy enemy) => OnEnemyKilled?.Invoke(enemy);

        // ── Game State ────────────────────────────────────────────────
        public static event Action<GameState> OnGameStateChanged;
        public static void RaiseGameStateChanged(GameState state) => OnGameStateChanged?.Invoke(state);

        // ── Waves ─────────────────────────────────────────────────────
        public static event Action<int, int> OnWaveStarted;    // (waveNumber, totalWaves)
        public static void RaiseWaveStarted(int wave, int total) => OnWaveStarted?.Invoke(wave, total);

        public static event Action OnAllWavesComplete;
        public static void RaiseAllWavesComplete() => OnAllWavesComplete?.Invoke();
    }

    public enum GameState
    {
        Preparing,
        Playing,
        Paused,
        Victory,
        Defeat
    }
}
