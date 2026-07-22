using System.Collections.Generic;
using UnityEngine;

namespace Dungeon.RoomSystem
{
    /// <summary>
    /// 统一管理 DungeonGrid 中全部房间的视觉表现。
    ///
    /// 当前后端仍使用 RoomVisualController + RoomCellView。
    /// 未来可以将内部实现替换为 Chunk Mesh，
    /// 而不影响 DungeonGrid 和 RoomInstance。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RoomVisualSystem : MonoBehaviour
    {
        [Header("References")]

        [SerializeField]
        private DungeonGrid dungeonGrid;

        [SerializeField]
        private SpriteRenderer placedCellPrefab;

        [SerializeField]
        private Transform visualRoot;

        private readonly Dictionary<
            RoomInstance,
            RoomVisualController>
            roomVisuals =
                new Dictionary<
                    RoomInstance,
                    RoomVisualController>();

        private void Awake()
        {
            if (visualRoot == null)
            {
                visualRoot = transform;
            }
        }

        private void OnEnable()
        {
            if (dungeonGrid == null)
            {
                return;
            }

            dungeonGrid.GridChanged +=
                HandleGridChanged;

            dungeonGrid.VisualSettingsChanged +=
                RefreshAllVisuals;
        }

        private void OnDisable()
        {
            if (dungeonGrid == null)
            {
                return;
            }

            dungeonGrid.GridChanged -=
                HandleGridChanged;

            dungeonGrid.VisualSettingsChanged -=
                RefreshAllVisuals;
        }

        private void HandleGridChanged(
            IReadOnlyList<Vector2Int> changedCells)
        {
            SyncVisualInstances();

            RefreshVisualsAround(
                changedCells
            );
        }

        /// <summary>
        /// 给 RoomInstance 创建 Visual
        /// 当 RoomInstance 不存在时 删除 Visual。
        /// </summary>
        private void SyncVisualInstances()
        {
            HashSet<RoomInstance> existingRooms =
                new HashSet<RoomInstance>(
                    dungeonGrid.PlacedRooms
                );

            List<RoomInstance> removedRooms =
                new List<RoomInstance>();

            foreach (
                KeyValuePair<
                    RoomInstance,
                    RoomVisualController>
                pair in roomVisuals)
            {
                if (pair.Key == null ||
                    !existingRooms.Contains(pair.Key))
                {
                    removedRooms.Add(pair.Key);
                }
            }

            foreach (RoomInstance room in removedRooms)
            {
                if (roomVisuals.TryGetValue(
                        room,
                        out RoomVisualController visual))
                {
                    if (visual != null)
                    {
                        Destroy(
                            visual.gameObject
                        );
                    }

                    roomVisuals.Remove(room);
                }
            }

            foreach (RoomInstance room
                     in dungeonGrid.PlacedRooms)
            {
                if (room == null ||
                    roomVisuals.ContainsKey(room))
                {
                    continue;
                }

                CreateVisual(room);
            }
        }


        private void CreateVisual(
    RoomInstance room)
        {
            if (room == null ||
                placedCellPrefab == null)
            {
                return;
            }

            GameObject visualObject =
                new GameObject(
                    $"Visual_{room.name}"
                );

            visualObject.transform.SetParent(
                visualRoot,
                false
            );

            RoomVisualController visual =
                visualObject.AddComponent<
                    RoomVisualController>();

            visual.Initialize(
                room.Data,
                room.OccupiedCells,
                dungeonGrid,
                placedCellPrefab
            );

            roomVisuals.Add(
                room,
                visual
            );
        }

        private void RefreshVisualsAround(
    IReadOnlyList<Vector2Int> changedCells)
        {
            if (changedCells == null)
            {
                return;
            }

            HashSet<RoomInstance> affectedRooms =
                new HashSet<RoomInstance>();

            foreach (Vector2Int cell in changedCells)
            {
                AddRoomAtCell(
                    cell,
                    affectedRooms
                );

                AddRoomAtCell(
                    cell + Vector2Int.left,
                    affectedRooms
                );

                AddRoomAtCell(
                    cell + Vector2Int.right,
                    affectedRooms
                );

                AddRoomAtCell(
                    cell + Vector2Int.down,
                    affectedRooms
                );

                AddRoomAtCell(
                    cell + Vector2Int.up,
                    affectedRooms
                );
            }

            foreach (RoomInstance room
                     in affectedRooms)
            {
                if (room == null)
                {
                    continue;
                }

                if (roomVisuals.TryGetValue(
                        room,
                        out RoomVisualController visual))
                {
                    visual?.RefreshBorders();
                }
            }
        }

        private void AddRoomAtCell(
    Vector2Int cell,
    HashSet<RoomInstance> rooms)
        {
            if (dungeonGrid.TryGetRoom(
                    cell,
                    out RoomInstance room) &&
                room != null)
            {
                rooms.Add(room);
            }
        }

        private void RefreshAllVisuals()
        {
            SyncVisualInstances();

            foreach (RoomVisualController visual
                     in roomVisuals.Values)
            {
                if (visual != null)
                {
                    visual.RefreshBorders();
                }
            }
        }
    }
}