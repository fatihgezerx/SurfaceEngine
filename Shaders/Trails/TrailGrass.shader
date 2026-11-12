// Grass that leans away from whatever walks through it and stands back up (Surface Trails bend map), with a simple wind, a tip
// colour, alpha-clipped two-sided blades and full URP lighting (main light shadows, additional lights, SH, fog). The bend is read
// by world position from the global trail map, so nothing is assigned on the material: without Surface Trails the grass just waves.
// Use it on any grass mesh - a blade, a tuft card, a patch - with the pivot at the roots; a Terrain detail prototype in Instanced
// mode works too. The bend mask is the vertex height (object space Y over Height) or the mesh's uv.y (Mask). With a SurfaceWind in the
// scene the blades lean with the global wind (gusts travel across the field) scaled by Wind influence; without one the material's own
// simple wind (strength, speed, scale) is used.
Shader "SurfaceEngine/Trail Grass"
{
    Properties
    {
        [MainTexture] _BaseMap ("Texture (alpha = cutout)", 2D) = "white" {}
        [MainColor] _BaseColor ("Root colour", Color) = (0.25, 0.45, 0.12, 1)
        _TipColor ("Tip colour", Color) = (0.55, 0.75, 0.25, 1)
        _Cutoff ("Alpha cutoff", Range(0, 1)) = 0.5
        _Smoothness ("Smoothness", Range(0, 1)) = 0.2
        _Translucency ("Translucency", Range(0, 1)) = 0.3
        [Enum(Vertex height, 0, UV Y, 1)] _MaskMode ("Bend mask", Float) = 0
        _Height ("Blade height (m)", Range(0.05, 5)) = 0.6
        _BendDistance ("Bend distance (m)", Range(0, 2)) = 0.5
        _BendFlatten ("Bend flatten", Range(0, 1)) = 0.6
        _BendShade ("Darken when bent", Range(0, 1)) = 0.25
        _WindInfluence ("Wind influence (m, global wind)", Range(0, 1)) = 0.15
        _WindStrength ("Wind strength (m, fallback)", Range(0, 1)) = 0.08
        _WindSpeed ("Wind speed", Range(0, 10)) = 1.5
        _WindScale ("Wind scale", Range(0, 2)) = 0.3
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "TransparentCutout"
            "Queue" = "AlphaTest"
            "IgnoreProjector" = "True"
        }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "SurfaceTrails.hlsl"
        #include "SurfaceWind.hlsl"

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half4 _TipColor;
            half _Cutoff;
            half _Smoothness;
            half _Translucency;
            float _MaskMode;
            float _Height;
            float _BendDistance;
            float _BendFlatten;
            half _BendShade;
            float _WindInfluence;
            float _WindStrength;
            float _WindSpeed;
            float _WindScale;
        CBUFFER_END

        // How much of the blade moves: 0 at the roots, 1 at the tip, squared so the base stays put.
        float BendMask(float3 positionOS, float2 uv)
        {
            float mask = _MaskMode > 0.5 ? uv.y : saturate(positionOS.y / max(_Height, 1e-3));
            return mask * mask;
        }

        // The vertex after the trail bend and the wind. `bend` returns the trail strength 0..1 for shading.
        float3 Displace(float3 positionOS, float2 uv, out float bend)
        {
            float3 positionWS = TransformObjectToWorld(positionOS);
            float mask = BendMask(positionOS, uv);

            // Sampled at the vertex (the blade is small next to a texel), so a whole blade leans the same way.
            float3 trail = SampleSurfaceBend(positionWS);
            bend = trail.z;

            float2 push = trail.xy * _BendDistance * mask;
            positionWS.xz += push;
            // Leaning blades get lower; this keeps the length roughly right and flattens the tip under a body.
            positionWS.y -= bend * mask * _BendDistance * _BendFlatten;

            // Wind, weaker where the grass is held down: the global wind (gusts, flutter) when a SurfaceWind is active, otherwise the
            // material's own slow travelling wave.
            float2 windPush;
            if (SurfaceWindActive())
            {
                windPush = SurfaceWindAt(positionWS) * _WindInfluence;
                windPush += SurfaceWindFlutter(positionWS, length(windPush));
            }
            else
            {
                float phase = _Time.y * _WindSpeed + (positionWS.x + positionWS.z * 0.7) * _WindScale;
                float wind = sin(phase) * 0.5 + sin(phase * 2.3 + 1.7) * 0.25;
                windPush = float2(wind, wind * 0.5) * _WindStrength;
            }

            positionWS.xz += windPush * mask * (1.0 - bend * 0.7);

            return positionWS;
        }

        half4 SampleBase(float2 uv)
        {
            return SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv);
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull Off
            ZWrite On
            AlphaToMask On

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ProbeVolumeVariants.hlsl"
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #if defined(LOD_FADE_CROSSFADE)
                #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
            #endif

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half3 normalWS : TEXCOORD2;
                // x: bend strength, y: mask (root to tip), z: fog factor
                half3 bendMaskFog : TEXCOORD3;
                half3 vertexSH : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float bend;
                float3 positionWS = Displace(input.positionOS.xyz, input.uv, bend);
                output.positionWS = positionWS;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);

                // Blades are thin: the lighting normal is the mesh normal pulled towards up, which hides the facet of a crossed card.
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.normalWS = normalize(lerp(normalWS, float3(0.0, 1.0, 0.0), 0.5));
                output.bendMaskFog = half3(bend, sqrt(BendMask(input.positionOS.xyz, input.uv)), ComputeFogFactor(output.positionCS.z));
                output.vertexSH = SampleSHVertex(output.normalWS);
                return output;
            }

            half4 Frag(Varyings input, FRONT_FACE_TYPE facing : FRONT_FACE_SEMANTIC) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half4 base = SampleBase(input.uv);
                clip(base.a - _Cutoff);
                #if defined(LOD_FADE_CROSSFADE)
                    LODFadeCrossFade(input.positionCS);
                #endif

                half bend = input.bendMaskFog.x;
                half mask = input.bendMaskFog.y;
                half3 albedo = base.rgb * lerp(_BaseColor.rgb, _TipColor.rgb, mask) * (1.0 - bend * _BendShade);

                half3 normalWS = input.normalWS * IS_FRONT_VFACE(facing, 1.0, -1.0);
                normalWS = NormalizeNormalPerPixel(normalWS);

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.positionCS = input.positionCS;
                inputData.normalWS = normalWS;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                inputData.fogCoord = InitializeInputDataFog(float4(input.positionWS, 1.0), input.bendMaskFog.z);
                inputData.vertexLighting = 0;
                inputData.bakedGI = SampleSHPixel(input.vertexSH, normalWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.shadowMask = half4(1, 1, 1, 1);

                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = albedo;
                surfaceData.alpha = 1.0;
                surfaceData.metallic = 0.0;
                surfaceData.specular = 0.0;
                surfaceData.smoothness = _Smoothness;
                surfaceData.occlusion = lerp(0.6, 1.0, mask);
                surfaceData.normalTS = half3(0, 0, 1);

                half4 color = UniversalFragmentPBR(inputData, surfaceData);

                // Light through the blade: the main light seen from behind brightens the tips.
                Light mainLight = GetMainLight(inputData.shadowCoord);
                half back = saturate(dot(-inputData.viewDirectionWS, mainLight.direction));
                color.rgb += albedo * mainLight.color * mainLight.shadowAttenuation * back * back * _Translucency * mask;

                color.rgb = MixFog(color.rgb, inputData.fogCoord);
                return half4(color.rgb, base.a);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            #if defined(LOD_FADE_CROSSFADE)
                #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
            #endif

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float bend;
                float3 positionWS = Displace(input.positionOS.xyz, input.uv, bend);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDirectionWS = _LightDirection;
                #endif

                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
                output.positionCS = ApplyShadowClamping(positionCS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                clip(SampleBase(input.uv).a - _Cutoff);
                #if defined(LOD_FADE_CROSSFADE)
                    LODFadeCrossFade(input.positionCS);
                #endif
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ LOD_FADE_CROSSFADE

            #if defined(LOD_FADE_CROSSFADE)
                #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
            #endif

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
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float bend;
                output.positionCS = TransformWorldToHClip(Displace(input.positionOS.xyz, input.uv, bend));
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                clip(SampleBase(input.uv).a - _Cutoff);
                #if defined(LOD_FADE_CROSSFADE)
                    LODFadeCrossFade(input.positionCS);
                #endif
                return input.positionCS.z;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On
            Cull Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT

            #if defined(LOD_FADE_CROSSFADE)
                #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
            #endif
            #if defined(_GBUFFER_NORMALS_OCT)
                #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"
            #endif

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float bend;
                output.positionCS = TransformWorldToHClip(Displace(input.positionOS.xyz, input.uv, bend));
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.normalWS = normalize(lerp(TransformObjectToWorldNormal(input.normalOS), float3(0.0, 1.0, 0.0), 0.5));
                return output;
            }

            half4 Frag(Varyings input, FRONT_FACE_TYPE facing : FRONT_FACE_SEMANTIC) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                clip(SampleBase(input.uv).a - _Cutoff);
                #if defined(LOD_FADE_CROSSFADE)
                    LODFadeCrossFade(input.positionCS);
                #endif

                float3 normalWS = NormalizeNormalPerPixel(input.normalWS * IS_FRONT_VFACE(facing, 1.0, -1.0));
                #if defined(_GBUFFER_NORMALS_OCT)
                    float2 octNormalWS = PackNormalOctQuadEncode(normalWS);
                    float2 remappedOctNormalWS = saturate(octNormalWS * 0.5 + 0.5);
                    half3 packedNormalWS = PackFloat2To888(remappedOctNormalWS);
                    return half4(packedNormalWS, 0.0);
                #else
                    return half4(normalWS, 0.0);
                #endif
            }
            ENDHLSL
        }
    }

    FallBack Off
}
