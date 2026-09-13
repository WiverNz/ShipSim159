#ifndef RIVER_VEGETATION_WIND_INCLUDED
#define RIVER_VEGETATION_WIND_INCLUDED
// Requires RiverClouds.hlsl for the wind, its travel and RiverGust.

// Previous-frame wind and time for motion vectors, from WeatherController and RiverLighting.
float4 _RiverPreviousWind;
float4 _RiverPreviousWindTravel;
float _RiverPreviousTime;

// Wind displacement of a plant vertex, after the layered bending of GPU Gems 3 chapter 16
// (Crysis vegetation). The whole plant leans downwind with the square of height so trunks stay
// rooted, rocks a little on its own timing, and leaves flutter faster inside a gust. Most of the
// time a plant stands in a lull; gusts pass downwind on the same field that roughens the water.
// `world` is the undisplaced vertex, `root` the plant origin, `flutter` 0 for wood and up to 1
// for leaf tips. Amplitudes are visual estimates.
float3 RiverVegetationSway(float3 world, float3 root, float flutter, float4 wind, float2 travel, float time)
{
    float speed = wind.w;
    float2 downwind = speed > 0.01 ? wind.xz / speed : float2(0, 1);
    float gust = smoothstep(0.35, 1.0, RiverGust(root.xz, travel, time));
    float strength = saturate(speed / 12) * (0.2 + 0.8 * gust);
    float seed = dot(root.xz, float2(0.37, 0.61));
    float height = max(world.y - root.y, 0);
    float rock = 0.75 + 0.3 * sin(time * 1.3 + seed) + 0.12 * sin(time * 2.9 + seed * 1.7);
    float lean = (0.0016 * height * height + 0.02 * height) * strength * rock;
    world.xz += downwind * lean;
    // Sink slightly while leaning so branches keep roughly their length.
    world.y -= lean * lean * 0.5 / max(height, 0.5);
    float phase = dot(world.xz, float2(0.36, 0.57)) + time * (3.5 + 3.0 * gust) + seed;
    world += float3(sin(phase), 0.5 * sin(phase * 1.31), cos(phase * 0.77)) *
        flutter * (0.01 + 0.05 * strength);
    return world;
}
#endif
