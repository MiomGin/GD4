using UnityEngine;

namespace Dungeon.RoomSystem
{
    /// <summary>
    /// 表示房间墙体在单个 Cell 0～1 局部空间中的几何参数。
    ///
    /// 该数据同时提供给：
    /// 1. Shader，用于绘制 Gap、OuterBorder 和 Wall。
    /// 2. RoomVisualChunk，用于生成与视觉一致的真实墙体 Collider。
    ///
    /// 碰撞范围：
    /// GapWidth
    ///     ↓
    /// BorderWidth
    ///     ↓
    /// WallWidth
    ///
    /// RoomShadow 不属于实体墙，因此不参与碰撞。
    /// </summary>
    public readonly struct RoomWallGeometry
    {
        public float GapWidth { get; }
        public float BorderWidth { get; }
        public float WallWidth { get; }

        /// <summary>
        /// OuterBorder 开始位置。
        /// </summary>
        public float BorderStart => GapWidth;

        /// <summary>
        /// OuterBorder 结束，同时也是 Wall 开始位置。
        /// </summary>
        public float BorderEnd => GapWidth + BorderWidth;

        /// <summary>
        /// Wall 结束，同时也是实体碰撞结束位置。
        /// </summary>
        public float WallEnd => GapWidth + BorderWidth + WallWidth;

        /// <summary>
        /// 实际实体墙厚度，不包含最外侧透明 Gap。
        /// </summary>
        public float CollisionWidth => BorderWidth + WallWidth;

        public RoomWallGeometry(float gapWidth, float borderWidth, float wallWidth)
        {
            GapWidth = Mathf.Clamp(gapWidth, 0f, 0.49f);
            BorderWidth = Mathf.Clamp(borderWidth, 0f, 0.5f - GapWidth);

            float borderEnd = GapWidth + BorderWidth;
            WallWidth = Mathf.Clamp(wallWidth, 0f, 0.5f - borderEnd);
        }
    }
}