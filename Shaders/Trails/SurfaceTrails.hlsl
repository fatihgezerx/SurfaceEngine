// The trail maps of Surface Engine, as the ground shaders see them. SurfaceTrails publishes them as globals every frame: include this
// file in any URP shader to bend grass, ripple water or sink prints by world position with nothing to assign on the material.
//   _SurfaceTrailParams : xy = world XZ of the map centre, z = 1 / area size (m), w = 1 while the maps are active (0 = draw plain)
//   _SurfaceTrailTexel  : x = uv per texel, y = meters per texel, z = texels per side, w = area size (m)
//   _SurfaceTrailBend   : RG = push direction (0.5 = none), A = strength 0..1
//   _SurfaceTrailRipple : R = water height, signed, -1..1 (a dent is negative)
//   _SurfaceTrailDeform : R = print depth 0..1
// Every sample fades out towards the edge of the map, so trails never cut off hard where the map ends. Vertex-stage sampling uses LOD 0.
#ifndef SURFACE_TRAILS_INCLUDED
#define SURFACE_TRAILS_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

TEXTURE2D(_SurfaceTrailBend);
TEXTURE2D(_SurfaceTrailRipple);
TEXTURE2D(_SurfaceTrailDeform);
float4 _SurfaceTrailParams;
float4 _SurfaceTrailTexel;

// Map uv of a world position.
float2 SurfaceTrailUV(float3 positionWS)
{
    return (positionWS.xz - _SurfaceTrailParams.xy) * _SurfaceTrailParams.z + 0.5;
}

// 1 inside the map, fading to 0 at its border; 0 everywhere while the maps are off.
float SurfaceTrailEdge(float2 uv)
{
    float2 fromCentre = abs(uv - 0.5);
    float outermost = max(fromCentre.x, fromCentre.y);
    return (1.0 - smoothstep(0.42, 0.5, outermost)) * _SurfaceTrailParams.w;
}

// ---- Bend (grass) ----

// xy: the push on the XZ plane (direction times strength), z: the strength 0..1.
float3 SampleSurfaceBend(float3 positionWS)
{
    float2 uv = SurfaceTrailUV(positionWS);
    float4 map = SAMPLE_TEXTURE2D_LOD(_SurfaceTrailBend, sampler_LinearClamp, uv, 0);
    float strength = map.a * SurfaceTrailEdge(uv);
    // Blending two directions shortens the stored vector: bring it back to unit length.
    float2 direction = map.rg * 2.0 - 1.0;
    direction /= max(length(direction), 1e-3);
    return float3(direction * strength, strength);
}

// ---- Ripple (water) ----

float SampleSurfaceRippleHeightUV(float2 uv)
{
    return SAMPLE_TEXTURE2D_LOD(_SurfaceTrailRipple, sampler_LinearClamp, uv, 0).r * SurfaceTrailEdge(uv);
}

// Signed water height at a world position, -1..1.
float SampleSurfaceRippleHeight(float3 positionWS)
{
    return SampleSurfaceRippleHeightUV(SurfaceTrailUV(positionWS));
}

// World-space normal (y up) of the rippled surface, with the map's height scaled by `heightScale` meters.
float3 SampleSurfaceRippleNormal(float3 positionWS, float heightScale)
{
    float2 uv = SurfaceTrailUV(positionWS);
    float2 step = float2(_SurfaceTrailTexel.x, 0.0);
    float meters = max(_SurfaceTrailTexel.y, 1e-4);
    float left = SampleSurfaceRippleHeightUV(uv - step.xy);
    float right = SampleSurfaceRippleHeightUV(uv + step.xy);
    float down = SampleSurfaceRippleHeightUV(uv - step.yx);
    float up = SampleSurfaceRippleHeightUV(uv + step.yx);
    float slopeX = (right - left) * heightScale / (2.0 * meters);
    float slopeZ = (up - down) * heightScale / (2.0 * meters);
    return normalize(float3(-slopeX, 1.0, -slopeZ));
}

// ---- Deform (snow, sand, mud) ----

float SampleSurfaceDeformUV(float2 uv)
{
    return SAMPLE_TEXTURE2D_LOD(_SurfaceTrailDeform, sampler_LinearClamp, uv, 0).r * SurfaceTrailEdge(uv);
}

// Print depth 0..1 at a world position.
float SampleSurfaceDeform(float3 positionWS)
{
    return SampleSurfaceDeformUV(SurfaceTrailUV(positionWS));
}

// Depth averaged over a ring `radiusTexels` texels wide: wider than the print itself, so the difference to the print makes a soft rim.
float SampleSurfaceDeformWide(float2 uv, float radiusTexels)
{
    float2 step = float2(_SurfaceTrailTexel.x * radiusTexels, 0.0);
    float sum = SampleSurfaceDeformUV(uv - step.xy) + SampleSurfaceDeformUV(uv + step.xy)
              + SampleSurfaceDeformUV(uv - step.yx) + SampleSurfaceDeformUV(uv + step.yx);
    return sum * 0.25;
}

// World-space normal (y up) of a surface sunk by the print depth times `depthScale` meters.
float3 SampleSurfaceDeformNormal(float3 positionWS, float depthScale)
{
    float2 uv = SurfaceTrailUV(positionWS);
    float2 step = float2(_SurfaceTrailTexel.x, 0.0);
    float meters = max(_SurfaceTrailTexel.y, 1e-4);
    float left = SampleSurfaceDeformUV(uv - step.xy);
    float right = SampleSurfaceDeformUV(uv + step.xy);
    float down = SampleSurfaceDeformUV(uv - step.yx);
    float up = SampleSurfaceDeformUV(uv + step.yx);
    // The surface goes down where the depth goes up.
    float slopeX = -(right - left) * depthScale / (2.0 * meters);
    float slopeZ = -(up - down) * depthScale / (2.0 * meters);
    return normalize(float3(-slopeX, 1.0, -slopeZ));
}

#endif
