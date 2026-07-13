using System;
using UnityEngine;

namespace Dungeon.RoomSystem
{
    /// <summary>
    /// 用于判断房间标签是否满足一组条件。
    /// 可用于效果安装条件、房间升级条件和后续联动条件。
    /// </summary>
    [Serializable]
    public sealed class RoomTagQuery
    {
        [Tooltip("房间必须拥有这里的全部标签。")]
        [SerializeField]
        private RoomTag[] allTags =
            Array.Empty<RoomTag>();

        [Tooltip("房间必须至少拥有这里的一个标签。为空时忽略。")]
        [SerializeField]
        private RoomTag[] anyTags =
            Array.Empty<RoomTag>();

        [Tooltip("房间不能拥有这里的任何标签。")]
        [SerializeField]
        private RoomTag[] noneTags =
            Array.Empty<RoomTag>();

        /// <summary>
        /// 判断指定标签集合是否满足当前查询条件。
        /// </summary>
        public bool Matches(RoomTagSet tags)
        {
            if (tags == null)
            {
                return false;
            }

            if (allTags != null)
            {
                foreach (RoomTag tag in allTags)
                {
                    if (!tags.Has(tag))
                    {
                        return false;
                    }
                }
            }

            if (anyTags != null &&
                anyTags.Length > 0)
            {
                bool hasAny = false;

                foreach (RoomTag tag in anyTags)
                {
                    if (tags.Has(tag))
                    {
                        hasAny = true;
                        break;
                    }
                }

                if (!hasAny)
                {
                    return false;
                }
            }

            if (noneTags != null)
            {
                foreach (RoomTag tag in noneTags)
                {
                    if (tags.Has(tag))
                    {
                        return false;
                    }
                }
            }

            return true;
        }
    }
}