using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gymageddon.Data
{
    [CreateAssetMenu(fileName = "WaveData", menuName = "Gymageddon/Wave Data")]
    public class WaveData : ScriptableObject
    {
        [Serializable]
        public class EnemySpawn
        {
            public EnemyData enemyData;
            public int count = 3;
            public float spawnInterval = 2f;  // seconds between each enemy in this group
            public float delayBeforeGroup = 0f; // delay before this group starts spawning
        }

        [Header("Wave")]
        public string waveName = "Wave 1";
        public float delayBeforeWave = 5f;    // seconds before wave begins
        public List<EnemySpawn> enemyGroups = new List<EnemySpawn>();
    }
}
