using System;
using DungeonGenerator;
using UnityEngine;

namespace CleanupCrawler.Levels
{
    [Serializable]
    public class LevelEnemyEntry
    {
        [SerializeField] private GameObject enemyPrefab;
        [SerializeField] private WeaponItemDefinition weapon;
        [SerializeField, Min(1)] private int weight = 1;

        public GameObject EnemyPrefab => enemyPrefab;
        public WeaponItemDefinition Weapon => weapon;
        public int Weight => Mathf.Max(1, weight);
    }
}
