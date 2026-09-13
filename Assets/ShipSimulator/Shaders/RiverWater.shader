Shader "ShipSimulator/RiverWater"
{
    Properties
    {
        _ShallowColor("Shallow Color", Color) = (0.16, 0.31, 0.28, 0.9)
        _DeepColor("Deep Color", Color) = (0.035, 0.12, 0.13, 0.96)
        _ReflectionTint("Reflection Tint", Color) = (0.48, 0.58, 0.62, 1)
        _FoamColor("Foam Color", Color) = (0.86, 0.89, 0.86, 1)
        _AeratedColor("Aerated Water Color", Color) = (0.3, 0.4, 0.33, 1)
        [Normal][NoScaleOffset] _RippleNormal("Ripple Normal Map", 2D) = "bump" {}
        _RippleTileM("Ripple Tile Size (m)", Float) = 11
        _RippleStrength("Ripple Strength", Range(0, 1)) = 0.2
        _ReflectionDistortion("Reflection Distortion", Range(0, 1)) = 0.3
        _Smoothness("Smoothness", Range(0, 1)) = 0.8
        _WaveScale("Broad Ripple Scale", Float) = 0.055
        _WaveHeight("Broad Ripple Height", Float) = 0.03
        _WaveSpeed("Current Speed", Float) = 0.42
        _FlowDirection("Current Direction XZ", Vector) = (0.08, -1, 0, 0)
        _StreakScale("Current Streak Scale", Float) = 0.075
        _Turbidity("Turbidity", Range(0, 1)) = 0.62
        _ReflectionStrength("Reflection Strength", Range(0, 2)) = 0.6
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
            #include_with_pragmas "RiverWaterSurface.hlsl"
            ENDHLSL
        }
        Pass
        {
            Name "RiverMotionVectors"
            Tags { "LightMode"="RiverMotionVectors" }
            ZWrite Off
            ZTest LEqual
            ColorMask RG
            HLSLPROGRAM
            #pragma vertex WaterMotionVert
            #pragma fragment WaterMotionFrag
            #include_with_pragmas "RiverWaterSurface.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/MotionVectorsCommon.hlsl"
            float _RiverPreviousTime;
            float4 _PreviousWakePoints[WAKE_CAPACITY];
            float4 _PreviousWakeInfo[WAKE_CAPACITY];
            float _PreviousWakeCount;
            float4 _PreviousWakeBounds, _PreviousWakeShip, _PreviousWakeHull;
            float _PreviousWakeAmplitude;
            void PreviousFindWakeCoordinates(float2 position, out float4 wake, out float4 frame)
            {
                wake = float4(-1000, 0, 0, 0);
                frame = float4(0, 0, 1, 0);
                int count = (int)_PreviousWakeCount;
                if (count < 2) return;
                // Beyond this the Kelvin wedge of the longest published track has faded.
                const float reach = 420;
                if (any(position < _PreviousWakeBounds.xy - reach) || any(position > _PreviousWakeBounds.zw + reach))
                    return;

                float bestDistanceSq = 1e12;
                int best = 0;
                float bestT = 0;
                [loop] for (int i = 0; i < WAKE_CAPACITY - 1; i++)
                {
                    if (i >= count - 1) break;
                    float2 start = _PreviousWakePoints[i].xy;
                    float2 segment = _PreviousWakePoints[i + 1].xy - start;
                    float t = saturate(dot(position - start, segment) / max(dot(segment, segment), 0.0001));
                    float2 offset = position - (start + segment * t);
                    float distanceSq = dot(offset, offset);
                    if (distanceSq < bestDistanceSq)
                    {
                        bestDistanceSq = distanceSq;
                        best = i;
                        bestT = t;
                    }
                }

                float2 origin = _PreviousWakePoints[best].xy;
                float2 segmentVector = _PreviousWakePoints[best + 1].xy - origin;
                float segmentLength = length(segmentVector);
                float2 hullAxis = _PreviousWakePoints[1].xy - _PreviousWakePoints[0].xy;
                float2 back = segmentLength > 0.05
                    ? segmentVector / segmentLength
                    : normalize(hullAxis + 0.00001);
                float2 perpendicular = float2(-back.y, back.x);
                float along = dot(position - origin, back);
                float side = dot(position - origin, perpendicular);

                // Past either end the track continues straight on, so the coordinates stay
                // continuous: ahead of the bow for the bow wave, astern of the oldest sample for
                // the fading tail. Clamping to the end point would flip the lateral sign there.
                bool aheadOfBow = best == 0 && along < 0;
                bool pastOldest = best == count - 2 && along > segmentLength;
                float distanceBehindBow = aheadOfBow
                    ? _PreviousWakePoints[0].z + along
                    : pastOldest
                        ? _PreviousWakePoints[best + 1].z + along - segmentLength
                        : lerp(_PreviousWakePoints[best].z, _PreviousWakePoints[best + 1].z, bestT);
                float lateral = aheadOfBow || pastOldest
                    ? side
                    : (side >= 0 ? 1 : -1) * sqrt(bestDistanceSq);
                float coverage = pastOldest ? 1 - saturate((along - segmentLength) / 80) : 1;

                wake = float4(
                    distanceBehindBow,
                    lateral,
                    lerp(_PreviousWakePoints[best].w, _PreviousWakePoints[best + 1].w, bestT),
                    lerp(_PreviousWakeInfo[best].x, _PreviousWakeInfo[best + 1].x, bestT));
                frame = float4(
                    lerp(_PreviousWakeInfo[best].y, _PreviousWakeInfo[best + 1].y, bestT),
                    back,
                    coverage);
            }

            // Deep-water Kelvin wake of a source moving at `speed`, by stationary phase.
            // x: distance behind the source, y: lateral offset. Returns height and its gradient
            // along (x, y). Waves shorter than a few `footprint`s are faded out.
            float3 PreviousKelvinWake(float x, float y, float speed, float amplitude, float reference, float footprint)
            {
                if (x <= 0.2 || speed < 0.3 || amplitude <= 0) return 0;
                float ySign = y >= 0 ? 1 : -1;
                y = abs(y);
                float k0 = Gravity / (speed * speed);
                float r = y / x;
                // tan(19.47 deg): the Kelvin half-angle.
                const float cuspSlope = 0.353553;
                float q = r / cuspSlope;
                float inside = saturate(1 - (q - 1) * 6);
                if (inside <= 0) return 0;

                // Wave directions with tan(theta) = t solve 2 y t^2 - x t + y = 0.
                float disc = sqrt(saturate(1 - q * q));
                float transverseT = 2 * r / (1 + disc);
                float divergingT = (1 + disc) / (4 * max(r, 0.0001));
                float transverseRoot = sqrt(1 + transverseT * transverseT);
                float divergingRoot = sqrt(1 + divergingT * divergingT);
                float transversePhase = k0 * (x - y * transverseT) * transverseRoot;
                float divergingPhase = k0 * (x - y * divergingT) * divergingRoot;

                float shortest = max(footprint * 4, 0.6);
                float transverseFilter = saturate(TWO_PI / (k0 * transverseRoot * transverseRoot) / shortest - 1);
                float divergingFilter = saturate(TWO_PI / (k0 * divergingRoot * divergingRoot) / shortest - 1);

                // Energy piles up toward the cusp line and spreads out with distance.
                float envelope = amplitude * inside * pow(max(disc, 0.1), -0.45) *
                    sqrt(reference / (x + 0.3 * reference));
                float transverseAmplitude = 0.7 * envelope * transverseFilter;
                float divergingAmplitude = envelope * divergingFilter /
                    (1 + 0.35 * divergingT * divergingT);

                float height = transverseAmplitude * cos(transversePhase) +
                    divergingAmplitude * cos(divergingPhase);
                // At a stationary point the wave vector is k0 * sec(theta) * (1, -tan(theta)).
                float transverseSlope = -transverseAmplitude * sin(transversePhase) * k0 * transverseRoot;
                float divergingSlope = -divergingAmplitude * sin(divergingPhase) * k0 * divergingRoot;
                return float3(
                    height,
                    transverseSlope + divergingSlope,
                    -(transverseSlope * transverseT + divergingSlope * divergingT) * ySign);
            }

            // Signed distance to a stadium standing in for the waterline, and position along the hull.
            float PreviousHullDistance(float2 position, out float along)
            {
                float2 relative = position - _PreviousWakeShip.xy;
                float2 forward = _PreviousWakeShip.zw;
                along = dot(relative, forward);
                float across = dot(relative, float2(forward.y, -forward.x));
                float halfBeam = _PreviousWakeHull.y;
                float straight = max(_PreviousWakeHull.x - halfBeam, 0);
                float2 fromAxis = float2(across, along - clamp(along, -straight, straight));
                return length(fromAxis) - halfBeam;
            }

            // Bow pressure crest, midship drawdown and stern quarter rise, scaled by the
            // stagnation head. Estimated shape, tuned by eye.
            float PreviousHullWaveHeight(float2 position)
            {
                float along;
                float hullDistance = max(PreviousHullDistance(position, along), 0);
                float speed = max(_PreviousWakeHull.z, 0);
                float head = speed * speed / (2 * Gravity) * _PreviousWakeAmplitude / TunedWakeAmplitude;
                float u = along / max(_PreviousWakeHull.x, 1);
                float bow = smoothstep(0.3, 1.0, u);
                float midship = 1 - smoothstep(0.3, 0.8, abs(u));
                float stern = 1 - smoothstep(-1.0, -0.55, u);
                return head * (0.45 * bow * exp(-hullDistance / 5.5) -
                    0.10 * midship * exp(-hullDistance / 11) +
                    0.10 * stern * exp(-hullDistance / 8));
            }

            // Height and world-space xz gradient of every ship-generated wave.
            float3 PreviousOpenWaterShipWaves(float2 position, float4 wake, float4 frame, float footprint)
            {
                float speed = max(wake.w, 0);
                float amplitude = _PreviousWakeAmplitude * speed * speed / Gravity * frame.w * exp(-wake.z / 90);
                float3 kelvin = PreviousKelvinWake(wake.x, wake.y, speed, amplitude,
                    2 * max(_PreviousWakeHull.x, 1), footprint);
                float2 back = normalize(frame.yz + 0.00001);
                float2 lateralAxis = float2(-back.y, back.x);

                const float epsilon = 0.35;
                float hull = PreviousHullWaveHeight(position);
                float hullX = PreviousHullWaveHeight(position + float2(epsilon, 0));
                float hullZ = PreviousHullWaveHeight(position + float2(0, epsilon));
                float2 gradient = kelvin.y * back + kelvin.z * lateralAxis +
                    float2(hullX - hull, hullZ - hull) / epsilon;
                return float3(kelvin.x + hull, gradient);
            }

            // Churned water behind the propellers, widening with distance. x is aeration, which
            // lingers as a pale band; y is surface foam, which breaks up sooner.

            float3 PreviousShipWaves(float2 position, float4 wake, float4 frame, float footprint)
            {
                float4 shore = ShoreCoordinates(position);
                if (shore.x <= 0) return 0;
                float3 wave = PreviousOpenWaterShipWaves(position, wake, frame, footprint);
                if (shore.x < 22)
                {
                    float2 mirrored = position - 2 * shore.x * shore.yz;
                    float4 reflectedWake, reflectedFrame;
                    PreviousFindWakeCoordinates(mirrored, reflectedWake, reflectedFrame);
                    float3 reflected = PreviousOpenWaterShipWaves(mirrored, reflectedWake, reflectedFrame, footprint);
                    reflected.yz -= 2 * dot(reflected.yz, shore.yz) * shore.yz;
                    wave += reflected * (0.16 * (1 - smoothstep(3, 22, shore.x)));
                }
                return LimitShoreWave(wave, shore);
            }
            struct MotionOutput { float4 positionCS:SV_POSITION; float4 current:TEXCOORD0; float4 previous:TEXCOORD1; };
            MotionOutput WaterMotionVert(Attributes input)
            {
                MotionOutput o;
                Varyings surface=Vert(input);
                // The water renderer never moves and is drawn without per-object motion data,
                // so its current model matrix is also the previous one.
                float3 previous=TransformObjectToWorld(input.positionOS.xyz);
                previous.y+=BroadSurface(previous.xz,_RiverPreviousTime*_WaveSpeed)*_WaveHeight;
                if (_PreviousWakeCount>=2)
                {
                    float4 wake,frame;
                    PreviousFindWakeCoordinates(previous.xz,wake,frame);
                    previous.y+=PreviousShipWaves(previous.xz,wake,frame,VertexFootprint).x;
                }
                o.positionCS=surface.positionCS;
                o.current=mul(_NonJitteredViewProjMatrix,float4(surface.positionWS,1));
                o.previous=mul(_PrevViewProjMatrix,float4(previous,1));
                return o;
            }
            // Verification hook: GraphicsPhaseOneCheck sets it to prove the pass reaches the texture.
            float _RiverMotionProbe;
            half4 WaterMotionFrag(MotionOutput i):SV_Target
            {
                if (_RiverMotionProbe > 0.5) return half4(0.001, 0.001, 0, 0);
                // CalcNdcMotionVectorFromCsPositions returns zero unless per-object motion data is
                // bound, and requesting that data filters out this stationary renderer.
                float2 velocity = i.current.xy / i.current.w - i.previous.xy / i.previous.w;
                #if UNITY_UV_STARTS_AT_TOP
                    velocity.y = -velocity.y;
                #endif
                return half4(velocity * 0.5, 0, 0);
            }
            ENDHLSL
        }
    }
}
