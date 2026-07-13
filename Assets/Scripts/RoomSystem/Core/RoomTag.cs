namespace Dungeon.RoomSystem
{
    /// <summary>
    /// 描述房间当前具备的结构身份、功能和性质。
    /// Entrance、Throne 等固定地图身份通常来自 RoomData；
    /// Trap、Poison 等可变功能通常由 RoomEffect 提供。
    /// </summary>
    public enum RoomTag
    {
        Room,

        // 固定地图身份
        Entrance,
        Throne,
        Unique,

        // 可变房间功能
        Trap,
        Treasure,

        // 元素或性质
        Fire,
        Poison,
        Mechanical,
        Magical,
        Cursed
    }
}