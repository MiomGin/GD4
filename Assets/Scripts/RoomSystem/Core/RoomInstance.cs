using System;
using System.Collections.Generic;
using UnityEngine;

namespace Dungeon.RoomSystem
{
    /// <summary>
    /// 场景中一个已经放置完成的房间实例。
    /// 管理房间占用格、当前标签、当前效果和房间事件。
    /// </summary>
    public sealed class RoomInstance : MonoBehaviour, IRoom
    {
        private readonly List<Vector2Int> occupiedCells =
            new List<Vector2Int>();

        private readonly List<RoomEffectHandle> effects =
            new List<RoomEffectHandle>();

        private DungeonGrid dungeonGrid;

        /// <summary>
        /// 房间对应的静态配置。
        /// </summary>
        public RoomData Data { get; private set; }

        /// <summary>
        /// 房间摆放时使用的锚点格子。
        /// </summary>
        public Vector2Int AnchorCell { get; private set; }

        /// <summary>
        /// 房间当前旋转次数。
        /// </summary>
        public int Rotation { get; private set; }

        /// <summary>
        /// 房间当前拥有的标签集合。
        /// </summary>
        public RoomTagSet Tags { get; } =
            new RoomTagSet();

        /// <summary>
        /// 房间当前占用的全部世界网格坐标。
        /// </summary>
        public IReadOnlyList<Vector2Int> OccupiedCells =>
            occupiedCells;

        /// <summary>
        /// 房间当前持有的全部效果。
        /// </summary>
        public IReadOnlyList<RoomEffectHandle> Effects =>
            effects;

        /// <summary>
        /// 初始化一个已经完成网格注册的房间实例。
        /// </summary>
        public void Initialize(
            RoomData roomData,
            Vector2Int anchorCell,
            int rotation,
            IReadOnlyList<Vector2Int> worldCells,
            DungeonGrid grid,
            SpriteRenderer placedCellPrefab)
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
            dungeonGrid = grid;

            name =
                $"{roomData.DisplayName}_{anchorCell.x}_{anchorCell.y}";

            occupiedCells.Clear();

            foreach (Vector2Int cell in worldCells)
            {
                occupiedCells.Add(cell);
            }

            BuildVisuals(placedCellPrefab);
            AddBaseTags();
            AddInitialEffects();
        }

        /// <summary>
        /// 在房间完成创建和网格注册后发送 RoomPlaced 事件。
        /// </summary>
        public void NotifyPlaced()
        {
            Publish(RoomEvent.RoomPlaced(this));
        }

        /// <summary>
        /// 通知房间有敌人进入。
        /// 后续敌人移动系统确认进入新房间时调用此方法。
        /// </summary>
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
        /// </summary>
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
        /// </summary>
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

            foreach (RoomTag tag
                     in effectData.GrantedTags)
            {
                Tags.Add(tag, handle);
            }

            runtime?.OnAdded(this, handle);

            Publish(RoomEvent.EffectAdded(handle));

            return true;
        }

        /// <summary>
        /// 从房间中移除指定效果。
        /// 同时移除该效果提供的所有标签。
        /// </summary>
        public bool RemoveEffect(RoomEffectHandle handle)
        {
            if (handle == null ||
                !effects.Remove(handle))
            {
                return false;
            }

            handle.Runtime?.OnRemoved(this, handle);

            foreach (RoomTag tag
                     in handle.Data.GrantedTags)
            {
                Tags.Remove(tag, handle);
            }

            Publish(RoomEvent.EffectRemoved(handle));

            return true;
        }

        /// <summary>
        /// 将一个房间事件发送给当前所有效果。
        /// </summary>
        public void Publish(RoomEvent roomEvent)
        {
            // 使用副本遍历，防止效果响应事件时增删效果，
            // 从而导致原列表遍历失效。
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
        /// 在房间被删除前清理全部运行时效果。
        /// 由 DungeonGrid.RemoveRoom 调用。
        /// </summary>
        public void PrepareForRemoval()
        {
            Publish(RoomEvent.RoomRemoved(this));

            for (int i = effects.Count - 1; i >= 0; i--)
            {
                RemoveEffect(effects[i]);
            }
        }

        private void AddBaseTags()
        {
            foreach (RoomTag tag in Data.BaseTags)
            {
                Tags.Add(tag, Data);
            }
        }

        private void AddInitialEffects()
        {
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

        private void BuildVisuals(
            SpriteRenderer placedCellPrefab)
        {
            if (placedCellPrefab == null)
            {
                return;
            }

            foreach (Vector2Int cell in occupiedCells)
            {
                SpriteRenderer cellRenderer =
                    Instantiate(
                        placedCellPrefab,
                        transform
                    );

                cellRenderer.name =
                    $"Cell_{cell.x}_{cell.y}";

                cellRenderer.transform.position =
                    dungeonGrid.CellToWorld(cell);

                // 默认方块 Sprite 的世界尺寸为 1×1。
                cellRenderer.transform.localScale =
                    new Vector3(
                        dungeonGrid.CellSize,
                        dungeonGrid.CellSize,
                        1f
                    );

                cellRenderer.color = Data.RoomColor;
            }
        }

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

        private static int NormalizeRotation(int rotation)
        {
            return ((rotation % 4) + 4) % 4;
        }
    }
}