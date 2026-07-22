using System;
using System.Collections.Generic;
using UnityEngine;

namespace Dungeon.RoomSystem
{
    /// <summary>
    /// 管理一个已放置房间的全部视觉表现。
    /// 负责创建格子、设置颜色、计算自适应描边，
    /// 以及刷新和销毁房间视觉对象。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RoomVisualController : MonoBehaviour
    {
        [Header("Border")]

        [Tooltip("描边宽度，使用单个格子的局部 UV 比例。")]
        [SerializeField, Range(0f, 0.5f)]
        private float borderWidth = 0.12f;

        /// <summary>
        /// 当前房间占用格的视觉侧副本。
        /// </summary>
        private readonly List<Vector2Int> occupiedCells =
            new List<Vector2Int>();

        /// <summary>
        /// 当前房间创建的全部格子视觉。
        /// </summary>
        private readonly List<RoomCellView> cellViews =
            new List<RoomCellView>();

        private DungeonGrid dungeonGrid;
        private SpriteRenderer placedCellPrefab;
        private Color roomColor;
        private bool isInitialized;

        /// <summary>
        /// 当前已经生成的全部房间格子视觉。
        /// </summary>
        public IReadOnlyList<RoomCellView> CellViews =>
            cellViews;

        /// <summary>
        /// 初始化房间视觉，并根据占用格生成全部 Cell。
        /// </summary>
        /// <param name="roomData">房间静态配置。</param>
        /// <param name="worldCells">房间占用的世界网格坐标。</param>
        /// <param name="grid">所属地牢网格。</param>
        /// <param name="cellPrefab">
        /// 正式房间格子使用的 SpriteRenderer Prefab。
        /// Prefab 必须挂载 RoomCellView。
        /// </param>
        public void Initialize(
            RoomData roomData,
            IReadOnlyList<Vector2Int> worldCells,
            DungeonGrid grid,
            SpriteRenderer cellPrefab)
        {
            if (roomData == null)
            {
                throw new ArgumentNullException(
                    nameof(roomData)
                );
            }

            if (grid == null)
            {
                throw new ArgumentNullException(
                    nameof(grid)
                );
            }

            if (cellPrefab == null)
            {
                throw new ArgumentNullException(
                    nameof(cellPrefab)
                );
            }

            dungeonGrid = grid;
            placedCellPrefab = cellPrefab;
            roomColor = roomData.RoomColor;

            occupiedCells.Clear();

            if (worldCells != null)
            {
                foreach (Vector2Int cell in worldCells)
                {
                    occupiedCells.Add(cell);
                }
            }

            isInitialized = true;

            RebuildVisuals();
        }

        /// <summary>
        /// 删除现有视觉，并根据当前房间数据重新生成全部格子。
        /// </summary>
        public void RebuildVisuals()
        {
            if (!isInitialized)
            {
                return;
            }

            ClearVisuals();

            RoomCellColors colors =
                RoomCellColorFactory.Create(
                    roomColor
                );

            foreach (Vector2Int cell in occupiedCells)
            {
                CreateCellView(
                    cell,
                    colors
                );
            }

            RefreshBorders();
        }

        /// <summary>
        /// 根据当前房间和网格状态重新计算全部格子的外露边、
        /// 凹角连接区域以及房间连接门洞。
        /// </summary>
        public void RefreshBorders()
        {
            if (dungeonGrid == null)
            {
                return;
            }

            HashSet<Vector2Int> roomCellSet =
                new HashSet<Vector2Int>(
                    occupiedCells
                );

            float normalizedDoorSize = 0f;

            if (dungeonGrid.CellSize >
                Mathf.Epsilon)
            {
                normalizedDoorSize =
                    Mathf.Clamp01(
                        dungeonGrid.DoorSize /
                        dungeonGrid.CellSize
                    );
            }

            foreach (RoomCellView cellView in cellViews)
            {
                if (cellView == null)
                {
                    continue;
                }

                RoomBorderMask borderMask =
                    RoomBorderUtility.CalculateMask(
                        cellView.Cell,
                        roomCellSet
                    );

                RoomInnerCornerMask innerCornerMask =
                    RoomBorderUtility
                        .CalculateInnerCornerMask(
                            cellView.Cell,
                            roomCellSet
                        );

                RoomDoorMask doorMask =
                    RoomBorderUtility.CalculateDoorMask(
                        cellView.Cell,
                        roomCellSet,
                        dungeonGrid
                    );

                cellView.SetBorderMasks(
                    borderMask,
                    innerCornerMask
                );

                cellView.SetDoorData(
                    doorMask,
                    normalizedDoorSize
                );
            }
        }

        /// <summary>
        /// 修改当前房间的视觉颜色。
        /// 描边颜色会通过 RoomCellColorFactory 自动重新计算。
        /// </summary>
        /// <param name="newRoomColor">新的房间基础颜色。</param>
        public void SetRoomColor(Color newRoomColor)
        {
            roomColor = newRoomColor;

            RoomCellColors colors =
                RoomCellColorFactory.Create(
                    roomColor
                );

            foreach (RoomCellView cellView in cellViews)
            {
                if (cellView == null)
                {
                    continue;
                }

                cellView.SetColors(
                    colors.FillColor,
                    colors.BorderColor
                );
            }
        }

        /// <summary>
        /// 修改当前房间所有格子的描边宽度。
        /// </summary>
        /// <param name="newBorderWidth">
        /// 使用单个格子的局部 UV 比例表示的描边宽度。
        /// </param>
        public void SetBorderWidth(float newBorderWidth)
        {
            borderWidth =
                Mathf.Clamp(
                    newBorderWidth,
                    0f,
                    0.5f
                );

            foreach (RoomCellView cellView in cellViews)
            {
                if (cellView == null)
                {
                    continue;
                }

                cellView.SetBorderWidth(
                    borderWidth
                );
            }
        }

        /// <summary>
        /// 隐藏并销毁当前房间的全部格子视觉。
        /// </summary>
        public void ClearVisuals()
        {
            for (int i = cellViews.Count - 1;
                 i >= 0;
                 i--)
            {
                RoomCellView cellView =
                    cellViews[i];

                if (cellView == null)
                {
                    continue;
                }

                cellView.gameObject.SetActive(false);
                Destroy(cellView.gameObject);
            }

            cellViews.Clear();
        }

        /// <summary>
        /// 创建并初始化一个房间格子视觉。
        /// </summary>
        private void CreateCellView(
            Vector2Int cell,
            RoomCellColors colors)
        {
            SpriteRenderer cellRenderer =
                Instantiate(
                    placedCellPrefab,
                    transform
                );

            cellRenderer.gameObject.SetActive(true);
            cellRenderer.enabled = true;

            cellRenderer.name =
                $"Cell_{cell.x}_{cell.y}";

            cellRenderer.transform.position =
                dungeonGrid.CellToWorld(cell);

            cellRenderer.transform.localRotation =
                Quaternion.identity;

            /*
             * 当前约定 Cell Sprite 的原始世界尺寸为 1×1。
             * 因此使用 CellSize 直接缩放到网格尺寸。
             */
            cellRenderer.transform.localScale =
                new Vector3(
                    dungeonGrid.CellSize,
                    dungeonGrid.CellSize,
                    1f
                );

            /*
             * Shader 已经通过 _FillColor 和 _BorderColor 控制颜色。
             * SpriteRenderer 顶点色保持白色，避免再次相乘。
             */
            cellRenderer.color = Color.white;

            RoomCellView cellView =
                cellRenderer.GetComponent<RoomCellView>();

            if (cellView == null)
            {
                Debug.LogError(
                    "Placed Cell Prefab 缺少 RoomCellView 组件。",
                    cellRenderer
                );

                Destroy(cellRenderer.gameObject);
                return;
            }

            cellView.Initialize(
                cell,
                colors.FillColor,
                colors.BorderColor,
                borderWidth
            );

            cellViews.Add(cellView);
        }
    }
}