using UnityEngine;

namespace Dungeon.RoomSystem
{
    /// <summary>
    /// 管理单个房间格子的视觉参数。
    /// 使用 MaterialPropertyBlock 设置颜色、描边宽度和方向 Mask，
    /// 避免为每个格子创建独立材质实例。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class RoomCellView : MonoBehaviour
    {
        private static readonly int FillColorId =
            Shader.PropertyToID("_FillColor");

        private static readonly int BorderColorId =
            Shader.PropertyToID("_BorderColor");

        private static readonly int BorderWidthId =
            Shader.PropertyToID("_BorderWidth");

        private static readonly int BorderMaskId =
            Shader.PropertyToID("_BorderMask");

        private static readonly int InnerCornerMaskId =
            Shader.PropertyToID("_InnerCornerMask");

        private static readonly int DoorMaskId =
            Shader.PropertyToID("_DoorMask");

        private static readonly int DoorSizeId =
            Shader.PropertyToID("_DoorSize");

        private SpriteRenderer spriteRenderer;
        private MaterialPropertyBlock propertyBlock;

        /// <summary>
        /// 当前视觉对应的世界逻辑格子坐标。
        /// </summary>
        public Vector2Int Cell { get; private set; }

        /// <summary>
        /// 当前格子使用的 SpriteRenderer。
        /// </summary>
        public SpriteRenderer SpriteRenderer
        {
            get
            {
                CacheComponents();
                return spriteRenderer;
            }
        }

        private void Awake()
        {
            CacheComponents();
        }

        /// <summary>
        /// 初始化格子的坐标、填充颜色、描边颜色和描边宽度。
        /// </summary>
        /// <param name="cell">对应的世界逻辑格坐标。</param>
        /// <param name="fillColor">房间内部填充颜色。</param>
        /// <param name="borderColor">房间描边颜色。</param>
        /// <param name="borderWidth">基于局部 UV 的描边宽度。</param>
        public void Initialize(
            Vector2Int cell,
            Color fillColor,
            Color borderColor,
            float borderWidth)
        {
            CacheComponents();

            Cell = cell;

            spriteRenderer.GetPropertyBlock(
                propertyBlock
            );

            propertyBlock.SetColor(
                FillColorId,
                fillColor
            );

            propertyBlock.SetColor(
                BorderColorId,
                borderColor
            );

            propertyBlock.SetFloat(
                BorderWidthId,
                Mathf.Clamp(
                    borderWidth,
                    0f,
                    0.5f
                )
            );

            spriteRenderer.SetPropertyBlock(
                propertyBlock
            );
        }

        /// <summary>
        /// 同时修改格子的填充颜色和描边颜色。
        /// </summary>
        public void SetColors(
            Color fillColor,
            Color borderColor)
        {
            CacheComponents();

            spriteRenderer.GetPropertyBlock(
                propertyBlock
            );

            propertyBlock.SetColor(
                FillColorId,
                fillColor
            );

            propertyBlock.SetColor(
                BorderColorId,
                borderColor
            );

            spriteRenderer.SetPropertyBlock(
                propertyBlock
            );
        }

        /// <summary>
        /// 修改当前格子的填充颜色。
        /// </summary>
        public void SetFillColor(Color fillColor)
        {
            CacheComponents();

            spriteRenderer.GetPropertyBlock(
                propertyBlock
            );

            propertyBlock.SetColor(
                FillColorId,
                fillColor
            );

            spriteRenderer.SetPropertyBlock(
                propertyBlock
            );
        }

        /// <summary>
        /// 修改当前格子的描边颜色。
        /// </summary>
        public void SetBorderColor(Color borderColor)
        {
            CacheComponents();

            spriteRenderer.GetPropertyBlock(
                propertyBlock
            );

            propertyBlock.SetColor(
                BorderColorId,
                borderColor
            );

            spriteRenderer.SetPropertyBlock(
                propertyBlock
            );
        }

        /// <summary>
        /// 修改当前格子的描边宽度。
        /// </summary>
        public void SetBorderWidth(float borderWidth)
        {
            CacheComponents();

            spriteRenderer.GetPropertyBlock(
                propertyBlock
            );

            propertyBlock.SetFloat(
                BorderWidthId,
                Mathf.Clamp(
                    borderWidth,
                    0f,
                    0.5f
                )
            );

            spriteRenderer.SetPropertyBlock(
                propertyBlock
            );
        }

        /// <summary>
        /// 设置格子的四方向外露边和 L 形凹角连接区域。
        /// </summary>
        public void SetBorderMasks(
            RoomBorderMask borderMask,
            RoomInnerCornerMask innerCornerMask)
        {
            CacheComponents();

            spriteRenderer.GetPropertyBlock(
                propertyBlock
            );

            propertyBlock.SetFloat(
                BorderMaskId,
                (int)borderMask
            );

            propertyBlock.SetFloat(
                InnerCornerMaskId,
                (int)innerCornerMask
            );

            spriteRenderer.SetPropertyBlock(
                propertyBlock
            );
        }

        /// <summary>
        /// 设置当前格子的门洞方向和门洞大小。
        /// </summary>
        /// <param name="doorMask">
        /// 与其他房间相邻、需要显示门洞的方向。
        /// </param>
        /// <param name="normalizedDoorSize">
        /// 相对于 CellSize 的归一化门洞长度，范围为 0～1。
        /// </param>
        public void SetDoorData(
            RoomDoorMask doorMask,
            float normalizedDoorSize)
        {
            CacheComponents();

            spriteRenderer.GetPropertyBlock(
                propertyBlock
            );

            propertyBlock.SetFloat(
                DoorMaskId,
                (int)doorMask
            );

            propertyBlock.SetFloat(
                DoorSizeId,
                Mathf.Clamp01(
                    normalizedDoorSize
                )
            );

            spriteRenderer.SetPropertyBlock(
                propertyBlock
            );
        }

        /// <summary>
        /// 缓存 SpriteRenderer 和 MaterialPropertyBlock。
        /// </summary>
        private void CacheComponents()
        {
            if (spriteRenderer == null)
            {
                spriteRenderer =
                    GetComponent<SpriteRenderer>();
            }

            if (propertyBlock == null)
            {
                propertyBlock =
                    new MaterialPropertyBlock();
            }
        }
    }
}