using UnityEngine;

namespace Dungeon.RoomSystem
{
    /// <summary>
    /// 在房间内部传递的通用事件数据。
    /// </summary>
    public readonly struct RoomEvent
    {
        /// <summary>
        /// 事件类型。
        /// </summary>
        public RoomEventType Type { get; }

        /// <summary>
        /// 与事件相关的场景对象，例如进入房间的敌人。
        /// </summary>
        public GameObject Actor { get; }

        /// <summary>
        /// 事件附带的扩展数据。
        /// </summary>
        public object Payload { get; }

        /// <summary>
        /// 事件附带的数值参数。
        /// </summary>
        public float Value { get; }

        public RoomEvent(
            RoomEventType type,
            GameObject actor = null,
            object payload = null,
            float value = 0f)
        {
            Type = type;
            Actor = actor;
            Payload = payload;
            Value = value;
        }

        /// <summary>
        /// 创建房间已放置事件。
        /// </summary>
        public static RoomEvent RoomPlaced(
            RoomInstance room)
        {
            return new RoomEvent(
                RoomEventType.RoomPlaced,
                payload: room
            );
        }

        /// <summary>
        /// 创建房间即将被移除事件。
        /// </summary>
        public static RoomEvent RoomRemoved(
            RoomInstance room)
        {
            return new RoomEvent(
                RoomEventType.RoomRemoved,
                payload: room
            );
        }

        /// <summary>
        /// 创建敌人进入房间事件。
        /// </summary>
        public static RoomEvent EnemyEntered(
            GameObject enemy)
        {
            return new RoomEvent(
                RoomEventType.EnemyEntered,
                actor: enemy
            );
        }

        /// <summary>
        /// 创建敌人离开房间事件。
        /// </summary>
        public static RoomEvent EnemyExited(
            GameObject enemy)
        {
            return new RoomEvent(
                RoomEventType.EnemyExited,
                actor: enemy
            );
        }

        /// <summary>
        /// 创建效果添加事件。
        /// </summary>
        public static RoomEvent EffectAdded(
            RoomEffectHandle handle)
        {
            return new RoomEvent(
                RoomEventType.EffectAdded,
                payload: handle
            );
        }

        /// <summary>
        /// 创建效果移除事件。
        /// </summary>
        public static RoomEvent EffectRemoved(
            RoomEffectHandle handle)
        {
            return new RoomEvent(
                RoomEventType.EffectRemoved,
                payload: handle
            );
        }

        /// <summary>
        /// 创建效果叠层事件。
        /// </summary>
        public static RoomEvent EffectStacked(
            RoomEffectHandle handle)
        {
            return new RoomEvent(
                RoomEventType.EffectStacked,
                payload: handle,
                value: handle.StackCount
            );
        }

        /// <summary>
        /// 创建效果刷新事件。
        /// </summary>
        public static RoomEvent EffectRefreshed(
            RoomEffectHandle handle)
        {
            return new RoomEvent(
                RoomEventType.EffectRefreshed,
                payload: handle
            );
        }
    }
}