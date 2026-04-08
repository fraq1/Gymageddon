using UnityEngine;

namespace Gymageddon.Data
{
    [CreateAssetMenu(fileName = "EnemyData", menuName = "Gymageddon/Enemy Data")]
    public class EnemyData : ScriptableObject
    {
        [Header("Identity")]
        public string enemyName = "Couch Potato";
        [TextArea] public string description = "A lazy enemy that slowly advances.";

        [Header("Stats")]
        public int maxHealth = 60;
        public int attackDamage = 10;
        public float attackSpeed = 0.5f;  // attacks per second
        public float moveSpeed = 1f;      // units per second

        [Header("Reward")]
        public int energyReward = 25;     // energy given to player on kill

        [Header("Visual")]
        public Color bodyColor = new Color(0.85f, 0.33f, 0.31f); // red
    }
}
