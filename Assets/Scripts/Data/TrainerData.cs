using UnityEngine;

namespace Gymageddon.Data
{
    [CreateAssetMenu(fileName = "TrainerData", menuName = "Gymageddon/Trainer Data")]
    public class TrainerData : ScriptableObject
    {
        [Header("Identity")]
        public string trainerName = "Weight Rack";
        [TextArea] public string description = "Buffs the character in the same lane.";

        [Header("Effect")]
        public TrainerEffectType effectType = TrainerEffectType.DamageBoost;
        public float effectValue = 0.25f;   // e.g. 25% damage boost
        public float energyRegenPerSecond = 0f;

        [Header("Cost")]
        public int energyCost = 75;

        [Header("Visual")]
        public Color bodyColor = new Color(0.36f, 0.72f, 0.36f); // green
    }

    public enum TrainerEffectType
    {
        DamageBoost,
        AttackSpeedBoost,
        HealthBoost,
        EnergyRegen
    }
}
