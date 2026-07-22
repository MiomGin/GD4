Shader "Game/Dungeon/RoomCellBorderChunkParadox"
{
    Properties
    {
        [Header(Outer Border)]
        _BorderWidth("Outer Border Width", Range(0, 0.5)) = 0.08
        _GapWidth("Transparent Gap Width", Range(0, 0.2)) = 0.02

        [Header(Wall)]
        _WallWidth("Wall Width", Range(0, 0.4)) = 0.08
        _WallColorBlend("Wall Base Color Blend", Range(0, 1)) = 0.45

        _WallDarkColor("Wall Dark Color", Color) = (0, 0, 0, 1)
        _WallDarkStrength("Wall Dark Strength", Range(0, 1)) = 0.25

        _WallLightColor("Wall Light Color", Color) = (1, 1, 1, 1)
        _WallLightStrength("Wall Light Strength", Range(0, 1)) = 0.25

        [Header(Room Shadow)]
        _RoomShadowWidth("Room Shadow Width", Range(0, 0.3)) = 0.02
        _RoomShadowColor("Room Shadow Color", Color) = (0, 0, 0, 1)
        _RoomShadowStrength("Room Shadow Strength", Range(0, 1)) = 0.12

        [Header(Door)]
        _DoorSize("Door Opening Size", Range(0, 1)) = 0

        [Header(Anti Aliasing)]
        [Toggle(_ROOM_BORDER_AA_ON)]
        _EnableAA("Enable Anti-Aliasing", Float) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "RoomCellBorderChunkParadox"

            Tags
            {
                "LightMode" = "Universal2D"
            }

            HLSLPROGRAM

            #pragma vertex Vert
            #pragma fragment Frag
            #pragma shader_feature_local_fragment _ROOM_BORDER_AA_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 cellUV : TEXCOORD0;

                // x = BorderMask
                // y = InnerCornerMask
                // z = DoorMask
                // w = Reserved
                float4 topology : TEXCOORD1;

                float4 fillColor : TEXCOORD2;
                float4 borderColor : TEXCOORD3;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 cellUV : TEXCOORD0;
                nointerpolation float4 topology : TEXCOORD1;
                float4 fillColor : TEXCOORD2;
                float4 borderColor : TEXCOORD3;
            };

            CBUFFER_START(UnityPerMaterial)

            float _BorderWidth;
            float _GapWidth;

            float _WallWidth;
            float _WallColorBlend;

            half4 _WallDarkColor;
            float _WallDarkStrength;

            half4 _WallLightColor;
            float _WallLightStrength;

            float _RoomShadowWidth;
            half4 _RoomShadowColor;
            float _RoomShadowStrength;

            float _DoorSize;

            CBUFFER_END


            // =====================================================
            // Utilities
            // =====================================================

            float HasMaskBit(float mask, float bitValue)
            {
                float divided = floor(mask / bitValue);
                return step(0.5, fmod(divided, 2.0));
            }

            float CalculateEdgeZone(float distanceToEdge, float enabled, float width, float antiAliasWidth)
            {
                float hasWidth = step(0.00001, width);
                float zone;

                #if defined(_ROOM_BORDER_AA_ON)
                    zone = 1.0 - smoothstep(width - antiAliasWidth, width + antiAliasWidth, distanceToEdge);
                #else
                    zone = step(distanceToEdge, width);
                #endif

                return zone * enabled * hasWidth;
            }

            float CalculateEdgeBand(float distanceToEdge, float enabled, float startDistance, float bandWidth, float antiAliasWidth)
            {
                float hasBandWidth = step(0.00001, bandWidth);

                float outerZone = CalculateEdgeZone(
                    distanceToEdge,
                    enabled,
                    startDistance + bandWidth,
                    antiAliasWidth
                );

                float innerZone = CalculateEdgeZone(
                    distanceToEdge,
                    enabled,
                    startDistance,
                    antiAliasWidth
                );

                return saturate(outerZone - innerZone) * hasBandWidth;
            }

            float CalculateCenteredDoorOpening(float axisCoordinate, float enabled, float doorSize, float antiAliasWidth)
            {
                float hasDoor = step(0.00001, doorSize);
                float halfDoorSize = doorSize * 0.5;
                float distanceToCenter = abs(axisCoordinate - 0.5);
                float opening;

                #if defined(_ROOM_BORDER_AA_ON)
                    opening = 1.0 - smoothstep(
                        halfDoorSize - antiAliasWidth,
                        halfDoorSize + antiAliasWidth,
                        distanceToCenter
                    );
                #else
                    opening = step(distanceToCenter, halfDoorSize);
                #endif

                return opening * enabled * hasDoor;
            }

            float CalculateSquareZone(float horizontalDistance, float verticalDistance, float enabled, float width, float antiAliasWidth)
            {
                float horizontalZone = CalculateEdgeZone(
                    horizontalDistance,
                    enabled,
                    width,
                    antiAliasWidth
                );

                float verticalZone = CalculateEdgeZone(
                    verticalDistance,
                    enabled,
                    width,
                    antiAliasWidth
                );

                return min(horizontalZone, verticalZone);
            }

            float CalculateSquareBand(
                float horizontalDistance,
                float verticalDistance,
                float enabled,
                float startDistance,
                float bandWidth,
                float antiAliasWidth)
            {
                float hasBandWidth = step(0.00001, bandWidth);

                float outerZone = CalculateSquareZone(
                    horizontalDistance,
                    verticalDistance,
                    enabled,
                    startDistance + bandWidth,
                    antiAliasWidth
                );

                float innerZone = CalculateSquareZone(
                    horizontalDistance,
                    verticalDistance,
                    enabled,
                    startDistance,
                    antiAliasWidth
                );

                return saturate(outerZone - innerZone) * hasBandWidth;
            }

            float CalculateRangeZone(float coordinate, float minimum, float maximum, float antiAliasWidth)
            {
                float hasWidth = step(0.00001, maximum - minimum);

                #if defined(_ROOM_BORDER_AA_ON)

                    float lower = smoothstep(
                        minimum - antiAliasWidth,
                        minimum + antiAliasWidth,
                        coordinate
                    );

                    float upper = 1.0 - smoothstep(
                        maximum - antiAliasWidth,
                        maximum + antiAliasWidth,
                        coordinate
                    );

                    return lower * upper * hasWidth;

                #else

                    return step(minimum, coordinate) *
                           step(coordinate, maximum) *
                           hasWidth;

                #endif
            }


            // =====================================================
            // Vertex
            // =====================================================

            Varyings Vert(Attributes input)
            {
                Varyings output;

                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.cellUV = input.cellUV;
                output.topology = input.topology;
                output.fillColor = input.fillColor;
                output.borderColor = input.borderColor;

                return output;
            }


            // =====================================================
            // Fragment
            // =====================================================

            half4 Frag(Varyings input) : SV_Target
            {
                float2 cellUV = saturate(input.cellUV);

                float borderMask = round(input.topology.x);
                float innerCornerMask = round(input.topology.y);
                float doorMask = round(input.topology.z);


                // =================================================
                // Geometry
                // =================================================

                float gapWidth = clamp(_GapWidth, 0.0, 0.49);
                float borderWidth = clamp(_BorderWidth, 0.0, 0.5 - gapWidth);
                float borderEnd = gapWidth + borderWidth;

                float wallWidth = clamp(_WallWidth, 0.0, 0.5 - borderEnd);
                float wallEnd = borderEnd + wallWidth;

                float roomShadowWidth = clamp(
                    _RoomShadowWidth,
                    0.0,
                    0.5 - wallEnd
                );

                float antiAliasWidth = 0.0;

                #if defined(_ROOM_BORDER_AA_ON)

                    antiAliasWidth = max(
                        max(
                            fwidth(cellUV.x),
                            fwidth(cellUV.y)
                        ),
                        0.0001
                    );

                #endif

                float leftDistance = cellUV.x;
                float rightDistance = 1.0 - cellUV.x;
                float bottomDistance = cellUV.y;
                float topDistance = 1.0 - cellUV.y;


                // =================================================
                // Topology
                // =================================================

                float leftEnabled = HasMaskBit(borderMask, 1.0);
                float rightEnabled = HasMaskBit(borderMask, 2.0);
                float bottomEnabled = HasMaskBit(borderMask, 4.0);
                float topEnabled = HasMaskBit(borderMask, 8.0);

                /*
                 * InnerCorner stays exactly as the original CPU
                 * topology result.
                 *
                 * It is NOT multiplied by DoorOpening.
                 */
                float bottomLeftInner = HasMaskBit(innerCornerMask, 1.0);
                float bottomRightInner = HasMaskBit(innerCornerMask, 2.0);
                float topLeftInner = HasMaskBit(innerCornerMask, 4.0);
                float topRightInner = HasMaskBit(innerCornerMask, 8.0);

                float leftDoorEnabled = HasMaskBit(doorMask, 1.0);
                float rightDoorEnabled = HasMaskBit(doorMask, 2.0);
                float bottomDoorEnabled = HasMaskBit(doorMask, 4.0);
                float topDoorEnabled = HasMaskBit(doorMask, 8.0);


                // =================================================
                // Door Opening
                // =================================================

                float doorSize = clamp(_DoorSize, 0.0, 0.9999);
                float halfDoorSize = doorSize * 0.5;

                float doorMinimum = 0.5 - halfDoorSize;
                float doorMaximum = 0.5 + halfDoorSize;

                float leftDoorOpening = CalculateCenteredDoorOpening(
                    cellUV.y,
                    leftDoorEnabled,
                    doorSize,
                    antiAliasWidth
                );

                float rightDoorOpening = CalculateCenteredDoorOpening(
                    cellUV.y,
                    rightDoorEnabled,
                    doorSize,
                    antiAliasWidth
                );

                float bottomDoorOpening = CalculateCenteredDoorOpening(
                    cellUV.x,
                    bottomDoorEnabled,
                    doorSize,
                    antiAliasWidth
                );

                float topDoorOpening = CalculateCenteredDoorOpening(
                    cellUV.x,
                    topDoorEnabled,
                    doorSize,
                    antiAliasWidth
                );


                // =================================================
                // Gap
                // =================================================

                float leftGap = CalculateEdgeZone(
                    leftDistance,
                    leftEnabled,
                    gapWidth,
                    antiAliasWidth
                ) * (1.0 - leftDoorOpening);

                float rightGap = CalculateEdgeZone(
                    rightDistance,
                    rightEnabled,
                    gapWidth,
                    antiAliasWidth
                ) * (1.0 - rightDoorOpening);

                float bottomGap = CalculateEdgeZone(
                    bottomDistance,
                    bottomEnabled,
                    gapWidth,
                    antiAliasWidth
                ) * (1.0 - bottomDoorOpening);

                float topGap = CalculateEdgeZone(
                    topDistance,
                    topEnabled,
                    gapWidth,
                    antiAliasWidth
                ) * (1.0 - topDoorOpening);

                float edgeGapFactor = max(
                    max(leftGap, rightGap),
                    max(bottomGap, topGap)
                );

                float bottomLeftGap = CalculateSquareZone(
                    leftDistance,
                    bottomDistance,
                    bottomLeftInner,
                    gapWidth,
                    antiAliasWidth
                );

                float bottomRightGap = CalculateSquareZone(
                    rightDistance,
                    bottomDistance,
                    bottomRightInner,
                    gapWidth,
                    antiAliasWidth
                );

                float topLeftGap = CalculateSquareZone(
                    leftDistance,
                    topDistance,
                    topLeftInner,
                    gapWidth,
                    antiAliasWidth
                );

                float topRightGap = CalculateSquareZone(
                    rightDistance,
                    topDistance,
                    topRightInner,
                    gapWidth,
                    antiAliasWidth
                );

                float cornerGapFactor = max(
                    max(bottomLeftGap, bottomRightGap),
                    max(topLeftGap, topRightGap)
                );

                float gapFactor = saturate(
                    max(
                        edgeGapFactor,
                        cornerGapFactor
                    )
                );


                // =================================================
                // Outer Border
                // =================================================

                float leftBorder = CalculateEdgeBand(
                    leftDistance,
                    leftEnabled,
                    gapWidth,
                    borderWidth,
                    antiAliasWidth
                ) * (1.0 - leftDoorOpening);

                float rightBorder = CalculateEdgeBand(
                    rightDistance,
                    rightEnabled,
                    gapWidth,
                    borderWidth,
                    antiAliasWidth
                ) * (1.0 - rightDoorOpening);

                float bottomBorder = CalculateEdgeBand(
                    bottomDistance,
                    bottomEnabled,
                    gapWidth,
                    borderWidth,
                    antiAliasWidth
                ) * (1.0 - bottomDoorOpening);

                float topBorder = CalculateEdgeBand(
                    topDistance,
                    topEnabled,
                    gapWidth,
                    borderWidth,
                    antiAliasWidth
                ) * (1.0 - topDoorOpening);

                float edgeBorderFactor = max(
                    max(leftBorder, rightBorder),
                    max(bottomBorder, topBorder)
                );

                float bottomLeftBorder = CalculateSquareBand(
                    leftDistance,
                    bottomDistance,
                    bottomLeftInner,
                    gapWidth,
                    borderWidth,
                    antiAliasWidth
                );

                float bottomRightBorder = CalculateSquareBand(
                    rightDistance,
                    bottomDistance,
                    bottomRightInner,
                    gapWidth,
                    borderWidth,
                    antiAliasWidth
                );

                float topLeftBorder = CalculateSquareBand(
                    leftDistance,
                    topDistance,
                    topLeftInner,
                    gapWidth,
                    borderWidth,
                    antiAliasWidth
                );

                float topRightBorder = CalculateSquareBand(
                    rightDistance,
                    topDistance,
                    topRightInner,
                    gapWidth,
                    borderWidth,
                    antiAliasWidth
                );

                float cornerBorderFactor = max(
                    max(bottomLeftBorder, bottomRightBorder),
                    max(topLeftBorder, topRightBorder)
                );

                float outerBorderFactor = saturate(
                    max(
                        edgeBorderFactor,
                        cornerBorderFactor
                    )
                );


                // =================================================
                // Raw Wall
                // =================================================

                float leftWallRaw = CalculateEdgeBand(
                    leftDistance,
                    leftEnabled,
                    borderEnd,
                    wallWidth,
                    antiAliasWidth
                );

                float rightWallRaw = CalculateEdgeBand(
                    rightDistance,
                    rightEnabled,
                    borderEnd,
                    wallWidth,
                    antiAliasWidth
                );

                float bottomWallRaw = CalculateEdgeBand(
                    bottomDistance,
                    bottomEnabled,
                    borderEnd,
                    wallWidth,
                    antiAliasWidth
                );

                float topWallRaw = CalculateEdgeBand(
                    topDistance,
                    topEnabled,
                    borderEnd,
                    wallWidth,
                    antiAliasWidth
                );


                // =================================================
                // Door Wrap
                // =================================================

                float doorWrapWidth = min(
                    wallWidth,
                    halfDoorSize
                );


                // Top Door

                float topDoorDepth = CalculateEdgeZone(
                    topDistance,
                    topDoorEnabled,
                    wallEnd,
                    antiAliasWidth
                );

                float topDoorLeftWrap = topDoorDepth * CalculateRangeZone(
                    cellUV.x,
                    doorMinimum,
                    doorMinimum + doorWrapWidth,
                    antiAliasWidth
                );

                float topDoorRightWrap = topDoorDepth * CalculateRangeZone(
                    cellUV.x,
                    doorMaximum - doorWrapWidth,
                    doorMaximum,
                    antiAliasWidth
                );


                // Bottom Door

                float bottomDoorDepth = CalculateEdgeZone(
                    bottomDistance,
                    bottomDoorEnabled,
                    wallEnd,
                    antiAliasWidth
                );

                float bottomDoorLeftWrap = bottomDoorDepth * CalculateRangeZone(
                    cellUV.x,
                    doorMinimum,
                    doorMinimum + doorWrapWidth,
                    antiAliasWidth
                );

                float bottomDoorRightWrap = bottomDoorDepth * CalculateRangeZone(
                    cellUV.x,
                    doorMaximum - doorWrapWidth,
                    doorMaximum,
                    antiAliasWidth
                );


                // Left Door

                float leftDoorDepth = CalculateEdgeZone(
                    leftDistance,
                    leftDoorEnabled,
                    wallEnd,
                    antiAliasWidth
                );

                float leftDoorBottomWrap = leftDoorDepth * CalculateRangeZone(
                    cellUV.y,
                    doorMinimum,
                    doorMinimum + doorWrapWidth,
                    antiAliasWidth
                );

                float leftDoorTopWrap = leftDoorDepth * CalculateRangeZone(
                    cellUV.y,
                    doorMaximum - doorWrapWidth,
                    doorMaximum,
                    antiAliasWidth
                );


                // Right Door

                float rightDoorDepth = CalculateEdgeZone(
                    rightDistance,
                    rightDoorEnabled,
                    wallEnd,
                    antiAliasWidth
                );

                float rightDoorBottomWrap = rightDoorDepth * CalculateRangeZone(
                    cellUV.y,
                    doorMinimum,
                    doorMinimum + doorWrapWidth,
                    antiAliasWidth
                );

                float rightDoorTopWrap = rightDoorDepth * CalculateRangeZone(
                    cellUV.y,
                    doorMaximum - doorWrapWidth,
                    doorMaximum,
                    antiAliasWidth
                );


                // =================================================
                // Door Endpoint Join Regions
                //
                // Join only exists where:
                //
                // Raw straight Wall
                //      กษ
                // Door Wrap
                //
                // This region belongs exclusively to the Mitre.
                // =================================================

                float topDoorLeftJoin =
                    topDoorLeftWrap *
                    topWallRaw;

                float topDoorRightJoin =
                    topDoorRightWrap *
                    topWallRaw;

                float bottomDoorLeftJoin =
                    bottomDoorLeftWrap *
                    bottomWallRaw;

                float bottomDoorRightJoin =
                    bottomDoorRightWrap *
                    bottomWallRaw;

                float leftDoorBottomJoin =
                    leftDoorBottomWrap *
                    leftWallRaw;

                float leftDoorTopJoin =
                    leftDoorTopWrap *
                    leftWallRaw;

                float rightDoorBottomJoin =
                    rightDoorBottomWrap *
                    rightWallRaw;

                float rightDoorTopJoin =
                    rightDoorTopWrap *
                    rightWallRaw;


                // =================================================
                // Straight Wall
                //
                // Door opening removes the middle section.
                //
                // Endpoint Join regions are then explicitly removed
                // so Straight Wall cannot overlap Mitre geometry.
                // =================================================

                float topDoorJoinMask =
                    saturate(
                        max(
                            topDoorLeftJoin,
                            topDoorRightJoin
                        )
                    );

                float bottomDoorJoinMask =
                    saturate(
                        max(
                            bottomDoorLeftJoin,
                            bottomDoorRightJoin
                        )
                    );

                float leftDoorJoinMask =
                    saturate(
                        max(
                            leftDoorBottomJoin,
                            leftDoorTopJoin
                        )
                    );

                float rightDoorJoinMask =
                    saturate(
                        max(
                            rightDoorBottomJoin,
                            rightDoorTopJoin
                        )
                    );


                float leftWall =
                    leftWallRaw *
                    (1.0 - leftDoorOpening) *
                    (1.0 - leftDoorJoinMask);

                float rightWall =
                    rightWallRaw *
                    (1.0 - rightDoorOpening) *
                    (1.0 - rightDoorJoinMask);

                float bottomWall =
                    bottomWallRaw *
                    (1.0 - bottomDoorOpening) *
                    (1.0 - bottomDoorJoinMask);

                float topWall =
                    topWallRaw *
                    (1.0 - topDoorOpening) *
                    (1.0 - topDoorJoinMask);


                // =================================================
                // Door Wrap Bodies
                //
                // Join region is removed from each Wrap as well.
                // =================================================

                float topDoorLeftWrapBody =
                    topDoorLeftWrap *
                    (1.0 - topDoorLeftJoin);

                float topDoorRightWrapBody =
                    topDoorRightWrap *
                    (1.0 - topDoorRightJoin);


                float bottomDoorLeftWrapBody =
                    bottomDoorLeftWrap *
                    (1.0 - bottomDoorLeftJoin);

                float bottomDoorRightWrapBody =
                    bottomDoorRightWrap *
                    (1.0 - bottomDoorRightJoin);


                float leftDoorBottomWrapBody =
                    leftDoorBottomWrap *
                    (1.0 - leftDoorBottomJoin);

                float leftDoorTopWrapBody =
                    leftDoorTopWrap *
                    (1.0 - leftDoorTopJoin);


                float rightDoorBottomWrapBody =
                    rightDoorBottomWrap *
                    (1.0 - rightDoorBottomJoin);

                float rightDoorTopWrapBody =
                    rightDoorTopWrap *
                    (1.0 - rightDoorTopJoin);


                // =================================================
                // Main Wall Lighting
                // =================================================

                float mainDarkWallFactor = 0.0;
                float mainLightWallFactor = 0.0;


                // -------------------------------------------------
                // Straight outer corner: Top Right
                //
                // Top Dark / Right Light
                // -------------------------------------------------

                float topRightOverlap =
                    topWall *
                    rightWall;

                float topRightDarkSplit =
                    step(
                        topDistance,
                        rightDistance
                    );


                mainDarkWallFactor =
                    max(
                        mainDarkWallFactor,
                        topWall *
                        (1.0 - topRightOverlap)
                    );

                mainDarkWallFactor =
                    max(
                        mainDarkWallFactor,
                        topRightOverlap *
                        topRightDarkSplit
                    );


                mainLightWallFactor =
                    max(
                        mainLightWallFactor,
                        rightWall *
                        (1.0 - topRightOverlap)
                    );

                mainLightWallFactor =
                    max(
                        mainLightWallFactor,
                        topRightOverlap *
                        (1.0 - topRightDarkSplit)
                    );


                // -------------------------------------------------
                // Straight outer corner: Bottom Left
                //
                // Left Dark / Bottom Light
                // -------------------------------------------------

                float bottomLeftOverlap =
                    leftWall *
                    bottomWall;

                float bottomLeftDarkSplit =
                    step(
                        leftDistance,
                        bottomDistance
                    );


                mainDarkWallFactor =
                    max(
                        mainDarkWallFactor,
                        leftWall *
                        (1.0 - bottomLeftOverlap)
                    );

                mainDarkWallFactor =
                    max(
                        mainDarkWallFactor,
                        bottomLeftOverlap *
                        bottomLeftDarkSplit
                    );


                mainLightWallFactor =
                    max(
                        mainLightWallFactor,
                        bottomWall *
                        (1.0 - bottomLeftOverlap)
                    );

                mainLightWallFactor =
                    max(
                        mainLightWallFactor,
                        bottomLeftOverlap *
                        (1.0 - bottomLeftDarkSplit)
                    );


                // Same-color outer corners.

                mainDarkWallFactor =
                    max(
                        mainDarkWallFactor,
                        leftWall *
                        topWall
                    );

                mainLightWallFactor =
                    max(
                        mainLightWallFactor,
                        rightWall *
                        bottomWall
                    );


                // =================================================
                // Original Inner Corner Geometry
                //
                // No Door-aware modification here.
                // =================================================

                float bottomLeftWall = CalculateSquareBand(
                    leftDistance,
                    bottomDistance,
                    bottomLeftInner,
                    borderEnd,
                    wallWidth,
                    antiAliasWidth
                );

                float bottomRightWall = CalculateSquareBand(
                    rightDistance,
                    bottomDistance,
                    bottomRightInner,
                    borderEnd,
                    wallWidth,
                    antiAliasWidth
                );

                float topLeftWall = CalculateSquareBand(
                    leftDistance,
                    topDistance,
                    topLeftInner,
                    borderEnd,
                    wallWidth,
                    antiAliasWidth
                );

                float topRightWall = CalculateSquareBand(
                    rightDistance,
                    topDistance,
                    topRightInner,
                    borderEnd,
                    wallWidth,
                    antiAliasWidth
                );


                // Top Left = Dark.

                mainDarkWallFactor =
                    max(
                        mainDarkWallFactor,
                        topLeftWall
                    );


                // Bottom Right = Light.

                mainLightWallFactor =
                    max(
                        mainLightWallFactor,
                        bottomRightWall
                    );


                // Bottom Left = Dark / Light split.

                float bottomLeftInnerDarkSplit =
                    step(
                        leftDistance,
                        bottomDistance
                    );

                mainDarkWallFactor =
                    max(
                        mainDarkWallFactor,
                        bottomLeftWall *
                        bottomLeftInnerDarkSplit
                    );

                mainLightWallFactor =
                    max(
                        mainLightWallFactor,
                        bottomLeftWall *
                        (1.0 - bottomLeftInnerDarkSplit)
                    );


                // Top Right = Dark / Light split.

                float topRightInnerDarkSplit =
                    step(
                        topDistance,
                        rightDistance
                    );

                mainDarkWallFactor =
                    max(
                        mainDarkWallFactor,
                        topRightWall *
                        topRightInnerDarkSplit
                    );

                mainLightWallFactor =
                    max(
                        mainLightWallFactor,
                        topRightWall *
                        (1.0 - topRightInnerDarkSplit)
                    );


                // =================================================
                // Door Wall Lighting
                //
                // Door Wrap and Door Mitre are separated from the
                // normal Wall so they can later render over the cut
                // OuterBorder without changing normal wall priority.
                // =================================================

                float doorDarkWallFactor = 0.0;
                float doorLightWallFactor = 0.0;


                // =================================================
                // TOP DOOR
                // =================================================

                // Left wrap = Dark.

                doorDarkWallFactor =
                    max(
                        doorDarkWallFactor,
                        topDoorLeftWrapBody
                    );


                /*
                 * Left Join:
                 *
                 * Top = Dark
                 * Left Wrap = Dark
                 *
                 * Entire Join is Dark.
                 */

                doorDarkWallFactor =
                    max(
                        doorDarkWallFactor,
                        topDoorLeftJoin
                    );


                // Right wrap = Light.

                doorLightWallFactor =
                    max(
                        doorLightWallFactor,
                        topDoorRightWrapBody
                    );


                /*
                 * Right Join:
                 *
                 * Top Dark
                 * Right Wrap Light
                 */

                float topDoorRightDistanceToTop =
                    max(
                        topDistance -
                        borderEnd,
                        0.0
                    );

                float topDoorRightDistanceToSide =
                    max(
                        doorMaximum -
                        cellUV.x,
                        0.0
                    );

                float topDoorRightDarkSplit =
                    step(
                        topDoorRightDistanceToTop,
                        topDoorRightDistanceToSide
                    );


                doorDarkWallFactor =
                    max(
                        doorDarkWallFactor,
                        topDoorRightJoin *
                        topDoorRightDarkSplit
                    );

                doorLightWallFactor =
                    max(
                        doorLightWallFactor,
                        topDoorRightJoin *
                        (1.0 - topDoorRightDarkSplit)
                    );


                // =================================================
                // BOTTOM DOOR
                // =================================================

                // Left wrap = Dark.

                doorDarkWallFactor =
                    max(
                        doorDarkWallFactor,
                        bottomDoorLeftWrapBody
                    );


                /*
                 * Left Join:
                 *
                 * Left Wrap Dark
                 * Bottom Light
                 */

                float bottomDoorLeftDistanceToSide =
                    max(
                        cellUV.x -
                        doorMinimum,
                        0.0
                    );

                float bottomDoorLeftDistanceToBottom =
                    max(
                        bottomDistance -
                        borderEnd,
                        0.0
                    );

                float bottomDoorLeftDarkSplit =
                    step(
                        bottomDoorLeftDistanceToSide,
                        bottomDoorLeftDistanceToBottom
                    );


                doorDarkWallFactor =
                    max(
                        doorDarkWallFactor,
                        bottomDoorLeftJoin *
                        bottomDoorLeftDarkSplit
                    );

                doorLightWallFactor =
                    max(
                        doorLightWallFactor,
                        bottomDoorLeftJoin *
                        (1.0 - bottomDoorLeftDarkSplit)
                    );


                // Right wrap = Light.

                doorLightWallFactor =
                    max(
                        doorLightWallFactor,
                        bottomDoorRightWrapBody
                    );


                // Right Join = Light + Light.

                doorLightWallFactor =
                    max(
                        doorLightWallFactor,
                        bottomDoorRightJoin
                    );


                // =================================================
                // LEFT DOOR
                // =================================================

                // Top wrap = Dark.

                doorDarkWallFactor =
                    max(
                        doorDarkWallFactor,
                        leftDoorTopWrapBody
                    );


                // Top Join = Dark + Dark.

                doorDarkWallFactor =
                    max(
                        doorDarkWallFactor,
                        leftDoorTopJoin
                    );


                // Bottom wrap = Light.

                doorLightWallFactor =
                    max(
                        doorLightWallFactor,
                        leftDoorBottomWrapBody
                    );


                /*
                 * Bottom Join:
                 *
                 * Left Dark
                 * Bottom Wrap Light
                 */

                float leftDoorBottomDistanceToLeft =
                    max(
                        leftDistance -
                        borderEnd,
                        0.0
                    );

                float leftDoorBottomDistanceToSide =
                    max(
                        cellUV.y -
                        doorMinimum,
                        0.0
                    );

                float leftDoorBottomDarkSplit =
                    step(
                        leftDoorBottomDistanceToLeft,
                        leftDoorBottomDistanceToSide
                    );


                doorDarkWallFactor =
                    max(
                        doorDarkWallFactor,
                        leftDoorBottomJoin *
                        leftDoorBottomDarkSplit
                    );

                doorLightWallFactor =
                    max(
                        doorLightWallFactor,
                        leftDoorBottomJoin *
                        (1.0 - leftDoorBottomDarkSplit)
                    );


                // =================================================
                // RIGHT DOOR
                // =================================================

                // Bottom wrap = Light.

                doorLightWallFactor =
                    max(
                        doorLightWallFactor,
                        rightDoorBottomWrapBody
                    );


                // Bottom Join = Light + Light.

                doorLightWallFactor =
                    max(
                        doorLightWallFactor,
                        rightDoorBottomJoin
                    );


                // Top wrap = Dark.

                doorDarkWallFactor =
                    max(
                        doorDarkWallFactor,
                        rightDoorTopWrapBody
                    );


                /*
                 * Top Join:
                 *
                 * Top Wrap Dark
                 * Right Light
                 */

                float rightDoorTopDistanceToSide =
                    max(
                        doorMaximum -
                        cellUV.y,
                        0.0
                    );

                float rightDoorTopDistanceToRight =
                    max(
                        rightDistance -
                        borderEnd,
                        0.0
                    );

                float rightDoorTopDarkSplit =
                    step(
                        rightDoorTopDistanceToSide,
                        rightDoorTopDistanceToRight
                    );


                doorDarkWallFactor =
                    max(
                        doorDarkWallFactor,
                        rightDoorTopJoin *
                        rightDoorTopDarkSplit
                    );

                doorLightWallFactor =
                    max(
                        doorLightWallFactor,
                        rightDoorTopJoin *
                        (1.0 - rightDoorTopDarkSplit)
                    );


                // =================================================
                // Resolve Main Wall
                // =================================================

                mainDarkWallFactor =
                    saturate(
                        mainDarkWallFactor
                    );

                mainLightWallFactor =
                    saturate(
                        mainLightWallFactor *
                        (1.0 - mainDarkWallFactor)
                    );

                float mainWallFactor =
                    saturate(
                        max(
                            mainDarkWallFactor,
                            mainLightWallFactor
                        )
                    );


                // =================================================
                // Resolve Door Wall
                // =================================================

                doorDarkWallFactor =
                    saturate(
                        doorDarkWallFactor
                    );

                doorLightWallFactor =
                    saturate(
                        doorLightWallFactor *
                        (1.0 - doorDarkWallFactor)
                    );

                float doorWallFactor =
                    saturate(
                        max(
                            doorDarkWallFactor,
                            doorLightWallFactor
                        )
                    );


                // =================================================
                // Room-side Hard Shadow
                // =================================================

                float leftRoomShadow = CalculateEdgeBand(
                    leftDistance,
                    leftEnabled,
                    wallEnd,
                    roomShadowWidth,
                    antiAliasWidth
                ) * (1.0 - leftDoorOpening);

                float rightRoomShadow = CalculateEdgeBand(
                    rightDistance,
                    rightEnabled,
                    wallEnd,
                    roomShadowWidth,
                    antiAliasWidth
                ) * (1.0 - rightDoorOpening);

                float bottomRoomShadow = CalculateEdgeBand(
                    bottomDistance,
                    bottomEnabled,
                    wallEnd,
                    roomShadowWidth,
                    antiAliasWidth
                ) * (1.0 - bottomDoorOpening);

                float topRoomShadow = CalculateEdgeBand(
                    topDistance,
                    topEnabled,
                    wallEnd,
                    roomShadowWidth,
                    antiAliasWidth
                ) * (1.0 - topDoorOpening);


                /*
                 * InnerCorner shadow also stays on the original
                 * topology logic.
                 */

                float bottomLeftRoomShadow = CalculateSquareBand(
                    leftDistance,
                    bottomDistance,
                    bottomLeftInner,
                    wallEnd,
                    roomShadowWidth,
                    antiAliasWidth
                );

                float bottomRightRoomShadow = CalculateSquareBand(
                    rightDistance,
                    bottomDistance,
                    bottomRightInner,
                    wallEnd,
                    roomShadowWidth,
                    antiAliasWidth
                );

                float topLeftRoomShadow = CalculateSquareBand(
                    leftDistance,
                    topDistance,
                    topLeftInner,
                    wallEnd,
                    roomShadowWidth,
                    antiAliasWidth
                );

                float topRightRoomShadow = CalculateSquareBand(
                    rightDistance,
                    topDistance,
                    topRightInner,
                    wallEnd,
                    roomShadowWidth,
                    antiAliasWidth
                );


                float roomShadowFactor =
                    saturate(
                        max(
                            max(
                                max(
                                    leftRoomShadow,
                                    rightRoomShadow
                                ),
                                max(
                                    bottomRoomShadow,
                                    topRoomShadow
                                )
                            ),
                            max(
                                max(
                                    bottomLeftRoomShadow,
                                    bottomRightRoomShadow
                                ),
                                max(
                                    topLeftRoomShadow,
                                    topRightRoomShadow
                                )
                            )
                        )
                    );


                // =================================================
                // Colors
                // =================================================

                half4 fillColor = input.fillColor;
                half4 outerBorderColor = input.borderColor;

                half4 wallBaseColor =
                    lerp(
                        outerBorderColor,
                        fillColor,
                        _WallColorBlend
                    );

                half3 wallDarkColor =
                    lerp(
                        wallBaseColor.rgb,
                        _WallDarkColor.rgb,
                        _WallDarkStrength *
                        _WallDarkColor.a
                    );

                half3 wallLightColor =
                    lerp(
                        wallBaseColor.rgb,
                        _WallLightColor.rgb,
                        _WallLightStrength *
                        _WallLightColor.a
                    );


                // =================================================
                // Composition
                //
                // Main Wall stays below OuterBorder.
                //
                // Only Door Wall renders above OuterBorder, because
                // it is specifically responsible for wrapping the
                // cut face of the frame.
                // =================================================

                half4 finalColor = fillColor;


                // Room shadow.

                finalColor.rgb =
                    lerp(
                        finalColor.rgb,
                        _RoomShadowColor.rgb,
                        roomShadowFactor *
                        _RoomShadowStrength *
                        _RoomShadowColor.a
                    );


                // Main wall base.

                finalColor =
                    lerp(
                        finalColor,
                        wallBaseColor,
                        mainWallFactor
                    );


                // Main dark surfaces.

                finalColor.rgb =
                    lerp(
                        finalColor.rgb,
                        wallDarkColor,
                        mainDarkWallFactor
                    );


                // Main light surfaces.

                finalColor.rgb =
                    lerp(
                        finalColor.rgb,
                        wallLightColor,
                        mainLightWallFactor
                    );


                // Normal OuterBorder remains above normal Wall.

                finalColor =
                    lerp(
                        finalColor,
                        outerBorderColor,
                        outerBorderFactor
                    );


                /*
                 * Door Wall alone renders over OuterBorder.
                 *
                 * This allows the Door Wrap to cover the exposed
                 * cut face without changing the rendering priority
                 * of every normal Wall.
                 */

                finalColor =
                    lerp(
                        finalColor,
                        wallBaseColor,
                        doorWallFactor
                    );


                finalColor.rgb =
                    lerp(
                        finalColor.rgb,
                        wallDarkColor,
                        doorDarkWallFactor
                    );


                finalColor.rgb =
                    lerp(
                        finalColor.rgb,
                        wallLightColor,
                        doorLightWallFactor
                    );


                // Transparent Gap remains final.

                finalColor.a *=
                    1.0 -
                    gapFactor;


                return finalColor;
            }

            ENDHLSL
        }
    }

    Fallback Off
}