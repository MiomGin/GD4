using UnityEngine;

namespace Dungeon.RoomSystem
{
    /// <summary>
    /// 敌人进入房间时立即造成伤害的具体房间效果。
    /// 第一阶段直接在效果中保存伤害值，
    /// 后续加入 Stats 系统后可改为读取 RoomStatType.Damage。
    /// </summary>
    [CreateAssetMenu(
        fileName = "DamageOnEnterEffect",
        menuName =
            "Dungeon/Room Effects/Damage On Enter"
    )]
    public sealed class DamageOnEnterEffectData :
        RoomEffectData
    {
        [Header("伤害设置")]

        [SerializeField, Min(0f)]
        private float damagePerStack = 10f;

        /// <summary>
        /// 创建该效果独立的运行时实例。
        /// </summary>
        public override IRoomEffectRuntime CreateRuntime()
        {
            return new Runtime(this);
        }

        private sealed class Runtime :
            IRoomEffectRuntime
        {
            private readonly DamageOnEnterEffectData data;

            public Runtime(
                DamageOnEnterEffectData data)
            {
                this.data = data;
            }

            public void OnAdded(
                RoomInstance room,
                RoomEffectHandle handle)
            {
            }

            public void OnRemoved(
                RoomInstance room,
                RoomEffectHandle handle)
            {
            }

            public void OnEvent(
                RoomInstance room,
                RoomEffectHandle handle,
                RoomEvent roomEvent)
            {
                if (roomEvent.Type !=
                    RoomEventType.EnemyEntered)
                {
                    return;
                }

                if (roomEvent.Actor == null)
                {
                    return;
                }

                if (!TryFindDamageable(
                        roomEvent.Actor,
                        out IDamageable damageable))
                {
                    return;
                }

                float damage =
                    data.damagePerStack *
                    handle.StackCount;

                damageable.TakeDamage(
                    damage,
                    room.gameObject
                );
            }

            private static bool TryFindDamageable(
                GameObject actor,
                out IDamageable damageable)
            {
                damageable = null;

                // actor 可能是敌人的碰撞子物体，
                // 因此向父级查找实现 IDamageable 的组件。
                MonoBehaviour[] behaviours =
                    actor.GetComponentsInParent<
                        MonoBehaviour
                    >(true);

                foreach (MonoBehaviour behaviour
                         in behaviours)
                {
                    if (behaviour is IDamageable target)
                    {
                        damageable = target;
                        return true;
                    }
                }

                return false;
            }
        }
    }
}