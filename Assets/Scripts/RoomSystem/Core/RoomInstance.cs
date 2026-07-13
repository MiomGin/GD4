using System;
using System.Collections.Generic;
using UnityEngine;

namespace Dungeon.RoomSystem
{
    /// <summary>
    /// 表示场景中一个已经放置完成的房间运行时实例。
    /// 负责保存房间状态、管理标签和效果、分发房间事件，
    /// 以及协调房间逻辑生命周期。
    /// </summary>
    public sealed class RoomInstance : MonoBehaviour, IRoom
    {
        /// <summary>
        /// 当前房间实际占用的世界网格坐标。
        /// </summary>
        private readonly List<Vector2Int> occupiedCells =
            new List<Vector2Int>();

        /// <summary>
        /// 当前房间持有的全部运行时效果。
        /// </summary>
        private readonly List<RoomEffectHandle> effects =
            new List<RoomEffectHandle>();

        private bool hasNotifiedPlaced;
        private bool hasPreparedForRemoval;

        /// <summary>
        /// 当前房间对应的静态配置。
        /// </summary>
        public RoomData Data { get; private set; }

        /// <summary>
        /// 当前房间的摆放锚点格子。
        /// </summary>
        public Vector2Int AnchorCell { get; private set; }

        /// <summary>
        /// 当前房间的顺时针旋转次数。
        /// 取值范围为 0～3。
        /// </summary>
        public int Rotation { get; private set; }

        /// <summary>
        /// 当前房间所属的地牢网格。
        /// </summary>
        public DungeonGrid Grid { get; private set; }

        /// <summary>
        /// 当前房间拥有的标签集合。
        /// </summary>
        public RoomTagSet Tags { get; } =
            new RoomTagSet();

        /// <summary>
        /// 当前房间占用的全部世界网格坐标。
        /// </summary>
        public IReadOnlyList<Vector2Int> OccupiedCells =>
            occupiedCells;

        /// <summary>
        /// 当前房间持有的全部效果实例。
        /// </summary>
        public IReadOnlyList<RoomEffectHandle> Effects =>
            effects;

        /// <summary>
        /// 初始化一个已经确定摆放位置的房间运行时实例。
        /// 此阶段只建立房间数据和基础标签，不处理任何视觉内容。
        /// </summary>
        /// <param name="roomData">房间静态配置。</param>
        /// <param name="anchorCell">房间摆放锚点。</param>
        /// <param name="rotation">顺时针旋转次数。</param>
        /// <param name="worldCells">房间实际占用的世界格子。</param>
        /// <param name="grid">所属地牢网格。</param>
        public void Initialize(
            RoomData roomData,
            Vector2Int anchorCell,
            int rotation,
            IReadOnlyList<Vector2Int> worldCells,
            DungeonGrid grid)
        {
            if (roomData == null)
            {
                throw new ArgumentNullException(
                    nameof(roomData)
                );
            }

            if (grid == null)
            {
                throw new ArgumentNullException(
                    nameof(grid)
                );
            }

            Data = roomData;
            AnchorCell = anchorCell;
            Rotation = NormalizeRotation(rotation);
            Grid = grid;

            hasNotifiedPlaced = false;
            hasPreparedForRemoval = false;

            name =
                $"{roomData.DisplayName}_{anchorCell.x}_{anchorCell.y}";

            occupiedCells.Clear();

            if (worldCells != null)
            {
                foreach (Vector2Int cell in worldCells)
                {
                    occupiedCells.Add(cell);
                }
            }

            AddBaseTags();
        }

        /// <summary>
        /// 通知房间已经完成网格注册和全部初始化。
        /// 初始效果会在这里添加，随后发送 RoomPlaced 事件。
        /// 重复调用不会重复添加初始效果。
        /// </summary>
        public void NotifyPlaced()
        {
            if (hasNotifiedPlaced)
            {
                return;
            }

            hasNotifiedPlaced = true;

            AddInitialEffects();
            Publish(RoomEvent.RoomPlaced(this));
        }

        /// <summary>
        /// 通知房间有敌人进入。
        /// 当前房间中的全部效果都会收到 EnemyEntered 事件。
        /// </summary>
        /// <param name="enemy">进入房间的敌人对象。</param>
        public void NotifyEnemyEntered(GameObject enemy)
        {
            if (enemy == null)
            {
                return;
            }

            Publish(RoomEvent.EnemyEntered(enemy));
        }

        /// <summary>
        /// 通知房间有敌人离开。
        /// 当前房间中的全部效果都会收到 EnemyExited 事件。
        /// </summary>
        /// <param name="enemy">离开房间的敌人对象。</param>
        public void NotifyEnemyExited(GameObject enemy)
        {
            if (enemy == null)
            {
                return;
            }

            Publish(RoomEvent.EnemyExited(enemy));
        }

