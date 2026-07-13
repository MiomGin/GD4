using Dungeon.EnemySystem;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.RoundSystem
{
    /// <summary>
    /// 负责生成当前小循环需要的测试敌人，
    /// 并将测试路径交给 EnemyMovement 执行。
    /// </summary>
    public sealed class EnemyWaveSpawner : MonoBehaviour
    {
        [Header("生成设置")]

        [SerializeField]
        private EnemyLifecycle enemyPrefab;

        [SerializeField]
        private Transform spawnPoint;

        [SerializeField, Min(1)]
        private int enemyCount = 3;

        [Header("测试路线")]

        [SerializeField]
        private Transform[] routePoints =
            Array.Empty<Transform>();

        /// <summary>
        /// 生成当前测试波次，并返回全部敌人实例。
        /// </summary>
        public List<EnemyLifecycle> SpawnWave()
        {
            List<EnemyLifecycle> spawnedEnemies =
                new List<EnemyLifecycle>();

            if (enemyPrefab == null ||
                spawnPoint == null)
            {
                Debug.LogError(
                    "EnemyWaveSpawner 的敌人 Prefab 或生成点未设置。",
                    this
                );

                return spawnedEnemies;
            }

            List<Vector3> path =
                BuildWorldPath();

            for (int i = 0; i < enemyCount; i++)
            {
                EnemyLifecycle enemy =
                    Instantiate(
                        enemyPrefab,
                        spawnPoint.position,
                        Quaternion.identity
                    );

                spawnedEnemies.Add(enemy);

                if (enemy.TryGetComponent(
                        out EnemyMovement movement))
                {
                    movement.SetPath(path);
                }
            }

            return spawnedEnemies;
        }

        private List<Vector3> BuildWorldPath()
        {
            List<Vector3> path =
                new List<Vector3>();

            if (routePoints == null)
            {
                return path;
            }

            foreach (Transform routePoint in routePoints)
            {
                if (routePoint != null)
                {
                    path.Add(routePoint.position);
                }
            }

            return path;
        }
    }
}