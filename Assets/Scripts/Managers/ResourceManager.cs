using UnityEngine;
using Gymageddon.Core;

namespace Gymageddon.Managers
{
    /// <summary>
    /// Manages the player's Energy resource (equivalent to "sun" in Plants vs. Zombies).
    /// Energy is passively generated over time and awarded for killing enemies.
    /// </summary>
    public class ResourceManager : MonoBehaviour
    {
        public static ResourceManager Instance { get; private set; }

        [Header("Energy Settings")]
        [SerializeField] private int _startingEnergy   = 150;
        [SerializeField] private int _maxEnergy        = 999;
        [SerializeField] private float _regenPerSecond = 25f; // passive energy per second

        public int CurrentEnergy { get; private set; }

        private float _regenAccumulator;

        // ── Lifecycle ─────────────────────────────────────────────────
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            CurrentEnergy = _startingEnergy;
            GameEvents.RaiseEnergyChanged(CurrentEnergy);
        }

        private void Update()
        {
            // Passive regeneration
            _regenAccumulator += _regenPerSecond * Time.deltaTime;
            if (_regenAccumulator >= 1f)
            {
                int gained = Mathf.FloorToInt(_regenAccumulator);
                _regenAccumulator -= gained;
                AddEnergy(gained);
            }
        }

        // ── Public API ────────────────────────────────────────────────
        public bool CanAfford(int cost) => CurrentEnergy >= cost;

        public bool SpendEnergy(int cost)
        {
            if (!CanAfford(cost)) return false;
            CurrentEnergy -= cost;
            GameEvents.RaiseEnergyChanged(CurrentEnergy);
            return true;
        }

        public void AddEnergy(int amount)
        {
            CurrentEnergy = Mathf.Min(CurrentEnergy + amount, _maxEnergy);
            GameEvents.RaiseEnergyChanged(CurrentEnergy);
        }
    }
}
