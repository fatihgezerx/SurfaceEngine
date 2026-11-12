// The global wind of Surface Engine, as the shaders see it. A SurfaceWind component publishes the globals; include this file in any
// URP shader to lean grass, scroll water or sway leaves with the same wind, gusts and all, computed per vertex on the GPU.
//   _SurfaceWindParams : xy = wind direction on XZ (unit), z = steady strength, w = gust amplitude (times the steady strength)
//   _SurfaceWindGust   : x = 1 / gust size (m), y = gust speed (m/s), z = turbulence, w = 1 while a SurfaceWind is enabled (0 = calm)
// SurfaceWindAt returns the push on XZ in meters at strength 1 (multiply by a material's wind influence and a height mask).
#ifndef SURFACE_WIND_INCLUDED
#define SURFACE_WIND_INCLUDED

float4 _SurfaceWindParams;
float4 _SurfaceWindGust;

// Whether a SurfaceWind drives the shaders (otherwise use the material's own fallback, or nothing).
bool SurfaceWindActive()
{
    return _SurfaceWindGust.w > 0.0;
}

// The wind at a world position: the steady push plus gust fronts travelling downwind (broken up across the wind so they are not
// straight bands) and a sideways flutter. Mirrored by SurfaceWind.VelocityAt on the CPU.
float2 SurfaceWindAt(float3 positionWS)
{
    float2 direction = _SurfaceWindParams.xy;
    float2 across = float2(-direction.y, direction.x);
    float along = dot(positionWS.xz, direction) * _SurfaceWindGust.x;
    float side = dot(positionWS.xz, across) * _SurfaceWindGust.x;
    float time = _Time.y * _SurfaceWindGust.y * _SurfaceWindGust.x;

    float gust = sin((along - time) * 6.2832) * 0.5 + 0.5;
    gust *= sin((along * 0.37 + side * 0.61 - time * 0.7) * 6.2832) * 0.5 + 0.5;
    float turbulence = sin(positionWS.x * 3.1 + _Time.y * 4.3) * sin(positionWS.z * 2.7 - _Time.y * 3.7) * _SurfaceWindGust.z;

    float magnitude = _SurfaceWindParams.z * (1.0 + gust * _SurfaceWindParams.w);
    float2 push = direction * magnitude + across * (turbulence * magnitude * 0.3);
    return push * _SurfaceWindGust.w;
}

// A fast, small flutter on top of the push (blade tips tremble in a strong wind); `seed` separates neighbours.
float SurfaceWindFlutter(float3 positionWS, float magnitude)
{
    return sin(_Time.y * 9.0 + positionWS.x * 7.3 + positionWS.z * 5.1) * magnitude * 0.1;
}

#endif
