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
    /// 当两个正交方向连接同一房间，
    /// 但对应对角格不属于该房间时形成的内凹角。
    /// </summary>
    [Flags]
    public enum RoomInnerCornerMask
    {
        None = 0,

        BottomLeft = 1 << 0,
        BottomRight = 1 << 1,
        TopLeft = 1 << 2,
        TopRight = 1 << 3,

        All = BottomLeft | BottomRight | TopLeft | TopRight
    }

    /// <summary>
    /// 当前格两个相邻正交方向同时暴露时形成的外凸角。
    ///
    /// 主要用于 Shader 中生成圆角墙壁。
    /// </summary>
    [Flags]
    public enum RoomOuterCornerMask
    {
        None = 0,

        BottomLeft = 1 << 0,
        BottomRight = 1 << 1,
        TopLeft = 1 << 2,
        TopRight = 1 << 3,

        All = BottomLeft | BottomRight | TopLeft | TopRight
    }

    /// <summary>
    /// 单个格子四方向上需要生成门洞的连接边。
    ///
    /// 只有相邻格属于另一个房间时，
    /// 对应方向才会启用。
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
    /// 统一计算房间格子的墙壁拓扑数据。
    ///
    /// 当前同时保留两套接口：
    ///
    /// 1. HashSet 版本
    ///    用于兼容旧 RoomVisualController。
    ///
    /// 2. RoomInstance + DungeonGrid 版本
    ///    用于新的 Chunk Mesh 视觉系统。
    ///
    /// 新系统不需要为每个房间额外构建 HashSet，
    /// 可以直接通过 DungeonGrid 查询指定 Cell 属于哪个 RoomInstance。
    /// </summary>
    public static class RoomBorderUtility
    {
        #region Chunk Visual API

        /// <summary>
        /// 根据 DungeonGrid 当前占用关系计算指定 Cell 的四方向外露边。
        /// </summary>
        public static RoomBorderMask CalculateMask(Vector2Int cell, RoomInstance room, DungeonGrid grid)
        {
            if (room == null || grid == null)
            {
                return RoomBorderMask.All;
            }

            RoomBorderMask mask = RoomBorderMask.None;

            if (!BelongsToRoom(cell + Vector2Int.left, room, grid))
            {
                mask |= RoomBorderMask.Left;
            }

            if (!BelongsToRoom(cell + Vector2Int.right, room, grid))
            {
                mask |= RoomBorderMask.Right;
            }

            if (!BelongsToRoom(cell + Vector2Int.down, room, grid))
            {
                mask |= RoomBorderMask.Bottom;
            }

            if (!BelongsToRoom(cell + Vector2Int.up, room, grid))
            {
                mask |= RoomBorderMask.Top;
            }

            return mask;
        }

        /// <summary>
        /// 计算指定 Cell 的内凹角。
        ///
        /// 例如左侧和下侧都属于当前房间，
        /// 但左下对角格不属于当前房间，
        /// 则产生 BottomLeft 内凹角。
        /// </summary>
        public static RoomInnerCornerMask CalculateInnerCornerMask(Vector2Int cell, RoomInstance room, DungeonGrid grid)
        {
            if (room == null || grid == null)
            {
                return RoomInnerCornerMask.None;
            }

            bool hasLeft = BelongsToRoom(cell + Vector2Int.left, room, grid);
            bool hasRight = BelongsToRoom(cell + Vector2Int.right, room, grid);
            bool hasBottom = BelongsToRoom(cell + Vector2Int.down, room, grid);
            bool hasTop = BelongsToRoom(cell + Vector2Int.up, room, grid);

            RoomInnerCornerMask mask = RoomInnerCornerMask.None;

            if (hasLeft && hasBottom &&
                !BelongsToRoom(cell + Vector2Int.left + Vector2Int.down, room, grid))
            {
                mask |= RoomInnerCornerMask.BottomLeft;
            }

            if (hasRight && hasBottom &&
                !BelongsToRoom(cell + Vector2Int.right + Vector2Int.down, room, grid))
            {
                mask |= RoomInnerCornerMask.BottomRight;
            }

            if (hasLeft && hasTop &&
                !BelongsToRoom(cell + Vector2Int.left + Vector2Int.up, room, grid))
            {
                mask |= RoomInnerCornerMask.TopLeft;
            }

            if (hasRight && hasTop &&
                !BelongsToRoom(cell + Vector2Int.right + Vector2Int.up, room, grid))
            {
                mask |= RoomInnerCornerMask.TopRight;
            }

            return mask;
        }

        /// <summary>
        /// 计算当前 Cell 的四个外凸角。
        ///
        /// 当一个角对应的两个正交方向都属于外露边时，
        /// 该位置可以在 Shader 中绘制圆形外角。
        /// </summary>
        public static RoomOuterCornerMask CalculateOuterCornerMask(Vector2Int cell, RoomInstance room, DungeonGrid grid)
        {
            if (room == null || grid == null)
            {
                return RoomOuterCornerMask.All;
            }

            bool exposedLeft = !BelongsToRoom(cell + Vector2Int.left, room, grid);
            bool exposedRight = !BelongsToRoom(cell + Vector2Int.right, room, grid);
            bool exposedBottom = !BelongsToRoom(cell + Vector2Int.down, room, grid);
            bool exposedTop = !BelongsToRoom(cell + Vector2Int.up, room, grid);

            RoomOuterCornerMask mask = RoomOuterCornerMask.None;

            if (exposedLeft && exposedBottom)
            {
                mask |= RoomOuterCornerMask.BottomLeft;
            }

            if (exposedRight && exposedBottom)
            {
                mask |= RoomOuterCornerMask.BottomRight;
            }

            if (exposedLeft && exposedTop)
            {
                mask |= RoomOuterCornerMask.TopLeft;
            }

            if (exposedRight && exposedTop)
            {
                mask |= RoomOuterCornerMask.TopRight;
            }

            return mask;
        }

        /// <summary>
        /// 计算当前 Cell 哪些外侧边连接了其他 RoomInstance。
        ///
        /// 同一 RoomInstance 内部不生成门洞。
        /// 空格不生成门洞。
        /// 只有相邻格属于另一个 RoomInstance 时生成门洞。
        /// </summary>
        public static RoomDoorMask CalculateDoorMask(Vector2Int cell, RoomInstance room, DungeonGrid grid)
        {
            if (room == null || grid == null)
            {
                return RoomDoorMask.None;
            }

            RoomDoorMask mask = RoomDoorMask.None;

            if (BelongsToOtherRoom(cell + Vector2Int.left, room, grid))
            {
                mask |= RoomDoorMask.Left;
            }

            if (BelongsToOtherRoom(cell + Vector2Int.right, room, grid))
            {
                mask |= RoomDoorMask.Right;
            }

            if (BelongsToOtherRoom(cell + Vector2Int.down, room, grid))
            {
                mask |= RoomDoorMask.Bottom;
            }

            if (BelongsToOtherRoom(cell + Vector2Int.up, room, grid))
            {
                mask |= RoomDoorMask.Top;
            }

            return mask;
        }

        /// <summary>
        /// 判断指定 Cell 是否属于目标 RoomInstance。
        /// </summary>
        private static bool BelongsToRoom(Vector2Int cell, RoomInstance room, DungeonGrid grid)
        {
            return grid.TryGetRoom(cell, out RoomInstance neighborRoom) &&
                   neighborRoom == room;
        }

        /// <summary>
        /// 判断指定 Cell 是否属于当前房间之外的其他房间。
        /// </summary>
        private static bool BelongsToOtherRoom(Vector2Int cell, RoomInstance room, DungeonGrid grid)
        {
            return grid.TryGetRoom(cell, out RoomInstance neighborRoom) &&
                   neighborRoom != null &&
                   neighborRoom != room;
        }

        #endregion

        #region Legacy Sprite Visual API

        /// <summary>
        /// 旧 SpriteRenderer 后端使用的外露边计算接口。
        /// </summary>
        public static RoomBorderMask CalculateMask(Vector2Int cell, HashSet<Vector2Int> roomCells)
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
        /// 旧 SpriteRenderer 后端使用的内凹角计算接口。
        /// </summary>
        public static RoomInnerCornerMask CalculateInnerCornerMask(Vector2Int cell, HashSet<Vector2Int> roomCells)
        {
            if (roomCells == null)
            {
                return RoomInnerCornerMask.None;
            }

            bool hasLeft = roomCells.Contains(cell + Vector2Int.left);
            bool hasRight = roomCells.Contains(cell + Vector2Int.right);
            bool hasBottom = roomCells.Contains(cell + Vector2Int.down);
            bool hasTop = roomCells.Contains(cell + Vector2Int.up);

            RoomInnerCornerMask mask = RoomInnerCornerMask.None;

            if (hasLeft && hasBottom &&
                !roomCells.Contains(cell + Vector2Int.left + Vector2Int.down))
            {
                mask |= RoomInnerCornerMask.BottomLeft;
            }

            if (hasRight && hasBottom &&
                !roomCells.Contains(cell + Vector2Int.right + Vector2Int.down))
            {
                mask |= RoomInnerCornerMask.BottomRight;
            }

            if (hasLeft && hasTop &&
                !roomCells.Contains(cell + Vector2Int.left + Vector2Int.up))
            {
                mask |= RoomInnerCornerMask.TopLeft;
            }

            if (hasRight && hasTop &&
                !roomCells.Contains(cell + Vector2Int.right + Vector2Int.up))
            {
                mask |= RoomInnerCornerMask.TopRight;
            }

            return mask;
        }

        /// <summary>
        /// 旧 SpriteRenderer 后端使用的门洞计算接口。
        /// </summary>
        public static RoomDoorMask CalculateDoorMask(
            Vector2Int cell,
            HashSet<Vector2Int> roomCells,
            DungeonGrid dungeonGrid)
        {
            if (roomCells == null || dungeonGrid == null)
            {
                return RoomDoorMask.None;
            }

            RoomDoorMask mask = RoomDoorMask.None;

            if (HasOtherRoom(cell + Vector2Int.left, roomCells, dungeonGrid))
            {
                mask |= RoomDoorMask.Left;
            }

            if (HasOtherRoom(cell + Vector2Int.right, roomCells, dungeonGrid))
            {
                mask |= RoomDoorMask.Right;
            }

            if (HasOtherRoom(cell + Vector2Int.down, roomCells, dungeonGrid))
            {
                mask |= RoomDoorMask.Bottom;
            }

            if (HasOtherRoom(cell + Vector2Int.up, roomCells, dungeonGrid))
            {
                mask |= RoomDoorMask.Top;
            }

            return mask;
        }

        private static bool HasOtherRoom(
            Vector2Int neighborCell,
            HashSet<Vector2Int> roomCells,
            DungeonGrid dungeonGrid)
        {
            if (roomCells.Contains(neighborCell))
            {
                return false;
            }

            return dungeonGrid.IsCellOccupied(neighborCell);
        }

        #endregion
    }
}