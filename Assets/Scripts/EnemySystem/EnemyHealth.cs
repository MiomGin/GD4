using System;
using Game.Common.Combat;
using UnityEngine;

namespace Dungeon.EnemySystem
{
    /// <summary>
    /// 管理敌人的生命值、受伤和死亡。
    /// 通过实现 IDamageable，使敌人能够接收来自房间、
    /// 玩家或其他系统的统一伤害。
    /// </summary>
    public sealed class EnemyHealth : MonoBehaviour, IDamageable
    {
        [Header("生命值")]

        [SerializeField, Min(1f)]
        private float maxHealth = 100f;

        private float currentHealth;
        private bool isDead;

        /// <summary>
        /// 当前生命值。
        /// </summary>
        public float CurrentHealth => currentHealth;

        /// <summary>
        /// 最大生命值。
        /// </summary>
        public float MaxHealth => maxHealth;

        /// <summary>
        /// 当前敌人是否已经死亡。
        /// </summary>
        public bool IsDead => isDead;

        /// <summary>
        /// 敌人死亡时触发。
        /// 参数分别为死亡的生命组件和伤害来源。
        /// </summary>
        public event Action<EnemyHealth, GameObject> Died;

        private void Awake()
        {
            currentHealth = maxHealth;
        }

        /// <summary>
        /// 对敌人造成伤害。
        /// </summary>
        /// <param name="amount">伤害数值。</param>
        /// <param name="source">造成伤害的来源对象。</param>
        public void TakeDamage(
            float amount,
            GameObject source)
        {
            if (isDead || amount <= 0f)
            {
                return;
            }

            currentHealth = Mathf.Max(
                0f,
                currentHealth - amount
            );

            if (currentHealth <= 0f)
            {
                Die(source);
            }
        }

        /// <summary>
        /// 将敌人生命值恢复到最大值。
        /// 可用于对象池重新启用敌人时重置状态。
        /// </summary>
        public void ResetHealth()
        {
            currentHealth = maxHealth;
            isDead = false;
        }

        private void Die(GameObject source)
        {
            if (isDead)
            {
                return;
            }

            isDead = true;
            Died?.Invoke(this, source);

            // 第一阶段直接销毁。
            // 后续接入对象池时替换为回收逻辑。
            Destroy(gameObject);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            maxHealth = Mathf.Max(1f, maxHealth);
        }
#endif
    }
}