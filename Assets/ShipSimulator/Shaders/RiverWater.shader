Shader "ShipSimulator/RiverWater"
{
    Properties
    {
        _ShallowColor("Shallow Color", Color) = (0.16, 0.31, 0.28, 0.9)
        _DeepColor("Deep Color", Color) = (0.035, 0.12, 0.13, 0.96)
        _ReflectionTint("Reflection Tint", Color) = (0.48, 0.58, 0.62, 1)
        _FoamColor("Current Highlight Color", Color) = (0.68, 0.73, 0.67, 1)
        _Smoothness("Smoothness", Range(0, 1)) = 0.7
        _WaveScale("Broad Ripple Scale", Float) = 0.055
        _WaveHeight("Broad Ripple Height", Float) = 0.03
        _WaveSpeed("Current Speed", Float) = 0.42
        _FlowDirection("Current Direction XZ", Vector) = (0.08, -1, 0, 0)
        _RippleScale("Fine Ripple Scale", Float) = 0.38
        _RippleStrength("Fine Ripple Strength", Range(0, 1)) = 0.16
        _StreakScale("Current Streak Scale", Float) = 0.075
        _Turbidity("Turbidity", Range(0, 1)) = 0.62
        _ReflectionStrength("Reflection Strength", Range(0, 2)) = 0.48
        _FresnelPower("Fresnel Power", Range(0.5, 8)) = 4.2
        _Opacity("Opacity", Range(0, 1)) = 0.94
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ShallowColor;
                half4 _DeepColor;
                half4 _ReflectionTint;
                half4 _FoamColor;
                half _Smoothness;
                half _WaveScale;
                half _WaveHeight;
                half _WaveSpeed;
                half4 _FlowDirection;
                half _RippleScale;
                half _RippleStrength;
                half _StreakScale;
                half _Turbidity;
                half _ReflectionStrength;
                half _FresnelPower;
                half _Opacity;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half broadWave : TEXCOORD2;
                half fogFactor : TEXCOORD3;
            };

            float Hash21(float2 position)
            {
                position = frac(position * float2(123.34, 345.45));
                position += dot(position, position + 34.345);
                return frac(position.x * position.y);
            }

            float ValueNoise(float2 position)
            {
                float2 cell = floor(position);
                float2 fraction = frac(position);
                fraction = fraction * fraction * (3.0 - 2.0 * fraction);
                float a = Hash21(cell);
                float b = Hash21(cell + float2(1.0, 0.0));
                float c = Hash21(cell + float2(0.0, 1.0));
                float d = Hash21(cell + 1.0);
                return lerp(lerp(a, b, fraction.x), lerp(c, d, fraction.x), fraction.y);
            }

            float2 FlowBasisX()
            {
                float2 direction = normalize(_FlowDirection.xy + float2(0.0001, 0.0001));
                return float2(direction.y, -direction.x);
            }

            float BroadSurface(float2 worldXZ, float time)
            {
                float2 flow = normalize(_FlowDirection.xy + float2(0.0001, 0.0001));
                float2 across = FlowBasisX();
                float2 firstDirection = normalize(flow + across * 0.48);
                float2 secondDirection = normalize(flow - across * 0.72);
                float first = sin(dot(worldXZ, firstDirection) * _WaveScale * 1.9 - time);
                float second = sin(dot(worldXZ, secondDirection) * _WaveScale * 2.8 - time * 1.27);
                float irregularity = ValueNoise(
                    worldXZ * float2(0.047, 0.031) - flow * time * 0.11) * 2.0 - 1.0;
                return first * 0.23 + second * 0.15 + irregularity * 0.62;
            }

            float FineSurface(float2 worldXZ, float time)
            {
                float2 flow = normalize(_FlowDirection.xy + float2(0.0001, 0.0001));
                float2 across = FlowBasisX();
                float2 movingPosition = worldXZ * _RippleScale - flow * time * 1.35;
                float broadNoise = ValueNoise(movingPosition) * 2.0 - 1.0;
                float detailNoise = ValueNoise(
                    movingPosition * 2.13 + float2(13.7, -8.2)) * 2.0 - 1.0;
                float crossedRipple = sin(
                    dot(worldXZ, normalize(flow + across * 1.35)) * _RippleScale * 2.1 -
                    time * 3.1);
                return broadNoise * 0.52 + detailNoise * 0.32 + crossedRipple * 0.16;
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float time = _Time.y * _WaveSpeed;
                float broadWave = BroadSurface(positionWS.xz, time);
                positionWS.y += broadWave * _WaveHeight;

                float epsilon = 0.35;
                float waveX = BroadSurface(positionWS.xz + float2(epsilon, 0), time);
                float waveZ = BroadSurface(positionWS.xz + float2(0, epsilon), time);
                float3 tangentX = float3(epsilon, (waveX - broadWave) * _WaveHeight, 0);
                float3 tangentZ = float3(0, (waveZ - broadWave) * _WaveHeight, epsilon);

                output.positionWS = positionWS;
                output.normalWS = normalize(cross(tangentZ, tangentX));
                output.positionCS = TransformWorldToHClip(positionWS);
                output.broadWave = broadWave;
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float time = _Time.y * _WaveSpeed;
                float fine = FineSurface(input.positionWS.xz, time);
                float epsilon = 0.16;
                float fineX = FineSurface(input.positionWS.xz + float2(epsilon, 0), time);
                float fineZ = FineSurface(input.positionWS.xz + float2(0, epsilon), time);
                half3 fineNormal = normalize(half3(
                    -(fineX - fine) * _RippleStrength,
                    epsilon,
                    -(fineZ - fine) * _RippleStrength));
                half3 normalWS = normalize(input.normalWS + half3(fineNormal.x, 0, fineNormal.z));
                half3 viewDirection = SafeNormalize(GetWorldSpaceViewDir(input.positionWS));
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));

                half fresnel = pow(1.0h - saturate(dot(normalWS, viewDirection)), _FresnelPower);
                half diffuse = saturate(dot(normalWS, mainLight.direction)) * 0.28h + 0.72h;
                half3 halfDirection = SafeNormalize(mainLight.direction + viewDirection);
                half specular = pow(
                    saturate(dot(normalWS, halfDirection)),
                    lerp(18.0h, 150.0h, _Smoothness));

                float2 flow = normalize(_FlowDirection.xy + float2(0.0001, 0.0001));
                float2 across = FlowBasisX();
                float downstream = dot(input.positionWS.xz, flow);
                float crossRiver = dot(input.positionWS.xz, across);
                half streakNoise = ValueNoise(float2(
                    crossRiver * _StreakScale,
                    downstream * _StreakScale * 0.13 - time * 0.62));
                half narrowStreaks = smoothstep(0.84h, 0.97h,
                    streakNoise + fine * 0.06h);

                half depthVariation = saturate(
                    0.42h + input.broadWave * 0.08h + streakNoise * 0.17h);
                half3 muddyShallow = lerp(
                    _ShallowColor.rgb,
                    half3(0.22h, 0.27h, 0.19h),
                    _Turbidity * 0.38h);
                half3 waterColor = lerp(_DeepColor.rgb, muddyShallow, depthVariation);
                waterColor *= diffuse * mainLight.color;

                half perceptualRoughness = 1.0h - _Smoothness;
                half3 reflection = GlossyEnvironmentReflection(
                    reflect(-viewDirection, normalWS), perceptualRoughness, 1.0h);
                reflection = lerp(reflection, reflection * _ReflectionTint.rgb, 0.48h);

                half3 color = waterColor;
                color += reflection * fresnel * _ReflectionStrength;
                color += specular * mainLight.color * (0.22h + fresnel * 0.72h);
                color += _FoamColor.rgb * narrowStreaks * 0.025h;
                color += SampleSH(normalWS) * 0.08h;
                color = MixFog(color, input.fogFactor);

                half alpha = saturate(_Opacity + fresnel * 0.045h);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
