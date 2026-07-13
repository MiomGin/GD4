using System;
using System.Collections.Generic;
using UnityEngine;

namespace Dungeon.RoomSystem
{
    /// <summary>
    /// 管理地牢网格坐标、房间占用关系和房间创建删除。
    /// </summary>
    public sealed class DungeonGrid : MonoBehaviour
    {
        [Serializable]
        private struct InitialRoom
        {
            public RoomData roomData;
            public Vector2Int anchorCell;

            [Range(0, 3)]
            public int rotation;
        }

        private static readonly Vector2Int[] FourDirections =
        {
            Vector2Int.up,
            Vector2Int.down,
            Vector2Int.left,
            Vector2Int.right
        };

        [Header("网格设置")]

        [SerializeField, Min(0.01f)]
        private float cellSize = 1f;

        [Header("地图边界")]

        [SerializeField]
        private bool useBounds = true;

        [SerializeField]
        private Vector2Int minCell =
            new Vector2Int(-20, -20);

        [SerializeField]
        private Vector2Int maxCell =
            new Vector2Int(20, 20);

        [Header("房间表现")]

        [Tooltip("已经放置的房间使用的单格 SpriteRenderer Prefab。")]
        [SerializeField]
        private SpriteRenderer placedCellPrefab;

        [Header("初始固定房间")]

        [Tooltip("可配置王座室和地牢入口等初始房间。")]
        [SerializeField]
        private InitialRoom[] initialRooms =
            Array.Empty<InitialRoom>();

        private readonly Dictionary<
            Vector2Int,
            RoomInstance
        > occupiedCells =
            new Dictionary<Vector2Int, RoomInstance>();

        /// <summary>
        /// 单个网格的世界空间边长。
        /// </summary>
        public float CellSize => cellSize;

        /// <summary>
        /// 当前被房间占用的格子数量。
        /// </summary>
        public int OccupiedCellCount =>
            occupiedCells.Count;

        private void Start()
        {
            PlaceInitialRooms();
        }

        /// <summary>
        /// 将世界坐标转换为网格坐标。
        /// </summary>
        public Vector2Int WorldToCell(
            Vector3 worldPosition)
        {
            Vector3 localPosition =
                transform.InverseTransformPoint(
                    worldPosition
                );

            return new Vector2Int(
                Mathf.FloorToInt(
                    localPosition.x / cellSize
                ),
                Mathf.FloorToInt(
                    localPosition.y / cellSize
                )
            );
        }

        /// <summary>
        /// 将网格坐标转换为格子中心的世界坐标。
        /// </summary>
        public Vector3 CellToWorld(Vector2Int cell)
        {
            Vector3 localPosition =
                new Vector3(
                    (cell.x + 0.5f) * cellSize,
                    (cell.y + 0.5f) * cellSize,
                    0f
                );

            return transform.TransformPoint(
                localPosition
            );
        }

        /// <summary>
        /// 判断指定格子当前是否已被房间占用。
        /// </summary>
        public bool IsCellOccupied(Vector2Int cell)
        {
            return occupiedCells.ContainsKey(cell);
        }

        /// <summary>
        /// 获取指定格子所属的房间。
        /// </summary>
        public bool TryGetRoom(
            Vector2Int cell,
            out RoomInstance room)
        {
            return occupiedCells.TryGetValue(
                cell,
                out room
            );
        }

        /// <summary>
        /// 检查房间能否放置，并返回旋转后的全部目标格子。
        /// 即使放置非法，也会返回完整目标格子供虚影显示。
        /// </summary>
        public bool CanPlace(
            RoomData roomData,
            Vector2Int anchorCell,
            int rotation,
            out List<Vector2Int> targetCells,
            bool ignoreConnectionRule = false)
        {
            targetCells =
                new List<Vector2Int>();

            if (roomData == null)
            {
                return false;
            }

            List<Vector2Int> localCells =
                roomData.GetRotatedCells(rotation);

            foreach (Vector2Int localCell in localCells)
            {
                targetCells.Add(
                    anchorCell + localCell
                );
            }

            if (targetCells.Count == 0)
            {
                return false;
            }

            bool valid = true;

            HashSet<Vector2Int> targetCellSet =
                new HashSet<Vector2Int>(
                    targetCells
                );

            if (targetCellSet.Count !=
                targetCells.Count)
            {
                valid = false;
            }

            foreach (Vector2Int cell in targetCells)
            {
                if (!IsInsideBounds(cell))
                {
                    valid = false;
                }

                if (occupiedCells.ContainsKey(cell))
                {
                    valid = false;
                }
            }

            bool shouldCheckConnection =
                !ignoreConnectionRule &&
                roomData.MustConnectToExistingRoom &&
                occupiedCells.Count > 0;

            if (shouldCheckConnection &&
                !TouchesExistingRoom(targetCellSet))
            {
                valid = false;
            }

            return valid;
        }

