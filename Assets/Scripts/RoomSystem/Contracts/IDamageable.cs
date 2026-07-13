using UnityEngine;

namespace Dungeon.RoomSystem
{
    /// <summary>
    /// 可以受到房间伤害的对象需要实现的接口。
    /// 后续可以替换成项目已有的生命值或伤害接口。
    /// </summary>
    public interface IDamageable
    {
        /// <summary>
        /// 对当前对象造成伤害。
        /// </summary>
        /// <param name="amount">伤害数值。</param>
        /// <param name="source">伤害来源对象。</param>
        void TakeDamage(
            float amount,
            GameObject source
        );
    }
}