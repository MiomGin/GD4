namespace Dungeon.RoomSystem
{
    /// <summary>
    /// 房间可以持有的基础标签。
    /// 一个房间允许同时拥有多个标签。
    /// </summary>
    public enum RoomTag
    {
        Room,

        Normal,
        Trap,
        Treasure,
        Combat,
        Spawn,
        Shop,

        Fire,
        Ice,
        Poison,
        Electric,
        Wet,

        Mechanical,
        Magical,
        Cursed,
        Holy,

        Boss,
        Unique,
        Upgradeable,
        Destroyable
    }
}