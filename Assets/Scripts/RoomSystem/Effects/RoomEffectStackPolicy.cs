namespace Dungeon.RoomSystem
{
    /// <summary>
    /// 同一种房间效果被重复添加时采用的处理方式。
    /// </summary>
    public enum RoomEffectStackPolicy
    {
        /// <summary>
        /// 同一种效果只能存在一个。
        /// </summary>
        Unique,

        /// <summary>
        /// 重复添加时增加效果层数。
        /// </summary>
        Stack,

        /// <summary>
        /// 重复添加时刷新效果状态。
        /// 具体刷新逻辑由效果运行时响应事件完成。
        /// </summary>
        Refresh,

        /// <summary>
        /// 移除旧效果并创建一个新的效果实例。
        /// </summary>
        Replace
    }
}