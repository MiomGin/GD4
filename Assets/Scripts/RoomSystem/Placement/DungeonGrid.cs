using System;
using System.Collections.Generic;
using UnityEngine;

namespace Dungeon.RoomSystem
{
    /// <summary>
    /// 表示一个需要在游戏开始时自动生成的固定房间。
    /// 常用于 Entrance、Throne 等地图基础结构。
    /// </summary>
    [Serializable]
    public sealed class InitialRoomPlacement
    {
        [SerializeField]
        private RoomData roomData;

        [SerializeField]
        private Vector2Int anchorCell;

        [SerializeField, Range(0, 3)]
        private int rotation;

        /// <summary>
        /// 需要生成的房间配置。
        /// </summary>
        public RoomData RoomData => roomData;

        /// <summary>
        /// 房间摆放锚点。
        /// </summary>
        public Vector2Int AnchorCell => anchorCell;

        /// <summary>
        /// 房间顺时针旋转次数。
        /// </summary>
        public int Rotation => rotation;
    }

    /// <summary>
    /// 管理地牢逻辑网格、房间占用关系以及房间实例的创建和删除。
    /// 同时负责组装 RoomInstance 与 RoomVisualController。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DungeonGrid : MonoBehaviour
    {
        [Header("Grid")]
        [SerializeField, Min(0.01f)]
        private float cellSize = 1f;
        [SerializeField, Min(0f)]
        private float doorSize = 0.4f;

        [SerializeField]
        private bool useBounds = true;

        [Tooltip("网格允许使用的最小逻辑坐标。")]
        [SerializeField]
        private Vector2Int minimumCell =
            new Vector2Int(-20, -20);

        [Tooltip("网格范围的宽度和高度。")]
        [SerializeField]
        private Vector2Int gridSize =
            new Vector2Int(40, 40);

        //[Header("Room Visual")]

        //[Tooltip("正式房间单个格子的 SpriteRenderer Prefab。")]
        //[SerializeField]
        //private SpriteRenderer placedCellPrefab;

        /// <summary>
        /// 两个相邻房间连接边中央的门洞长度。
        /// 使用世界单位，始终限制在 [0, CellSize)。
        /// </summary>
        public float DoorSize =>
            ClampDoorSize(doorSize);

        [Tooltip("生成房间根对象的父节点。为空时使用 DungeonGrid 自身。")]
        [SerializeField]
        private Transform roomRoot;

        [Header("Initial Rooms")]

        [SerializeField]
        private List<InitialRoomPlacement> initialRooms =
            new List<InitialRoomPlacement>();

        /// <summary>
        /// 每个世界逻辑格当前所属的房间。
        /// </summary>
        private readonly Dictionary<Vector2Int, RoomInstance>
            occupiedRooms =
                new Dictionary<Vector2Int, RoomInstance>();

        /// <summary>
        /// 当前已经注册到网格中的全部房间。
        /// </summary>
        private readonly HashSet<RoomInstance> placedRooms =
            new HashSet<RoomInstance>();

        /// <summary>
        /// 单个逻辑格子的世界尺寸。
        /// </summary>
        public float CellSize => cellSize;

        /// <summary>
        /// 当前已经放置的全部房间。
        /// </summary>
        public IReadOnlyCollection<RoomInstance> PlacedRooms =>
            placedRooms;

        /// <summary>
        /// 当网格占用关系发生变化时触发。
        /// ChangedCells 表示本次实际发生占用变化的逻辑格。
        /// 局部变化。
        /// </summary>
        public event Action<IReadOnlyList<Vector2Int>>
            GridChanged;

        /// <summary>
        /// 全局网格视觉参数发生变化时触发。
        /// 例如门洞尺寸修改。
        /// 全局重新刷新。
        /// </summary>
        public event Action VisualSettingsChanged;

        private void Awake()
        {
            if (roomRoot == null)
            {
                roomRoot = transform;
            }
        }

        private void Start()
        {
            CreateInitialRooms();
        }

        private void OnValidate()
        {
            cellSize = Mathf.Max(
                0.01f,
                cellSize
            );

            doorSize =
                ClampDoorSize(doorSize);

            //if (Application.isPlaying)
            //{
            //    RefreshAllRoomVisuals();
            //}
        }

        /// <summary>
        /// 将门洞大小限制在 [0, CellSize)。
        /// </summary>
        private float ClampDoorSize(float value)
        {
            float maximumDoorSize =
                Mathf.Max(
                    0f,
                    cellSize - 0.0001f
                );

            return Mathf.Clamp(
                value,
                0f,
                maximumDoorSize
            );
        }

        /// <summary>
        /// 修改全部房间连接处的门洞大小，并刷新现有房间视觉。
        /// </summary>
        /// <param name="newDoorSize">
        /// 使用世界单位表示的新门洞长度。
        /// </param>
        public void SetDoorSize(float newDoorSize)
        {
            //doorSize =
            //    ClampDoorSize(newDoorSize);

            //RefreshAllRoomVisuals();

            float clampedValue = ClampDoorSize(newDoorSize);

            if (Mathf.Approximately(doorSize, clampedValue))
                return;

            doorSize = clampedValue;

            VisualSettingsChanged?.Invoke();
        }

        /// <summary>
        /// 将世界坐标转换为逻辑网格坐标。
        /// </summary>
        public Vector2Int WorldToCell(Vector3 worldPosition)
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
        /// 将逻辑网格坐标转换为格子中心的世界坐标。
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
        /// 判断指定逻辑格是否已经被房间占用。
        /// </summary>
        public bool IsCellOccupied(Vector2Int cell)
        {
            return occupiedRooms.ContainsKey(cell);
        }

        /// <summary>
        /// 尝试获取指定逻辑格所属的房间。
        /// </summary>
        public bool TryGetRoom(
            Vector2Int cell,
            out RoomInstance room)
        {
            return occupiedRooms.TryGetValue(
                cell,
                out room
            );
        }

        /// <summary>
        /// 获取房间在指定锚点和旋转状态下占用的世界格子。
        /// </summary>
        public List<Vector2Int> GetWorldCells(
            RoomData roomData,
            Vector2Int anchorCell,
            int rotation)
        {
            List<Vector2Int> worldCells =
                new List<Vector2Int>();

            if (roomData == null)
            {
                return worldCells;
            }

            IReadOnlyList<Vector2Int> localCells =
                roomData.GetRotatedCells(
                    NormalizeRotation(rotation)
                );

            if (localCells == null)
            {
                return worldCells;
            }

            foreach (Vector2Int localCell in localCells)
            {
                worldCells.Add(
                    anchorCell + localCell
                );
            }

            return worldCells;
        }

        /// <summary>
        /// 判断房间是否可以放置在指定位置。
        /// </summary>
        public bool CanPlace(
            RoomData roomData,
            Vector2Int anchorCell,
            int rotation,
            bool ignoreConnectionRule = false)
        {
            List<Vector2Int> worldCells =
                new List<Vector2Int>();

            return TryGetPlacementCells(
                roomData,
                anchorCell,
                rotation,
                worldCells,
                ignoreConnectionRule
            );
        }

        /// <summary>
        /// 检查房间放置条件，并输出最终占用的世界格子。
        /// </summary>
        public bool TryGetPlacementCells(
            RoomData roomData,
            Vector2Int anchorCell,
            int rotation,
            List<Vector2Int> worldCells,
            bool ignoreConnectionRule = false)
        {
            if (worldCells == null)
            {
                throw new ArgumentNullException(
                    nameof(worldCells)
                );
            }

            worldCells.Clear();

            if (roomData == null)
            {
                return false;
            }

            IReadOnlyList<Vector2Int> localCells =
                roomData.GetRotatedCells(
                    NormalizeRotation(rotation)
                );

            if (localCells == null ||
                localCells.Count == 0)
            {
                return false;
            }

            HashSet<Vector2Int> candidateCells =
                new HashSet<Vector2Int>();

            foreach (Vector2Int localCell in localCells)
            {
                Vector2Int worldCell =
                    anchorCell + localCell;

                // 防止 RoomData 中存在重复坐标。
                if (!candidateCells.Add(worldCell))
                {
                    worldCells.Clear();
                    return false;
                }

                if (!IsWithinBounds(worldCell))
                {
                    worldCells.Clear();
                    return false;
                }

                if (occupiedRooms.ContainsKey(worldCell))
                {
                    worldCells.Clear();
                    return false;
                }

                worldCells.Add(worldCell);
            }

            if (!ignoreConnectionRule &&
                roomData.MustConnect &&
                occupiedRooms.Count > 0 &&
                !TouchesExistingRoom(candidateCells))
            {
                worldCells.Clear();
                return false;
            }

            return true;
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

            //if (placedCellPrefab == null)
            //{
            //    Debug.LogError(
            //        "DungeonGrid 的 Placed Cell Prefab 未设置。",
            //        this
            //    );

            //    return false;
            //}

            List<Vector2Int> worldCells =
                new List<Vector2Int>();

            if (!TryGetPlacementCells(
                    roomData,
                    anchorCell,
                    rotation,
                    worldCells,
                    ignoreConnectionRule))
            {
                return false;
            }

            GameObject roomObject =
                new GameObject("Room");

            roomObject.transform.SetParent(
                roomRoot,
                false
            );

            RoomInstance runtime =
                roomObject.AddComponent<RoomInstance>();

            //RoomVisualController visual =
            //    roomObject.AddComponent<RoomVisualController>();

            try
            {
                runtime.Initialize(
                    roomData,
                    anchorCell,
                    rotation,
                    worldCells,
                    this
                );

                //visual.Initialize(
                //    roomData,
                //    worldCells,
                //    this,
                //    placedCellPrefab
                //);

                RegisterRoom(
                    runtime,
                    worldCells
                );

                NotifyGridChanged(worldCells);

                /*
                 * 房间注册完成后刷新新房间及其相邻房间。
                 * 这样连接边两侧都会生成对称门洞。
                 */

                //RefreshRoomVisualsAround(
                //    worldCells
                //);

                /*
                 * NotifyPlaced 放在网格注册之后。
                 * 这样初始效果在 OnAdded 或 RoomPlaced 中查询网格时，
                 * 房间已经可以被 DungeonGrid 找到。
                 */
                runtime.NotifyPlaced();

                roomInstance = runtime;
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogException(
                    exception,
                    roomObject
                );

                UnregisterRoom(
                    runtime,
                    worldCells
                );

                runtime.PrepareForRemoval();

                Destroy(roomObject);

                roomInstance = null;
                return false;
            }
        }

        /// <summary>
        /// TryPlaceRoom 的兼容别名。
        /// </summary>
        public bool TryCreateRoom(
            RoomData roomData,
            Vector2Int anchorCell,
            int rotation,
            out RoomInstance roomInstance,
            bool ignoreConnectionRule = false)
        {
            return TryPlaceRoom(
                roomData,
                anchorCell,
                rotation,
                out roomInstance,
                ignoreConnectionRule
            );
        }

        /// <summary>
        /// 创建房间并返回生成的实例。
        /// 放置失败时返回 null。
        /// </summary>
        public RoomInstance PlaceRoom(
            RoomData roomData,
            Vector2Int anchorCell,
            int rotation,
            bool ignoreConnectionRule = false)
        {
            TryPlaceRoom(
                roomData,
                anchorCell,
                rotation,
                out RoomInstance roomInstance,
                ignoreConnectionRule
            );

            return roomInstance;
        }

        /// <summary>
        /// PlaceRoom 的兼容别名。
        /// </summary>
        public RoomInstance CreateRoom(
            RoomData roomData,
            Vector2Int anchorCell,
            int rotation,
            bool ignoreConnectionRule = false)
        {
            return PlaceRoom(
                roomData,
                anchorCell,
                rotation,
                ignoreConnectionRule
            );
        }

        /// <summary>
        /// 从网格中删除一个已经放置的房间。
        /// 删除后会刷新相邻房间的描边与门洞。
        /// </summary>
        public bool RemoveRoom(
            RoomInstance roomInstance)
        {
            if (roomInstance == null ||
                !placedRooms.Contains(roomInstance))
            {
                return false;
            }

            /*
             * RoomInstance 销毁前仍可安全读取 OccupiedCells。
             */
            IReadOnlyList<Vector2Int> removedCells =
                roomInstance.OccupiedCells;

            roomInstance.PrepareForRemoval();

            UnregisterRoom(
                roomInstance,
                removedCells
            );

            /*
             * 房间注销后，相邻房间原本的门洞应恢复为完整外墙。
             */
            //RefreshRoomVisualsAround(
            //    removedCells
            //)

            NotifyGridChanged(removedCells);

            Destroy(roomInstance.gameObject);

            return true;
        }

        /// <summary>
        /// 删除当前网格中的全部房间。
        /// </summary>
        public void ClearAllRooms()
        {
            RoomInstance[] snapshot =
                new RoomInstance[placedRooms.Count];

            placedRooms.CopyTo(snapshot);

            foreach (RoomInstance roomInstance in snapshot)
            {
                if (roomInstance != null)
                {
                    RemoveRoom(roomInstance);
                }
            }
        }

        /// <summary>
        /// 生成 Inspector 中配置的固定初始房间。
        /// 初始房间默认忽略连接规则。
        /// </summary>
        private void CreateInitialRooms()
        {
            if (initialRooms == null)
            {
                return;
            }

            foreach (InitialRoomPlacement placement
                     in initialRooms)
            {
                if (placement == null ||
                    placement.RoomData == null)
                {
                    continue;
                }

                bool created =
                    TryPlaceRoom(
                        placement.RoomData,
                        placement.AnchorCell,
                        placement.Rotation,
                        out _,
                        true
                    );

                if (!created)
                {
                    Debug.LogWarning(
                        $"无法生成初始房间：{placement.RoomData.name}，" +
                        $"Anchor={placement.AnchorCell}，" +
                        $"Rotation={placement.Rotation}",
                        this
                    );
                }
            }
        }

        /// <summary>
        /// 将房间及其占用格注册到网格。
        /// </summary>
        private void RegisterRoom(
            RoomInstance roomInstance,
            IReadOnlyList<Vector2Int> worldCells)
        {
            placedRooms.Add(roomInstance);

            foreach (Vector2Int cell in worldCells)
            {
                occupiedRooms.Add(
                    cell,
                    roomInstance
                );
            }
        }

        /// <summary>
        /// 从网格占用记录中注销房间。
        /// 只有格子仍属于指定房间时才会移除。
        /// </summary>
        private void UnregisterRoom(
            RoomInstance roomInstance,
            IReadOnlyList<Vector2Int> worldCells)
        {
            placedRooms.Remove(roomInstance);

            if (worldCells == null)
            {
                return;
            }

            foreach (Vector2Int cell in worldCells)
            {
                if (occupiedRooms.TryGetValue(
                        cell,
                        out RoomInstance registeredRoom) &&
                    registeredRoom == roomInstance)
                {
                    occupiedRooms.Remove(cell);
                }
            }
        }

        /// <summary>
        /// 通知外部系统指定格子的占用状态发生变化。
        /// DungeonGrid 只负责发布逻辑变化，不直接处理视觉刷新。
        /// </summary>
        private void NotifyGridChanged(
            IReadOnlyList<Vector2Int> changedCells)
        {
            if (changedCells == null ||
                changedCells.Count == 0)
            {
                return;
            }

            GridChanged?.Invoke(changedCells);
        }

        /// <summary>
        /// 刷新指定变化区域内的房间及其四方向相邻房间。
        /// 用于房间新增、删除后更新连接门洞。
        /// 暂弃用。
        /// </summary>
        private void RefreshRoomVisualsAround(
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

                RoomVisualController visual =
                    room.GetComponent<RoomVisualController>();

                visual?.RefreshBorders();
            }
        }

