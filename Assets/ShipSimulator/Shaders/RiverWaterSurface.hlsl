            #pragma target 3.5
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "RiverClouds.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            // Must match ShipWakeTrack.Capacity.
            #define WAKE_CAPACITY 48
            static const float Gravity = 9.81;
            // Vertex spacing of the refined water mesh; shorter waves are left to the normals.
            static const float VertexFootprint = 1.7;
            // Estimated visual gain: seen from the bridge, ship waves read through their slopes,
            // which would otherwise drown under the ambient ripples.
            static const float ShipWaveSlopeGain = 1.15;
            // ShipWakeController's default amplitude, where the hull wave shape was tuned.
            static const float TunedWakeAmplitude = 0.07;

            TEXTURE2D(_RiverPlanarReflection);
            SAMPLER(sampler_RiverPlanarReflection);
            float4x4 _RiverReflectionVP;
            float _RiverReflectionAvailable;
            TEXTURE2D(_RippleNormal);
            SAMPLER(sampler_RippleNormal);

            // Published by ShipWakeController.
            float4 _WakePoints[WAKE_CAPACITY];
            float4 _WakeInfo[WAKE_CAPACITY];
            float _WakeCount;
            float4 _WakeBounds;
            float4 _WakeShip;
            float4 _WakeHull;
            float _WakeAmplitude;
            // Set by WeatherController.


            CBUFFER_START(UnityPerMaterial)
                half4 _ShallowColor;
                half4 _DeepColor;
                half4 _ReflectionTint;
                half4 _FoamColor;
                half4 _AeratedColor;
                float _RippleTileM;
                half _RippleStrength;
                half _ReflectionDistortion;
                half _Smoothness;
                half _WaveScale;
                half _WaveHeight;
                half _WaveSpeed;
                half4 _FlowDirection;
                half _StreakScale;
                half _Turbidity;
                half _ReflectionStrength;
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
                half fogFactor : TEXCOORD2;
            };

            float Hash21(float2 position)
            {
                position = frac(position * float2(123.34, 345.45));
                position += dot(position, position + 34.345);
                return frac(position.x * position.y);
            }

            float2 Hash22(float2 position)
            {
                float3 a = frac(position.xyx * float3(123.34, 234.34, 345.65));
                a += dot(a, a + 34.45);
                return frac(float2(a.x * a.y, a.y * a.z));
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

            // Distance between the two nearest cell points: zero along cell borders.
            float CellEdges(float2 position)
            {
                float2 cell = floor(position);
                float2 fraction = frac(position);
                float first = 8;
                float second = 8;
                for (int y = -1; y <= 1; y++)
                for (int x = -1; x <= 1; x++)
                {
                    float2 neighbour = float2(x, y);
                    float2 offset = neighbour + Hash22(cell + neighbour) - fraction;
                    float distanceSq = dot(offset, offset);
                    if (distanceSq < first)
                    {
                        second = first;
                        first = distanceSq;
                    }
                    else if (distanceSq < second)
                    {
                        second = distanceSq;
                    }
                }
                return sqrt(second) - sqrt(first);
            }

            float2 FlowDirection()
            {
                return normalize(_FlowDirection.xy + float2(0.0001, 0.0001));
            }

            float2 FlowBasisX()
            {
                float2 direction = FlowDirection();
                return float2(direction.y, -direction.x);
            }

            float BroadSurface(float2 worldXZ, float time)
            {
                float2 flow = FlowDirection();
                float2 across = FlowBasisX();
                float2 firstDirection = normalize(flow + across * 0.48);
                float2 secondDirection = normalize(flow - across * 0.72);
                float first = sin(dot(worldXZ, firstDirection) * _WaveScale * 1.9 - time);
                float second = sin(dot(worldXZ, secondDirection) * _WaveScale * 2.8 - time * 1.27);
                float irregularity = ValueNoise(
                    worldXZ * float2(0.047, 0.031) - flow * time * 0.11) * 2.0 - 1.0;
                return first * 0.23 + second * 0.15 + irregularity * 0.62;
            }

            // World-space xz of the ripple normal, from three drifting layers of the tileable map.
            half2 RippleNormal(float2 positionXZ, float time)
            {
                float2 flow = FlowDirection();
                float2 across = FlowBasisX();
                float2 wind = _RiverWindTravel.xz * 0.003;
                float2 rotated = float2(
                    dot(positionXZ, float2(0.8, 0.6)), dot(positionXZ, float2(-0.6, 0.8)));
                float2 uvA = positionXZ / _RippleTileM + flow * time * 0.05 + wind;
                float2 uvB = rotated / (_RippleTileM * 2.9) + (flow - across) * time * 0.018;
                float2 uvC = positionXZ / (_RippleTileM * 0.41) - across * time * 0.07 + wind * 2;
                half2 a = UnpackNormal(SAMPLE_TEXTURE2D(_RippleNormal, sampler_RippleNormal, uvA)).xy;
                half2 b = UnpackNormal(SAMPLE_TEXTURE2D(_RippleNormal, sampler_RippleNormal, uvB)).xy;
                half2 c = UnpackNormal(SAMPLE_TEXTURE2D(_RippleNormal, sampler_RippleNormal, uvC)).xy;
                // Layer B was sampled in rotated space, so rotate its slope back to world axes.
                b = half2(0.8h * b.x - 0.6h * b.y, 0.6h * b.x + 0.8h * b.y);
                return a * 0.6h + b * 0.55h + c * 0.3h;
            }

            // Cat's paws: patches where a gust roughens the water, carried downwind with the gust.
            half GustPatches(float2 positionXZ)
            {
                return lerp(0.45h, 1.55h, RiverGust(positionXZ, _RiverWindTravel.xz, _Time.y));
            }

            static const float WindWavelengths[2] = { 2.3, 5.1 };

            // Short wind waves on six headings per band. Headings are fixed and weighted toward the
            // wind (the weights sum to 1 for any direction), so a wind shift turns the waves smoothly
            // instead of swinging world-space coordinates. Deep-water dispersion sets their speed.
            // Slopes are estimated visual values for a short river fetch.
            half2 WindWaveNormal(float2 positionXZ, float footprint)
            {
                float speed = _RiverWind.w;
                if (speed < 0.01) return 0;
                float2 wind = _RiverWind.xz / speed;
                half steepness = lerp(0.012h, 0.08h, saturate(speed / 10));
                half2 slope = 0;
                for (int band = 0; band < 2; band++)
                {
                    float wavelength = WindWavelengths[band];
                    half filter = saturate(wavelength / (footprint * 4) - 0.5);
                    if (filter <= 0) continue;
                    float k = TWO_PI / wavelength;
                    float omega = sqrt(Gravity * k);
                    // Wave groups: amplitude varies in patches that drift downwind.
                    half groups = 0.35h + 1.3h * ValueNoise((positionXZ - _RiverWindTravel.xz * 0.35) / (wavelength * 5) + band * 13.1);
                    for (int heading = 0; heading < 6; heading++)
                    {
                        float angle = (heading + band * 0.5) * PI / 3;
                        float2 direction = float2(cos(angle), sin(angle));
                        half weight = saturate(dot(direction, wind));
                        weight = weight * weight / 1.5;
                        if (weight <= 0) continue;
                        float phase = k * dot(positionXZ, direction) - omega * _Time.y + heading * 1.7 + band * 4.1;
                        slope -= direction * (cos(phase) * weight * filter * groups);
                    }
                }
                return slope * steepness;
            }

            // Nearest point on the published track, with values interpolated between samples.
            // wake: distance behind the bow along the wake, lateral offset, age, forward speed.
            // frame: propeller wash, backward track direction xz, coverage.
            void FindWakeCoordinates(float2 position, out float4 wake, out float4 frame)
            {
                wake = float4(-1000, 0, 0, 0);
                frame = float4(0, 0, 1, 0);
                int count = (int)_WakeCount;
                if (count < 2) return;
                // Beyond this the Kelvin wedge of the longest published track has faded.
                const float reach = 420;
                if (any(position < _WakeBounds.xy - reach) || any(position > _WakeBounds.zw + reach))
                    return;

                float bestDistanceSq = 1e12;
                int best = 0;
                float bestT = 0;
                [loop] for (int i = 0; i < WAKE_CAPACITY - 1; i++)
                {
                    if (i >= count - 1) break;
                    float2 start = _WakePoints[i].xy;
                    float2 segment = _WakePoints[i + 1].xy - start;
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

                float2 origin = _WakePoints[best].xy;
                float2 segmentVector = _WakePoints[best + 1].xy - origin;
                float segmentLength = length(segmentVector);
                float2 hullAxis = _WakePoints[1].xy - _WakePoints[0].xy;
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
                    ? _WakePoints[0].z + along
                    : pastOldest
                        ? _WakePoints[best + 1].z + along - segmentLength
                        : lerp(_WakePoints[best].z, _WakePoints[best + 1].z, bestT);
                float lateral = aheadOfBow || pastOldest
                    ? side
                    : (side >= 0 ? 1 : -1) * sqrt(bestDistanceSq);
                float coverage = pastOldest ? 1 - saturate((along - segmentLength) / 80) : 1;

                wake = float4(
                    distanceBehindBow,
                    lateral,
                    lerp(_WakePoints[best].w, _WakePoints[best + 1].w, bestT),
                    lerp(_WakeInfo[best].x, _WakeInfo[best + 1].x, bestT));
                frame = float4(
                    lerp(_WakeInfo[best].y, _WakeInfo[best + 1].y, bestT),
                    back,
                    coverage);
            }

            // Deep-water Kelvin wake of a source moving at `speed`, by stationary phase.
            // x: distance behind the source, y: lateral offset. Returns height and its gradient
            // along (x, y). Waves shorter than a few `footprint`s are faded out.
            float3 KelvinWake(float x, float y, float speed, float amplitude, float reference, float footprint)
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
            float HullDistance(float2 position, out float along)
            {
                float2 relative = position - _WakeShip.xy;
                float2 forward = _WakeShip.zw;
                along = dot(relative, forward);
                float across = dot(relative, float2(forward.y, -forward.x));
                float halfBeam = _WakeHull.y;
                float straight = max(_WakeHull.x - halfBeam, 0);
                float2 fromAxis = float2(across, along - clamp(along, -straight, straight));
                return length(fromAxis) - halfBeam;
            }

            // Bow pressure crest, midship drawdown and stern quarter rise, scaled by the
            // stagnation head. Estimated shape, tuned by eye.
            float HullWaveHeight(float2 position)
            {
                float along;
                float hullDistance = max(HullDistance(position, along), 0);
                float speed = max(_WakeHull.z, 0);
                float head = speed * speed / (2 * Gravity) * _WakeAmplitude / TunedWakeAmplitude;
                float u = along / max(_WakeHull.x, 1);
                float bow = smoothstep(0.3, 1.0, u);
                float midship = 1 - smoothstep(0.3, 0.8, abs(u));
                float stern = 1 - smoothstep(-1.0, -0.55, u);
                return head * (0.45 * bow * exp(-hullDistance / 5.5) -
                    0.10 * midship * exp(-hullDistance / 11) +
                    0.10 * stern * exp(-hullDistance / 8));
            }

            TEXTURE2D(_RiverShoreProfile);
            SAMPLER(sampler_RiverShoreProfile);
            float4 _RiverShoreRange;
            // x: inward distance, yz: inward normal, w: estimated shallow depth.
            float4 ShoreCoordinates(float2 position)
            {
                if (_RiverShoreRange.w < 0.5 || position.y < _RiverShoreRange.x ||
                    position.y > _RiverShoreRange.x + _RiverShoreRange.y) return float4(1000, 1, 0, 10);
                float u = (position.y - _RiverShoreRange.x) / _RiverShoreRange.y;
                float texel = 1 / _RiverShoreRange.z;
                float2 uv = float2(lerp(0.5 * texel, 1 - 0.5 * texel, u), 0.5);
                float2 banks = SAMPLE_TEXTURE2D_LOD(_RiverShoreProfile, sampler_RiverShoreProfile, uv, 0).rg;
                float2 next = SAMPLE_TEXTURE2D_LOD(_RiverShoreProfile, sampler_RiverShoreProfile,
                    uv + float2(texel, 0), 0).rg;
                float2 tangent = (next - banks) * (_RiverShoreRange.z - 1) / _RiverShoreRange.y;
                bool left = position.x - banks.x < banks.y - position.x;
                float slope = left ? tangent.x : tangent.y;
                float2 inward = normalize(float2(1, -slope)) * (left ? 1 : -1);
                float distance = (left ? position.x - banks.x : banks.y - position.x) / sqrt(1 + slope * slope);
                return float4(distance, inward, max(distance, 0) * 0.14);
            }

            float3 LimitShoreWave(float3 wave, float4 shore)
            {
                // Soft banks absorb most incident energy. A depth-limited crest breaks before dry land.
                float wet = smoothstep(0, 1.8, shore.x);
                float limit = max(0.55 * shore.w, 0.001);
                float ratio = wave.x / limit;
                float bounded = limit * ratio / sqrt(1 + ratio * ratio);
                float slopeScale = pow(1 + ratio * ratio, -1.5);
                return float3(bounded, wave.yz * slopeScale) * wet;
            }

            // Height and world-space xz gradient of every ship-generated wave.
            float3 OpenWaterShipWaves(float2 position, float4 wake, float4 frame, float footprint)
            {
                float speed = max(wake.w, 0);
                float amplitude = _WakeAmplitude * speed * speed / Gravity * frame.w * exp(-wake.z / 90);
                float3 kelvin = KelvinWake(wake.x, wake.y, speed, amplitude,
                    2 * max(_WakeHull.x, 1), footprint);
                float2 back = normalize(frame.yz + 0.00001);
                float2 lateralAxis = float2(-back.y, back.x);

                const float epsilon = 0.35;
                float hull = HullWaveHeight(position);
                float hullX = HullWaveHeight(position + float2(epsilon, 0));
                float hullZ = HullWaveHeight(position + float2(0, epsilon));
                float2 gradient = kelvin.y * back + kelvin.z * lateralAxis +
                    float2(hullX - hull, hullZ - hull) / epsilon;
                return float3(kelvin.x + hull, gradient);
            }

            float3 ShipWaves(float2 position, float4 wake, float4 frame, float footprint)
            {
                float4 shore = ShoreCoordinates(position);
                if (shore.x <= 0) return 0;
                float3 wave = OpenWaterShipWaves(position, wake, frame, footprint);
                if (shore.x < 22)
                {
                    float2 mirrored = position - 2 * shore.x * shore.yz;
                    float4 reflectedWake, reflectedFrame;
                    FindWakeCoordinates(mirrored, reflectedWake, reflectedFrame);
                    float3 reflected = OpenWaterShipWaves(mirrored, reflectedWake, reflectedFrame, footprint);
                    reflected.yz -= 2 * dot(reflected.yz, shore.yz) * shore.yz;
                    wave += reflected * (0.16 * (1 - smoothstep(3, 22, shore.x)));
                }
                return LimitShoreWave(wave, shore);
            }

            // Churned water behind the propellers, widening with distance. x is aeration, which
            // lingers as a pale band; y is surface foam, which breaks up sooner.
            half2 PropellerWash(float4 wake, float4 frame)
            {
                float hullLength = 2 * _WakeHull.x;
                float behind = wake.x - hullLength + 8;
                if (behind <= 0) return 0;
                float halfWidth = _WakeHull.y * (0.6 + 1.6 * sqrt(behind / hullLength));
                float profile = exp(-2 * wake.y * wake.y / (halfWidth * halfWidth));
                float strength = saturate(frame.x * 0.8 + saturate(wake.w / 5) * 0.5) *
                    profile * saturate(behind / 12) * frame.w;
                return saturate(half2(strength * exp(-wake.z / 100), strength * exp(-wake.z / 28)));
            }

            half HullFoam(float hullDistance, float along)
            {
                float speed = max(_WakeHull.z, 0);
                float bow = smoothstep(0.2, 1.0, along / max(_WakeHull.x, 1));
                return saturate(speed / 3.5) * exp(-hullDistance / (0.6 + 1.6 * bow)) * lerp(0.3, 0.75, bow);
            }

            // Streaky foam: noise stretched along the track, with bubbles only near the camera.
            half FoamPattern(float2 position, float2 back, float footprint)
            {
                float2 aligned = float2(dot(position, back) * 0.3, dot(position, float2(-back.y, back.x)));
                half detail = saturate(1 - footprint * 2);
                half large = ValueNoise(aligned * 0.35);
                half medium = ValueNoise(aligned * 1.3 + 7.1);
                half small = lerp(0.5h, ValueNoise(position * 4.3 + 3.7), detail);
                half bubbles = lerp(0.2h, 1 - smoothstep(0.0, 0.16, CellEdges(position * 2.2)), detail);
                return saturate(large * 0.45h + medium * 0.3h + small * 0.17h + bubbles * 0.12h);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float time = _Time.y * _WaveSpeed;
                float broadWave = BroadSurface(positionWS.xz, time);
                float epsilon = 0.35;
                float waveX = BroadSurface(positionWS.xz + float2(epsilon, 0), time);
                float waveZ = BroadSurface(positionWS.xz + float2(0, epsilon), time);
                float3 tangentX = float3(epsilon, (waveX - broadWave) * _WaveHeight, 0);
                float3 tangentZ = float3(0, (waveZ - broadWave) * _WaveHeight, epsilon);

                float shipWave = 0;
                if (_WakeCount >= 2)
                {
                    float4 wake;
                    float4 wakeFrame;
                    FindWakeCoordinates(positionWS.xz, wake, wakeFrame);
                    shipWave = ShipWaves(positionWS.xz, wake, wakeFrame, VertexFootprint).x;
                }
                positionWS.y += broadWave * _WaveHeight + shipWave;

                output.positionWS = positionWS;
                output.normalWS = normalize(cross(tangentZ, tangentX));
                output.positionCS = TransformWorldToHClip(positionWS);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            float _RiverRain;
            half2 RainRippleNormal(float2 position, float footprint)
            {
                float visibility = saturate(1 - footprint * 8);
                if (_RiverRain < 0.001 || visibility < 0.001) return 0;
                float2 cell = floor(position * 0.85);
                half2 slope = 0;
                for (int y = -1; y <= 1; y++)
                for (int x = -1; x <= 1; x++)
                {
                    float2 id = cell + float2(x,y);
                    float2 random = Hash22(id);
                    float age = frac(_Time.y * 0.85 + random.x);
                    float2 delta = position - (id + random) / 0.85;
                    float radius = length(delta);
                    float ring = radius - age * 0.72;
                    float envelope = exp(-ring * ring * 180) * sin(age * PI) * exp(-age * 2);
                    slope += delta / max(radius,0.02) * cos(ring * 55) * envelope * 0.15;
                }
                return slope * _RiverRain * visibility;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 positionWS = input.positionWS;
                float time = _Time.y * _WaveSpeed;
                float distanceToCamera = distance(_WorldSpaceCameraPos, positionWS);
                float footprint = max(length(fwidth(positionWS.xz)), 0.001);
                float2 flow = FlowDirection();
                float2 across = FlowBasisX();

                half gust = GustPatches(positionWS.xz);
                half2 ripple = (RippleNormal(positionWS.xz, time) * _RippleStrength *
                    lerp(1.0h, 0.5h, saturate(distanceToCamera / 900)) +
                    WindWaveNormal(positionWS.xz, footprint)) * gust + RainRippleNormal(positionWS.xz, footprint);

                float3 shipWave = 0;
                half aeration = 0;
                half foamAmount = 0;
                float hullDistance = 10000;
                // Searched per pixel: coordinates interpolated from the vertices form jagged seams
                // wherever the nearest track segment changes inside a triangle.
                float4 wake;
                float4 wakeFrame;
                FindWakeCoordinates(positionWS.xz, wake, wakeFrame);
                if (_WakeCount >= 2)
                {
                    shipWave = ShipWaves(positionWS.xz, wake, wakeFrame, footprint);
                    float hullAlong;
                    hullDistance = max(HullDistance(positionWS.xz, hullAlong), 0);
                    half2 wash = PropellerWash(wake, wakeFrame);
                    aeration = wash.x;
                    half crest = saturate((shipWave.x - 0.32) * 2.5) * exp(-max(wake.x, 0) / 260);
                    foamAmount = max(max(wash.y, HullFoam(hullDistance, hullAlong)), crest);
                }

                half3 normalWS = normalize(half3(
                    input.normalWS.x + ripple.x - shipWave.y * ShipWaveSlopeGain,
                    input.normalWS.y,
                    input.normalWS.z + ripple.y - shipWave.z * ShipWaveSlopeGain));
                half3 viewDirection = SafeNormalize(GetWorldSpaceViewDir(positionWS));
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(positionWS));
                mainLight.shadowAttenuation *= RiverCloudShadow(positionWS, mainLight.direction);
                half NdotV = saturate(dot(normalWS, viewDirection));
                half fresnel = 0.02h + 0.98h * pow(1.0h - NdotV, 5.0h);
                half diffuse = saturate(dot(normalWS, mainLight.direction)) * mainLight.shadowAttenuation;

                float downstream = dot(positionWS.xz, flow);
                float crossRiver = dot(positionWS.xz, across);
                half streakNoise = ValueNoise(float2(
                    crossRiver * _StreakScale,
                    downstream * _StreakScale * 0.13 - time * 0.62));
                half narrowStreaks = smoothstep(0.86h, 0.97h, streakNoise + length(ripple) * 0.05h);

                float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);
                float sceneDepth = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
                float waterDepth = -TransformWorldToView(positionWS).z;
                float opticalDepth = max(0, sceneDepth - waterDepth);
                // Convert eye-space separation to vertical depth so shallows remain consistent at grazing views.
                float depth = opticalDepth * abs(viewDirection.y) /
                    max(abs(TransformWorldToViewDir(viewDirection).z), 0.05);
                half depthVariation = exp(-depth * lerp(1.5, 2.4, saturate(_Turbidity)));
                half shoreline = 1 - saturate(depth / 0.65);
                float4 bank = ShoreCoordinates(positionWS.xz);
                half breaking = saturate(abs(shipWave.x) / max(bank.w, 0.03) - 0.22) *
                    (1 - smoothstep(0.2, 1.6, bank.w)) * smoothstep(0, 0.35, bank.x);
                foamAmount = max(foamAmount, breaking * 2.5);
                // The waterline against a moving hull churns much more than a quiet bank.
                foamAmount = max(foamAmount,
                    shoreline * (0.07h + 0.55h * saturate(_WakeHull.z / 3) * exp(-hullDistance / 3))) +
                    narrowStreaks * 0.2h;

                half3 lighting = SampleSH(normalWS) * 0.65 + diffuse * mainLight.color * 0.55;
                half3 muddyShallow = lerp(
                    _ShallowColor.rgb,
                    half3(0.22h, 0.27h, 0.19h),
                    _Turbidity * 0.38h);
                half3 waterColor = lerp(_DeepColor.rgb, muddyShallow, depthVariation) * lighting;
                waterColor = lerp(waterColor, _AeratedColor.rgb * lighting, aeration * 0.85h);

                half perceptualRoughness = saturate(1.0h - _Smoothness + _RiverRain * 0.15h);
                half3 reflection = GlossyEnvironmentReflection(
                    reflect(-viewDirection, normalWS), perceptualRoughness, 1.0h);
                reflection = lerp(reflection, reflection * _ReflectionTint.rgb, 0.48h);

                float4 reflected = mul(_RiverReflectionVP, float4(positionWS, 1));
                float2 reflectionUV = reflected.xy / max(reflected.w, 0.001) * 0.5 + 0.5;
                #if UNITY_UV_STARTS_AT_TOP
                    reflectionUV.y = 1 - reflectionUV.y;
                #endif
                // A tilted facet bends the mirrored ray by an angle, so the offset does not fade
                // with distance; it stretches reflections along the view as on real water.
                float2 viewRight = normalize(UNITY_MATRIX_V[0].xz + 0.0001);
                float2 viewForward = normalize(-UNITY_MATRIX_V[2].xz + 0.0001);
                reflectionUV += float2(dot(normalWS.xz, viewRight) * 0.5, dot(normalWS.xz, viewForward)) *
                    _ReflectionDistortion;
                // Clamped at the screen edge instead of falling back: the environment probe is not
                // rebaked for the cloud sky and renders there as a black fringe.
                half3 mirrored = SAMPLE_TEXTURE2D_LOD(_RiverPlanarReflection, sampler_RiverPlanarReflection,
                    saturate(reflectionUV), lerp(0.4, 2.4, perceptualRoughness) + aeration * 2).rgb;
                reflection = lerp(reflection, mirrored, _RiverReflectionAvailable);

                half reflectance = saturate(fresnel * lerp(0.75h, 1.15h, saturate(_ReflectionStrength))) *
                    (1 - aeration * 0.5h);
                half3 color = lerp(waterColor, reflection, reflectance);

                half3 halfDirection = SafeNormalize(mainLight.direction + viewDirection);
                half NdotH = saturate(dot(normalWS, halfDirection));
                // Far water averages many facets, so its glint widens and dims.
                half exponent = lerp(lerp(80.0h, 1200.0h, _Smoothness), 60.0h, saturate(distanceToCamera / 900));
                half glint = pow(NdotH, exponent) * (exponent + 8) / (8 * PI);
                half sunFresnel = 0.02h + 0.98h * pow(1.0h - saturate(dot(halfDirection, viewDirection)), 5.0h);
                color += mainLight.color * mainLight.shadowAttenuation *
                    min(glint * sunFresnel, 24.0h) * (1 - aeration);

                half foam = 0;
                if (foamAmount > 0.01)
                {
                    half pattern = FoamPattern(positionWS.xz - flow * _Time.y * _WaveSpeed * 0.8,
                        normalize(wakeFrame.yz + 0.00001), footprint);
                    foam = smoothstep(0.0h, 0.6h, foamAmount + pattern - 1.0h) * 0.92h;
                }
                half3 foamLight = SampleSH(half3(0, 1, 0)) * 0.9 +
                    mainLight.color * (saturate(mainLight.direction.y) * mainLight.shadowAttenuation * 0.85 + 0.08);
                color = lerp(color, _FoamColor.rgb * foamLight, foam);
                color = MixFog(color, InitializeInputDataFog(float4(positionWS, 1), input.fogFactor));

                // Reveal wet sediment at the contact edge, with suspended silt hiding the deeper bed.
                half transmission = exp(-depth * lerp(2.4, 4.0, saturate(_Turbidity)));
                half contact = smoothstep(0, 0.12, opticalDepth);
                half alpha = saturate((1 - transmission + transmission * reflectance) * _Opacity + foam) * contact;
                return half4(color, alpha);
            }
