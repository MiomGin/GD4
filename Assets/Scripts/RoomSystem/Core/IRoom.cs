using System.Collections.Generic;

namespace Dungeon.RoomSystem
{
    /// <summary>
    /// 房间运行时实例对外暴露的基础接口。
    /// 外部系统应通过该接口添加效果、移除效果和发送房间事件，
    /// 而不是直接修改房间内部状态。
    /// </summary>
    public interface IRoom
    {
        /// <summary>
        /// 房间对应的静态配置。
        /// </summary>
        RoomData Data { get; }

        /// <summary>
        /// 房间当前拥有的全部标签。
        /// </summary>
        RoomTagSet Tags { get; }

        /// <summary>
        /// 房间当前持有的全部效果实例。
        /// </summary>
        IReadOnlyList<RoomEffectHandle> Effects { get; }

        /// <summary>
        /// 尝试向房间添加一个效果。
        /// </summary>
        /// <param name="effectData">效果静态配置。</param>
        /// <param name="source">效果来源，例如道具、升级或房间配置。</param>
        /// <param name="handle">成功添加或叠加后的效果句柄。</param>
        /// <returns>添加、刷新或叠加成功时返回 true。</returns>
        bool TryAddEffect(
            RoomEffectData effectData,
            object source,
            out RoomEffectHandle handle
        );

        /// <summary>
        /// 移除房间上的一个效果实例。
        /// </summary>
        bool RemoveEffect(RoomEffectHandle handle);

        /// <summary>
        /// 向房间当前持有的所有效果发送事件。
        /// </summary>
        void Publish(RoomEvent roomEvent);
    }
}