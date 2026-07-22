using Game.Common.Colors;
using UnityEngine;

namespace Dungeon.RoomSystem
{
    /// <summary>
    /// 表示单个房间 Cell 使用的完整颜色组。
    ///
    /// 所有 Wall 颜色都在 CPU 侧通过 OKLCH 生成，
    /// Shader 只负责根据拓扑 Mask 选择颜色，不再进行 RGB 颜色混合。
    /// </summary>
    public readonly struct RoomCellColors
    {
        public Color FillColor { get; }
        public Color BorderColor { get; }

        public Color WallBaseColor { get; }
        public Color WallDarkColor { get; }
        public Color WallLightColor { get; }

        public RoomCellColors(
            Color fillColor,
            Color borderColor,
            Color wallBaseColor,
            Color wallDarkColor,
            Color wallLightColor)
        {
            FillColor = fillColor;
            BorderColor = borderColor;
            WallBaseColor = wallBaseColor;
            WallDarkColor = wallDarkColor;
            WallLightColor = wallLightColor;
        }
    }

    /// <summary>
    /// 根据 RoomData 的房间颜色生成完整视觉颜色组。
    ///
    /// Border、WallBase、WallDark、WallLight 都在 OKLCH 空间中生成，
    /// 从而避免 Shader 中直接使用 RGB Lerp 带来的感知亮度不均匀。
    /// </summary>
    public static class RoomCellColorFactory
    {
        // Border 对深色房间进行更明显的亮度补偿。
        private const float DarkLightnessBoost = 0.35f;
        private const float LightLightnessBoost = 0.15f;

        private const float DarkLightnessThreshold = 0.2f;
        private const float LightLightnessThreshold = 0.85f;

        /*
         * 与旧 Shader 的 _WallColorBlend = 0.45 对齐。
         *
         * 0 = BorderColor
         * 1 = FillColor
         *
         * 区别是现在插值发生在 OKLCH，而不是 RGB。
         */
        private const float WallBaseBlend = 0.01f;

        /*
         * Wall 明暗面直接调整 OKLCH Lightness。
         *
         * 这两个值现在就是主要的 Wall 明暗调节参数。
         * OKLCH 的 Lightness 更接近人眼感知，因此不再需要
         * DarkColor + DarkStrength / LightColor + LightStrength 两级 RGB 混合。
         */
        private const float WallDarkLightnessOffset = -0.12f;
        private const float WallLightLightnessOffset = 0.1f;

        /// <summary>
        /// 根据房间基础颜色生成 Fill、Border 和三种 Wall 颜色。
        /// </summary>
        public static RoomCellColors Create(Color roomColor)
        {
            float lightness =
                OklchColorUtility.GetLightness(
                    roomColor
                );

            float normalizedLightness =
                Mathf.InverseLerp(
                    DarkLightnessThreshold,
                    LightLightnessThreshold,
                    lightness
                );

            normalizedLightness =
                Mathf.SmoothStep(
                    0f,
                    1f,
                    normalizedLightness
                );

            float borderLightnessBoost =
                Mathf.Lerp(
                    DarkLightnessBoost,
                    LightLightnessBoost,
                    normalizedLightness
                );

            Color fillColor =
                roomColor;

            Color borderColor =
                OklchColorUtility.ShiftLightness(
                    roomColor,
                    borderLightnessBoost,
                    1f
                );

            float wallBaseLightnessBoost =
                borderLightnessBoost *
                (1f - WallBaseBlend);

            Color wallBaseColor =
                OklchColorUtility.ShiftLightness(
                    roomColor,
                    wallBaseLightnessBoost,
                    1f
                );

            Color wallDarkColor =
                OklchColorUtility.ShiftLightness(
                    wallBaseColor,
                    WallDarkLightnessOffset,
                    1f
                );

            Color wallLightColor =
                OklchColorUtility.ShiftLightness(
                    wallBaseColor,
                    WallLightLightnessOffset,
                    1f
                );

            return new RoomCellColors(
                fillColor,
                borderColor,
                wallBaseColor,
                wallDarkColor,
                wallLightColor
            );
        }
    }
}