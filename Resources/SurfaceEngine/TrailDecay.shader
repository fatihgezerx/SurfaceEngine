// Full-screen passes that let a trail map recover (Graphics.Blit, _MainTex = the map). Pass 0 (bend): the strength (A) decays by
// _Factor, the direction is kept. Pass 1 (deform): the print depth (R) loses _Amount. Internal: loaded from Resources by SurfaceTrails.
Shader "Hidden/SurfaceEngine/Trail Decay"
{
    Properties
    {
        _MainTex ("Map", 2D) = "black" {}
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        Cull Off
        ZWrite Off
        ZTest Always

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_MainTex);
        float _Factor;
        float _Amount;

        struct Attributes
        {
            float4 positionOS : POSITION;
            float2 uv : TEXCOORD0;
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float2 uv : TEXCOORD0;
        };

        Varyings Vert(Attributes input)
        {
            Varyings output;
            output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
            output.uv = input.uv;
            return output;
        }
        ENDHLSL

        Pass
        {
            Name "BendDecay"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            float4 Frag(Varyings input) : SV_Target
            {
                float4 map = SAMPLE_TEXTURE2D(_MainTex, sampler_PointClamp, input.uv);
                map.a *= _Factor;
                // Below one 8-bit step the strength would never reach zero: snap it, so the map counts as empty.
                map.a = map.a < (1.0 / 255.0) ? 0.0 : map.a;
                return map;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DeformFill"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            float4 Frag(Varyings input) : SV_Target
            {
                float4 map = SAMPLE_TEXTURE2D(_MainTex, sampler_PointClamp, input.uv);
                map.r = max(0.0, map.r - _Amount);
                return map;
            }
            ENDHLSL
        }
    }
}
