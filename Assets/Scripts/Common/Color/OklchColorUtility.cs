using UnityEngine;

namespace Game.Common.Colors
{
    /// <summary>
    /// 提供 sRGB 与 OKLCH 之间的转换和亮度调整功能。
    /// 转换细节和 sRGB 色域映射均封装在该工具内部。
    /// </summary>
    public static class OklchColorUtility
    {
        private const float Epsilon = 0.000001f;

        private readonly struct OklabColor
        {
            public readonly float L;
            public readonly float A;
            public readonly float B;

            public OklabColor(
                float lightness,
                float a,
                float b)
            {
                L = lightness;
                A = a;
                B = b;
            }
        }

        private readonly struct OklchColor
        {
            public readonly float L;
            public readonly float C;
            public readonly float H;

            public OklchColor(
                float lightness,
                float chroma,
                float hue)
            {
                L = lightness;
                C = chroma;
                H = hue;
            }
        }

        /// <summary>
        /// 获取指定 sRGB 颜色的 OKLCH 感知亮度。
        /// 返回值通常位于 0～1。
        /// </summary>
        public static float GetLightness(Color color)
        {
            Color linearRgb = SrgbToLinear(color);

            OklabColor lab =
                LinearRgbToOklab(linearRgb);

            return lab.L;
        }

        /// <summary>
        /// 在 OKLCH 空间中调整颜色亮度。
        /// 保留原色相，尽量保持原色度。
        /// 超出 sRGB 色域时会降低色度，而不是直接截断 RGB。
        /// </summary>
        /// <param name="color">输入的 sRGB 颜色。</param>
        /// <param name="lightnessOffset">OKLCH 亮度增量。</param>
        /// <param name="alpha">输出颜色的 Alpha。</param>
        public static Color ShiftLightness(
            Color color,
            float lightnessOffset,
            float alpha)
        {
            Color linearRgb = SrgbToLinear(color);

            OklabColor sourceLab =
                LinearRgbToOklab(linearRgb);

            OklchColor sourceLch =
                OklabToOklch(sourceLab);

            OklchColor targetLch =
                new OklchColor(
                    Mathf.Clamp01(
                        sourceLch.L + lightnessOffset
                    ),
                    sourceLch.C,
                    sourceLch.H
                );

            return OklchToSrgbGamutMapped(
                targetLch,
                alpha
            );
        }

        /// <summary>
        /// 将 sRGB 转换为线性 RGB。
        /// </summary>
        private static Color SrgbToLinear(Color color)
        {
            return new Color(
                Mathf.GammaToLinearSpace(color.r),
                Mathf.GammaToLinearSpace(color.g),
                Mathf.GammaToLinearSpace(color.b),
                color.a
            );
        }

        /// <summary>
        /// 将线性 RGB 转换为 sRGB。
        /// </summary>
        private static Color LinearToSrgb(
            Color color,
            float alpha)
        {
            return new Color(
                Mathf.LinearToGammaSpace(
                    Mathf.Clamp01(color.r)
                ),
                Mathf.LinearToGammaSpace(
                    Mathf.Clamp01(color.g)
                ),
                Mathf.LinearToGammaSpace(
                    Mathf.Clamp01(color.b)
                ),
                alpha
            );
        }

        /// <summary>
        /// 将线性 sRGB 转换为 Oklab。
        /// </summary>
        private static OklabColor LinearRgbToOklab(
            Color rgb)
        {
            float l =
                0.4122214708f * rgb.r +
                0.5363325363f * rgb.g +
                0.0514459929f * rgb.b;

            float m =
                0.2119034982f * rgb.r +
                0.6806995451f * rgb.g +
                0.1073969566f * rgb.b;

            float s =
                0.0883024619f * rgb.r +
                0.2817188376f * rgb.g +
                0.6299787005f * rgb.b;

            float lRoot = SignedCubeRoot(l);
            float mRoot = SignedCubeRoot(m);
            float sRoot = SignedCubeRoot(s);

            return new OklabColor(
                0.2104542553f * lRoot +
                0.7936177850f * mRoot -
                0.0040720468f * sRoot,

                1.9779984951f * lRoot -
                2.4285922050f * mRoot +
                0.4505937099f * sRoot,

                0.0259040371f * lRoot +
                0.7827717662f * mRoot -
                0.8086757660f * sRoot
            );
        }

