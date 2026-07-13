using Game.Common.Colors;
using UnityEngine;

namespace Dungeon.RoomSystem
{
    /// <summary>
    /// 表示房间格子的填充颜色和描边颜色。
    /// </summary>
    public readonly struct RoomCellColors
    {
        /// <summary>
        /// 房间格子的内部填充颜色。
        /// </summary>
        public Color FillColor { get; }

        /// <summary>
        /// 房间格子的实色描边颜色。
        /// </summary>
        public Color BorderColor { get; }

        public RoomCellColors(
            Color fillColor,
            Color borderColor)
        {
            FillColor = fillColor;
            BorderColor = borderColor;
        }
    }

    /// <summary>
    /// 根据 RoomData 的房间颜色生成格子视觉颜色。
    /// 深色房间获得更明显的 OKLCH 亮度补偿，
    /// 亮色房间只进行轻微提亮。
    /// </summary>
    public static class RoomCellColorFactory
    {
        private const float DarkLightnessBoost = 0.2f;
        private const float LightLightnessBoost = 0.025f;

        private const float DarkLightnessThreshold = 0.4f;
        private const float LightLightnessThreshold = 0.85f;

        /// <summary>
        /// 根据房间颜色创建填充色和完全不透明的描边色。
        /// </summary>
        public static RoomCellColors Create(
            Color roomColor)
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

            float lightnessBoost =
                Mathf.Lerp(
                    DarkLightnessBoost,
                    LightLightnessBoost,
                    normalizedLightness
                );

            Color borderColor =
                OklchColorUtility.ShiftLightness(
                    roomColor,
                    lightnessBoost,
                    1f
                );

            return new RoomCellColors(
                roomColor,
                borderColor
            );
        }
    }
}