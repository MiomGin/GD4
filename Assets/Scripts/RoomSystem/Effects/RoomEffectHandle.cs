namespace Dungeon.RoomSystem
{
    /// <summary>
    /// 表示一个已经安装到房间上的运行时效果实例。
    /// </summary>
    public sealed class RoomEffectHandle
    {
        /// <summary>
        /// 效果静态配置。
        /// </summary>
        public RoomEffectData Data { get; }

        /// <summary>
        /// 当前房间独立拥有的效果运行时对象。
        /// </summary>
        public IRoomEffectRuntime Runtime { get; }

        /// <summary>
        /// 添加该效果的来源。
        /// </summary>
        public object Source { get; }

        /// <summary>
        /// 当前效果叠加层数。
        /// </summary>
        public int StackCount { get; internal set; }

        public RoomEffectHandle(
            RoomEffectData data,
            IRoomEffectRuntime runtime,
            object source)
        {
            Data = data;
            Runtime = runtime;
            Source = source;
            StackCount = 1;
        }
    }
}