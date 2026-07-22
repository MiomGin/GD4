using UnityEngine;

namespace Dungeon.RoomSystem
{
    /// <summary>
    /// 表示单个逻辑格在当前时刻生成视觉 Mesh 所需要的全部数据。
    ///
    /// 该结构是纯临时数据，不保存长期状态。
    ///
    /// RoomInstance 和 DungeonGrid 是视觉数据的真实来源，
    /// Chunk 重建时根据当前逻辑状态重新计算该结构。
    /// </summary>
    public readonly struct RoomCellVisualData
    {
        /// <summary>
        /// 当前 Cell 的世界逻辑网格坐标。
        /// </summary>
        public Vector2Int Cell { get; }

        /// <summary>
        /// 房间内部基础颜色。
        /// </summary>
        public Color FillColor { get; }

        /// <summary>
        /// 墙壁或描边颜色。
        /// </summary>
        public Color BorderColor { get; }

        /// <summary>
        /// 当前 Cell 四个方向需要绘制的外露边。
        /// </summary>
        public RoomBorderMask BorderMask { get; }

        /// <summary>
        /// 当前 Cell 需要绘制的内凹角。
        /// </summary>
        public RoomInnerCornerMask InnerCornerMask { get; }

        /// <summary>
        /// 当前 Cell 与其他房间连接形成的门洞方向。
        /// </summary>
        public RoomDoorMask DoorMask { get; }

        /// <summary>
        /// 当前 Cell 需要绘制圆形外角的方向。
        /// </summary>
        public RoomOuterCornerMask OuterCornerMask { get; }

        private RoomCellVisualData(
            Vector2Int cell,
            Color fillColor,
            Color borderColor,
            RoomBorderMask borderMask,
            RoomInnerCornerMask innerCornerMask,
            RoomDoorMask doorMask,
            RoomOuterCornerMask outerCornerMask)
        {
            Cell = cell;
            FillColor = fillColor;
            BorderColor = borderColor;
            BorderMask = borderMask;
            InnerCornerMask = innerCornerMask;
            DoorMask = doorMask;
            OuterCornerMask = outerCornerMask;
        }

        /// <summary>
        /// 根据当前 DungeonGrid 和 RoomInstance 的真实逻辑状态，
        /// 创建指定 Cell 的完整视觉数据。
        /// </summary>
        public static RoomCellVisualData Create(Vector2Int cell, RoomInstance room, DungeonGrid grid)
        {
            RoomCellColors colors =
                RoomCellColorFactory.Create(room.Data.RoomColor);

            RoomBorderMask borderMask =
                RoomBorderUtility.CalculateMask(cell, room, grid);

            RoomInnerCornerMask innerCornerMask =
                RoomBorderUtility.CalculateInnerCornerMask(cell, room, grid);

            RoomDoorMask doorMask =
                RoomBorderUtility.CalculateDoorMask(cell, room, grid);

            RoomOuterCornerMask outerCornerMask =
                RoomBorderUtility.CalculateOuterCornerMask(cell, room, grid);

            return new RoomCellVisualData(
                cell,
                colors.FillColor,
                colors.BorderColor,
                borderMask,
                innerCornerMask,
                doorMask,
                outerCornerMask
            );
        }
    }
}