Shader "Hidden/PostProcess/ObjectShadow"
{
    Properties
    {
        _ShadowColor(
            "Shadow Color",
            Color
        ) = (0, 0, 0, 0.5)


        _ShadowOffset(
            "Shadow Offset X Right Y Down",
            Vector
        ) = (4, 4, 0, 0)


        [Toggle]
        _EnableGroupShadow(
            "Enable Mask Group Shadow",
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
            Name "Object Shadow"


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
                    _ShadowColor;


                float4
                    _ShadowOffset;


                float
                    _EnableGroupShadow;

            CBUFFER_END


            #define INDEPENDENT_SHADOW_FLAG 2u


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
            // Sample
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

            bool HasIndependentShadow(
                MaskInfo info)
            {
                return
                    (
                        info.flags &
                        INDEPENDENT_SHADOW_FLAG
                    ) != 0u;
            }


            // ====================================================
            // Sorting
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


                float2 texelSize =
                    _ObjectInfoTexture_TexelSize.xy;


                // Inspector:
                //
                // +X = right
                // +Y = down
                float2 screenOffset =
                    float2(
                        _ShadowOffset.x,
                        -_ShadowOffset.y
                    );


                // The current pixel is the shadow destination.
                //
                // Sample backwards to find the source object.
                float2 sourceUV =
                    uv -
                    screenOffset *
                    texelSize;


                MaskInfo source =
                    SampleMaskInfo(
                        sourceUV
                    );


                if (source.id == 0u)
                {
                    return sceneColor;
                }


                MaskInfo current =
                    SampleMaskInfo(
                        uv
                    );


                // =================================================
                // Empty background.
                //
                // Preserve the original merged-mask shadow.
                // =================================================

                if (current.id == 0u)
                {
                    sceneColor.rgb =
                        lerp(
                            sceneColor.rgb,
                            _ShadowColor.rgb,
                            _ShadowColor.a
                        );


                    return sceneColor;
                }


                // =================================================
                // Occupied destination.
                // =================================================

                if (_EnableGroupShadow <
                    0.5)
                {
                    return sceneColor;
                }


                if (!HasIndependentShadow(
                        source))
                {
                    return sceneColor;
                }


                // Never cast onto the same MaskGroup.
                if (source.id ==
                    current.id)
                {
                    return sceneColor;
                }


                // Shadow can only fall onto a lower-sorting object.
                if (!IsHigherSortingPriority(
                        source,
                        current))
                {
                    return sceneColor;
                }


                sceneColor.rgb =
                    lerp(
                        sceneColor.rgb,
                        _ShadowColor.rgb,
                        _ShadowColor.a
                    );


                return sceneColor;
            }


            ENDHLSL
        }
    }
}