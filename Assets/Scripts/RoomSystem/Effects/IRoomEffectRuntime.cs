namespace Dungeon.RoomSystem
{
    /// <summary>
    /// 房间效果的运行时行为接口。
    /// ScriptableObject 只保存配置，每个房间都拥有独立的运行时对象。
    /// </summary>
    public interface IRoomEffectRuntime
    {
        /// <summary>
        /// 效果被添加到房间时调用。
        /// </summary>
        void OnAdded(
            RoomInstance room,
            RoomEffectHandle handle
        );

        /// <summary>
        /// 效果从房间移除时调用。
        /// </summary>
        void OnRemoved(
            RoomInstance room,
            RoomEffectHandle handle
        );

        /// <summary>
        /// 房间收到事件时调用。
        /// </summary>
        void OnEvent(
            RoomInstance room,
            RoomEffectHandle handle,
            RoomEvent roomEvent
        );
    }
}