        /// <summary>
        /// 尝试向房间添加一个效果。
        /// 添加前会检查效果条件和叠加规则。
        /// </summary>
        /// <param name="effectData">需要添加的效果配置。</param>
        /// <param name="source">效果来源。</param>
        /// <param name="handle">成功添加或叠加后的效果句柄。</param>
        /// <returns>效果成功添加、叠加或刷新时返回 true。</returns>
        public bool TryAddEffect(
            RoomEffectData effectData,
            object source,
            out RoomEffectHandle handle)
        {
            handle = null;

            if (effectData == null)
            {
                return false;
            }

            if (!effectData.CanApply(this))
            {
                return false;
            }

            RoomEffectHandle existingHandle =
                FindExistingEffect(effectData);

            if (existingHandle != null)
            {
                switch (effectData.StackPolicy)
                {
                    case RoomEffectStackPolicy.Unique:
                        return false;

                    case RoomEffectStackPolicy.Stack:
                        if (existingHandle.StackCount >=
                            effectData.MaxStack)
                        {
                            return false;
                        }

                        existingHandle.StackCount++;
                        handle = existingHandle;

                        Publish(
                            RoomEvent.EffectStacked(
                                existingHandle
                            )
                        );

                        return true;

                    case RoomEffectStackPolicy.Refresh:
                        handle = existingHandle;

                        Publish(
                            RoomEvent.EffectRefreshed(
                                existingHandle
                            )
                        );

                        return true;

                    case RoomEffectStackPolicy.Replace:
                        RemoveEffect(existingHandle);
                        break;
                }
            }

            object actualSource =
                source ?? effectData;

            IRoomEffectRuntime runtime =
                effectData.CreateRuntime();

            handle = new RoomEffectHandle(
                effectData,
                runtime,
                actualSource
            );

            effects.Add(handle);

            if (effectData.GrantedTags != null)
            {
                foreach (RoomTag tag
                         in effectData.GrantedTags)
                {
                    Tags.Add(tag, handle);
                }
            }

            runtime?.OnAdded(this, handle);

            Publish(RoomEvent.EffectAdded(handle));

            return true;
        }

        /// <summary>
        /// 从房间中移除指定效果。
        /// 同时撤销该效果句柄提供的全部标签。
        /// </summary>
        /// <param name="handle">需要移除的效果句柄。</param>
        /// <returns>成功移除时返回 true。</returns>
        public bool RemoveEffect(RoomEffectHandle handle)
        {
            if (handle == null ||
                !effects.Remove(handle))
            {
                return false;
            }

            handle.Runtime?.OnRemoved(this, handle);

            if (handle.Data.GrantedTags != null)
            {
                foreach (RoomTag tag
                         in handle.Data.GrantedTags)
                {
                    Tags.Remove(tag, handle);
                }
            }

            Publish(RoomEvent.EffectRemoved(handle));

            return true;
        }

        /// <summary>
        /// 将一个房间事件发送给当前房间的全部效果。
        /// 使用快照遍历，允许效果在事件响应中添加或移除效果。
        /// </summary>
        /// <param name="roomEvent">需要分发的房间事件。</param>
        public void Publish(RoomEvent roomEvent)
        {
            RoomEffectHandle[] snapshot =
                effects.ToArray();

            foreach (RoomEffectHandle handle in snapshot)
            {
                if (!effects.Contains(handle))
                {
                    continue;
                }

                handle.Runtime?.OnEvent(
                    this,
                    handle,
                    roomEvent
                );
            }
        }

        /// <summary>
        /// 在房间从网格中删除前清理运行时状态。
        /// 会先发送 RoomRemoved，再依次移除全部效果。
        /// </summary>
        public void PrepareForRemoval()
        {
            if (hasPreparedForRemoval)
            {
                return;
            }

            hasPreparedForRemoval = true;

            Publish(RoomEvent.RoomRemoved(this));

            for (int i = effects.Count - 1;
                 i >= 0;
                 i--)
            {
                RemoveEffect(effects[i]);
            }
        }

        /// <summary>
        /// 将 RoomData 中的基础标签添加到房间。
        /// 基础标签的来源为 RoomData 本身。
        /// </summary>
        private void AddBaseTags()
        {
            if (Data.BaseTags == null)
            {
                return;
            }

            foreach (RoomTag tag in Data.BaseTags)
            {
                Tags.Add(tag, Data);
            }
        }

        /// <summary>
        /// 将 RoomData 中配置的初始效果添加到房间。
        /// </summary>
        private void AddInitialEffects()
        {
            if (Data.InitialEffects == null)
            {
                return;
            }

            foreach (RoomEffectData effectData
                     in Data.InitialEffects)
            {
                if (effectData == null)
                {
                    continue;
                }

                TryAddEffect(
                    effectData,
                    Data,
                    out _
                );
            }
        }

        /// <summary>
        /// 查找与指定配置相同的现有效果。
        /// 优先比较配置引用，其次比较 EffectId。
        /// </summary>
        private RoomEffectHandle FindExistingEffect(
            RoomEffectData effectData)
        {
            foreach (RoomEffectHandle handle in effects)
            {
                if (ReferenceEquals(
                        handle.Data,
                        effectData))
                {
                    return handle;
                }

                if (!string.IsNullOrWhiteSpace(
                        effectData.EffectId) &&
                    string.Equals(
                        handle.Data.EffectId,
                        effectData.EffectId,
                        StringComparison.Ordinal))
                {
                    return handle;
                }
            }

            return null;
        }

        /// <summary>
        /// 将任意旋转次数规范到 0～3。
        /// </summary>
        private static int NormalizeRotation(int rotation)
        {
            return ((rotation % 4) + 4) % 4;
        }
    }
}