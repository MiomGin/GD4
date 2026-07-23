Shader "Hidden/PostProcess/ObjectOutline"
{
    Properties
    {
        _OutlineColor(
            "Outline Color",
            Color
        ) = (0, 0, 0, 1)


        _OutlineWidth(
            "Outline Width Pixels",
            Range(1, 8)
        ) = 2


        [Toggle]
        _EnableGroupBoundary(
            "Enable Mask Group Outline",
            Float
        ) = 1
    }


    SubShader
    {
        Tags
        {
            "RenderPipeline" =
                "UniversalPipeline"
        }


        Cull Off

        ZWrite Off

        ZTest Always


        Pass
        {
            Name "Object Outline"


            HLSLPROGRAM


            #pragma vertex Vert

            #pragma fragment Frag

            #pragma target 3.5


            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"


            // ====================================================
            // Buffers
            // ====================================================

            TEXTURE2D_X(
                _ObjectInfoTexture
            );


            SAMPLER(
                sampler_ObjectInfoTexture
            );


            TEXTURE2D_X(
                _ObjectSortingTexture
            );


            SAMPLER(
                sampler_ObjectSortingTexture
            );


            float4
                _ObjectInfoTexture_TexelSize;


            float
                _UseObjectInfoBuffer;


            // ====================================================
            // Material
            // ====================================================

            CBUFFER_START(
                UnityPerMaterial
            )

                half4
                    _OutlineColor;


                float
                    _OutlineWidth;


                float
                    _EnableGroupBoundary;

            CBUFFER_END


            #define MAX_OUTLINE_WIDTH 8


            #define INDEPENDENT_OUTLINE_FLAG 1u


            // ====================================================
            // Data
            // ====================================================

            struct MaskInfo
            {
                uint id;

                uint flags;

                float sortingLayer;

                float sortingOrder;
            };


            // ====================================================
            // Decode
            // ====================================================

            MaskInfo SampleMaskInfo(
                float2 uv)
            {
                MaskInfo info;


                if (
                    uv.x < 0.0 ||
                    uv.x > 1.0 ||
                    uv.y < 0.0 ||
                    uv.y > 1.0
                )
                {
                    info.id =
                        0u;


                    info.flags =
                        0u;


                    info.sortingLayer =
                        0.0;


                    info.sortingOrder =
                        0.0;


                    return info;
                }


                float4 encoded =
                    SAMPLE_TEXTURE2D_X(
                        _ObjectInfoTexture,
                        sampler_ObjectInfoTexture,
                        uv
                    );


                uint r =
                    (uint)round(
                        saturate(
                            encoded.r
                        ) *
                        255.0
                    );


                uint g =
                    (uint)round(
                        saturate(
                            encoded.g
                        ) *
                        255.0
                    );


                uint b =
                    (uint)round(
                        saturate(
                            encoded.b
                        ) *
                        255.0
                    );


                info.id =
                    r +
                    g * 256u +
                    b * 65536u;


                info.flags =
                    (uint)round(
                        saturate(
                            encoded.a
                        ) *
                        255.0
                    );


                float2 sorting =
                    SAMPLE_TEXTURE2D_X(
                        _ObjectSortingTexture,
                        sampler_ObjectSortingTexture,
                        uv
                    ).rg;


                info.sortingLayer =
                    sorting.x;


                info.sortingOrder =
                    sorting.y;


                return info;
            }


            // ====================================================
            // Flags
            // ====================================================

            bool HasIndependentOutline(
                MaskInfo info)
            {
                return
                    (
                        info.flags &
                        INDEPENDENT_OUTLINE_FLAG
                    ) != 0u;
            }


            // ====================================================
            // Sorting Comparison
            //
            // Sorting Layer first.
            //
            // Order in Layer second.
            //
            // Equal priority:
            // no group boundary is generated.
            // ====================================================

            bool IsHigherSortingPriority(
                MaskInfo source,
                MaskInfo destination)
            {
                if (
                    source.sortingLayer >
                    destination.sortingLayer
                )
                {
                    return true;
                }


                if (
                    source.sortingLayer <
                    destination.sortingLayer
                )
                {
                    return false;
                }


                return
                    source.sortingOrder >
                    destination.sortingOrder;
            }


            // ====================================================
            // Independent Outline Expansion
            // ====================================================

            bool CanExpandInto(
                MaskInfo source,
                MaskInfo destination)
            {
                if (source.id == 0u)
                {
                    return false;
                }


                if (!HasIndependentOutline(
                        source))
                {
                    return false;
                }


                if (source.id ==
                    destination.id)
                {
                    return false;
                }


                // Background is handled by the global silhouette.
                if (destination.id == 0u)
                {
                    return false;
                }


                // The independent object's outline may only
                // cover a visually lower object.
                return
                    IsHigherSortingPriority(
                        source,
                        destination
                    );
            }


            // ====================================================
            // Fragment
            // ====================================================

            half4 Frag(
                Varyings input)
                : SV_Target
            {
                float2 uv =
                    input.texcoord;


                half4 sceneColor =
                    SAMPLE_TEXTURE2D_X(
                        _BlitTexture,
                        sampler_LinearClamp,
                        uv
                    );


                if (_UseObjectInfoBuffer <
                    0.5)
                {
                    return sceneColor;
                }


                MaskInfo center =
                    SampleMaskInfo(
                        uv
                    );


                float2 texelSize =
                    _ObjectInfoTexture_TexelSize.xy;


                int outlineWidth =
                    clamp(
                        (int)round(
                            _OutlineWidth
                        ),

                        1,

                        MAX_OUTLINE_WIDTH
                    );


                bool isOutline =
                    false;


                // =================================================
                // Neighbor Search
                // =================================================

                [loop]
                for (
                    int y =
                        -MAX_OUTLINE_WIDTH;

                    y <=
                        MAX_OUTLINE_WIDTH;

                    y++)
                {
                    if (abs(y) >
                        outlineWidth)
                    {
                        continue;
                    }


                    [loop]
                    for (
                        int x =
                            -MAX_OUTLINE_WIDTH;

                        x <=
                            MAX_OUTLINE_WIDTH;

                        x++)
                    {
                        if (abs(x) >
                            outlineWidth)
                        {
                            continue;
                        }


                        if (
                            x == 0 &&
                            y == 0
                        )
                        {
                            continue;
                        }


                        float2 sampleUV =
                            uv +
                            float2(
                                x,
                                y
                            ) *
                            texelSize;


                        MaskInfo neighbour =
                            SampleMaskInfo(
                                sampleUV
                            );


                        // =========================================
                        // Global silhouette.
                        //
                        // Preserve the clean original ObjectMask
                        // behaviour.
                        // =========================================

                        if (
                            center.id == 0u &&
                            neighbour.id != 0u
                        )
                        {
                            isOutline =
                                true;

                            break;
                        }


                        // =========================================
                        // Independent MaskGroup.
                        //
                        // Neighbour is the source object.
                        //
                        // Its outline expands OUTWARD onto the
                        // current lower-sorting object.
                        // =========================================

                        if (
                            _EnableGroupBoundary >
                                0.5 &&

                            CanExpandInto(
                                neighbour,
                                center
                            )
                        )
                        {
                            isOutline =
                                true;

                            break;
                        }
                    }


                    if (isOutline)
                    {
                        break;
                    }
                }


                if (!isOutline)
                {
                    return sceneColor;
                }


                sceneColor.rgb =
                    lerp(
                        sceneColor.rgb,
                        _OutlineColor.rgb,
                        _OutlineColor.a
                    );


                return sceneColor;
            }


            ENDHLSL
        }
    }
}