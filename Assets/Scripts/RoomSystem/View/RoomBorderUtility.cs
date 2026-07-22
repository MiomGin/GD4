using System;
using System.Collections.Generic;
using UnityEngine;

namespace Dungeon.RoomSystem
{
    /// <summary>
    /// 单个格子需要显示的四方向描边。
    /// </summary>
    [Flags]
    public enum RoomBorderMask
    {
        None = 0,
        Left = 1 << 0,
        Right = 1 << 1,
        Bottom = 1 << 2,
        Top = 1 << 3,

        All = Left | Right | Bottom | Top
    }

    /// <summary>
    /// 为连接 L 形凹角而需要补充的方形描边区域。
    /// </summary>
    [Flags]
    public enum RoomInnerCornerMask
    {
        None = 0,
        BottomLeft = 1 << 0,
        BottomRight = 1 << 1,
        TopLeft = 1 << 2,
        TopRight = 1 << 3
    }

    /// <summary>
    /// 单个格子四方向上需要生成门洞的连接边。
    /// 只有相邻格属于另一个房间时，对应方向才会启用。
    /// </summary>
    [Flags]
    public enum RoomDoorMask
    {
        None = 0,

        Left = 1 << 0,
        Right = 1 << 1,
        Bottom = 1 << 2,
        Top = 1 << 3,

        All = Left | Right | Bottom | Top
    }

    /// <summary>
    /// 计算房间格子的外露边和凹角连接区域。
    /// </summary>
    public static class RoomBorderUtility
    {
        /// <summary>
        /// 计算指定格子的四方向外露边。
        /// </summary>
        public static RoomBorderMask CalculateMask(
            Vector2Int cell,
            HashSet<Vector2Int> roomCells)
        {
            if (roomCells == null)
            {
                return RoomBorderMask.All;
            }

            RoomBorderMask mask = RoomBorderMask.None;

            if (!roomCells.Contains(cell + Vector2Int.left))
            {
                mask |= RoomBorderMask.Left;
            }

            if (!roomCells.Contains(cell + Vector2Int.right))
            {
                mask |= RoomBorderMask.Right;
            }

            if (!roomCells.Contains(cell + Vector2Int.down))
            {
                mask |= RoomBorderMask.Bottom;
            }

            if (!roomCells.Contains(cell + Vector2Int.up))
            {
                mask |= RoomBorderMask.Top;
            }

            return mask;
        }

        /// <summary>
        /// 计算需要补充的凹角区域。
        /// 当两个正交相邻格存在、但对应对角格不存在时，
        /// 当前格子的角落需要补充描边以连接两条边。
        /// </summary>
        public static RoomInnerCornerMask CalculateInnerCornerMask(
            Vector2Int cell,
            HashSet<Vector2Int> roomCells)
        {
            if (roomCells == null)
            {
                return RoomInnerCornerMask.None;
            }

            bool hasLeft =
                roomCells.Contains(cell + Vector2Int.left);

            bool hasRight =
                roomCells.Contains(cell + Vector2Int.right);

            bool hasBottom =
                roomCells.Contains(cell + Vector2Int.down);

            bool hasTop =
                roomCells.Contains(cell + Vector2Int.up);

            RoomInnerCornerMask mask =
                RoomInnerCornerMask.None;

            if (hasLeft &&
                hasBottom &&
                !roomCells.Contains(
                    cell + Vector2Int.left + Vector2Int.down))
            {
                mask |= RoomInnerCornerMask.BottomLeft;
            }

            if (hasRight &&
                hasBottom &&
                !roomCells.Contains(
                    cell + Vector2Int.right + Vector2Int.down))
            {
                mask |= RoomInnerCornerMask.BottomRight;
            }

            if (hasLeft &&
                hasTop &&
                !roomCells.Contains(
                    cell + Vector2Int.left + Vector2Int.up))
            {
                mask |= RoomInnerCornerMask.TopLeft;
            }

            if (hasRight &&
                hasTop &&
                !roomCells.Contains(
                    cell + Vector2Int.right + Vector2Int.up))
            {
                mask |= RoomInnerCornerMask.TopRight;
            }

            return mask;
        }

        /// <summary>
        /// 计算当前格子哪些外侧边连接了另一个房间。
        ///
        /// 同一房间内部相邻边不生成门洞；
        /// 相邻格为空时也不生成门洞；
        /// 只有相邻格被其他房间占用时生成门洞。
        /// </summary>
        /// <param name="cell">当前世界逻辑格。</param>
        /// <param name="roomCells">当前房间占用的全部逻辑格。</param>
        /// <param name="dungeonGrid">所属地牢网格。</param>
        public static RoomDoorMask CalculateDoorMask(
            Vector2Int cell,
            HashSet<Vector2Int> roomCells,
            DungeonGrid dungeonGrid)
        {
            if (roomCells == null ||
                dungeonGrid == null)
            {
                return RoomDoorMask.None;
            }

            RoomDoorMask mask =
                RoomDoorMask.None;

            if (HasOtherRoom(
                    cell + Vector2Int.left,
                    roomCells,
                    dungeonGrid))
            {
                mask |= RoomDoorMask.Left;
            }

            if (HasOtherRoom(
                    cell + Vector2Int.right,
                    roomCells,
                    dungeonGrid))
            {
                mask |= RoomDoorMask.Right;
            }

            if (HasOtherRoom(
                    cell + Vector2Int.down,
                    roomCells,
                    dungeonGrid))
            {
                mask |= RoomDoorMask.Bottom;
            }

            if (HasOtherRoom(
                    cell + Vector2Int.up,
                    roomCells,
                    dungeonGrid))
            {
                mask |= RoomDoorMask.Top;
            }

            return mask;
        }

        /// <summary>
        /// 判断指定格是否被当前房间以外的房间占用。
        /// </summary>
        private static bool HasOtherRoom(
            Vector2Int neighborCell,
            HashSet<Vector2Int> roomCells,
            DungeonGrid dungeonGrid)
        {
            /*
             * 属于当前房间时是内部连接，
             * 当前内部边本来就完全不绘制。
             */
            if (roomCells.Contains(neighborCell))
            {
                return false;
            }

            return dungeonGrid.IsCellOccupied(
                neighborCell
            );
        }
    }
}