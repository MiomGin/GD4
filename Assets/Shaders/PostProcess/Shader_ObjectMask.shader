Shader "Hidden/PostProcess/ObjectInfoSorting"
{
    Properties
    {
        [PerRendererData]
        _MainTex(
            "Sprite Texture",
            2D
        ) = "white" {}


        // RGB = Group ID
        // A   = Flags
        [PerRendererData]
        _MaskGroupData(
            "Mask Group Data",
            Vector
        ) = (
            0.0039215686,
            0,
            0,
            0
        )


        // X = SortingLayer Value
        // Y = SortingOrder
        [PerRendererData]
        _MaskSortingData(
            "Mask Sorting Data",
            Vector
        ) = (
            0,
            0,
            0,
            0
        )


        _AlphaClipThreshold(
            "Alpha Clip Threshold",
            Range(0, 1)
        ) = 0.01
    }


    SubShader
    {
        Tags
        {
            "RenderPipeline" =
                "UniversalPipeline"

            "Queue" =
                "Transparent"
        }


        Pass
        {
            Name "Object Info Sorting"


            Cull Off

            ZWrite Off

            ZTest Always

            Blend One Zero


            HLSLPROGRAM


            #pragma vertex Vert

            #pragma fragment Frag

            #pragma target 3.5


            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"


            // ====================================================
            // Sprite Texture
            // ====================================================

            TEXTURE2D(
                _MainTex
            );


            SAMPLER(
                sampler_MainTex
            );


            // ====================================================
            // Per Renderer Metadata
            // ====================================================

            float4
                _MaskGroupData;


            float4
                _MaskSortingData;


            float
                _AlphaClipThreshold;


            // ====================================================
            // Vertex
            // ====================================================

            struct Attributes
            {
                float4 positionOS
                    : POSITION;


                float2 uv
                    : TEXCOORD0;


                half4 color
                    : COLOR;
            };


            struct Varyings
            {
                float4 positionCS
                    : SV_POSITION;


                float2 uv
                    : TEXCOORD0;


                half4 color
                    : COLOR;
            };


            Varyings Vert(
                Attributes input)
            {
                Varyings output;


                output.positionCS =
                    TransformObjectToHClip(
                        input
                            .positionOS
                            .xyz
                    );


                output.uv =
                    input.uv;


                output.color =
                    input.color;


                return output;
            }


            // ====================================================
            // MRT Output
            // ====================================================

            struct FragmentOutput
            {
                float4 objectInfo
                    : SV_Target0;


                float4 sortingInfo
                    : SV_Target1;
            };


            FragmentOutput Frag(
                Varyings input)
            {
                half textureAlpha =
                    SAMPLE_TEXTURE2D(
                        _MainTex,
                        sampler_MainTex,
                        input.uv
                    ).a;


                half finalAlpha =
                    textureAlpha *
                    input.color.a;


                clip(
                    finalAlpha -
                    _AlphaClipThreshold
                );


                FragmentOutput output;


                // RGB = Group ID
                // A   = Group Flags
                output.objectInfo =
                    _MaskGroupData;


                // R = SortingLayer Value
                // G = SortingOrder
                output.sortingInfo =
                    float4(
                        _MaskSortingData.x,
                        _MaskSortingData.y,
                        0.0,
                        0.0
                    );


                return output;
            }


            ENDHLSL
        }
    }
}