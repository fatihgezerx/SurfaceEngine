// Water that ripples where things wade through it (Surface Trails ripple map): the simulated height moves the vertices and bends
// the normals, crests get foam. On top: depth-tinted colour (needs the URP Depth Texture), two scrolling normal layers, fresnel
// opacity, shore foam and full URP lighting (specular highlights, reflection probes, fog). The ripples are read by world position
// from the global map, so nothing is assigned on the material; without Surface Trails the water is plain. Use a plane with enough
// vertices for the height to show (a 2 m grid is fine, the normals carry the detail); the surface is assumed to lie flat.
Shader "SurfaceEngine/Trail Water"
{
    Properties
    {
        [MainColor] _ShallowColor ("Shallow colour", Color) = (0.25, 0.6, 0.65, 0.55)
        _DeepColor ("Deep colour", Color) = (0.03, 0.15, 0.25, 0.95)
        _DepthDistance ("Depth fade distance (m)", Range(0.05, 20)) = 2
        _Smoothness ("Smoothness", Range(0, 1)) = 0.92
        _FresnelPower ("Fresnel power", Range(0.5, 8)) = 3
        [Normal] _NormalMap ("Normal map (waves)", 2D) = "bump" {}
        _NormalStrength ("Normal strength", Range(0, 2)) = 0.5
        _NormalScale ("Normal tiling (per m)", Range(0.01, 5)) = 0.25
        _FlowSpeed ("Flow speed", Range(0, 1)) = 0.03
        _WindInfluence ("Wind influence (global wind)", Range(0, 1)) = 0.05
        _RippleHeight ("Ripple height (m)", Range(0, 0.5)) = 0.08
        _RippleNormal ("Ripple normal strength", Range(0, 4)) = 1.5
        _FoamColor ("Foam colour", Color) = (0.9, 0.95, 0.95, 1)
        _FoamAmount ("Foam amount", Range(0, 1)) = 0.6
        _FoamThreshold ("Foam starts at height", Range(0, 1)) = 0.25
        _ShoreFoam ("Shore foam width (m)", Range(0, 2)) = 0.3
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
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ProbeVolumeVariants.hlsl"
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "SurfaceTrails.hlsl"
            #include "SurfaceWind.hlsl"

            TEXTURE2D(_NormalMap);
            SAMPLER(sampler_NormalMap);

            CBUFFER_START(UnityPerMaterial)
                half4 _ShallowColor;
                half4 _DeepColor;
                float _DepthDistance;
                half _Smoothness;
                half _FresnelPower;
                float4 _NormalMap_ST;
                half _NormalStrength;
                float _NormalScale;
                float _FlowSpeed;
                half _WindInfluence;
                float _RippleHeight;
                half _RippleNormal;
                half4 _FoamColor;
                half _FoamAmount;
                half _FoamThreshold;
                float _ShoreFoam;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                // x: ripple height, y: fog factor
                half2 heightFog : TEXCOORD1;
                half3 vertexSH : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float height = SampleSurfaceRippleHeight(positionWS);
                positionWS.y += height * _RippleHeight;

                output.positionWS = positionWS;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.heightFog = half2(height, ComputeFogFactor(output.positionCS.z));
                output.vertexSH = SampleSHVertex(half3(0, 1, 0));
                return output;
            }

            // A tangent-space normal of the flat water plane in world space (x east, z north).
            half3 FlatNormalToWorld(half3 normalTS)
            {
                return normalize(half3(normalTS.x, normalTS.z, normalTS.y));
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float3 positionWS = input.positionWS;
                float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);

                // Water depth below this pixel: what is behind the surface minus the surface itself.
                float sceneDepth = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
                float surfaceDepth = input.positionCS.w;
                float waterDepth = max(sceneDepth - surfaceDepth, 0.0);
                half depthFade = saturate(waterDepth / _DepthDistance);

                // Two scrolling normal layers against each other, then the simulated ripples on top.
                // The waves drift downwind, faster in a strong wind; without a SurfaceWind they drift diagonally at the flow speed.
                float2 flowUV = positionWS.xz * _NormalScale;
                float2 flowDirection = SurfaceWindActive() ? _SurfaceWindParams.xy : float2(0.7071, 0.7071);
                float flowSpeed = _FlowSpeed + (SurfaceWindActive() ? _SurfaceWindParams.z * _WindInfluence : 0.0);
                float2 scroll = flowDirection * (_Time.y * flowSpeed);
                half3 waveA = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, flowUV + scroll), _NormalStrength);
                half3 waveB = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, flowUV * 0.7 - scroll * 1.3), _NormalStrength);
                half3 waveTS = normalize(half3(waveA.xy + waveB.xy, waveA.z * waveB.z));
                half3 waveWS = FlatNormalToWorld(waveTS);
                half3 rippleWS = SampleSurfaceRippleNormal(positionWS, _RippleHeight * _RippleNormal);
                half3 normalWS = normalize(half3(waveWS.x + rippleWS.x, waveWS.y * rippleWS.y, waveWS.z + rippleWS.z));

                half3 viewDirectionWS = GetWorldSpaceNormalizeViewDir(positionWS);
                half fresnel = pow(1.0 - saturate(dot(viewDirectionWS, normalWS)), _FresnelPower);

                half4 waterColor = lerp(_ShallowColor, _DeepColor, depthFade);
                half alpha = saturate(lerp(waterColor.a, 1.0, fresnel));

                // Foam on the crests and troughs of the ripples and along the shore.
                half height = abs(input.heightFog.x);
                half crestFoam = saturate((height - _FoamThreshold) / max(1.0 - _FoamThreshold, 0.01)) * _FoamAmount;
                half shoreFoam = _ShoreFoam > 0.0 ? (1.0 - saturate(waterDepth / _ShoreFoam)) * _FoamAmount : 0.0;
                half foam = saturate(crestFoam + shoreFoam);
                half3 albedo = lerp(waterColor.rgb, _FoamColor.rgb, foam);
                alpha = saturate(alpha + foam * 0.5);

                InputData inputData = (InputData)0;
                inputData.positionWS = positionWS;
                inputData.positionCS = input.positionCS;
                inputData.normalWS = normalWS;
                inputData.viewDirectionWS = viewDirectionWS;
                inputData.shadowCoord = TransformWorldToShadowCoord(positionWS);
                inputData.fogCoord = InitializeInputDataFog(float4(positionWS, 1.0), input.heightFog.y);
                inputData.vertexLighting = 0;
                inputData.bakedGI = SampleSHPixel(input.vertexSH, normalWS);
                inputData.normalizedScreenSpaceUV = screenUV;
                inputData.shadowMask = half4(1, 1, 1, 1);

                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = albedo;
                surfaceData.alpha = alpha;
                surfaceData.metallic = 0.0;
                surfaceData.specular = 0.0;
                surfaceData.smoothness = lerp(_Smoothness, 0.3, foam);
                surfaceData.occlusion = 1.0;
                surfaceData.normalTS = half3(0, 0, 1);

                half4 color = UniversalFragmentPBR(inputData, surfaceData);
                color.rgb = MixFog(color.rgb, inputData.fogCoord);
                return half4(color.rgb, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
