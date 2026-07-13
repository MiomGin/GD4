namespace Dungeon.RoomSystem
{
    /// <summary>
    /// 房间系统第一阶段使用的事件类型。
    /// </summary>
    public enum RoomEventType
    {
        RoomPlaced,
        RoomRemoved,

        EnemyEntered,
        EnemyExited,

        RoomTriggered,

        EffectAdded,
        EffectRemoved,
        EffectStacked,
        EffectRefreshed
    }
}