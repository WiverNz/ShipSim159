Shader "ShipSimulator/RiverFoliage"
{
    Properties
    {
        _BaseColor("Leaf tint", Color) = (0.32, 0.43, 0.16, 1)
        _WindStrength("Wind movement", Range(0,1)) = 0.2
    }
    SubShader
    {
        Tags { "RenderType"="TransparentCutout" "Queue"="AlphaTest" "RenderPipeline"="UniversalPipeline" }
        Cull Off
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "RiverClouds.hlsl"
        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half _WindStrength;
        CBUFFER_END
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/MotionVectorsCommon.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
        float _RiverPreviousTime;
        float3 LeafPosition(float3 world, half4 color, float2 uv, float time)
        {
            float phase=dot(world.xz,float2(0.12,0.19))+time*1.2;
            world.xz+=float2(sin(phase),cos(phase*0.77))*_WindStrength*color.a*uv.y;
            return world;
        }
        struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0; half4 color:COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
        struct Varyings { float4 positionCS:SV_POSITION; float3 world:TEXCOORD0; half3 normal:TEXCOORD1; float2 uv:TEXCOORD2; half4 color:COLOR; half fog:TEXCOORD3; };
        Varyings Vert(Attributes input)
        {
            UNITY_SETUP_INSTANCE_ID(input);
            Varyings o;
            o.world=TransformObjectToWorld(input.positionOS.xyz);
            o.world=LeafPosition(o.world,input.color,input.uv,_Time.y);
            o.positionCS=TransformWorldToHClip(o.world);
            o.normal=TransformObjectToWorldNormal(input.normalOS);
            o.uv=input.uv; o.color=input.color; o.fog=ComputeFogFactor(o.positionCS.z);
            return o;
        }
        void LeafClip(float2 uv, float4 positionCS)
        {
            #if defined(LOD_FADE_CROSSFADE)
                LODFadeCrossFade(positionCS);
            #endif
            float2 p=uv*2-1;
            float width=pow(saturate(1-p.y*p.y),0.72);
            clip(width-abs(p.x)*1.08-0.06);
        }
        ENDHLSL
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            half4 Frag(Varyings i, FRONT_FACE_TYPE face:FRONT_FACE_SEMANTIC):SV_Target
            {
                LeafClip(i.uv,i.positionCS);
                half3 n=normalize(i.normal)*IS_FRONT_VFACE(face,1,-1);
                Light sun=GetMainLight(TransformWorldToShadowCoord(i.world));
                sun.shadowAttenuation*=RiverCloudShadow(i.world,sun.direction);
                half vein=exp(-abs(i.uv.x-0.5)*70);
                half3 albedo=_BaseColor.rgb*i.color.rgb*lerp(0.72,1.18,i.uv.y);
                albedo+=vein*half3(0.035,0.045,0.012);
                half diffuse=saturate(dot(n,sun.direction)*0.65+0.35);
                half backlight=pow(saturate(dot(-GetWorldSpaceNormalizeViewDir(i.world),sun.direction)),4)*0.35;
                half3 light=SampleSH(n)*0.75+sun.color*(diffuse+backlight)*lerp(0.25,1,sun.shadowAttenuation);
                return half4(MixFog(albedo*light,i.fog),1);
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
            #pragma vertex Vert
            #pragma fragment Shadow
            #pragma multi_compile_instancing
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            half4 Shadow(Varyings i):SV_Target { LeafClip(i.uv,i.positionCS); return 0; }
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
            #pragma fragment Depth
            #pragma multi_compile_instancing
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            half4 Depth(Varyings i):SV_Target { LeafClip(i.uv,i.positionCS); return 0; }
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }
            ZWrite On
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Normals
            #pragma multi_compile_instancing
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            half4 Normals(Varyings i):SV_Target { LeafClip(i.uv,i.positionCS); return half4(normalize(i.normal),0); }
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
            struct MotionOutput { float4 positionCS:SV_POSITION; float4 current:TEXCOORD0; float4 previous:TEXCOORD1; float2 uv:TEXCOORD2; };
            MotionOutput MotionVert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                MotionOutput o;
                float3 current=LeafPosition(TransformObjectToWorld(input.positionOS.xyz),input.color,input.uv,_Time.y);
                float3 previous=LeafPosition(mul(UNITY_PREV_MATRIX_M,input.positionOS).xyz,input.color,input.uv,_RiverPreviousTime);
                o.positionCS=TransformWorldToHClip(current);
                o.current=mul(_NonJitteredViewProjMatrix,float4(current,1));
                o.previous=mul(_PrevViewProjMatrix,float4(previous,1));
                o.uv=input.uv;
                return o;
            }
            half4 MotionFrag(MotionOutput i):SV_Target
            {
                LeafClip(i.uv,i.positionCS);
                return half4(CalcNdcMotionVectorFromCsPositions(i.current,i.previous),0,0);
            }
            ENDHLSL
        }
    }
}
