using System;
using System.Collections.Generic;
using UnityEngine;

namespace Dungeon.RoomSystem
{
    /// <summary>
    /// 房间的静态配置数据。
    /// 保存房间形状、基础标签、初始效果和基础表现，
    /// 不保存某个具体房间的运行时状态。
    /// </summary>
    [CreateAssetMenu(
        fileName = "RoomData",
        menuName = "Dungeon/Room/Room Data"
    )]
    public sealed class RoomData : ScriptableObject
    {
        [Header("基础信息")]

        [SerializeField]
        private string roomId;

        [SerializeField]
        private string displayName;

        [Header("房间形状")]

        [Tooltip("房间相对于锚点格子的局部坐标。")]
        [SerializeField]
        private Vector2Int[] occupiedCells =
        {
            Vector2Int.zero
        };

        [Tooltip("是否必须与已有房间四方向相邻。")]
        [SerializeField]
        private bool mustConnectToExistingRoom = true;

        [Header("房间表现")]

        [SerializeField]
        private Color roomColor = Color.white;

        [Header("基础标签")]

        [SerializeField]
        private RoomTag[] baseTags =
        {
            RoomTag.Room
        };

        [Header("初始效果")]

        [SerializeField]
        private RoomEffectData[] initialEffects =
            Array.Empty<RoomEffectData>();

        /// <summary>
        /// 房间的唯一配置标识。
        /// 后续存档时应保存该标识，而不是直接保存 ScriptableObject 引用。
        /// </summary>
        public string RoomId => roomId;

        /// <summary>
        /// 房间显示名称。
        /// </summary>
        public string DisplayName =>
            string.IsNullOrWhiteSpace(displayName)
                ? name
                : displayName;

        /// <summary>
        /// 房间是否必须连接已有房间。
        /// </summary>
        public bool MustConnectToExistingRoom =>
            mustConnectToExistingRoom;

        /// <summary>
        /// 房间基础显示颜色。
        /// </summary>
        public Color RoomColor => roomColor;

        /// <summary>
        /// 房间创建时自动添加的标签。
        /// </summary>
        public IReadOnlyList<RoomTag> BaseTags =>
            baseTags;

        /// <summary>
        /// 房间创建时自动添加的效果。
        /// </summary>
        public IReadOnlyList<RoomEffectData> InitialEffects =>
            initialEffects;

        /// <summary>
        /// 获取房间经过指定次数顺时针旋转后的局部格子坐标。
        /// </summary>
        /// <param name="rotation">
        /// 旋转次数：
        /// 0 为 0°，1 为 90°，2 为 180°，3 为 270°。
        /// </param>
        public List<Vector2Int> GetRotatedCells(int rotation)
        {
            int normalizedRotation =
                NormalizeRotation(rotation);

            List<Vector2Int> result =
                new List<Vector2Int>();

            HashSet<Vector2Int> uniqueCells =
                new HashSet<Vector2Int>();

            Vector2Int[] sourceCells =
                occupiedCells == null ||
                occupiedCells.Length == 0
                    ? new[] { Vector2Int.zero }
                    : occupiedCells;

            foreach (Vector2Int cell in sourceCells)
            {
                Vector2Int rotatedCell =
                    RotateCell(cell, normalizedRotation);

                // 防止配置中重复填写相同坐标。
                if (uniqueCells.Add(rotatedCell))
                {
                    result.Add(rotatedCell);
                }
            }

            return result;
        }

        private static Vector2Int RotateCell(
            Vector2Int cell,
            int rotation)
        {
            switch (rotation)
            {
                case 1:
                    // 顺时针旋转 90°。
                    return new Vector2Int(
                        cell.y,
                        -cell.x
                    );

                case 2:
                    return new Vector2Int(
                        -cell.x,
                        -cell.y
                    );

                case 3:
                    // 顺时针旋转 270°。
                    return new Vector2Int(
                        -cell.y,
                        cell.x
                    );

                default:
                    return cell;
            }
        }

        private static int NormalizeRotation(int rotation)
        {
            return ((rotation % 4) + 4) % 4;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(roomId))
            {
                roomId = name;
            }

            if (occupiedCells == null ||
                occupiedCells.Length == 0)
            {
                occupiedCells =
                    new[] { Vector2Int.zero };
            }

            if (baseTags == null)
            {
                baseTags = Array.Empty<RoomTag>();
            }

            if (initialEffects == null)
            {
                initialEffects =
                    Array.Empty<RoomEffectData>();
            }
        }
#endif
    }
}