        /// <summary>
        /// 将 Oklab 转换为线性 sRGB。
        /// 返回值可能暂时超出 0～1。
        /// </summary>
        private static Color OklabToLinearRgb(
            OklabColor lab)
        {
            float lRoot =
                lab.L +
                0.3963377774f * lab.A +
                0.2158037573f * lab.B;

            float mRoot =
                lab.L -
                0.1055613458f * lab.A -
                0.0638541728f * lab.B;

            float sRoot =
                lab.L -
                0.0894841775f * lab.A -
                1.2914855480f * lab.B;

            float l = lRoot * lRoot * lRoot;
            float m = mRoot * mRoot * mRoot;
            float s = sRoot * sRoot * sRoot;

            return new Color(
                4.0767416621f * l -
                3.3077115913f * m +
                0.2309699292f * s,

                -1.2684380046f * l +
                2.6097574011f * m -
                0.3413193965f * s,

                -0.0041960863f * l -
                0.7034186147f * m +
                1.7076147010f * s,

                1f
            );
        }

        /// <summary>
        /// 将 Oklab 转换为 OKLCH。
        /// Hue 使用弧度表示。
        /// </summary>
        private static OklchColor OklabToOklch(
            OklabColor lab)
        {
            float chroma =
                Mathf.Sqrt(
                    lab.A * lab.A +
                    lab.B * lab.B
                );

            float hue =
                chroma > Epsilon
                    ? Mathf.Atan2(lab.B, lab.A)
                    : 0f;

            return new OklchColor(
                lab.L,
                chroma,
                hue
            );
        }

        /// <summary>
        /// 将 OKLCH 转换为 Oklab。
        /// </summary>
        private static OklabColor OklchToOklab(
            OklchColor lch)
        {
            return new OklabColor(
                lch.L,
                lch.C * Mathf.Cos(lch.H),
                lch.C * Mathf.Sin(lch.H)
            );
        }

        /// <summary>
        /// 将 OKLCH 转换为 sRGB。
        /// 超出色域时保持亮度和色相，并逐渐降低色度。
        /// </summary>
        private static Color OklchToSrgbGamutMapped(
            OklchColor lch,
            float alpha)
        {
            Color linearRgb =
                OklabToLinearRgb(
                    OklchToOklab(lch)
                );

            if (!IsInsideSrgbGamut(linearRgb))
            {
                float minimumChroma = 0f;
                float maximumChroma = lch.C;

                for (int i = 0; i < 12; i++)
                {
                    float testChroma =
                        (minimumChroma + maximumChroma) *
                        0.5f;

                    OklchColor testLch =
                        new OklchColor(
                            lch.L,
                            testChroma,
                            lch.H
                        );

                    Color testRgb =
                        OklabToLinearRgb(
                            OklchToOklab(testLch)
                        );

                    if (IsInsideSrgbGamut(testRgb))
                    {
                        minimumChroma = testChroma;
                        linearRgb = testRgb;
                    }
                    else
                    {
                        maximumChroma = testChroma;
                    }
                }
            }

            return LinearToSrgb(
                linearRgb,
                alpha
            );
        }

        /// <summary>
        /// 判断线性 RGB 是否处于标准 sRGB 色域内。
        /// </summary>
        private static bool IsInsideSrgbGamut(
            Color color)
        {
            return
                color.r >= 0f &&
                color.r <= 1f &&
                color.g >= 0f &&
                color.g <= 1f &&
                color.b >= 0f &&
                color.b <= 1f;
        }

        private static float SignedCubeRoot(float value)
        {
            return
                Mathf.Sign(value) *
                Mathf.Pow(
                    Mathf.Abs(value),
                    1f / 3f
                );
        }
    }
}