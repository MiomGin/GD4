using System;
using System.Collections.Generic;

namespace Dungeon.RoomSystem
{
    /// <summary>
    /// 管理房间当前拥有的标签及每个标签的来源。
    /// 同一个标签可以由多个来源共同提供。
    /// </summary>
    public sealed class RoomTagSet
    {
        private readonly Dictionary<
            RoomTag,
            HashSet<object>
        > sourcesByTag =
            new Dictionary<RoomTag, HashSet<object>>();

        /// <summary>
        /// 标签集合发生变化时触发。
        /// 后续联动系统可以监听该事件。
        /// </summary>
        public event Action Changed;

        /// <summary>
        /// 判断房间当前是否拥有指定标签。
        /// </summary>
        public bool Has(RoomTag tag)
        {
            return sourcesByTag.TryGetValue(
                       tag,
                       out HashSet<object> sources
                   ) &&
                   sources.Count > 0;
        }

        /// <summary>
        /// 使用指定来源为房间添加标签。
        /// 相同来源重复添加同一标签不会重复计数。
        /// </summary>
        public bool Add(RoomTag tag, object source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(
                    nameof(source)
                );
            }

            if (!sourcesByTag.TryGetValue(
                    tag,
                    out HashSet<object> sources))
            {
                sources = new HashSet<object>();
                sourcesByTag.Add(tag, sources);
            }

            if (!sources.Add(source))
            {
                return false;
            }

            Changed?.Invoke();
            return true;
        }

        /// <summary>
        /// 删除指定来源提供的标签。
        /// 只有所有来源都被删除后，该标签才会真正失效。
        /// </summary>
        public bool Remove(RoomTag tag, object source)
        {
            if (source == null)
            {
                return false;
            }

            if (!sourcesByTag.TryGetValue(
                    tag,
                    out HashSet<object> sources))
            {
                return false;
            }

            if (!sources.Remove(source))
            {
                return false;
            }

            if (sources.Count == 0)
            {
                sourcesByTag.Remove(tag);
            }

            Changed?.Invoke();
            return true;
        }

        /// <summary>
        /// 删除指定来源提供的所有标签。
        /// </summary>
        public void RemoveAllFromSource(object source)
        {
            if (source == null)
            {
                return;
            }

            List<RoomTag> changedTags =
                new List<RoomTag>();

            foreach (KeyValuePair<
                         RoomTag,
                         HashSet<object>
                     > pair in sourcesByTag)
            {
                if (pair.Value.Remove(source))
                {
                    changedTags.Add(pair.Key);
                }
            }

            foreach (RoomTag tag in changedTags)
            {
                if (sourcesByTag[tag].Count == 0)
                {
                    sourcesByTag.Remove(tag);
                }
            }

            if (changedTags.Count > 0)
            {
                Changed?.Invoke();
            }
        }
    }
}