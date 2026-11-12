// Snow, sand or mud that keeps the prints of whatever walks over it (Surface Trails deform map): hardware tessellation adds vertices
// near the camera, the domain shader sinks them by the print depth with a soft rim pushed up around each print, and the fragment
// shader bends the normals to the print and tints the trampled area. Every pass (lit, shadow, depth, normals) tessellates and sinks
// the same way, so prints never self-shadow. The depth is read by world position from the global map, so nothing is assigned on
// the material; without Surface Trails the surface is plain. Needs shader model 4.6 (hull / domain stages): PC and the consoles.
Shader "SurfaceEngine/Trail Deform"
{
    Properties
    {
        [MainTexture] _BaseMap ("Texture", 2D) = "white" {}
        [MainColor] _BaseColor ("Colour", Color) = (1, 1, 1, 1)
        [Normal] _NormalMap ("Normal map", 2D) = "bump" {}
        _NormalStrength ("Normal strength", Range(0, 2)) = 1
        _Smoothness ("Smoothness", Range(0, 1)) = 0.3
        _TrailColor ("Trampled colour (multiply)", Color) = (0.8, 0.82, 0.88, 1)
        _TrailSmoothness ("Trampled smoothness", Range(0, 1)) = 0.5
        _PrintDepth ("Print depth (m)", Range(0, 1)) = 0.15
        _RimHeight ("Rim height (m)", Range(0, 0.2)) = 0.03
        _RimWidth ("Rim width (texels)", Range(1, 8)) = 3
        _TessFactor ("Tessellation", Range(1, 32)) = 12
        _TessMinDistance ("Full tessellation up to (m)", Range(0, 50)) = 6
        _TessMaxDistance ("No tessellation beyond (m)", Range(1, 200)) = 40
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/GeometricTools.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Tessellation.hlsl"
        #include "SurfaceTrails.hlsl"

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);
        TEXTURE2D(_NormalMap);
        SAMPLER(sampler_NormalMap);

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            float4 _NormalMap_ST;
            half _NormalStrength;
            half _Smoothness;
            half4 _TrailColor;
            half _TrailSmoothness;
            float _PrintDepth;
            float _RimHeight;
            float _RimWidth;
            float _TessFactor;
            float _TessMinDistance;
            float _TessMaxDistance;
        CBUFFER_END

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float4 tangentOS : TANGENT;
            float2 uv : TEXCOORD0;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        // A vertex between the vertex and the domain stage, already in world space.
        struct ControlPoint
        {
            float3 positionWS : INTERNALTESSPOS;
            float3 normalWS : NORMAL;
            float4 tangentWS : TANGENT;
            float2 uv : TEXCOORD0;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct TessFactors
        {
            float edge[3] : SV_TessFactor;
            float inside : SV_InsideTessFactor;
        };

        ControlPoint TessVert(Attributes input)
        {
            ControlPoint output;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_TRANSFER_INSTANCE_ID(input, output);
            VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);
            output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
            output.normalWS = normalInput.normalWS;
            output.tangentWS = float4(normalInput.tangentWS, input.tangentOS.w * GetOddNegativeScale());
            output.uv = input.uv;
            return output;
        }

        // Distance based: full tessellation up to the min distance, none beyond the max.
        TessFactors PatchConstant(InputPatch<ControlPoint, 3> patch)
        {
            real3 edges = GetDistanceBasedTessFactor(patch[0].positionWS, patch[1].positionWS, patch[2].positionWS, GetCameraPositionWS(), _TessMinDistance, _TessMaxDistance);
            real4 factors = CalcTriTessFactorsFromEdgeTessFactors(max(edges * _TessFactor, 1.0));
            TessFactors output;
            output.edge[0] = factors.x;
            output.edge[1] = factors.y;
            output.edge[2] = factors.z;
            output.inside = factors.w;
            return output;
        }

        [domain("tri")]
        [partitioning("fractional_odd")]
        [outputtopology("triangle_cw")]
        [patchconstantfunc("PatchConstant")]
        [outputcontrolpoints(3)]
        ControlPoint Hull(InputPatch<ControlPoint, 3> patch, uint id : SV_OutputControlPointID)
        {
            return patch[id];
        }

        #define INTERPOLATE(field) output.field = patch[0].field * bary.x + patch[1].field * bary.y + patch[2].field * bary.z

        ControlPoint Interpolate(OutputPatch<ControlPoint, 3> patch, float3 bary)
        {
            ControlPoint output;
            UNITY_TRANSFER_INSTANCE_ID(patch[0], output);
            INTERPOLATE(positionWS);
            INTERPOLATE(normalWS);
            INTERPOLATE(tangentWS);
            INTERPOLATE(uv);
            output.normalWS = normalize(output.normalWS);
            return output;
        }

        // Sinks a point by the print depth, pushes it up by the rim around prints. Returns the print depth 0..1 for shading.
        float Sink(inout float3 positionWS, float3 normalWS)
        {
            float2 trailUV = SurfaceTrailUV(positionWS);
            float depth = SampleSurfaceDeformUV(trailUV);
            float wide = SampleSurfaceDeformWide(trailUV, _RimWidth);
            float rim = max(wide - depth, 0.0) * _RimHeight;
            positionWS += normalWS * (rim - depth * _PrintDepth);
            return depth;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma target 4.6
            #pragma vertex TessVert
            #pragma hull Hull
            #pragma domain Domain
            #pragma fragment Frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ProbeVolumeVariants.hlsl"
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DBuffer.hlsl"

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half3 normalWS : TEXCOORD2;
                half4 tangentWS : TEXCOORD3;
                // x: print depth, y: fog factor
                half2 depthFog : TEXCOORD4;
                half3 vertexSH : TEXCOORD5;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            [domain("tri")]
            Varyings Domain(TessFactors factors, OutputPatch<ControlPoint, 3> patch, float3 bary : SV_DomainLocation)
            {
                ControlPoint vertex = Interpolate(patch, bary);
                Varyings output = (Varyings)0;
                UNITY_TRANSFER_INSTANCE_ID(vertex, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 positionWS = vertex.positionWS;
                float depth = Sink(positionWS, vertex.normalWS);
                output.positionWS = positionWS;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv = TRANSFORM_TEX(vertex.uv, _BaseMap);
                output.normalWS = vertex.normalWS;
                output.tangentWS = vertex.tangentWS;
                output.depthFog = half2(depth, ComputeFogFactor(output.positionCS.z));
                output.vertexSH = SampleSHVertex(vertex.normalWS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half depth = input.depthFog.x;
                half4 base = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                half trampled = saturate(depth * 2.0);
                half3 albedo = base.rgb * lerp(half3(1, 1, 1), _TrailColor.rgb, trampled);

                // The mesh normal with the normal map, then the slope of the print on top.
                float sign = input.tangentWS.w;
                float3 bitangentWS = sign * cross(input.normalWS, input.tangentWS.xyz);
                half3x3 tangentToWorld = half3x3(input.tangentWS.xyz, bitangentWS, input.normalWS);
                half3 normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, TRANSFORM_TEX(input.uv, _NormalMap)), _NormalStrength);
                half3 meshNormalWS = normalize(mul(normalTS, tangentToWorld));
                half3 printNormalWS = SampleSurfaceDeformNormal(input.positionWS, _PrintDepth);
                half3 normalWS = normalize(half3(meshNormalWS.x + printNormalWS.x, meshNormalWS.y * printNormalWS.y, meshNormalWS.z + printNormalWS.z));

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.positionCS = input.positionCS;
                inputData.normalWS = normalWS;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                inputData.fogCoord = InitializeInputDataFog(float4(input.positionWS, 1.0), input.depthFog.y);
                inputData.vertexLighting = 0;
                inputData.bakedGI = SampleSHPixel(input.vertexSH, normalWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.shadowMask = half4(1, 1, 1, 1);
                inputData.tangentToWorld = tangentToWorld;

                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = albedo;
                surfaceData.alpha = 1.0;
                surfaceData.metallic = 0.0;
                surfaceData.specular = 0.0;
                surfaceData.smoothness = lerp(_Smoothness, _TrailSmoothness, trampled);
                surfaceData.occlusion = 1.0 - depth * 0.3;
                surfaceData.normalTS = normalTS;

                #ifdef _DBUFFER
                    ApplyDecalToSurfaceData(input.positionCS, surfaceData, inputData);
                #endif

                half4 color = UniversalFragmentPBR(inputData, surfaceData);
                color.rgb = MixFog(color.rgb, inputData.fogCoord);
                return half4(color.rgb, 1.0);
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
            Cull Back

            HLSLPROGRAM
            #pragma target 4.6
            #pragma vertex TessVert
            #pragma hull Hull
            #pragma domain Domain
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            [domain("tri")]
            Varyings Domain(TessFactors factors, OutputPatch<ControlPoint, 3> patch, float3 bary : SV_DomainLocation)
            {
                ControlPoint vertex = Interpolate(patch, bary);
                Varyings output;
                UNITY_TRANSFER_INSTANCE_ID(vertex, output);

                float3 positionWS = vertex.positionWS;
                Sink(positionWS, vertex.normalWS);
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDirectionWS = _LightDirection;
                #endif

                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, vertex.normalWS, lightDirectionWS));
                output.positionCS = ApplyShadowClamping(positionCS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
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
            Cull Back

            HLSLPROGRAM
            #pragma target 4.6
            #pragma vertex TessVert
            #pragma hull Hull
            #pragma domain Domain
            #pragma fragment Frag
            #pragma multi_compile_instancing

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            [domain("tri")]
            Varyings Domain(TessFactors factors, OutputPatch<ControlPoint, 3> patch, float3 bary : SV_DomainLocation)
            {
                ControlPoint vertex = Interpolate(patch, bary);
                Varyings output = (Varyings)0;
                UNITY_TRANSFER_INSTANCE_ID(vertex, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 positionWS = vertex.positionWS;
                Sink(positionWS, vertex.normalWS);
                output.positionCS = TransformWorldToHClip(positionWS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                return input.positionCS.z;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma target 4.6
            #pragma vertex TessVert
            #pragma hull Hull
            #pragma domain Domain
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT

            #if defined(_GBUFFER_NORMALS_OCT)
                #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"
            #endif

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            [domain("tri")]
            Varyings Domain(TessFactors factors, OutputPatch<ControlPoint, 3> patch, float3 bary : SV_DomainLocation)
            {
                ControlPoint vertex = Interpolate(patch, bary);
                Varyings output = (Varyings)0;
                UNITY_TRANSFER_INSTANCE_ID(vertex, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 positionWS = vertex.positionWS;
                Sink(positionWS, vertex.normalWS);
                output.positionWS = positionWS;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.normalWS = vertex.normalWS;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half3 printNormalWS = SampleSurfaceDeformNormal(input.positionWS, _PrintDepth);
                float3 normalWS = normalize(float3(input.normalWS.x + printNormalWS.x, input.normalWS.y * printNormalWS.y, input.normalWS.z + printNormalWS.z));
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
