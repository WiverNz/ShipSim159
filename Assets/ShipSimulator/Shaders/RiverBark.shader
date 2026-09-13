Shader "ShipSimulator/RiverBark"
{
    Properties
    {
        _BaseColor("Bark tint", Color) = (0.22, 0.18, 0.13, 1)
        _Smoothness("Smoothness", Range(0, 1)) = 0.18
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
        #include "RiverClouds.hlsl"
        #include "RiverVegetationWind.hlsl"
        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half _Smoothness;
        CBUFFER_END

        struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
        struct Varyings { float4 positionCS : SV_POSITION; float3 world : TEXCOORD0; half3 normal : TEXCOORD1; half fog : TEXCOORD2; };

        // Branches bend with the same wind as their leaves, so the canopy never detaches from its wood.
        float3 BarkPosition(float3 positionOS)
        {
            return RiverVegetationSway(TransformObjectToWorld(positionOS), TransformObjectToWorld(float3(0, 0, 0)), 0,
                _RiverWind, _RiverWindTravel.xz, _Time.y);
        }

        Varyings Vert(Attributes input)
        {
            UNITY_SETUP_INSTANCE_ID(input);
            Varyings o;
            o.world = BarkPosition(input.positionOS.xyz);
            o.positionCS = TransformWorldToHClip(o.world);
            o.normal = TransformObjectToWorldNormal(input.normalOS);
            o.fog = ComputeFogFactor(o.positionCS.z);
            return o;
        }

        void CrossFade(float4 positionCS)
        {
            #if defined(LOD_FADE_CROSSFADE)
                LODFadeCrossFade(positionCS);
            #endif
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            half4 Frag(Varyings i) : SV_Target
            {
                CrossFade(i.positionCS);
                half3 n = normalize(i.normal);
                Light sun = GetMainLight(TransformWorldToShadowCoord(i.world));
                sun.shadowAttenuation *= RiverCloudShadow(i.world, sun.direction);
                half3 halfDirection = SafeNormalize(sun.direction + GetWorldSpaceNormalizeViewDir(i.world));
                half specular = pow(saturate(dot(n, halfDirection)), lerp(8.0h, 64.0h, _Smoothness)) * _Smoothness * 0.25h;
                half3 direct = sun.color * sun.shadowAttenuation;
                half3 color = _BaseColor.rgb * (SampleSH(n) + direct * saturate(dot(n, sun.direction))) + direct * specular;
                return half4(MixFog(color, i.fog), 1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On
            ColorMask 0
            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_instancing
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            // Set by URP while rendering shadow maps.
            float3 _LightDirection;
            float4 ShadowVert(Attributes input) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float3 world = BarkPosition(input.positionOS.xyz);
                float3 normal = TransformObjectToWorldNormal(input.normalOS);
                return ApplyShadowClamping(TransformWorldToHClip(ApplyShadowBias(world, normal, _LightDirection)));
            }
            half4 ShadowFrag(float4 positionCS : SV_POSITION) : SV_Target
            {
                CrossFade(positionCS);
                return 0;
            }
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On
            ColorMask R
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            half4 DepthFrag(Varyings i) : SV_Target
            {
                CrossFade(i.positionCS);
                return 0;
            }
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }
            ZWrite On
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment NormalsFrag
            #pragma multi_compile_instancing
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            half4 NormalsFrag(Varyings i) : SV_Target
            {
                CrossFade(i.positionCS);
                return half4(normalize(i.normal), 0);
            }
            ENDHLSL
        }
        Pass
        {
            Name "MotionVectors"
            Tags { "LightMode"="MotionVectors" }
            ColorMask RG
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex MotionVert
            #pragma fragment MotionFrag
            #pragma multi_compile_instancing
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/MotionVectorsCommon.hlsl"
            struct MotionOutput { float4 positionCS : SV_POSITION; float4 current : TEXCOORD0; float4 previous : TEXCOORD1; };
            MotionOutput MotionVert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                MotionOutput o;
                float3 current = BarkPosition(input.positionOS.xyz);
                float3 previous = RiverVegetationSway(mul(UNITY_PREV_MATRIX_M, input.positionOS).xyz,
                    mul(UNITY_PREV_MATRIX_M, float4(0, 0, 0, 1)).xyz, 0,
                    _RiverPreviousWind, _RiverPreviousWindTravel.xz, _RiverPreviousTime);
                o.positionCS = TransformWorldToHClip(current);
                o.current = mul(_NonJitteredViewProjMatrix, float4(current, 1));
                o.previous = mul(_PrevViewProjMatrix, float4(previous, 1));
                return o;
            }
            half4 MotionFrag(MotionOutput i) : SV_Target
            {
                CrossFade(i.positionCS);
                return half4(CalcNdcMotionVectorFromCsPositions(i.current, i.previous), 0, 0);
            }
            ENDHLSL
        }
    }
}
