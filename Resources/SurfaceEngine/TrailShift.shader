// Moves a trail map's content by a whole number of texels when the maps re-centre on the focus (Graphics.Blit, _MainTex = the old
// content, _Offset = the move in uv). Texels that come in from outside the old map get _ClearValue, the map's empty value.
// Internal: loaded from Resources by SurfaceTrails.
Shader "Hidden/SurfaceEngine/Trail Shift"
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

        Pass
        {
            Name "Shift"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            float4 _Offset;
            float4 _ClearValue;

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

            float4 Frag(Varyings input) : SV_Target
            {
                float2 source = input.uv + _Offset.xy;
                bool inside = all(source >= 0.0) && all(source <= 1.0);
                return inside ? SAMPLE_TEXTURE2D(_MainTex, sampler_PointClamp, source) : _ClearValue;
            }
            ENDHLSL
        }
    }
}
