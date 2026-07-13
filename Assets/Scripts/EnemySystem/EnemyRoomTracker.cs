using Dungeon.RoomSystem;
using UnityEngine;

namespace Dungeon.EnemySystem
{
    /// <summary>
    /// 追踪敌人当前所在的网格和房间。
    /// 当敌人切换房间时，负责通知房间效果系统和敌人生命周期系统。
    /// </summary>
    public sealed class EnemyRoomTracker : MonoBehaviour
    {
        [Header("引用")]

        [SerializeField]
        private DungeonGrid dungeonGrid;

        [SerializeField]
        private EnemyLifecycle lifecycle;

        private Vector2Int currentCell;
        private RoomInstance currentRoom;

        private bool hasCurrentCell;
        private bool hasStarted;

        /// <summary>
        /// 敌人当前所在的房间。
        /// 位于非房间格子时返回 null。
        /// </summary>
        public RoomInstance CurrentRoom => currentRoom;

        /// <summary>
        /// 敌人当前所在的网格坐标。
        /// </summary>
        public Vector2Int CurrentCell => currentCell;

        private void Awake()
        {
            if (lifecycle == null)
            {
                lifecycle = GetComponent<EnemyLifecycle>();
            }
        }

        private void Start()
        {
            hasStarted = true;

            if (dungeonGrid == null)
            {
                Debug.LogError(
                    "EnemyRoomTracker 未设置 DungeonGrid。",
                    this
                );

                enabled = false;
                return;
            }

            ForceRefresh();
        }

        private void LateUpdate()
        {
            RefreshWhenCellChanged();
        }

        private void OnDisable()
        {
            if (!hasStarted || currentRoom == null)
            {
                return;
            }

            currentRoom.NotifyEnemyExited(gameObject);
            currentRoom = null;
        }

        /// <summary>
        /// 立即重新查询敌人当前所在房间。
        /// 生成、传送或直接修改位置后可以调用。
        /// </summary>
        public void ForceRefresh()
        {
            if (dungeonGrid == null)
            {
                return;
            }

            Vector2Int newCell =
                dungeonGrid.WorldToCell(transform.position);

            hasCurrentCell = true;
            currentCell = newCell;

            UpdateCurrentRoom(newCell);
        }

        private void RefreshWhenCellChanged()
        {
            if (dungeonGrid == null)
            {
                return;
            }

            Vector2Int newCell =
                dungeonGrid.WorldToCell(transform.position);

            if (hasCurrentCell &&
                newCell == currentCell)
            {
                return;
            }

            hasCurrentCell = true;
            currentCell = newCell;

            UpdateCurrentRoom(newCell);
        }

        private void UpdateCurrentRoom(Vector2Int cell)
        {
            dungeonGrid.TryGetRoom(
                cell,
                out RoomInstance nextRoom
            );

            // 同一房间内跨格移动不重复触发进入和离开。
            if (nextRoom == currentRoom)
            {
                return;
            }

            RoomInstance previousRoom = currentRoom;

            previousRoom?.NotifyEnemyExited(gameObject);

            currentRoom = nextRoom;

            // 先判断到达王座或返回入口。
            lifecycle?.NotifyRoomChanged(
                previousRoom,
                currentRoom
            );

            // 已经到达王座、逃跑或死亡时，
            // 不再触发当前房间的普通效果。
            if (lifecycle != null &&
                lifecycle.IsResolved)
            {
                return;
            }

            currentRoom?.NotifyEnemyEntered(gameObject);
        }
    }
}