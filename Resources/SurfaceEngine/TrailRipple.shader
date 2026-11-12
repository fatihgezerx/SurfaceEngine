// One step of the water height-field simulation (Graphics.Blit: _MainTex = the current height, _PrevTex = the step before it, the
// target = the new height). The classic two-buffer wave equation: the new height is the average of the four neighbours times two,
// minus the previous height, damped. Internal: loaded from Resources by SurfaceTrails, run at a fixed rate.
Shader "Hidden/SurfaceEngine/Trail Ripple"
{
    Properties
    {
        _MainTex ("Current", 2D) = "black" {}
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            Name "RippleStep"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            TEXTURE2D(_PrevTex);
            float4 _MainTex_TexelSize;
            float _Damping;

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
                float2 texel = _MainTex_TexelSize.xy;
                float left = SAMPLE_TEXTURE2D(_MainTex, sampler_PointClamp, input.uv - float2(texel.x, 0.0)).r;
                float right = SAMPLE_TEXTURE2D(_MainTex, sampler_PointClamp, input.uv + float2(texel.x, 0.0)).r;
                float down = SAMPLE_TEXTURE2D(_MainTex, sampler_PointClamp, input.uv - float2(0.0, texel.y)).r;
                float up = SAMPLE_TEXTURE2D(_MainTex, sampler_PointClamp, input.uv + float2(0.0, texel.y)).r;
                float previous = SAMPLE_TEXTURE2D(_PrevTex, sampler_PointClamp, input.uv).r;

                float height = (left + right + down + up) * 0.5 - previous;
                height *= _Damping;
                return float4(clamp(height, -1.0, 1.0), 0.0, 0.0, 0.0);
            }
            ENDHLSL
        }
    }
}