        /// <summary>
        /// 尝试创建并放置一个房间。
        /// </summary>
        public bool TryPlaceRoom(
            RoomData roomData,
            Vector2Int anchorCell,
            int rotation,
            out RoomInstance roomInstance,
            bool ignoreConnectionRule = false)
        {
            roomInstance = null;

            if (placedCellPrefab == null)
            {
                Debug.LogError(
                    "DungeonGrid 未设置 Placed Cell Prefab。",
                    this
                );

                return false;
            }

            if (!CanPlace(
                    roomData,
                    anchorCell,
                    rotation,
                    out List<Vector2Int> targetCells,
                    ignoreConnectionRule))
            {
                return false;
            }

            GameObject roomObject =
                new GameObject(roomData.DisplayName);

            roomObject.transform.SetParent(
                transform,
                false
            );

            roomInstance =
                roomObject.AddComponent<RoomInstance>();

            // 先注册占用关系，使效果初始化时已经能够查询网格。
            foreach (Vector2Int cell in targetCells)
            {
                occupiedCells.Add(
                    cell,
                    roomInstance
                );
            }

            roomInstance.Initialize(
                roomData,
                anchorCell,
                rotation,
                targetCells,
                this,
                placedCellPrefab
            );

            roomInstance.NotifyPlaced();

            return true;
        }

        /// <summary>
        /// 删除一个已经放置的房间并释放其占用格子。
        /// </summary>
        public bool RemoveRoom(
            RoomInstance roomInstance)
        {
            if (roomInstance == null)
            {
                return false;
            }

            roomInstance.PrepareForRemoval();

            foreach (Vector2Int cell
                     in roomInstance.OccupiedCells)
            {
                if (occupiedCells.TryGetValue(
                        cell,
                        out RoomInstance currentRoom) &&
                    currentRoom == roomInstance)
                {
                    occupiedCells.Remove(cell);
                }
            }

            Destroy(roomInstance.gameObject);

            return true;
        }

        private void PlaceInitialRooms()
        {
            if (initialRooms == null)
            {
                return;
            }

            foreach (InitialRoom initialRoom
                     in initialRooms)
            {
                if (initialRoom.roomData == null)
                {
                    continue;
                }

                bool placed = TryPlaceRoom(
                    initialRoom.roomData,
                    initialRoom.anchorCell,
                    initialRoom.rotation,
                    out _,
                    true
                );

                if (!placed)
                {
                    Debug.LogWarning(
                        $"初始房间放置失败：{initialRoom.roomData.name}",
                        this
                    );
                }
            }
        }

        private bool IsInsideBounds(Vector2Int cell)
        {
            if (!useBounds)
            {
                return true;
            }

            return
                cell.x >= minCell.x &&
                cell.x <= maxCell.x &&
                cell.y >= minCell.y &&
                cell.y <= maxCell.y;
        }

        private bool TouchesExistingRoom(
            HashSet<Vector2Int> targetCells)
        {
            foreach (Vector2Int cell in targetCells)
            {
                foreach (Vector2Int direction
                         in FourDirections)
                {
                    Vector2Int neighbour =
                        cell + direction;

                    // 当前待放置房间内部的相邻格不算连接已有房间。
                    if (targetCells.Contains(neighbour))
                    {
                        continue;
                    }

                    if (occupiedCells.ContainsKey(
                            neighbour))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private void OnDrawGizmosSelected()
        {
            if (!useBounds || cellSize <= 0f)
            {
                return;
            }

            Gizmos.color = Color.yellow;

            Vector3 bottomLeft =
                CellToWorld(minCell) -
                new Vector3(
                    cellSize * 0.5f,
                    cellSize * 0.5f,
                    0f
                );

            Vector2Int cellCount =
                maxCell -
                minCell +
                Vector2Int.one;

            Vector3 size =
                new Vector3(
                    cellCount.x * cellSize,
                    cellCount.y * cellSize,
                    0f
                );

            Gizmos.DrawWireCube(
                bottomLeft + size * 0.5f,
                size
            );
        }
    }
}