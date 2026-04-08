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

        public UnitCard(CharacterData data) { CharacterData = data; }
        public UnitCard(TrainerData data)   { TrainerData   = data; }
    }
}
