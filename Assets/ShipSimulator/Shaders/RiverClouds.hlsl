#ifndef RIVER_CLOUDS_INCLUDED
#define RIVER_CLOUDS_INCLUDED
float _RiverCloudWeather;
float _RiverCloudShadowDisable;
// Gusting wind (xz in m/s, w speed) and its accumulated travel in metres, from WeatherController.
float4 _RiverWind;
float4 _RiverWindTravel;
float4 _RiverCloudSettings;
            float RiverCloudHash(float2 position)
            {
                position = frac(position * float2(123.34, 456.21));
                position += dot(position, position + 45.32);
                return frac(position.x * position.y);
            }

            float RiverCloudNoise(float2 position)
            {
                float2 cell = floor(position);
                float2 f = frac(position);
                f = f * f * f * (f * (f * 6.0 - 15.0) + 10.0);
                float a = RiverCloudHash(cell);
                float b = RiverCloudHash(cell + float2(1, 0));
                float c = RiverCloudHash(cell + float2(0, 1));
                float d = RiverCloudHash(cell + 1);
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            float RiverCloudFbm(float2 position, int octaves)
            {
                const float2x2 rotation = float2x2(0.8, -0.6, 0.6, 0.8);
                float sum = 0;
                float amplitude = 0.5;
                for (int i = 0; i < octaves; i++)
                {
                    sum += amplitude * RiverCloudNoise(position);
                    position = mul(rotation, position) * 2.03 + 11.7;
                    amplitude *= 0.5;
                }
                return sum;
            }


// Gust field near the water: 0 in lulls, 1 inside a gust. It rides downwind with the accumulated
// wind travel, so a gust that roughens the water goes on to bend the trees beyond it.
float RiverGust(float2 positionXZ, float2 travel, float time)
{
    float2 drift = positionXZ - travel * 0.9;
    float broad = RiverCloudNoise(drift / 90 + float2(time * 0.011, 0));
    float detail = RiverCloudNoise(drift / 28 + float2(3.7, -time * 0.019));
    return smoothstep(0.3, 0.8, broad * 0.65 + detail * 0.35);
}

float2 RiverCloudCoordinates(float3 origin, float3 ray, float scale, float speed)
{
    float2 deck = (origin.xz + ray.xz * max(900 - origin.y, 0) / max(ray.y, 0.06)) * (0.92 / 900) * scale;
    return deck + (_RiverWindTravel.xz * 0.0015 + float2(0.0008,0.0003) * _Time.y) * speed;
}
float RiverCloudDensity(float2 p, float coverage)
{
    float warp = RiverCloudFbm(p * 0.5 + 3.1,3);
    float shape = RiverCloudFbm(p + warp * 0.8,5);
    float threshold = lerp(0.62,0.2,coverage);
    return smoothstep(threshold,threshold + 0.22,shape);
}
// Single exit point on purpose: with an early return for the disabled case the GLSL
// translator reports the result as potentially uninitialized in the CloudCookie kernel.
half RiverCloudShadow(float3 world, float3 sun)
{
    half transmittance = 1;
    if (_RiverCloudShadowDisable <= 0.5)
    {
        float3 settings = _RiverCloudSettings.w > 0 ? _RiverCloudSettings.xyz : float3(0.45,1,1);
        float coverage = saturate(lerp(settings.x,1,_RiverCloudWeather));
        float2 p = RiverCloudCoordinates(world,sun,settings.y,settings.z);
        transmittance = 1 - RiverCloudDensity(p,coverage) * 0.58 * smoothstep(0,0.15,sun.y);
    }
    return transmittance;
}
#endif
