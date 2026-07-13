using System.Collections.Generic;
using UnityEngine;

namespace Dungeon.RoomSystem
{
    /// <summary>
    /// 负责显示当前待建造房间的格子虚影。
    /// 不负责判断放置是否合法。
    /// </summary>
    public sealed class RoomPlacementPreview : MonoBehaviour
    {
        [Header("虚影 Prefab")]

        [Tooltip("只需要包含一个 SpriteRenderer。")]
        [SerializeField]
        private SpriteRenderer ghostCellPrefab;

        [Header("虚影颜色")]

        [SerializeField]
        private Color validColor =
            new Color(0.25f, 1f, 0.45f, 0.45f);

        [SerializeField]
        private Color invalidColor =
            new Color(1f, 0.25f, 0.25f, 0.45f);

        [SerializeField]
        private int sortingOrder = 100;

        private readonly List<SpriteRenderer> ghostCells =
            new List<SpriteRenderer>();

        /// <summary>
        /// 在指定格子位置显示房间虚影。
        /// </summary>
        public void Show(
            DungeonGrid grid,
            IReadOnlyList<Vector2Int> cells,
            bool isValid)
        {
            if (grid == null ||
                cells == null ||
                ghostCellPrefab == null)
            {
                Hide();
                return;
            }

            gameObject.SetActive(true);

            EnsureGhostCount(cells.Count);

            Color targetColor =
                isValid
                    ? validColor
                    : invalidColor;

            for (int i = 0;
                 i < ghostCells.Count;
                 i++)
            {
                bool shouldShow =
                    i < cells.Count;

                ghostCells[i].gameObject.SetActive(
                    shouldShow
                );

                if (!shouldShow)
                {
                    continue;
                }

                SpriteRenderer renderer =
                    ghostCells[i];

                renderer.transform.position =
                    grid.CellToWorld(cells[i]);

                // 默认方块 Sprite 的世界尺寸为 1×1。
                renderer.transform.localScale =
                    new Vector3(
                        grid.CellSize,
                        grid.CellSize,
                        1f
                    );

                renderer.color = targetColor;
                renderer.sortingOrder = sortingOrder;
            }
        }

        /// <summary>
        /// 隐藏当前全部房间虚影。
        /// </summary>
        public void Hide()
        {
            foreach (SpriteRenderer ghostCell
                     in ghostCells)
            {
                ghostCell.gameObject.SetActive(false);
            }
        }

        private void EnsureGhostCount(
            int requiredCount)
        {
            while (ghostCells.Count < requiredCount)
            {
                SpriteRenderer renderer =
                    Instantiate(
                        ghostCellPrefab,
                        transform
                    );

                renderer.name =
                    $"GhostCell_{ghostCells.Count}";

                renderer.sortingOrder =
                    sortingOrder;

                ghostCells.Add(renderer);
            }
        }
    }
}