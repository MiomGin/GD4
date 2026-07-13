using Dungeon.RoomSystem;
using System;
using UnityEngine;

namespace Dungeon.EnemySystem
{
    /// <summary>
    /// 敌人在当前小循环中的最终结算结果。
    /// </summary>
    public enum EnemyResolution
    {
        /// <summary>
        /// 敌人在探索途中死亡。
        /// </summary>
        Dead,

        /// <summary>
        /// 敌人离开入口后重新返回入口并逃跑。
        /// </summary>
        Escaped,

        /// <summary>
        /// 敌人进入王座室，导致玩家失败。
        /// </summary>
        ReachedThrone
    }

    /// <summary>
    /// 管理敌人的小循环生命周期。
    /// 负责将敌人最终结算为死亡、逃跑或到达王座，
    /// 并保证每个敌人只能完成一次结算。
    /// </summary>
    public sealed class EnemyLifecycle : MonoBehaviour
    {
        [SerializeField]
        private EnemyHealth enemyHealth;

        private bool hasLeftEntrance;
        private bool isResolved;

        /// <summary>
        /// 当前敌人是否已经完成最终结算。
        /// </summary>
        public bool IsResolved => isResolved;

        /// <summary>
        /// 敌人完成最终结算时触发。
        /// </summary>
        public event Action<EnemyLifecycle, EnemyResolution> Resolved;

        private void Awake()
        {
            if (enemyHealth == null)
            {
                enemyHealth = GetComponent<EnemyHealth>();
            }
        }

        private void OnEnable()
        {
            if (enemyHealth != null)
            {
                enemyHealth.Died += HandleDied;
            }
        }

        private void OnDisable()
        {
            if (enemyHealth != null)
            {
                enemyHealth.Died -= HandleDied;
            }
        }

        /// <summary>
        /// 通知生命周期组件敌人所在房间发生了变化。
        /// 王座与返回入口的判定优先于当前房间效果。
        /// </summary>
        /// <param name="previousRoom">敌人之前所在的房间。</param>
        /// <param name="currentRoom">敌人现在所在的房间。</param>
        public void NotifyRoomChanged(
            RoomInstance previousRoom,
            RoomInstance currentRoom)
        {
            if (isResolved)
            {
                return;
            }

            // 进入王座立即失败，优先级最高。
            if (currentRoom != null &&
                currentRoom.Tags.Has(RoomTag.Throne))
            {
                Resolve(EnemyResolution.ReachedThrone);
                return;
            }

            bool previousWasEntrance =
                previousRoom != null &&
                previousRoom.Tags.Has(RoomTag.Entrance);

            bool currentIsEntrance =
                currentRoom != null &&
                currentRoom.Tags.Has(RoomTag.Entrance);

            // 从入口移动到其他位置后，才认为敌人真正开始探索。
            if (previousWasEntrance && !currentIsEntrance)
            {
                hasLeftEntrance = true;
            }

            // 敌人已经离开过入口，再次返回入口时判定逃跑。
            if (currentIsEntrance && hasLeftEntrance)
            {
                Resolve(EnemyResolution.Escaped);

                // 第一阶段直接销毁，后续可替换为对象池回收。
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// 重置生命周期状态。
        /// 后续使用对象池重新启用敌人时调用。
        /// </summary>
        public void ResetLifecycle()
        {
            hasLeftEntrance = false;
            isResolved = false;
        }

        private void HandleDied(
            EnemyHealth health,
            GameObject source)
        {
            Resolve(EnemyResolution.Dead);
        }

        private bool Resolve(EnemyResolution resolution)
        {
            if (isResolved)
            {
                return false;
            }

            isResolved = true;
            Resolved?.Invoke(this, resolution);

            return true;
        }
    }
}