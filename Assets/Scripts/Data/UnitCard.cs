using UnityEngine;

namespace Gymageddon.Data
{
    /// <summary>
    /// Wraps either a CharacterData or TrainerData as a single selectable "card"
    /// that is offered to the player at the start of each wave.
    /// </summary>
    public class UnitCard
    {
        public CharacterData CharacterData { get; }
        public TrainerData   TrainerData   { get; }

        public bool IsCharacter => CharacterData != null;
        public bool IsTrainer   => TrainerData   != null;

        public string Name      => IsCharacter ? CharacterData.characterName : TrainerData.trainerName;
        public Color  CardColor => IsCharacter ? CharacterData.bodyColor     : TrainerData.bodyColor;
        public int    Cost      => IsCharacter ? CharacterData.energyCost    : TrainerData.energyCost;
        public string TypeLabel => IsCharacter ? "FIGHTER"                   : "TRAINER";
        public string Description => IsCharacter ? CharacterData.description : TrainerData.description;
        public string StatsSummary => IsCharacter
            ? $"DMG {CharacterData.attackDamage}  RNG {CharacterData.attackRange:0.#}  SPD {CharacterData.attackSpeed:0.##}/s"
            : BuildTrainerSummary(TrainerData);

        public UnitCard(CharacterData data) { CharacterData = data; }
        public UnitCard(TrainerData data)   { TrainerData   = data; }

        private static string BuildTrainerSummary(TrainerData data)
        {
            if (data == null) return string.Empty;
            switch (data.effectType)
            {
                case TrainerEffectType.DamageBoost:
                    return $"BUFF: +{Mathf.RoundToInt(data.effectValue * 100f)}% damage";
                case TrainerEffectType.AttackSpeedBoost:
                    return $"BUFF: +{Mathf.RoundToInt(data.effectValue * 100f)}% attack speed";
                case TrainerEffectType.HealthBoost:
                    return $"BUFF: +{Mathf.RoundToInt(data.effectValue * 100f)}% max HP";
                case TrainerEffectType.EnergyRegen:
                    return $"BUFF: +{data.energyRegenPerSecond:0.##}/s energy";
                default:
                    return "BUFF";
            }
        }
    }
}
