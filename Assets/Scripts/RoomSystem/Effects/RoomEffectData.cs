using System;
using System.Collections.Generic;
using UnityEngine;

namespace Dungeon.RoomSystem
{
    /// <summary>
    /// 所有房间效果配置的抽象基类。
    /// 负责描述使用条件、叠加规则和效果提供的标签。
    /// </summary>
    public abstract class RoomEffectData : ScriptableObject
    {
        [Header("效果标识")]

        [SerializeField]
        private string effectId;

        [Header("添加条件")]

        [SerializeField]
        private RoomTagQuery requirements =
            new RoomTagQuery();

        [Header("叠加规则")]

        [SerializeField]
        private RoomEffectStackPolicy stackPolicy =
            RoomEffectStackPolicy.Unique;

        [SerializeField, Min(1)]
        private int maxStack = 1;

        [Header("效果提供的标签")]

        [SerializeField]
        private RoomTag[] grantedTags =
            Array.Empty<RoomTag>();

        /// <summary>
        /// 效果的唯一标识。
        /// </summary>
        public string EffectId => effectId;

        /// <summary>
        /// 效果重复添加时的叠加规则。
        /// </summary>
        public RoomEffectStackPolicy StackPolicy =>
            stackPolicy;

        /// <summary>
        /// 效果允许达到的最大层数。
        /// </summary>
        public int MaxStack =>
            Mathf.Max(1, maxStack);

        /// <summary>
        /// 效果存在期间为房间提供的标签。
        /// </summary>
        public IReadOnlyList<RoomTag> GrantedTags =>
            grantedTags;

        /// <summary>
        /// 判断当前效果能否安装到指定房间。
        /// </summary>
        public bool CanApply(RoomInstance room)
        {
            if (room == null)
            {
                return false;
            }

            return requirements == null ||
                   requirements.Matches(room.Tags);
        }

        /// <summary>
        /// 为某个房间创建独立的效果运行时对象。
        /// 不应直接在 ScriptableObject 中保存冷却、次数等运行状态。
        /// </summary>
        public abstract IRoomEffectRuntime CreateRuntime();

#if UNITY_EDITOR
        protected virtual void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(effectId))
            {
                effectId = name;
            }

            maxStack = Mathf.Max(1, maxStack);

            if (grantedTags == null)
            {
                grantedTags =
                    Array.Empty<RoomTag>();
            }
        }
#endif
    }
}