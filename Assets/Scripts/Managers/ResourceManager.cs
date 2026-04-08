using UnityEngine;
using Gymageddon.Core;

namespace Gymageddon.Managers
{
    /// <summary>
    /// Legacy no-op resource manager kept for wiring compatibility.
    /// Energy gameplay is disabled.
    /// </summary>
    public class ResourceManager : MonoBehaviour
    {
        public static ResourceManager Instance { get; private set; }

        public int CurrentEnergy { get; private set; }

        // ── Lifecycle ─────────────────────────────────────────────────
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            CurrentEnergy = 0;
        }

        // ── Public API ────────────────────────────────────────────────
        public bool CanAfford(int cost) => true;

        // Signature is kept for compatibility with existing callers.
        public bool SpendEnergy(int cost)
        {
            return true;
        }

        public void AddEnergy(int amount)
        {
            // Energy removed from gameplay.
        }
    }
}
