using Dungeon.EnemySystem;
using Dungeon.RoomSystem;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.RoundSystem
{
    /// <summary>
    /// 游戏大循环当前所处的阶段。
    /// </summary>
    public enum GameLoopState
    {
        /// <summary>
        /// 玩家可以放置或改造房间。
        /// </summary>
        Building,

        /// <summary>
        /// 当前小循环中的敌人正在探索地牢。
        /// </summary>
        EnemyRunning,

        /// <summary>
        /// 有敌人进入王座，当前大循环结束。
        /// </summary>
        GameOver
    }

    /// <summary>
    /// 控制建造、敌人探索、下一小循环和大循环失败。
    /// 所有当前敌人死亡或逃跑后进入下一轮建造；
    /// 任意敌人进入王座后立即结束整个大循环。
    /// </summary>
    public sealed class GameLoopController : MonoBehaviour
    {
        [Header("系统引用")]

        [SerializeField]
        private RoomPlacementController placementController;

        [SerializeField]
        private EnemyWaveSpawner enemyWaveSpawner;

        [Header("启动设置")]

        [SerializeField]
        private bool beginOnStart = true;

        private readonly HashSet<EnemyLifecycle> activeEnemies =
            new HashSet<EnemyLifecycle>();

        private int deadEnemyCount;
        private int escapedEnemyCount;

        /// <summary>
        /// 当前所处的游戏循环阶段。
        /// </summary>
        public GameLoopState State { get; private set; }

        /// <summary>
        /// 当前小循环编号，从 1 开始。
        /// </summary>
        public int CurrentRound { get; private set; }

        /// <summary>
        /// 每次进入新阶段时触发。
        /// </summary>
        public event Action<GameLoopState> StateChanged;

        /// <summary>
        /// 当前小循环正常结束时触发。
        /// 参数依次为轮次、死亡数和逃跑数。
        /// </summary>
        public event Action<int, int, int> SmallLoopFinished;

        /// <summary>
        /// 有敌人进入王座、玩家失败时触发。
        /// </summary>
        public event Action GameOver;

        private void Start()
        {
            if (beginOnStart)
            {
                BeginGame();
            }
        }

        private void OnDestroy()
        {
            UnsubscribeAllEnemies();
        }

        /// <summary>
        /// 开始整个游戏循环，并进入第一轮建造阶段。
        /// </summary>
        public void BeginGame()
        {
            UnsubscribeAllEnemies();

            CurrentRound = 1;

            EnterBuildingPhase();
        }

        /// <summary>
        /// 完成当前建造阶段并生成本轮敌人。
        /// 可绑定到“完成建造”按钮。
        /// </summary>
        public void CompleteBuildPhase()
        {
            if (State != GameLoopState.Building)
            {
                return;
            }

            placementController?.CancelPlacement();
            placementController?.SetBuildEnabled(false);

            SetState(GameLoopState.EnemyRunning);

            deadEnemyCount = 0;
            escapedEnemyCount = 0;

            List<EnemyLifecycle> spawnedEnemies =
                enemyWaveSpawner != null
                    ? enemyWaveSpawner.SpawnWave()
                    : new List<EnemyLifecycle>();

            activeEnemies.Clear();

            foreach (EnemyLifecycle enemy in spawnedEnemies)
            {
                if (enemy == null)
                {
                    continue;
                }

                activeEnemies.Add(enemy);
                enemy.Resolved += HandleEnemyResolved;
            }

            // 没有成功生成敌人时，直接结束当前小循环。
            if (activeEnemies.Count == 0)
            {
                FinishSmallLoop();
            }
        }

        private void HandleEnemyResolved(
            EnemyLifecycle enemy,
            EnemyResolution resolution)
        {
            if (enemy != null)
            {
                enemy.Resolved -= HandleEnemyResolved;
                activeEnemies.Remove(enemy);
            }

            if (State != GameLoopState.EnemyRunning)
            {
                return;
            }

            switch (resolution)
            {
                case EnemyResolution.ReachedThrone:
                    FailGame();
                    return;

                case EnemyResolution.Dead:
                    deadEnemyCount++;
                    break;

                case EnemyResolution.Escaped:
                    escapedEnemyCount++;
                    break;
            }

            // 只有本轮所有敌人都死亡或逃跑后，
            // 当前小循环才算正常结束。
            if (activeEnemies.Count == 0)
            {
                FinishSmallLoop();
            }
        }

        private void FinishSmallLoop()
        {
            if (State != GameLoopState.EnemyRunning)
            {
                return;
            }

            SmallLoopFinished?.Invoke(
                CurrentRound,
                deadEnemyCount,
                escapedEnemyCount
            );

            CurrentRound++;

            EnterBuildingPhase();
        }

        private void EnterBuildingPhase()
        {
            SetState(GameLoopState.Building);

            placementController?.SetBuildEnabled(true);
        }

        private void FailGame()
        {
            if (State == GameLoopState.GameOver)
            {
                return;
            }

            SetState(GameLoopState.GameOver);

            placementController?.CancelPlacement();
            placementController?.SetBuildEnabled(false);

            CleanupRemainingEnemies();

            GameOver?.Invoke();
        }

        private void CleanupRemainingEnemies()
        {
            EnemyLifecycle[] snapshot =
                new EnemyLifecycle[activeEnemies.Count];

            activeEnemies.CopyTo(snapshot);
            activeEnemies.Clear();

            foreach (EnemyLifecycle enemy in snapshot)
            {
                if (enemy == null)
                {
                    continue;
                }

                enemy.Resolved -= HandleEnemyResolved;
                Destroy(enemy.gameObject);
            }
        }

        private void UnsubscribeAllEnemies()
        {
            foreach (EnemyLifecycle enemy in activeEnemies)
            {
                if (enemy != null)
                {
                    enemy.Resolved -= HandleEnemyResolved;
                }
            }

            activeEnemies.Clear();
        }

        private void SetState(GameLoopState newState)
        {
            State = newState;
            StateChanged?.Invoke(newState);
        }
    }
}