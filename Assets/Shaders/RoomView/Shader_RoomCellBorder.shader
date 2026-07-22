Shader "Game/Dungeon/RoomCellBorderChunkParadox"
{
    Properties
    {
        [Header(Outer Border)]
        _BorderWidth("Outer Border Width", Range(0, 0.5)) = 0.08
        _GapWidth("Transparent Gap Width", Range(0, 0.2)) = 0.02

        [Header(Wall)]
        _WallWidth("Wall Width", Range(0, 0.4)) = 0.08
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
                float4 topology : TEXCOORD1;
                float4 fillColor : TEXCOORD2;
                float4 borderColor : TEXCOORD3;
                float4 wallBaseColor : TEXCOORD4;
                float4 wallDarkColor : TEXCOORD5;
                float4 wallLightColor : TEXCOORD6;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 cellUV : TEXCOORD0;
                nointerpolation float4 topology : TEXCOORD1;
                float4 fillColor : TEXCOORD2;
                float4 borderColor : TEXCOORD3;
                float4 wallBaseColor : TEXCOORD4;
                float4 wallDarkColor : TEXCOORD5;
                float4 wallLightColor : TEXCOORD6;
            };

            CBUFFER_START(UnityPerMaterial)
                float _BorderWidth;
                float _GapWidth;
                float _WallWidth;
                float _RoomShadowWidth;
                half4 _RoomShadowColor;
                float _RoomShadowStrength;
                float _DoorSize;
            CBUFFER_END

            // Edge order:   x = Left, y = Right, z = Bottom, w = Top
            // Corner order: x = BottomLeft, y = BottomRight, z = TopLeft, w = TopRight

            float Max4(float4 value)
            {
                return max(max(value.x, value.y), max(value.z, value.w));
            }

            float HasMaskBit(float mask, float bitValue)
            {
                float divided = floor(mask / bitValue);
                return step(0.5, fmod(divided, 2.0));
            }

            float4 DecodeMask4(float mask)
            {
                return float4(
                    HasMaskBit(mask, 1.0),
                    HasMaskBit(mask, 2.0),
                    HasMaskBit(mask, 4.0),
                    HasMaskBit(mask, 8.0)
                );
            }

            float CalculateEdgeZone(float distanceToEdge, float enabled, float width, float antiAliasWidth)
            {
                float hasWidth = step(0.00001, width);

                #if defined(_ROOM_BORDER_AA_ON)
                    float zone = 1.0 - smoothstep(width - antiAliasWidth, width + antiAliasWidth, distanceToEdge);
                #else
                    float zone = step(distanceToEdge, width);
                #endif

                return zone * enabled * hasWidth;
            }

            float4 CalculateEdgeZone4(float4 distanceToEdge, float4 enabled, float width, float antiAliasWidth)
            {
                return float4(
                    CalculateEdgeZone(distanceToEdge.x, enabled.x, width, antiAliasWidth),
                    CalculateEdgeZone(distanceToEdge.y, enabled.y, width, antiAliasWidth),
                    CalculateEdgeZone(distanceToEdge.z, enabled.z, width, antiAliasWidth),
                    CalculateEdgeZone(distanceToEdge.w, enabled.w, width, antiAliasWidth)
                );
            }

            float CalculateEdgeBand(float distanceToEdge, float enabled, float startDistance, float bandWidth, float antiAliasWidth)
            {
                float hasBandWidth = step(0.00001, bandWidth);
                float outerZone = CalculateEdgeZone(distanceToEdge, enabled, startDistance + bandWidth, antiAliasWidth);
                float innerZone = CalculateEdgeZone(distanceToEdge, enabled, startDistance, antiAliasWidth);
                return saturate(outerZone - innerZone) * hasBandWidth;
            }

            float4 CalculateEdgeBand4(float4 distanceToEdge, float4 enabled, float startDistance, float bandWidth, float antiAliasWidth)
            {
                return float4(
                    CalculateEdgeBand(distanceToEdge.x, enabled.x, startDistance, bandWidth, antiAliasWidth),
                    CalculateEdgeBand(distanceToEdge.y, enabled.y, startDistance, bandWidth, antiAliasWidth),
                    CalculateEdgeBand(distanceToEdge.z, enabled.z, startDistance, bandWidth, antiAliasWidth),
                    CalculateEdgeBand(distanceToEdge.w, enabled.w, startDistance, bandWidth, antiAliasWidth)
                );
            }

            float CalculateCenteredDoorOpening(float axisCoordinate, float enabled, float doorSize, float antiAliasWidth)
            {
                float hasDoor = step(0.00001, doorSize);
                float halfDoorSize = doorSize * 0.5;
                float distanceToCenter = abs(axisCoordinate - 0.5);

                #if defined(_ROOM_BORDER_AA_ON)
                    float opening = 1.0 - smoothstep(halfDoorSize - antiAliasWidth, halfDoorSize + antiAliasWidth, distanceToCenter);
                #else
                    float opening = step(distanceToCenter, halfDoorSize);
                #endif

                return opening * enabled * hasDoor;
            }

            float4 CalculateCenteredDoorOpening4(float4 axisCoordinate, float4 enabled, float doorSize, float antiAliasWidth)
            {
                return float4(
                    CalculateCenteredDoorOpening(axisCoordinate.x, enabled.x, doorSize, antiAliasWidth),
                    CalculateCenteredDoorOpening(axisCoordinate.y, enabled.y, doorSize, antiAliasWidth),
                    CalculateCenteredDoorOpening(axisCoordinate.z, enabled.z, doorSize, antiAliasWidth),
                    CalculateCenteredDoorOpening(axisCoordinate.w, enabled.w, doorSize, antiAliasWidth)
                );
            }

            float CalculateSquareZone(float horizontalDistance, float verticalDistance, float enabled, float width, float antiAliasWidth)
            {
                float horizontalZone = CalculateEdgeZone(horizontalDistance, enabled, width, antiAliasWidth);
                float verticalZone = CalculateEdgeZone(verticalDistance, enabled, width, antiAliasWidth);
                return min(horizontalZone, verticalZone);
            }

            float4 CalculateCornerZone4(float4 horizontalDistance, float4 verticalDistance, float4 enabled, float width, float antiAliasWidth)
            {
                return float4(
                    CalculateSquareZone(horizontalDistance.x, verticalDistance.x, enabled.x, width, antiAliasWidth),
                    CalculateSquareZone(horizontalDistance.y, verticalDistance.y, enabled.y, width, antiAliasWidth),
                    CalculateSquareZone(horizontalDistance.z, verticalDistance.z, enabled.z, width, antiAliasWidth),
                    CalculateSquareZone(horizontalDistance.w, verticalDistance.w, enabled.w, width, antiAliasWidth)
                );
            }

            float CalculateSquareBand(float horizontalDistance, float verticalDistance, float enabled, float startDistance, float bandWidth, float antiAliasWidth)
            {
                float hasBandWidth = step(0.00001, bandWidth);
                float outerZone = CalculateSquareZone(horizontalDistance, verticalDistance, enabled, startDistance + bandWidth, antiAliasWidth);
                float innerZone = CalculateSquareZone(horizontalDistance, verticalDistance, enabled, startDistance, antiAliasWidth);
                return saturate(outerZone - innerZone) * hasBandWidth;
            }

            float4 CalculateCornerBand4(float4 horizontalDistance, float4 verticalDistance, float4 enabled, float startDistance, float bandWidth, float antiAliasWidth)
            {
                return float4(
                    CalculateSquareBand(horizontalDistance.x, verticalDistance.x, enabled.x, startDistance, bandWidth, antiAliasWidth),
                    CalculateSquareBand(horizontalDistance.y, verticalDistance.y, enabled.y, startDistance, bandWidth, antiAliasWidth),
                    CalculateSquareBand(horizontalDistance.z, verticalDistance.z, enabled.z, startDistance, bandWidth, antiAliasWidth),
                    CalculateSquareBand(horizontalDistance.w, verticalDistance.w, enabled.w, startDistance, bandWidth, antiAliasWidth)
                );
            }

            float CalculateRangeZone(float coordinate, float minimumValue, float maximumValue, float antiAliasWidth)
            {
                float hasWidth = step(0.00001, maximumValue - minimumValue);

                #if defined(_ROOM_BORDER_AA_ON)
                    float lower = smoothstep(minimumValue - antiAliasWidth, minimumValue + antiAliasWidth, coordinate);
                    float upper = 1.0 - smoothstep(maximumValue - antiAliasWidth, maximumValue + antiAliasWidth, coordinate);
                    return lower * upper * hasWidth;
                #else
                    return step(minimumValue, coordinate) * step(coordinate, maximumValue) * hasWidth;
                #endif
            }

            // Returns 1 on the top-left side and 0 on the bottom-right side.
            float CalculateTopLeftLightSplit(float2 samplePosition, float2 minimumValue, float2 maximumValue, float antiAliasWidth)
            {
                float2 size = max(maximumValue - minimumValue, float2(0.00001, 0.00001));
                float2 localUV = saturate((samplePosition - minimumValue) / size);
                float diagonal = localUV.y - localUV.x;

                #if defined(_ROOM_BORDER_AA_ON)
                    float normalizedAA = max(antiAliasWidth / max(size.x, size.y), 0.0001);
                    return smoothstep(-normalizedAA, normalizedAA, diagonal);
                #else
                    return step(localUV.x, localUV.y);
                #endif
            }

            // Returns x = Dark contribution, y = Light contribution.
            // This preserves the original max-based Body/Join composition.
            float2 EvaluateDoorDiagonalEndpoint(float wrap, float joinZone, float lightSplit, float bodyIsLight)
            {
                float join = wrap * joinZone;
                float body = wrap * (1.0 - joinZone);

                float dark = max(body * (1.0 - bodyIsLight), join * (1.0 - lightSplit));
                float light = max(body * bodyIsLight, join * lightSplit);

                return float2(dark, light);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.cellUV = input.cellUV;
                output.topology = input.topology;
                output.fillColor = input.fillColor;
                output.borderColor = input.borderColor;
                output.wallBaseColor = input.wallBaseColor;
                output.wallDarkColor = input.wallDarkColor;
                output.wallLightColor = input.wallLightColor;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 cellUV = saturate(input.cellUV);

                float borderMask = round(input.topology.x);
                float innerCornerMask = round(input.topology.y);
                float doorMask = round(input.topology.z);

                float gapWidth = clamp(_GapWidth, 0.0, 0.49);
                float borderWidth = clamp(_BorderWidth, 0.0, 0.5 - gapWidth);
                float borderEnd = gapWidth + borderWidth;
                float wallWidth = clamp(_WallWidth, 0.0, 0.5 - borderEnd);
                float wallEnd = borderEnd + wallWidth;
                float roomShadowWidth = clamp(_RoomShadowWidth, 0.0, 0.5 - wallEnd);

                float antiAliasWidth = 0.0;

                #if defined(_ROOM_BORDER_AA_ON)
                    antiAliasWidth = max(max(fwidth(cellUV.x), fwidth(cellUV.y)), 0.0001);
                #endif

                // -------------------------------------------------
                // Shared topology and distance data
                // -------------------------------------------------

                float leftDistance = cellUV.x;
                float rightDistance = 1.0 - cellUV.x;
                float bottomDistance = cellUV.y;
                float topDistance = 1.0 - cellUV.y;

                float4 edgeDistance = float4(leftDistance, rightDistance, bottomDistance, topDistance);
                float4 cornerHorizontalDistance = float4(leftDistance, rightDistance, leftDistance, rightDistance);
                float4 cornerVerticalDistance = float4(bottomDistance, bottomDistance, topDistance, topDistance);

                float4 edgeEnabled = DecodeMask4(borderMask);
                float4 cornerEnabled = DecodeMask4(innerCornerMask);
                float4 doorEnabled = DecodeMask4(doorMask);

                float doorSize = clamp(_DoorSize, 0.0, 0.9999);
                float halfDoorSize = doorSize * 0.5;
                float doorMinimum = 0.5 - halfDoorSize;
                float doorMaximum = 0.5 + halfDoorSize;

                float4 doorAxis = float4(cellUV.y, cellUV.y, cellUV.x, cellUV.x);
                float4 doorOpening = CalculateCenteredDoorOpening4(doorAxis, doorEnabled, doorSize, antiAliasWidth);

                // =================================================
                // Gap
                // =================================================

                float4 edgeGap = CalculateEdgeZone4(edgeDistance, edgeEnabled, gapWidth, antiAliasWidth) * (1.0 - doorOpening);
                float4 cornerGap = CalculateCornerZone4(cornerHorizontalDistance, cornerVerticalDistance, cornerEnabled, gapWidth, antiAliasWidth);
                float gapFactor = saturate(max(Max4(edgeGap), Max4(cornerGap)));

                // =================================================
                // OuterBorder
                // =================================================

                float4 edgeBorder = CalculateEdgeBand4(edgeDistance, edgeEnabled, gapWidth, borderWidth, antiAliasWidth) * (1.0 - doorOpening);
                float4 cornerBorder = CalculateCornerBand4(cornerHorizontalDistance, cornerVerticalDistance, cornerEnabled, gapWidth, borderWidth, antiAliasWidth);
                float outerBorderFactor = saturate(max(Max4(edgeBorder), Max4(cornerBorder)));

                // =================================================
                // Main Wall
                // =================================================

                float4 wallRaw = CalculateEdgeBand4(edgeDistance, edgeEnabled, borderEnd, wallWidth, antiAliasWidth);
                float4 wall = wallRaw * (1.0 - doorOpening);

                float mainDarkWallFactor = 0.0;
                float mainLightWallFactor = 0.0;

                // Top-right outer corner: Top dark / Right light.
                float topRightOverlap = min(wall.w, wall.y);
                float topRightDarkSplit = step(topDistance, rightDistance);

                mainDarkWallFactor = max(mainDarkWallFactor, wall.w * (1.0 - topRightOverlap));
                mainDarkWallFactor = max(mainDarkWallFactor, topRightOverlap * topRightDarkSplit);
                mainLightWallFactor = max(mainLightWallFactor, wall.y * (1.0 - topRightOverlap));
                mainLightWallFactor = max(mainLightWallFactor, topRightOverlap * (1.0 - topRightDarkSplit));

                // Bottom-left outer corner: Left dark / Bottom light.
                float bottomLeftOverlap = min(wall.x, wall.z);
                float bottomLeftDarkSplit = step(leftDistance, bottomDistance);

                mainDarkWallFactor = max(mainDarkWallFactor, wall.x * (1.0 - bottomLeftOverlap));
                mainDarkWallFactor = max(mainDarkWallFactor, bottomLeftOverlap * bottomLeftDarkSplit);
                mainLightWallFactor = max(mainLightWallFactor, wall.z * (1.0 - bottomLeftOverlap));
                mainLightWallFactor = max(mainLightWallFactor, bottomLeftOverlap * (1.0 - bottomLeftDarkSplit));

                // Same-color outer corners.
                mainDarkWallFactor = max(mainDarkWallFactor, wall.x * wall.w);
                mainLightWallFactor = max(mainLightWallFactor, wall.y * wall.z);

                // =================================================
                // L-shaped Inner Corner Wall
                //
                // Outward-expanded lighting:
                // TopLeft     = Dark
                // TopRight    = Top Light / Right Dark
                // BottomLeft  = Left Light / Bottom Dark
                // BottomRight = Light
                // =================================================

                float4 cornerWall = CalculateCornerBand4(
                    cornerHorizontalDistance,
                    cornerVerticalDistance,
                    cornerEnabled,
                    borderEnd,
                    wallWidth,
                    antiAliasWidth
                );

                mainDarkWallFactor = max(mainDarkWallFactor, cornerWall.z);
                mainLightWallFactor = max(mainLightWallFactor, cornerWall.y);

                float bottomLeftInnerLeftSplit = step(leftDistance, bottomDistance);
                mainLightWallFactor = max(mainLightWallFactor, cornerWall.x * bottomLeftInnerLeftSplit);
                mainDarkWallFactor = max(mainDarkWallFactor, cornerWall.x * (1.0 - bottomLeftInnerLeftSplit));

                float topRightInnerTopSplit = step(topDistance, rightDistance);
                mainLightWallFactor = max(mainLightWallFactor, cornerWall.w * topRightInnerTopSplit);
                mainDarkWallFactor = max(mainDarkWallFactor, cornerWall.w * (1.0 - topRightInnerTopSplit));

                // =================================================
                // Outer-expanding DoorWrap
                // =================================================

                float doorWrapWidth = min(wallWidth, halfDoorSize);
                float4 doorDepth = CalculateEdgeZone4(edgeDistance, doorEnabled, wallEnd, antiAliasWidth);

                float topDoorLeftWrap = doorDepth.w * CalculateRangeZone(cellUV.x, doorMinimum, doorMinimum + doorWrapWidth, antiAliasWidth);
                float topDoorRightWrap = doorDepth.w * CalculateRangeZone(cellUV.x, doorMaximum - doorWrapWidth, doorMaximum, antiAliasWidth);

                float bottomDoorLeftWrap = doorDepth.z * CalculateRangeZone(cellUV.x, doorMinimum, doorMinimum + doorWrapWidth, antiAliasWidth);
                float bottomDoorRightWrap = doorDepth.z * CalculateRangeZone(cellUV.x, doorMaximum - doorWrapWidth, doorMaximum, antiAliasWidth);

                float leftDoorBottomWrap = doorDepth.x * CalculateRangeZone(cellUV.y, doorMinimum, doorMinimum + doorWrapWidth, antiAliasWidth);
                float leftDoorTopWrap = doorDepth.x * CalculateRangeZone(cellUV.y, doorMaximum - doorWrapWidth, doorMaximum, antiAliasWidth);

                float rightDoorBottomWrap = doorDepth.y * CalculateRangeZone(cellUV.y, doorMinimum, doorMinimum + doorWrapWidth, antiAliasWidth);
                float rightDoorTopWrap = doorDepth.y * CalculateRangeZone(cellUV.y, doorMaximum - doorWrapWidth, doorMaximum, antiAliasWidth);

                // =================================================
                // Door Endpoint Lighting
                //
                // Pure Dark:  TopDoor.Left, LeftDoor.Top
                // Pure Light: BottomDoor.Right, RightDoor.Bottom
                //
                // Diagonal endpoints use a square Join:
                // top-left = Light, bottom-right = Dark.
                // =================================================

                float doorJoinSize = doorWrapWidth;

                float topDoorRightJoinZone = CalculateRangeZone(cellUV.y, 1.0 - wallEnd, 1.0 - wallEnd + doorJoinSize, antiAliasWidth);
                float topDoorRightLightSplit = CalculateTopLeftLightSplit(
                    cellUV,
                    float2(doorMaximum - doorJoinSize, 1.0 - wallEnd),
                    float2(doorMaximum, 1.0 - wallEnd + doorJoinSize),
                    antiAliasWidth
                );
                float2 topDoorRightResult = EvaluateDoorDiagonalEndpoint(topDoorRightWrap, topDoorRightJoinZone, topDoorRightLightSplit, 1.0);

                float bottomDoorLeftJoinZone = CalculateRangeZone(cellUV.y, wallEnd - doorJoinSize, wallEnd, antiAliasWidth);
                float bottomDoorLeftLightSplit = CalculateTopLeftLightSplit(
                    cellUV,
                    float2(doorMinimum, wallEnd - doorJoinSize),
                    float2(doorMinimum + doorJoinSize, wallEnd),
                    antiAliasWidth
                );
                float2 bottomDoorLeftResult = EvaluateDoorDiagonalEndpoint(bottomDoorLeftWrap, bottomDoorLeftJoinZone, bottomDoorLeftLightSplit, 0.0);

                float leftDoorBottomJoinZone = CalculateRangeZone(cellUV.x, wallEnd - doorJoinSize, wallEnd, antiAliasWidth);
                float leftDoorBottomLightSplit = CalculateTopLeftLightSplit(
                    cellUV,
                    float2(wallEnd - doorJoinSize, doorMinimum),
                    float2(wallEnd, doorMinimum + doorJoinSize),
                    antiAliasWidth
                );
                float2 leftDoorBottomResult = EvaluateDoorDiagonalEndpoint(leftDoorBottomWrap, leftDoorBottomJoinZone, leftDoorBottomLightSplit, 1.0);

                float rightDoorTopJoinZone = CalculateRangeZone(cellUV.x, 1.0 - wallEnd, 1.0 - wallEnd + doorJoinSize, antiAliasWidth);
                float rightDoorTopLightSplit = CalculateTopLeftLightSplit(
                    cellUV,
                    float2(1.0 - wallEnd, doorMaximum - doorJoinSize),
                    float2(1.0 - wallEnd + doorJoinSize, doorMaximum),
                    antiAliasWidth
                );
                float2 rightDoorTopResult = EvaluateDoorDiagonalEndpoint(rightDoorTopWrap, rightDoorTopJoinZone, rightDoorTopLightSplit, 0.0);

                float doorDarkWallFactor = max(
                    max(topDoorLeftWrap, leftDoorTopWrap),
                    Max4(float4(
                        topDoorRightResult.x,
                        bottomDoorLeftResult.x,
                        leftDoorBottomResult.x,
                        rightDoorTopResult.x
                    ))
                );

                float doorLightWallFactor = max(
                    max(bottomDoorRightWrap, rightDoorBottomWrap),
                    Max4(float4(
                        topDoorRightResult.y,
                        bottomDoorLeftResult.y,
                        leftDoorBottomResult.y,
                        rightDoorTopResult.y
                    ))
                );

                // =================================================
                // Resolve Wall Factors
                // =================================================

                mainDarkWallFactor = saturate(mainDarkWallFactor);
                mainLightWallFactor = saturate(mainLightWallFactor * (1.0 - mainDarkWallFactor));
                float mainWallFactor = saturate(max(mainDarkWallFactor, mainLightWallFactor));

                doorDarkWallFactor = saturate(doorDarkWallFactor);
                doorLightWallFactor = saturate(doorLightWallFactor * (1.0 - doorDarkWallFactor));
                float doorWallFactor = saturate(max(doorDarkWallFactor, doorLightWallFactor));

                // =================================================
                // Room Shadow
                // =================================================

                float4 edgeRoomShadow = CalculateEdgeBand4(edgeDistance, edgeEnabled, wallEnd, roomShadowWidth, antiAliasWidth) * (1.0 - doorOpening);
                float4 cornerRoomShadow = CalculateCornerBand4(
                    cornerHorizontalDistance,
                    cornerVerticalDistance,
                    cornerEnabled,
                    wallEnd,
                    roomShadowWidth,
                    antiAliasWidth
                );

                float roomShadowFactor = saturate(max(Max4(edgeRoomShadow), Max4(cornerRoomShadow)));

                // =================================================
                // Colors and Composition
                //
                // All semantic colors are generated on CPU.
                // Shader only applies geometric coverage.
                //
                // Layer priority:
                // Fill < Main Wall < OuterBorder < DoorWrap
                // =================================================

                half4 fillColor = input.fillColor;
                half4 outerBorderColor = input.borderColor;
                half4 wallDarkColor = input.wallDarkColor;
                half4 wallLightColor = input.wallLightColor;

                // RoomData.RoomColor is passed directly as FillColor.
                // No color adjustment is applied to the room interior.
                half4 finalColor = fillColor;

                // =================================================
                // Main Wall
                //
                // Dark / Light colors are already final colors generated
                // by RoomCellColorFactory in OKLCH space.
                //
                // The factors below are only geometric / AA coverage.
                // =================================================

                finalColor = lerp(
                    finalColor,
                    wallDarkColor,
                    mainDarkWallFactor
                );

                finalColor = lerp(
                    finalColor,
                    wallLightColor,
                    mainLightWallFactor
                );

                // =================================================
                // Outer Border
                //
                // Border overrides normal Wall.
                // =================================================

                finalColor = lerp(
                    finalColor,
                    outerBorderColor,
                    outerBorderFactor
                );

                // =================================================
                // DoorWrap
                //
                // DoorWrap overrides OuterBorder.
                // =================================================

                finalColor = lerp(
                    finalColor,
                    wallDarkColor,
                    doorDarkWallFactor
                );

                finalColor = lerp(
                    finalColor,
                    wallLightColor,
                    doorLightWallFactor
                );

                // =================================================
                // Transparent Gap
                // =================================================

                finalColor.a *= 1.0 - gapFactor;

                return finalColor;
            }

            ENDHLSL
        }
    }

    Fallback Off
}
