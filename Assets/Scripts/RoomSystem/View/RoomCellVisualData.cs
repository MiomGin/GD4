using UnityEngine;

namespace Dungeon.RoomSystem
{
    /// <summary>
    /// 表示单个逻辑格在当前时刻生成视觉 Mesh 所需要的全部数据。
    ///
    /// 该结构是纯临时数据，不保存长期状态。
    /// RoomInstance 和 DungeonGrid 是视觉数据的真实来源，
    /// Chunk 重建时根据当前逻辑状态重新计算该结构。
    /// </summary>
    public readonly struct RoomCellVisualData
    {
        public Vector2Int Cell { get; }

        public Color FillColor { get; }
        public Color BorderColor { get; }

        /// <summary>
        /// Wall 中性基础色，由 RoomCellColorFactory 在 OKLCH 空间生成。
        /// </summary>
        public Color WallBaseColor { get; }

        /// <summary>
        /// Wall 暗面颜色，由 WallBaseColor 在 OKLCH 中降低 Lightness 得到。
        /// </summary>
        public Color WallDarkColor { get; }

        /// <summary>
        /// Wall 亮面颜色，由 WallBaseColor 在 OKLCH 中提高 Lightness 得到。
        /// </summary>
        public Color WallLightColor { get; }

        public RoomBorderMask BorderMask { get; }
        public RoomInnerCornerMask InnerCornerMask { get; }
        public RoomDoorMask DoorMask { get; }
        public RoomOuterCornerMask OuterCornerMask { get; }

        private RoomCellVisualData(
            Vector2Int cell,
            Color fillColor,
            Color borderColor,
            Color wallBaseColor,
            Color wallDarkColor,
            Color wallLightColor,
            RoomBorderMask borderMask,
            RoomInnerCornerMask innerCornerMask,
            RoomDoorMask doorMask,
            RoomOuterCornerMask outerCornerMask)
        {
            Cell = cell;

            FillColor = fillColor;
            BorderColor = borderColor;

            WallBaseColor = wallBaseColor;
            WallDarkColor = wallDarkColor;
            WallLightColor = wallLightColor;

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
                RoomCellColorFactory.Create(
                    room.Data.RoomColor
                );

            RoomBorderMask borderMask =
                RoomBorderUtility.CalculateMask(
                    cell,
                    room,
                    grid
                );

            RoomInnerCornerMask innerCornerMask =
                RoomBorderUtility.CalculateInnerCornerMask(
                    cell,
                    room,
                    grid
                );

            RoomDoorMask doorMask =
                RoomBorderUtility.CalculateDoorMask(
                    cell,
                    room,
                    grid
                );

            RoomOuterCornerMask outerCornerMask =
                RoomBorderUtility.CalculateOuterCornerMask(
                    cell,
                    room,
                    grid
                );

            return new RoomCellVisualData(
                cell,
                colors.FillColor,
                colors.BorderColor,
                colors.WallBaseColor,
                colors.WallDarkColor,
                colors.WallLightColor,
                borderMask,
                innerCornerMask,
                doorMask,
                outerCornerMask
            );
        }
    }
}