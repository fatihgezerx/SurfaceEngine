// Editor only: turns a trail map into something a person can read in the Trail Monitor window (Graphics.Blit, _MainTex = the map).
// Pass 0 Bend: the push direction as a hue-like colour (red = +X, green = +Z) at the strength's brightness. Pass 1 Ripple: mid grey is
// flat, bright a crest, dark a trough. Pass 2 Deform: black untouched, white the deepest print. Each has a faint grid every 8 texels.
Shader "Hidden/SurfaceEngine/Trail View"
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
        float4 _MainTex_TexelSize;

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

        float Grid(float2 uv)
        {
            float2 cell = frac(uv * _MainTex_TexelSize.zw / 8.0);
            return (cell.x < 0.08 || cell.y < 0.08) ? 0.06 : 0.0;
        }
        ENDHLSL

        Pass
        {
            Name "Bend"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            float4 Frag(Varyings input) : SV_Target
            {
                float4 map = SAMPLE_TEXTURE2D(_MainTex, sampler_LinearClamp, input.uv);
                float3 colour = float3(map.r, map.g, 0.5) * map.a + 0.1;
                return float4(colour + Grid(input.uv), 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Ripple"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            float4 Frag(Varyings input) : SV_Target
            {
                float height = SAMPLE_TEXTURE2D(_MainTex, sampler_LinearClamp, input.uv).r;
                float shade = saturate(0.5 + height * 2.0);
                return float4(shade * float3(0.6, 0.8, 1.0) + Grid(input.uv), 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Deform"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            float4 Frag(Varyings input) : SV_Target
            {
                float depth = SAMPLE_TEXTURE2D(_MainTex, sampler_LinearClamp, input.uv).r;
                return float4(depth.xxx * 0.9 + 0.1 + Grid(input.uv), 1.0);
            }
            ENDHLSL
        }
    }
}
