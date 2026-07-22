using System.Collections.Generic;
using UnityEngine;

namespace Dungeon.RoomSystem
{
    /// <summary>
    /// 统一管理 DungeonGrid 中全部房间的 Chunk 视觉与墙体碰撞。
    ///
    /// DungeonGrid 发生变化后不会立即重建，而是将变化区域对应的 Chunk
    /// 标记为 Dirty，并统一在 LateUpdate 中刷新。
    ///
    /// RoomVisualSystem 同时作为墙体几何参数的唯一配置入口：
    ///
    /// GapWidth
    /// BorderWidth
    /// WallWidth
    ///
    /// 这些参数会同时传递给：
    ///
    /// Shader
    ///     负责视觉表现。
    ///
    /// RoomVisualChunk
    ///     负责生成实际墙体 Collider。
    ///
    /// 从而保证视觉门洞和实际物理门洞使用完全相同的数据。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RoomVisualSystem : MonoBehaviour
    {
        private static readonly int GapWidthId = Shader.PropertyToID("_GapWidth");
        private static readonly int BorderWidthId = Shader.PropertyToID("_BorderWidth");
        private static readonly int WallWidthId = Shader.PropertyToID("_WallWidth");
        private static readonly int DoorSizeId = Shader.PropertyToID("_DoorSize");

        [Header("References")]

        [SerializeField]
        private DungeonGrid dungeonGrid;

        [Tooltip("所有 Chunk 使用的共享材质。")]
        [SerializeField]
        private Material roomMaterial;

        [Tooltip("所有 Chunk 视觉对象的父节点。为空时使用当前对象。")]
        [SerializeField]
        private Transform visualRoot;

        [Header("Wall Geometry")]

        [Tooltip("房间最外侧透明间隙，使用单个 Cell 的 0～1 局部比例。")]
        [SerializeField, Range(0f, 0.2f)]
        private float gapWidth = 0.02f;

        [Tooltip("厚外边缘宽度，使用单个 Cell 的 0～1 局部比例。")]
        [SerializeField, Range(0f, 0.5f)]
        private float borderWidth = 0.08f;

        [Tooltip("内描边实体墙宽度，使用单个 Cell 的 0～1 局部比例。")]
        [SerializeField, Range(0f, 0.4f)]
        private float wallWidth = 0.08f;

        [Header("Chunk")]

        [Tooltip("单个 Chunk 在 X、Y 方向包含的格子数量。")]
        [SerializeField, Min(1)]
        private int chunkSize = 16;

        [Header("Rendering")]

        [SerializeField]
        private string sortingLayerName = "Default";

        [SerializeField]
        private int sortingOrder;

        /// <summary>
        /// 当前已经创建的全部视觉 Chunk。
        /// Key 为 Chunk 的逻辑坐标。
        /// </summary>
        private readonly Dictionary<Vector2Int, RoomVisualChunk> chunks =
            new Dictionary<Vector2Int, RoomVisualChunk>();

        /// <summary>
        /// 当前等待重建的 Chunk。
        /// 使用 HashSet 保证同一个 Chunk 一帧最多重建一次。
        /// </summary>
        private readonly HashSet<Vector2Int> dirtyChunks =
            new HashSet<Vector2Int>();

        private void Awake()
        {
            if (visualRoot == null)
            {
                visualRoot = transform;
            }

            ClampWallGeometry();
            UpdateMaterialSettings();
        }

        private void OnEnable()
        {
            if (dungeonGrid == null)
            {
                return;
            }

            dungeonGrid.GridChanged += HandleGridChanged;
            dungeonGrid.VisualSettingsChanged += HandleVisualSettingsChanged;

            UpdateMaterialSettings();
            MarkAllOccupiedChunksDirty();
        }

        private void OnDisable()
        {
            if (dungeonGrid == null)
            {
                return;
            }

            dungeonGrid.GridChanged -= HandleGridChanged;
            dungeonGrid.VisualSettingsChanged -= HandleVisualSettingsChanged;
        }

        private void OnValidate()
        {
            chunkSize = Mathf.Max(1, chunkSize);

            ClampWallGeometry();
            UpdateMaterialSettings();

            if (Application.isPlaying)
            {
                MarkAllExistingChunksDirty();
            }
        }

        private void LateUpdate()
        {
            RebuildDirtyChunks();
        }

        /// <summary>
        /// 响应 DungeonGrid 的局部占用变化。
        /// 变化格及其八邻域对应的 Chunk 都会被标记为 Dirty。
        /// </summary>
        private void HandleGridChanged(IReadOnlyList<Vector2Int> changedCells)
        {
            MarkDirtyChunksAround(changedCells);
        }

        /// <summary>
        /// 响应 DoorSize 等全局参数变化。
        ///
        /// DoorSize 同时影响 Shader 门洞和实际 Collider 门洞，
        /// 因此必须重新构建所有现存 Chunk。
        /// </summary>
        private void HandleVisualSettingsChanged()
        {
            UpdateMaterialSettings();
            MarkAllExistingChunksDirty();
            MarkAllOccupiedChunksDirty();
        }

        /// <summary>
        /// 保证 CPU Collider 与 Shader 使用完全相同的有效墙体尺寸。
        /// </summary>
        private void ClampWallGeometry()
        {
            RoomWallGeometry geometry = CreateWallGeometry();

            gapWidth = geometry.GapWidth;
            borderWidth = geometry.BorderWidth;
            wallWidth = geometry.WallWidth;
        }

        /// <summary>
        /// 创建当前有效的墙体几何参数。
        /// </summary>
        private RoomWallGeometry CreateWallGeometry()
        {
            return new RoomWallGeometry(gapWidth, borderWidth, wallWidth);
        }

        /// <summary>
        /// 将 CPU 侧墙体参数同步到共享 Shader Material。
        ///
        /// 从现在开始建议在 RoomVisualSystem 上修改：
        /// GapWidth、BorderWidth、WallWidth。
        ///
        /// 不再直接修改 Material 中对应的三个值，
        /// 否则 Shader 与 Collider 会出现尺寸不一致。
        /// </summary>
        private void UpdateMaterialSettings()
        {
            if (roomMaterial == null)
            {
                return;
            }

            RoomWallGeometry geometry = CreateWallGeometry();

            roomMaterial.SetFloat(GapWidthId, geometry.GapWidth);
            roomMaterial.SetFloat(BorderWidthId, geometry.BorderWidth);
            roomMaterial.SetFloat(WallWidthId, geometry.WallWidth);

            float normalizedDoorSize = 0f;

            if (dungeonGrid != null && dungeonGrid.CellSize > Mathf.Epsilon)
            {
                normalizedDoorSize = Mathf.Clamp01(dungeonGrid.DoorSize / dungeonGrid.CellSize);
            }

            roomMaterial.SetFloat(DoorSizeId, normalizedDoorSize);
        }

        /// <summary>
        /// 将变化格及其完整八邻域对应的 Chunk 标记为 Dirty。
        ///
        /// 八邻域用于正确刷新 InnerCornerMask。
        /// </summary>
        private void MarkDirtyChunksAround(IReadOnlyList<Vector2Int> changedCells)
        {
            if (changedCells == null)
            {
                return;
            }

            foreach (Vector2Int cell in changedCells)
            {
                MarkCellChunkDirty(cell);

                MarkCellChunkDirty(cell + Vector2Int.left);
                MarkCellChunkDirty(cell + Vector2Int.right);
                MarkCellChunkDirty(cell + Vector2Int.down);
                MarkCellChunkDirty(cell + Vector2Int.up);

                MarkCellChunkDirty(cell + new Vector2Int(-1, -1));
                MarkCellChunkDirty(cell + new Vector2Int(1, -1));
                MarkCellChunkDirty(cell + new Vector2Int(-1, 1));
                MarkCellChunkDirty(cell + new Vector2Int(1, 1));
            }
        }

        /// <summary>
        /// 将指定 Cell 所属 Chunk 标记为 Dirty。
        /// </summary>
        private void MarkCellChunkDirty(Vector2Int cell)
        {
            dirtyChunks.Add(GetChunkCoord(cell));
        }

        /// <summary>
        /// 标记所有当前存在的 Chunk。
        ///
        /// 删除全部房间或改变全局参数时，
        /// 已经存在但即将变空的 Chunk 也必须进入重建流程。
        /// </summary>
        private void MarkAllExistingChunksDirty()
        {
            foreach (Vector2Int chunkCoord in chunks.Keys)
            {
                dirtyChunks.Add(chunkCoord);
            }
        }

        /// <summary>
        /// 将所有当前房间占用的 Chunk 标记为 Dirty。
        /// </summary>
        private void MarkAllOccupiedChunksDirty()
        {
            if (dungeonGrid == null)
            {
                return;
            }

            foreach (RoomInstance room in dungeonGrid.PlacedRooms)
            {
                if (room == null)
                {
                    continue;
                }

                foreach (Vector2Int cell in room.OccupiedCells)
                {
                    dirtyChunks.Add(GetChunkCoord(cell));
                }
            }
        }

        /// <summary>
        /// 统一重建当前帧产生的全部 Dirty Chunk。
        /// </summary>
        private void RebuildDirtyChunks()
        {
            if (dirtyChunks.Count == 0)
            {
                return;
            }

            Vector2Int[] rebuildList = new Vector2Int[dirtyChunks.Count];

            dirtyChunks.CopyTo(rebuildList);
            dirtyChunks.Clear();

            foreach (Vector2Int chunkCoord in rebuildList)
            {
                RebuildChunk(chunkCoord);
            }
        }

        /// <summary>
        /// 重建一个 Chunk 的 Mesh 和 Collider。
        ///
        /// 如果 Chunk 中已经没有任何房间格，
        /// 则直接销毁整个 Chunk。
        /// </summary>
        private void RebuildChunk(Vector2Int chunkCoord)
        {
            if (!HasAnyOccupiedCell(chunkCoord))
            {
                RemoveChunk(chunkCoord);
                return;
            }

            RoomVisualChunk chunk = GetOrCreateChunk(chunkCoord);
            RoomWallGeometry geometry = CreateWallGeometry();

            chunk.Rebuild(dungeonGrid, chunkCoord, chunkSize, geometry);
        }

        /// <summary>
        /// 获取或创建指定视觉 Chunk。
        /// </summary>
        private RoomVisualChunk GetOrCreateChunk(Vector2Int chunkCoord)
        {
            if (chunks.TryGetValue(chunkCoord, out RoomVisualChunk existingChunk))
            {
                return existingChunk;
            }

            GameObject chunkObject = new GameObject($"RoomChunk_{chunkCoord.x}_{chunkCoord.y}");

            chunkObject.layer = gameObject.layer;
            chunkObject.transform.SetParent(visualRoot, false);

            RoomVisualChunk chunk = chunkObject.AddComponent<RoomVisualChunk>();

            chunk.Initialize(roomMaterial, visualRoot, sortingLayerName, sortingOrder);

            chunks.Add(chunkCoord, chunk);

            return chunk;
        }

        /// <summary>
        /// 删除指定 Chunk。
        ///
        /// RoomVisualChunk 会同时销毁自己管理的 Collision Root。
        /// </summary>
        private void RemoveChunk(Vector2Int chunkCoord)
        {
            if (!chunks.TryGetValue(chunkCoord, out RoomVisualChunk chunk))
            {
                return;
            }

            chunks.Remove(chunkCoord);

            if (chunk != null)
            {
                Destroy(chunk.gameObject);
            }
        }

        /// <summary>
        /// 判断指定 Chunk 是否仍存在任意房间占用格。
        /// </summary>
        private bool HasAnyOccupiedCell(Vector2Int chunkCoord)
        {
            Vector2Int origin = GetChunkOrigin(chunkCoord);

            for (int y = 0; y < chunkSize; y++)
            {
                for (int x = 0; x < chunkSize; x++)
                {
                    Vector2Int cell = origin + new Vector2Int(x, y);

                    if (dungeonGrid.IsCellOccupied(cell))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// 将 Cell 坐标转换为 Chunk 坐标。
        /// FloorToInt 用于正确支持负坐标。
        /// </summary>
        private Vector2Int GetChunkCoord(Vector2Int cell)
        {
            return new Vector2Int(
                Mathf.FloorToInt((float)cell.x / chunkSize),
                Mathf.FloorToInt((float)cell.y / chunkSize)
            );
        }

        /// <summary>
        /// 获取指定 Chunk 左下角的逻辑 Cell。
        /// </summary>
        private Vector2Int GetChunkOrigin(Vector2Int chunkCoord)
        {
            return new Vector2Int(chunkCoord.x * chunkSize, chunkCoord.y * chunkSize);
        }

        /// <summary>
        /// 强制重新生成全部房间视觉和碰撞。
        /// </summary>
        public void RebuildAllVisuals()
        {
            ClearAllVisuals();
            UpdateMaterialSettings();
            MarkAllOccupiedChunksDirty();
        }

        /// <summary>
        /// 删除全部 Chunk。
        ///
        /// 不影响 DungeonGrid 与 RoomInstance 的逻辑数据。
        /// </summary>
        public void ClearAllVisuals()
        {
            foreach (RoomVisualChunk chunk in chunks.Values)
            {
                if (chunk != null)
                {
                    Destroy(chunk.gameObject);
                }
            }

            chunks.Clear();
            dirtyChunks.Clear();
        }
    }
}