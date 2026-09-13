Shader "ShipSimulator/RiverGround"
{
    Properties
    {
        _SoilColor("Alluvial soil", Color) = (0.25, 0.22, 0.16, 1)
        _GrassColor("Meadow grass", Color) = (0.22, 0.29, 0.12, 1)
        _SandColor("Shore sediment", Color) = (0.43, 0.36, 0.24, 1)
        _DryColor("Dry grass and sand", Color) = (0.39, 0.36, 0.23, 1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _SoilColor, _GrassColor, _DryColor, _SandColor;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; half4 color:COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS:SV_POSITION; float3 world:TEXCOORD0; half3 normal:TEXCOORD1; half4 color:COLOR; half fog:TEXCOORD2; };
            float Noise(float2 p)
            {
                float2 i=floor(p), f=frac(p); f=f*f*(3-2*f);
                float4 v=frac(sin(float4(dot(i,float2(127.1,311.7)),dot(i+float2(1,0),float2(127.1,311.7)),dot(i+float2(0,1),float2(127.1,311.7)),dot(i+1,float2(127.1,311.7))))*43758.5453);
                return lerp(lerp(v.x,v.y,f.x),lerp(v.z,v.w,f.x),f.y);
            }
            // Rotated octaves hide the square lattice that a single octave shows from altitude.
            float Fbm(float2 p)
            {
                const float2x2 rotation=float2x2(0.8,-0.6,0.6,0.8);
                float sum=0, amplitude=0.5;
                for(int octave=0;octave<3;octave++){ sum+=Noise(p)*amplitude; p=mul(rotation,p)*2.1+17.3; amplitude*=0.5; }
                return sum/0.875;
            }
            Varyings Vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Varyings o;
                o.world=TransformObjectToWorld(input.positionOS.xyz);
                o.positionCS=TransformWorldToHClip(o.world);
                o.normal=TransformObjectToWorldNormal(input.normalOS);
                o.color=input.color; o.fog=ComputeFogFactor(o.positionCS.z);
                return o;
            }
            half4 Frag(Varyings i):SV_Target
            {
                float2 p=i.world.xz;
                // Fine layers fade to their mean once a pixel spans their cells, instead of aliasing.
                float footprint=length(fwidth(p));
                float broad=Fbm(p*0.033);
                float detail=lerp(0.5,Noise(p*0.7),saturate(1.5-footprint*1.2));
                float grit=lerp(0.5,Noise(p*13.0),saturate(1-footprint*20));
                float grass=smoothstep(0.08,0.9,i.color.r+(broad-0.5)*0.48);
                half3 sediment=lerp(_SoilColor.rgb,_SandColor.rgb,0.55+0.35*broad);
                half3 albedo=lerp(sediment,_GrassColor.rgb,grass);
                albedo=lerp(albedo,_DryColor.rgb,smoothstep(0.46,0.8,Fbm(p*0.11+19))*grass*0.65);
                albedo*=lerp(0.66,1.24,detail)*lerp(0.83,1.1,grit);
                float dry=smoothstep(0.02,0.65,i.world.y+(detail-0.5)*0.12);
                albedo*=lerp(0.58,1,dry);
                half bump=1.2*saturate(1-footprint*4);
                half3 n=normalize(i.normal+half3((Noise(p*2+float2(0.05,0))-Noise(p*2))*bump,0,(Noise(p*2+float2(0,0.05))-Noise(p*2))*bump));
                Light sun=GetMainLight(TransformWorldToShadowCoord(i.world));
                half3 light=SampleSH(n)+sun.color*saturate(dot(n,sun.direction))*sun.shadowAttenuation;
                half3 color=albedo*light;
                return half4(MixFog(color,i.fog),1);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
    }
}