        /// <summary>
        /// 将指定格子所属房间加入待刷新集合。
        /// 暂弃用。
        /// </summary>
        private void AddRoomAtCell(
            Vector2Int cell,
            HashSet<RoomInstance> rooms)
        {
            if (occupiedRooms.TryGetValue(
                    cell,
                    out RoomInstance room) &&
                room != null)
            {
                rooms.Add(room);
            }
        }

        /// <summary>
        /// 刷新当前网格中全部房间的描边和门洞。
        /// 暂弃用。
        /// </summary>
        private void RefreshAllRoomVisuals()
        {
            foreach (RoomInstance room in placedRooms)
            {
                if (room == null)
                {
                    continue;
                }

                RoomVisualController visual =
                    room.GetComponent<RoomVisualController>();

                visual?.RefreshBorders();
            }
        }

        /// <summary>
        /// 判断候选房间是否与任意现有房间四方向相邻。
        /// </summary>
        private bool TouchesExistingRoom(
            HashSet<Vector2Int> candidateCells)
        {
            foreach (Vector2Int cell in candidateCells)
            {
                if (occupiedRooms.ContainsKey(
                        cell + Vector2Int.left) ||
                    occupiedRooms.ContainsKey(
                        cell + Vector2Int.right) ||
                    occupiedRooms.ContainsKey(
                        cell + Vector2Int.down) ||
                    occupiedRooms.ContainsKey(
                        cell + Vector2Int.up))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 判断逻辑格是否处于允许使用的网格范围内。
        /// </summary>
        private bool IsWithinBounds(Vector2Int cell)
        {
            if (!useBounds)
            {
                return true;
            }

            int maximumX =
                minimumCell.x + gridSize.x;

            int maximumY =
                minimumCell.y + gridSize.y;

            return
                cell.x >= minimumCell.x &&
                cell.y >= minimumCell.y &&
                cell.x < maximumX &&
                cell.y < maximumY;
        }

        /// <summary>
        /// 将任意旋转次数规范到 0～3。
        /// </summary>
        private static int NormalizeRotation(int rotation)
        {
            return ((rotation % 4) + 4) % 4;
        }
    }
}