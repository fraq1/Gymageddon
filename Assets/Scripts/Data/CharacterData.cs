using UnityEngine;

namespace Gymageddon.Data
{
    [CreateAssetMenu(fileName = "CharacterData", menuName = "Gymageddon/Character Data")]
    public class CharacterData : ScriptableObject
    {
        [Header("Identity")]
        public string characterName = "Boxer";
        [TextArea] public string description = "A gym fighter that punches enemies.";

        [Header("Stats")]
        public int maxHealth = 100;
        public int attackDamage = 20;
        public float attackSpeed = 1f;   // attacks per second
        public float attackRange = 3f;

        [Header("Cost")]
        public int energyCost = 100;

        [Header("Visual")]
        public Color bodyColor = new Color(0.29f, 0.56f, 0.89f); // blue
    }
}
