Shader "ShipSimulator/RiverSky"
{
    Properties
    {
        _SkyTint("Sky Tint", Color) = (0.5, 0.5, 0.5, 1)
        _GroundColor("Ground", Color) = (0.33, 0.35, 0.28, 1)
        _Exposure("Exposure", Range(0, 8)) = 1
        _SunSize("Sun Size", Range(0, 1)) = 0.035
        _AtmosphereThickness("Atmosphere Thickness", Range(0.2, 5)) = 1
        _ZenithColor("Zenith Color", Color) = (0.16, 0.34, 0.66, 1)
        _HorizonColor("Horizon Color", Color) = (0.66, 0.76, 0.86, 1)
        _CloudCoverage("Cloud Coverage", Range(0, 1)) = 0.45
        _CloudScale("Cloud Scale", Float) = 1
        _CloudSpeed("Cloud Drift", Float) = 1
        _CloudLitColor("Cloud Lit Color", Color) = (1, 0.97, 0.92, 1)
        _CloudShadeColor("Cloud Shade Color", Color) = (0.5, 0.55, 0.62, 1)
        _StarIntensity("Star Intensity", Range(0, 2)) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Background"
            "RenderType" = "Background"
            "PreviewType" = "Skybox"
            "RenderPipeline" = "UniversalPipeline"
        }
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _SkyTint;
                half4 _GroundColor;
                half _Exposure;
                half _SunSize;
                half _AtmosphereThickness;
                half4 _ZenithColor;
                half4 _HorizonColor;
                half _CloudCoverage;
                half _CloudScale;
                half _CloudSpeed;
                half4 _CloudLitColor;
                half4 _CloudShadeColor;
                half _StarIntensity;
            CBUFFER_END

            // Set by WeatherController.
            float _RiverCloudWeather;
            float4 _RiverWind;

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 direction : TEXCOORD0;
            };

            float Hash(float2 position)
            {
                position = frac(position * float2(123.34, 456.21));
                position += dot(position, position + 45.32);
                return frac(position.x * position.y);
            }

            float Noise(float2 position)
            {
                float2 cell = floor(position);
                float2 f = frac(position);
                f = f * f * f * (f * (f * 6.0 - 15.0) + 10.0);
                float a = Hash(cell);
                float b = Hash(cell + float2(1, 0));
                float c = Hash(cell + float2(0, 1));
                float d = Hash(cell + 1);
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            float Fbm(float2 position, int octaves)
            {
                const float2x2 rotation = float2x2(0.8, -0.6, 0.6, 0.8);
                float sum = 0;
                float amplitude = 0.5;
                for (int i = 0; i < octaves; i++)
                {
                    sum += amplitude * Noise(position);
                    position = mul(rotation, position) * 2.03 + 11.7;
                    amplitude *= 0.5;
                }
                return sum;
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.direction = input.positionOS.xyz;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 direction = normalize(input.direction);
                float3 sunDirection = _MainLightPosition.xyz;
                half3 light = _MainLightColor.rgb;
                half3 tint = _SkyTint.rgb * 2;
                float up = direction.y;
                float height = saturate(up);
                float sunDot = dot(direction, sunDirection);

                // The horizon leans toward the fog colour so distant banks dissolve into it.
                half3 horizon = lerp(_HorizonColor.rgb * tint, unity_FogColor.rgb, 0.55);
                float zenithBlend = 1 - exp(-height * 3.2 / max(_AtmosphereThickness, 0.2));
                half3 sky = lerp(horizon, _ZenithColor.rgb * tint, zenithBlend);

                half glow = pow(saturate(sunDot), 10) * 0.22 + pow(saturate(sunDot), 90) * 0.55;
                sky += light * glow * (0.5 + 0.5 * (1 - height));
                half lowSun = 1 - saturate(sunDirection.y * 3);
                sky *= lerp(1, half3(1.25, 0.95, 0.75),
                    lowSun * (1 - height) * pow(saturate(sunDot * 0.5 + 0.5), 3));

                float sunRadius = max(_SunSize, 0.005) * 0.2;
                half disc = smoothstep(cos(sunRadius * 1.6), cos(sunRadius), sunDot);

                half cloud = 0;
                if (up > 0.001)
                {
                    // Project the view ray onto a distant cloud deck and drift it with the wind.
                    float2 deck = direction.xz / (up + 0.06) * 0.92 * _CloudScale;
                    float2 drift = (_RiverWind.xz * 0.0015 + float2(0.0008, 0.0003)) *
                        _Time.y * _CloudSpeed;
                    float2 position = deck + drift;
                    float warp = Fbm(position * 0.5 + 3.1, 3);
                    float2 cloudPosition = position + warp * 0.8;
                    float shape = Fbm(cloudPosition, 5);

                    float coverage = saturate(lerp(_CloudCoverage, 1, _RiverCloudWeather));
                    float threshold = lerp(0.62, 0.2, coverage);
                    cloud = smoothstep(threshold, threshold + 0.22, shape);

                    // Thicker cloud between this point and the sun leaves it in its own shade.
                    float2 toSun = normalize(sunDirection.xz + 0.0001) * 0.07;
                    float sunward = Fbm(cloudPosition + toSun, 3) - Fbm(cloudPosition, 3);
                    half lit = saturate(0.6 - sunward * 6);
                    half overcast = smoothstep(0.65, 1, coverage);

                    half3 shade = _CloudShadeColor.rgb * tint * lerp(0.95, 0.6, overcast);
                    half3 cloudColor = lerp(shade, _CloudLitColor.rgb * light,
                        lit * (1 - overcast * 0.65));
                    cloudColor += light * pow(saturate(sunDot), 8) * (1 - cloud) * 0.5;
                    cloudColor = lerp(horizon, cloudColor, smoothstep(0.02, 0.3, up));

                    cloud *= smoothstep(0.0, 0.12, up);
                    sky = lerp(sky, cloudColor, cloud);
                    disc *= 1 - saturate(cloud * 1.6);
                }

                if (_StarIntensity > 0.001 && up > 0)
                {
                    float3 cell = floor(direction * 420);
                    half star = smoothstep(0.9985, 1, Hash(cell.xy + cell.z * 17.13));
                    sky += star * _StarIntensity * (1 - cloud) * saturate(up * 4) * 1.6;
                }

                // Below the horizon only shows past the ends of the terrain, so it must read as haze.
                half groundBlend = smoothstep(0.0, 0.04, -up);
                sky = lerp(sky, lerp(unity_FogColor.rgb, _GroundColor.rgb * tint, 0.15), groundBlend);

                // unity_FogParams.x is the exponential squared density divided by sqrt(ln 2).
                half haze = saturate((unity_FogParams.x - 0.0014) / 0.008);
                sky = lerp(sky, unity_FogColor.rgb, haze * (1 - height * 0.35));
                sky += light * disc * 18 * (1 - haze);
                return half4(sky * _Exposure, 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
