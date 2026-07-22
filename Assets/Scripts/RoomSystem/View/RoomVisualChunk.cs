using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Dungeon.RoomSystem
{
    /// <summary>
    /// 表示一个固定空间区域内的房间视觉 Mesh 与墙体碰撞。
    ///
    /// 一个 RoomVisualChunk 可以同时包含多个 RoomInstance。
    ///
    /// 每个房间 Cell 会：
    ///
    /// 1. 生成一个视觉 Quad。
    /// 2. 根据 BorderMask 生成真实墙体 Collider。
    /// 3. 根据 DoorMask 在墙体中央切出真实门洞。
    /// 4. 根据 InnerCornerMask 补充凹角墙体碰撞。
    ///
    /// Collider 覆盖：
    ///
    /// OuterBorder + Wall
    ///
    /// 不覆盖：
    ///
    /// Transparent Gap
    /// Room Shadow
    /// Room Fill
    ///
    /// 所有 BoxCollider2D 都挂在一个 Chunk Collision Root 上并重复复用，
    /// Chunk 重建时不会反复创建新的 GameObject。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public sealed class RoomVisualChunk : MonoBehaviour
    {
        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<int> triangles = new List<int>();
        private readonly List<Vector2> uv0 = new List<Vector2>();
        private readonly List<Vector4> uv1 = new List<Vector4>();
        private readonly List<Vector4> uv2 = new List<Vector4>();
        private readonly List<Vector4> uv3 = new List<Vector4>();

        /// <summary>
        /// 当前 Chunk 已经创建的全部 BoxCollider2D。
        ///
        /// 重建时优先复用已有组件，
        /// 多余 Collider 只会 Disable，不会反复 Destroy / AddComponent。
        /// </summary>
        private readonly List<BoxCollider2D> wallColliders = new List<BoxCollider2D>();

        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private Mesh mesh;
        private Transform visualRoot;
        private Transform collisionRoot;

        private int usedColliderCount;

        /// <summary>
        /// 初始化 Chunk 的视觉 Mesh。
        /// </summary>
        public void Initialize(Material material, Transform root, string sortingLayerName, int sortingOrder)
        {
            CacheComponents();

            visualRoot = root != null ? root : transform.parent;

            mesh = new Mesh
            {
                name = $"{name}_Mesh",
                indexFormat = IndexFormat.UInt32
            };

            mesh.MarkDynamic();

            meshFilter.sharedMesh = mesh;
            meshRenderer.sharedMaterial = material;
            meshRenderer.sortingLayerName = sortingLayerName;
            meshRenderer.sortingOrder = sortingOrder;
        }

        /// <summary>
        /// 根据 DungeonGrid 当前状态完整重建指定 Chunk 的视觉和碰撞。
        /// </summary>
        public void Rebuild(DungeonGrid grid, Vector2Int chunkCoord, int chunkSize, RoomWallGeometry wallGeometry)
        {
            if (grid == null || mesh == null || chunkSize <= 0)
            {
                return;
            }

            ClearBuildBuffers();
            BeginCollisionBuild(grid);

            Vector2Int chunkOrigin = new Vector2Int(chunkCoord.x * chunkSize, chunkCoord.y * chunkSize);

            for (int y = 0; y < chunkSize; y++)
            {
                for (int x = 0; x < chunkSize; x++)
                {
                    Vector2Int cell = chunkOrigin + new Vector2Int(x, y);

                    if (!grid.TryGetRoom(cell, out RoomInstance room) || room == null)
                    {
                        continue;
                    }

                    RoomCellVisualData visualData = RoomCellVisualData.Create(cell, room, grid);

                    AddCellQuad(grid, visualData);
                    AddCellCollision(grid, visualData, wallGeometry);
                }
            }

            UploadMesh();
            EndCollisionBuild();
        }

        /// <summary>
        /// 为单个 Cell 添加视觉 Quad。
        /// </summary>
        private void AddCellQuad(DungeonGrid grid, RoomCellVisualData data)
        {
            float cellSize = grid.CellSize;

            Vector3 bottomLeftWorld = grid.transform.TransformPoint(
                new Vector3(data.Cell.x * cellSize, data.Cell.y * cellSize, 0f)
            );

            Vector3 bottomRightWorld = grid.transform.TransformPoint(
                new Vector3((data.Cell.x + 1) * cellSize, data.Cell.y * cellSize, 0f)
            );

            Vector3 topRightWorld = grid.transform.TransformPoint(
                new Vector3((data.Cell.x + 1) * cellSize, (data.Cell.y + 1) * cellSize, 0f)
            );

            Vector3 topLeftWorld = grid.transform.TransformPoint(
                new Vector3(data.Cell.x * cellSize, (data.Cell.y + 1) * cellSize, 0f)
            );

            Vector3 bottomLeft = visualRoot.InverseTransformPoint(bottomLeftWorld);
            Vector3 bottomRight = visualRoot.InverseTransformPoint(bottomRightWorld);
            Vector3 topRight = visualRoot.InverseTransformPoint(topRightWorld);
            Vector3 topLeft = visualRoot.InverseTransformPoint(topLeftWorld);

            int vertexStart = vertices.Count;

            vertices.Add(bottomLeft);
            vertices.Add(bottomRight);
            vertices.Add(topRight);
            vertices.Add(topLeft);

            triangles.Add(vertexStart + 0);
            triangles.Add(vertexStart + 2);
            triangles.Add(vertexStart + 1);

            triangles.Add(vertexStart + 0);
            triangles.Add(vertexStart + 3);
            triangles.Add(vertexStart + 2);

            uv0.Add(new Vector2(0f, 0f));
            uv0.Add(new Vector2(1f, 0f));
            uv0.Add(new Vector2(1f, 1f));
            uv0.Add(new Vector2(0f, 1f));

            Vector4 topology = new Vector4(
                (float)data.BorderMask,
                (float)data.InnerCornerMask,
                (float)data.DoorMask,
                (float)data.OuterCornerMask
            );

            Vector4 fillColor = data.FillColor;
            Vector4 borderColor = data.BorderColor;

            for (int i = 0; i < 4; i++)
            {
                uv1.Add(topology);
                uv2.Add(fillColor);
                uv3.Add(borderColor);
            }
        }

        /// <summary>
        /// 根据当前 Cell 的墙体拓扑生成真实碰撞。
        ///
        /// 直墙使用 BorderMask。
        /// 门洞使用 DoorMask。
        /// 凹角连接使用 InnerCornerMask。
        /// </summary>
        private void AddCellCollision(DungeonGrid grid, RoomCellVisualData data, RoomWallGeometry geometry)
        {
            float cellSize = grid.CellSize;

            float cellLeft = data.Cell.x * cellSize;
            float cellBottom = data.Cell.y * cellSize;
            float cellRight = cellLeft + cellSize;
            float cellTop = cellBottom + cellSize;

            float collisionStart = geometry.GapWidth * cellSize;
            float collisionThickness = geometry.CollisionWidth * cellSize;

            if (collisionThickness <= Mathf.Epsilon)
            {
                return;
            }

            float centerOffset = collisionStart + collisionThickness * 0.5f;
            float doorSize = Mathf.Clamp(grid.DoorSize, 0f, cellSize);

            if (HasBorder(data.BorderMask, RoomBorderMask.Left))
            {
                bool hasDoor = HasDoor(data.DoorMask, RoomDoorMask.Left);

                AddVerticalWall(
                    cellLeft + centerOffset,
                    cellBottom,
                    cellSize,
                    collisionThickness,
                    hasDoor,
                    doorSize
                );
            }

            if (HasBorder(data.BorderMask, RoomBorderMask.Right))
            {
                bool hasDoor = HasDoor(data.DoorMask, RoomDoorMask.Right);

                AddVerticalWall(
                    cellRight - centerOffset,
                    cellBottom,
                    cellSize,
                    collisionThickness,
                    hasDoor,
                    doorSize
                );
            }

            if (HasBorder(data.BorderMask, RoomBorderMask.Bottom))
            {
                bool hasDoor = HasDoor(data.DoorMask, RoomDoorMask.Bottom);

                AddHorizontalWall(
                    cellLeft,
                    cellBottom + centerOffset,
                    cellSize,
                    collisionThickness,
                    hasDoor,
                    doorSize
                );
            }

            if (HasBorder(data.BorderMask, RoomBorderMask.Top))
            {
                bool hasDoor = HasDoor(data.DoorMask, RoomDoorMask.Top);

                AddHorizontalWall(
                    cellLeft,
                    cellTop - centerOffset,
                    cellSize,
                    collisionThickness,
                    hasDoor,
                    doorSize
                );
            }

            AddInnerCornerCollision(data, geometry, cellSize);
        }

        /// <summary>
        /// 添加一条纵向实体墙。
        ///
        /// 无门时生成完整 BoxCollider2D。
        /// 有门时拆成上下两段。
        /// </summary>
        private void AddVerticalWall(
            float centerX,
            float bottom,
            float totalLength,
            float thickness,
            bool hasDoor,
            float doorSize)
        {
            if (!hasDoor || doorSize <= Mathf.Epsilon)
            {
                AddBoxCollider(
                    new Vector2(centerX, bottom + totalLength * 0.5f),
                    new Vector2(thickness, totalLength)
                );

                return;
            }

            float segmentLength = Mathf.Max(0f, (totalLength - doorSize) * 0.5f);

            if (segmentLength <= Mathf.Epsilon)
            {
                return;
            }

            AddBoxCollider(
                new Vector2(centerX, bottom + segmentLength * 0.5f),
                new Vector2(thickness, segmentLength)
            );

            AddBoxCollider(
                new Vector2(centerX, bottom + totalLength - segmentLength * 0.5f),
                new Vector2(thickness, segmentLength)
            );
        }

        /// <summary>
        /// 添加一条横向实体墙。
        ///
        /// 无门时生成完整 BoxCollider2D。
        /// 有门时拆成左右两段。
        /// </summary>
        private void AddHorizontalWall(
            float left,
            float centerY,
            float totalLength,
            float thickness,
            bool hasDoor,
            float doorSize)
        {
            if (!hasDoor || doorSize <= Mathf.Epsilon)
            {
                AddBoxCollider(
                    new Vector2(left + totalLength * 0.5f, centerY),
                    new Vector2(totalLength, thickness)
                );

                return;
            }

            float segmentLength = Mathf.Max(0f, (totalLength - doorSize) * 0.5f);

            if (segmentLength <= Mathf.Epsilon)
            {
                return;
            }

            AddBoxCollider(
                new Vector2(left + segmentLength * 0.5f, centerY),
                new Vector2(segmentLength, thickness)
            );

            AddBoxCollider(
                new Vector2(left + totalLength - segmentLength * 0.5f, centerY),
                new Vector2(segmentLength, thickness)
            );
        }

        /// <summary>
        /// 为 Shader 中的 InnerCorner 方形连接区域生成对应实体碰撞。
        ///
        /// Border + Wall 的组合区域等价于：
        ///
        /// WallEnd 大方块
        /// -
        /// GapWidth 小方块
        ///
        /// 因此使用两个 BoxCollider2D 精确拼出该 L 型区域。
        /// </summary>
        private void AddInnerCornerCollision(RoomCellVisualData data, RoomWallGeometry geometry, float cellSize)
        {
            float outer = geometry.WallEnd * cellSize;
            float gap = geometry.GapWidth * cellSize;

            if (outer <= Mathf.Epsilon || outer - gap <= Mathf.Epsilon)
            {
                return;
            }

            float left = data.Cell.x * cellSize;
            float bottom = data.Cell.y * cellSize;
            float right = left + cellSize;
            float top = bottom + cellSize;

            float bandSize = outer - gap;

            if (HasInnerCorner(data.InnerCornerMask, RoomInnerCornerMask.BottomLeft))
            {
                AddBoxCollider(
                    new Vector2(left + outer * 0.5f, bottom + gap + bandSize * 0.5f),
                    new Vector2(outer, bandSize)
                );

                if (gap > Mathf.Epsilon)
                {
                    AddBoxCollider(
                        new Vector2(left + gap + bandSize * 0.5f, bottom + gap * 0.5f),
                        new Vector2(bandSize, gap)
                    );
                }
            }

            if (HasInnerCorner(data.InnerCornerMask, RoomInnerCornerMask.BottomRight))
            {
                AddBoxCollider(
                    new Vector2(right - outer * 0.5f, bottom + gap + bandSize * 0.5f),
                    new Vector2(outer, bandSize)
                );

                if (gap > Mathf.Epsilon)
                {
                    AddBoxCollider(
                        new Vector2(right - gap - bandSize * 0.5f, bottom + gap * 0.5f),
                        new Vector2(bandSize, gap)
                    );
                }
            }

            if (HasInnerCorner(data.InnerCornerMask, RoomInnerCornerMask.TopLeft))
            {
                AddBoxCollider(
                    new Vector2(left + outer * 0.5f, top - gap - bandSize * 0.5f),
                    new Vector2(outer, bandSize)
                );

                if (gap > Mathf.Epsilon)
                {
                    AddBoxCollider(
                        new Vector2(left + gap + bandSize * 0.5f, top - gap * 0.5f),
                        new Vector2(bandSize, gap)
                    );
                }
            }

            if (HasInnerCorner(data.InnerCornerMask, RoomInnerCornerMask.TopRight))
            {
                AddBoxCollider(
                    new Vector2(right - outer * 0.5f, top - gap - bandSize * 0.5f),
                    new Vector2(outer, bandSize)
                );

                if (gap > Mathf.Epsilon)
                {
                    AddBoxCollider(
                        new Vector2(right - gap - bandSize * 0.5f, top - gap * 0.5f),
                        new Vector2(bandSize, gap)
                    );
                }
            }
        }

        /// <summary>
        /// 开始本次 Collider 重建。
        ///
        /// Collision Root 挂在 DungeonGrid 下，
        /// 因此 Collider 使用的 Offset 可以直接使用 Grid Local 坐标。
        /// 即使 DungeonGrid 自身存在位移、旋转或缩放，
        /// Collider 也会跟随 DungeonGrid。
        /// </summary>
        private void BeginCollisionBuild(DungeonGrid grid)
        {
            EnsureCollisionRoot(grid);

            usedColliderCount = 0;

            if (collisionRoot != null)
            {
                collisionRoot.gameObject.SetActive(true);
                collisionRoot.gameObject.layer = gameObject.layer;
            }
        }

        /// <summary>
        /// 结束 Collider 重建。
        /// 未被本次重建复用的 Collider 会被禁用。
        /// </summary>
        private void EndCollisionBuild()
        {
            for (int i = usedColliderCount; i < wallColliders.Count; i++)
            {
                if (wallColliders[i] != null)
                {
                    wallColliders[i].enabled = false;
                }
            }
        }

        /// <summary>
        /// 确保当前 Chunk 存在自己的 Collision Root。
        /// </summary>
        private void EnsureCollisionRoot(DungeonGrid grid)
        {
            if (collisionRoot == null)
            {
                GameObject collisionObject = new GameObject($"{name}_Collision");

                collisionObject.layer = gameObject.layer;
                collisionObject.transform.SetParent(grid.transform, false);

                collisionRoot = collisionObject.transform;
                return;
            }

            if (collisionRoot.parent != grid.transform)
            {
                collisionRoot.SetParent(grid.transform, false);
            }

            collisionRoot.localPosition = Vector3.zero;
            collisionRoot.localRotation = Quaternion.identity;
            collisionRoot.localScale = Vector3.one;
        }

        /// <summary>
        /// 获取一个可使用的 BoxCollider2D。
        ///
        /// 优先复用已有 Collider，避免 Dirty Chunk 重建时产生大量组件分配。
        /// </summary>
        private BoxCollider2D GetNextCollider()
        {
            BoxCollider2D collider;

            if (usedColliderCount < wallColliders.Count)
            {
                collider = wallColliders[usedColliderCount];
            }
            else
            {
                collider = collisionRoot.gameObject.AddComponent<BoxCollider2D>();
                wallColliders.Add(collider);
            }

            usedColliderCount++;

            collider.enabled = true;
            collider.isTrigger = false;

            return collider;
        }

        /// <summary>
        /// 添加一个 Grid Local 空间中的 BoxCollider2D。
        /// </summary>
        private void AddBoxCollider(Vector2 center, Vector2 size)
        {
            if (size.x <= Mathf.Epsilon || size.y <= Mathf.Epsilon)
            {
                return;
            }

            BoxCollider2D collider = GetNextCollider();

            collider.offset = center;
            collider.size = size;
        }

        private static bool HasBorder(RoomBorderMask mask, RoomBorderMask value)
        {
            return (mask & value) != 0;
        }

        private static bool HasDoor(RoomDoorMask mask, RoomDoorMask value)
        {
            return (mask & value) != 0;
        }

        private static bool HasInnerCorner(RoomInnerCornerMask mask, RoomInnerCornerMask value)
        {
            return (mask & value) != 0;
        }

        /// <summary>
        /// 上传当前 Chunk Mesh。
        /// </summary>
        private void UploadMesh()
        {
            mesh.Clear();

            if (vertices.Count == 0)
            {
                return;
            }

            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.SetUVs(0, uv0);
            mesh.SetUVs(1, uv1);
            mesh.SetUVs(2, uv2);
            mesh.SetUVs(3, uv3);

            mesh.RecalculateBounds();
        }

        /// <summary>
        /// 清空 CPU Mesh 构建缓冲，但保留 List 容量供下次复用。
        /// </summary>
        private void ClearBuildBuffers()
        {
            vertices.Clear();
            triangles.Clear();
            uv0.Clear();
            uv1.Clear();
            uv2.Clear();
            uv3.Clear();
        }

        private void CacheComponents()
        {
            if (meshFilter == null)
            {
                meshFilter = GetComponent<MeshFilter>();
            }

            if (meshRenderer == null)
            {
                meshRenderer = GetComponent<MeshRenderer>();
            }
        }

        private void OnEnable()
        {
            if (collisionRoot != null)
            {
                collisionRoot.gameObject.SetActive(true);
            }
        }

        private void OnDisable()
        {
            if (collisionRoot != null)
            {
                collisionRoot.gameObject.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            if (mesh != null)
            {
                Destroy(mesh);
                mesh = null;
            }

            if (collisionRoot != null)
            {
                Destroy(collisionRoot.gameObject);
                collisionRoot = null;
            }
        }
    }
}