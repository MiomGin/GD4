using Dungeon.RoomSystem;
using UnityEngine;

namespace Dungeon.EnemySystem
{
    /// <summary>
    /// 追踪敌人当前所在的地牢格子和房间。
    /// 当敌人进入或离开房间时，向 RoomInstance 发送对应通知。
    /// </summary>
    public sealed class EnemyRoomTracker : MonoBehaviour
    {
        [Header("引用")]

        [SerializeField]
        private DungeonGrid dungeonGrid;

        private Vector2Int currentCell;
        private RoomInstance currentRoom;

        private bool hasCurrentCell;
        private bool hasStarted;

        /// <summary>
        /// 敌人当前所在的房间。
        /// 位于无房间格子时返回 null。
        /// </summary>
        public RoomInstance CurrentRoom =>
            currentRoom;

        /// <summary>
        /// 敌人当前所在的网格坐标。
        /// </summary>
        public Vector2Int CurrentCell =>
            currentCell;

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
        /// 立即重新查询敌人所在房间。
        /// 可用于传送、生成或位置被直接修改后刷新状态。
        /// </summary>
        public void ForceRefresh()
        {
            if (dungeonGrid == null)
            {
                return;
            }

            Vector2Int newCell =
                dungeonGrid.WorldToCell(
                    transform.position
                );

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
                dungeonGrid.WorldToCell(
                    transform.position
                );

            if (hasCurrentCell &&
                newCell == currentCell)
            {
                return;
            }

            hasCurrentCell = true;
            currentCell = newCell;

            UpdateCurrentRoom(newCell);
        }

        private void UpdateCurrentRoom(
            Vector2Int cell)
        {
            dungeonGrid.TryGetRoom(
                cell,
                out RoomInstance nextRoom
            );

            // 敌人可能从同一房间的一个格子移动到另一个格子，
            // 此时不应该重复触发进入和离开事件。
            if (nextRoom == currentRoom)
            {
                return;
            }

            RoomInstance previousRoom =
                currentRoom;

            currentRoom = nextRoom;

            previousRoom?.NotifyEnemyExited(
                gameObject
            );

            currentRoom?.NotifyEnemyEntered(
                gameObject
            );
        }
    }
}