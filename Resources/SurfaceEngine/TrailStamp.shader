// Draws one trail source into a trail map as an instanced quad (SurfaceTrails stamps every source each frame). Three passes, one
// per map: Bend writes the push direction (RG, 0.5 = none) blended by strength with the strength itself kept as the maximum (A);
// Ripple adds a dent (negative height) to the water height field; Deform keeps the deepest print (R, max). The per-instance
// _StampData is x, y: push direction, z: strength, w: bend = how much the direction counts against a radial push / ripple = frame
// scale. Internal: loaded from Resources by SurfaceTrails, never used on a material by hand.
Shader "Hidden/SurfaceEngine/Trail Stamp"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" }
        Cull Off
        ZWrite Off
        ZTest Always

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        UNITY_INSTANCING_BUFFER_START(SurfaceTrailStamp)
            UNITY_DEFINE_INSTANCED_PROP(float4, _StampData)
        UNITY_INSTANCING_BUFFER_END(SurfaceTrailStamp)

        struct Attributes
        {
            float4 positionOS : POSITION;
            float2 uv : TEXCOORD0;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float2 uv : TEXCOORD0;
            float4 data : TEXCOORD1;
        };

        Varyings Vert(Attributes input)
        {
            UNITY_SETUP_INSTANCE_ID(input);
            Varyings output;
            output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
            output.uv = input.uv;
            output.data = UNITY_ACCESS_INSTANCED_PROP(SurfaceTrailStamp, _StampData);
            return output;
        }

        // 1 at the centre of the stamp, 0 at its edge; `inner` is where the fall-off begins (0 = a cone, 0.6 = a flat sole with a soft rim).
        float Falloff(float2 uv, float inner)
        {
            float distance = length(uv * 2.0 - 1.0);
            return 1.0 - smoothstep(inner, 1.0, distance);
        }
        ENDHLSL

        Pass
        {
            Name "Bend"
            Blend SrcAlpha OneMinusSrcAlpha, One One
            BlendOp Add, Max

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            float4 Frag(Varyings input) : SV_Target
            {
                float strength = Falloff(input.uv, 0.15) * input.data.z;

                // A standing body pushes the grass outward from its centre; a moving one pushes it the way it goes.
                float2 radial = input.uv * 2.0 - 1.0;
                radial /= max(length(radial), 1e-3);
                float2 direction = lerp(radial, input.data.xy, input.data.w);
                direction /= max(length(direction), 1e-3);

                return float4(direction * 0.5 + 0.5, 0.0, strength);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Ripple"
            Blend One One
            BlendOp Add

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            // Height pushed down per stamp at full strength, per simulation step's worth of frame time (data.w).
            #define RIPPLE_DENT 0.12

            float4 Frag(Varyings input) : SV_Target
            {
                float dent = Falloff(input.uv, 0.0) * input.data.z * input.data.w * RIPPLE_DENT;
                return float4(-dent, 0.0, 0.0, 0.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Deform"
            Blend One One
            BlendOp Max

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            float4 Frag(Varyings input) : SV_Target
            {
                float depth = Falloff(input.uv, 0.55) * input.data.z;
                return float4(depth, 0.0, 0.0, 0.0);
            }
            ENDHLSL
        }
    }
}
