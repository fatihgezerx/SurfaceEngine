// A soft round dot for the particles of an impact (sparks, dust). Unlit; the particle's colour and alpha (colour over
// lifetime) tint and fade it. Additive for sparks, alpha blended for dust (the Blend setting); Intensity makes sparks shine under Bloom.
Shader "SurfaceEngine/Impact Particle"
{
    Properties
    {
        _Intensity ("Glow intensity", Range(0, 10)) = 3
        _Softness ("Edge softness", Range(0.5, 4)) = 2
        [Enum(Additive, 1, AlphaBlend, 10)] _DstBlend ("Blend", Float) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "ImpactParticle"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha [_DstBlend]
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Intensity;
                float _Softness;
                float _DstBlend;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color;
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // 1 in the middle of the quad, 0 at the edge of the circle.
                float distanceFromCentre = length(input.uv - 0.5) * 2.0;
                float spot = pow(saturate(1.0 - distanceFromCentre), _Softness);

                return half4(input.color.rgb * _Intensity, input.color.a * spot);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
