Shader "Game/Dungeon/RoomCellBorder"
{
    Properties
    {
        [PerRendererData]
        _MainTex("Sprite Texture", 2D) = "white" {}

        _FillColor(
            "Fill Color",
            Color
        ) = (1, 1, 1, 0.8)

        _BorderColor(
            "Border Color",
            Color
        ) = (1, 1, 1, 1)

        // Width of the solid border band in normalized cell UV.
        _BorderWidth(
            "Border Width",
            Range(0, 0.5)
        ) = 0.06

        // Transparent space at the outermost room boundary.
        _GapWidth(
            "Transparent Gap Width",
            Range(0, 0.2)
        ) = 0.02

        // Enables smooth transitions using screen-space derivatives.
        [Toggle(_ROOM_BORDER_AA_ON)]
        _EnableAA(
            "Enable Anti-Aliasing",
            Float
        ) = 1

        // Bit 0: Left
        // Bit 1: Right
        // Bit 2: Bottom
        // Bit 3: Top
        [IntRange]
        _BorderMask(
            "Border Mask",
            Range(0, 15)
        ) = 15

        // Bit 0: Bottom Left
        // Bit 1: Bottom Right
        // Bit 2: Top Left
        // Bit 3: Top Right
        [IntRange]
        _InnerCornerMask(
            "Inner Corner Mask",
            Range(0, 15)
        ) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "RoomCellBorder"

            Tags
            {
                "LightMode" = "Universal2D"
            }

            HLSLPROGRAM

            #pragma vertex Vert
            #pragma fragment Frag

            // Material-local keyword controlled by _EnableAA.
            #pragma shader_feature_local_fragment _ROOM_BORDER_AA_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                half4 color       : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;

                // Sprite texture coordinates.
                float2 uv : TEXCOORD0;

                // Normalized cell-local coordinates in the range 0 to 1.
                float2 cellUV : TEXCOORD1;

                half4 color : COLOR;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)

                half4 _FillColor;
                half4 _BorderColor;

                float _BorderWidth;
                float _GapWidth;

                float _BorderMask;
                float _InnerCornerMask;

            CBUFFER_END

            /**
             * Returns 1 when the requested bit is enabled.
             *
             * bitValue should be 1, 2, 4 or 8.
             */
            float HasMaskBit(
                float mask,
                float bitValue)
            {
                float divided =
                    floor(mask / bitValue);

                return step(
                    0.5,
                    fmod(divided, 2.0)
                );
            }

            /**
             * Creates a zone extending inward from one cell edge.
             *
             * With AA enabled, smoothstep softens the transition.
             * With AA disabled, step creates a completely hard edge.
             */
            float CalculateEdgeZone(
                float distanceToEdge,
                float enabled,
                float width,
                float antiAliasWidth)
            {
                float hasWidth =
                    step(0.00001, width);

                float zone;

                #if defined(_ROOM_BORDER_AA_ON)

                    zone =
                        1.0 -
                        smoothstep(
                            width - antiAliasWidth,
                            width + antiAliasWidth,
                            distanceToEdge
                        );

                #else

                    zone =
                        step(
                            distanceToEdge,
                            width
                        );

                #endif

                return zone * enabled * hasWidth;
            }

            /**
             * Creates a band between:
             *
             * startDistance
             * and
             * startDistance + bandWidth.
             *
             * Used for the solid border after the transparent gap.
             */
            float CalculateEdgeBand(
                float distanceToEdge,
                float enabled,
                float startDistance,
                float bandWidth,
                float antiAliasWidth)
            {
                float hasBandWidth =
                    step(0.00001, bandWidth);

                float outerZone =
                    CalculateEdgeZone(
                        distanceToEdge,
                        enabled,
                        startDistance + bandWidth,
                        antiAliasWidth
                    );

                float innerZone =
                    CalculateEdgeZone(
                        distanceToEdge,
                        enabled,
                        startDistance,
                        antiAliasWidth
                    );

                return saturate(
                    outerZone - innerZone
                ) * hasBandWidth;
            }

            /**
             * Creates a square zone in one cell corner.
             *
             * Both horizontal and vertical distances must fall
             * inside the requested width.
             */
            float CalculateSquareZone(
                float horizontalDistance,
                float verticalDistance,
                float enabled,
                float width,
                float antiAliasWidth)
            {
                float horizontalZone =
                    CalculateEdgeZone(
                        horizontalDistance,
                        enabled,
                        width,
                        antiAliasWidth
                    );

                float verticalZone =
                    CalculateEdgeZone(
                        verticalDistance,
                        enabled,
                        width,
                        antiAliasWidth
                    );

                return min(
                    horizontalZone,
                    verticalZone
                );
            }

            /**
             * Creates a square band in one cell corner.
             *
             * This connects straight borders around concave
             * corners without introducing rounded geometry.
             */
            float CalculateSquareBand(
                float horizontalDistance,
                float verticalDistance,
                float enabled,
                float startDistance,
                float bandWidth,
                float antiAliasWidth)
            {
                float hasBandWidth =
                    step(0.00001, bandWidth);

                float outerZone =
                    CalculateSquareZone(
                        horizontalDistance,
                        verticalDistance,
                        enabled,
                        startDistance + bandWidth,
                        antiAliasWidth
                    );

                float innerZone =
                    CalculateSquareZone(
                        horizontalDistance,
                        verticalDistance,
                        enabled,
                        startDistance,
                        antiAliasWidth
                    );

                return saturate(
                    outerZone - innerZone
                ) * hasBandWidth;
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;

                output.positionCS =
                    TransformObjectToHClip(
                        input.positionOS.xyz
                    );

                output.uv =
                    input.uv;

                output.color =
                    input.color;

                /*
                 * Assumptions:
                 *
                 * - Sprite world size is 1 x 1.
                 * - Sprite pivot is centered.
                 * - Sprite mesh type is Full Rect.
                 *
                 * The object-space vertex range is therefore
                 * approximately -0.5 to 0.5.
                 *
                 * Adding 0.5 converts it to normalized 0 to 1
                 * cell coordinates.
                 *
                 * Object-space coordinates are used instead of
                 * texture UV so Sprite Atlas packing does not
                 * affect border placement.
                 */
                output.cellUV =
                    saturate(
                        input.positionOS.xy + 0.5
                    );

                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 textureColor =
                    SAMPLE_TEXTURE2D(
                        _MainTex,
                        sampler_MainTex,
                        input.uv
                    );

                float2 cellUV =
                    saturate(input.cellUV);

                float gapWidth =
                    clamp(
                        _GapWidth,
                        0.0,
                        0.49
                    );

                float borderWidth =
                    clamp(
                        _BorderWidth,
                        0.0,
                        0.5 - gapWidth
                    );

                float antiAliasWidth = 0.0;

                #if defined(_ROOM_BORDER_AA_ON)

                    antiAliasWidth =
                        max(
                            max(
                                fwidth(cellUV.x),
                                fwidth(cellUV.y)
                            ),
                            0.0001
                        );

                #endif

                // -------------------------------------------------
                // Edge masks
                // -------------------------------------------------

                float leftEnabled =
                    HasMaskBit(
                        _BorderMask,
                        1.0
                    );

                float rightEnabled =
                    HasMaskBit(
                        _BorderMask,
                        2.0
                    );

                float bottomEnabled =
                    HasMaskBit(
                        _BorderMask,
                        4.0
                    );

                float topEnabled =
                    HasMaskBit(
                        _BorderMask,
                        8.0
                    );

                // -------------------------------------------------
                // Transparent edge gaps
                // -------------------------------------------------

                float leftGap =
                    CalculateEdgeZone(
                        cellUV.x,
                        leftEnabled,
                        gapWidth,
                        antiAliasWidth
                    );

                float rightGap =
                    CalculateEdgeZone(
                        1.0 - cellUV.x,
                        rightEnabled,
                        gapWidth,
                        antiAliasWidth
                    );

                float bottomGap =
                    CalculateEdgeZone(
                        cellUV.y,
                        bottomEnabled,
                        gapWidth,
                        antiAliasWidth
                    );

                float topGap =
                    CalculateEdgeZone(
                        1.0 - cellUV.y,
                        topEnabled,
                        gapWidth,
                        antiAliasWidth
                    );

                float edgeGapFactor =
                    max(
                        max(
                            leftGap,
                            rightGap
                        ),
                        max(
                            bottomGap,
                            topGap
                        )
                    );

                // -------------------------------------------------
                // Solid edge borders
                // -------------------------------------------------

                float leftBorder =
                    CalculateEdgeBand(
                        cellUV.x,
                        leftEnabled,
                        gapWidth,
                        borderWidth,
                        antiAliasWidth
                    );

                float rightBorder =
                    CalculateEdgeBand(
                        1.0 - cellUV.x,
                        rightEnabled,
                        gapWidth,
                        borderWidth,
                        antiAliasWidth
                    );

                float bottomBorder =
                    CalculateEdgeBand(
                        cellUV.y,
                        bottomEnabled,
                        gapWidth,
                        borderWidth,
                        antiAliasWidth
                    );

                float topBorder =
                    CalculateEdgeBand(
                        1.0 - cellUV.y,
                        topEnabled,
                        gapWidth,
                        borderWidth,
                        antiAliasWidth
                    );

                float edgeBorderFactor =
                    max(
                        max(
                            leftBorder,
                            rightBorder
                        ),
                        max(
                            bottomBorder,
                            topBorder
                        )
                    );

                // -------------------------------------------------
                // Concave corner masks
                // -------------------------------------------------

                float bottomLeftEnabled =
                    HasMaskBit(
                        _InnerCornerMask,
                        1.0
                    );

                float bottomRightEnabled =
                    HasMaskBit(
                        _InnerCornerMask,
                        2.0
                    );

                float topLeftEnabled =
                    HasMaskBit(
                        _InnerCornerMask,
                        4.0
                    );

                float topRightEnabled =
                    HasMaskBit(
                        _InnerCornerMask,
                        8.0
                    );

                // -------------------------------------------------
                // Transparent corner gaps
                // -------------------------------------------------

                float bottomLeftGap =
                    CalculateSquareZone(
                        cellUV.x,
                        cellUV.y,
                        bottomLeftEnabled,
                        gapWidth,
                        antiAliasWidth
                    );

                float bottomRightGap =
                    CalculateSquareZone(
                        1.0 - cellUV.x,
                        cellUV.y,
                        bottomRightEnabled,
                        gapWidth,
                        antiAliasWidth
                    );

                float topLeftGap =
                    CalculateSquareZone(
                        cellUV.x,
                        1.0 - cellUV.y,
                        topLeftEnabled,
                        gapWidth,
                        antiAliasWidth
                    );

                float topRightGap =
                    CalculateSquareZone(
                        1.0 - cellUV.x,
                        1.0 - cellUV.y,
                        topRightEnabled,
                        gapWidth,
                        antiAliasWidth
                    );

                float cornerGapFactor =
                    max(
                        max(
                            bottomLeftGap,
                            bottomRightGap
                        ),
                        max(
                            topLeftGap,
                            topRightGap
                        )
                    );

                // -------------------------------------------------
                // Solid corner border connections
                // -------------------------------------------------

                float bottomLeftBorder =
                    CalculateSquareBand(
                        cellUV.x,
                        cellUV.y,
                        bottomLeftEnabled,
                        gapWidth,
                        borderWidth,
                        antiAliasWidth
                    );

                float bottomRightBorder =
                    CalculateSquareBand(
                        1.0 - cellUV.x,
                        cellUV.y,
                        bottomRightEnabled,
                        gapWidth,
                        borderWidth,
                        antiAliasWidth
                    );

                float topLeftBorder =
                    CalculateSquareBand(
                        cellUV.x,
                        1.0 - cellUV.y,
                        topLeftEnabled,
                        gapWidth,
                        borderWidth,
                        antiAliasWidth
                    );

                float topRightBorder =
                    CalculateSquareBand(
                        1.0 - cellUV.x,
                        1.0 - cellUV.y,
                        topRightEnabled,
                        gapWidth,
                        borderWidth,
                        antiAliasWidth
                    );

                float cornerBorderFactor =
                    max(
                        max(
                            bottomLeftBorder,
                            bottomRightBorder
                        ),
                        max(
                            topLeftBorder,
                            topRightBorder
                        )
                    );

                // -------------------------------------------------
                // Merge edge and corner results
                // -------------------------------------------------

                float gapFactor =
                    saturate(
                        max(
                            edgeGapFactor,
                            cornerGapFactor
                        )
                    );

                float borderFactor =
                    saturate(
                        max(
                            edgeBorderFactor,
                            cornerBorderFactor
                        )
                    );

                half4 fillColor =
                    _FillColor * input.color;

                half4 borderColor =
                    _BorderColor * input.color;

                half4 finalColor =
                    lerp(
                        fillColor,
                        borderColor,
                        borderFactor
                    );

                /*
                 * The transparent gap has final priority over
                 * both the fill and solid border.
                 */
                finalColor.a *=
                    1.0 - gapFactor;

                // Preserve the source Sprite texture shape.
                finalColor.rgb *=
                    textureColor.rgb;

                finalColor.a *=
                    textureColor.a;

                return finalColor;
            }

            ENDHLSL
        }
    }

    Fallback Off
}