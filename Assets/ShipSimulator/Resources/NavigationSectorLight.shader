Shader "ShipSimulator/NavigationSectorLight"
{
    Properties { [MainColor][HDR] _BaseColor("Light", Color)=(1,1,1,1) _SectorCenter("Sector bearing",Float)=0 _SectorArc("Sector arc",Float)=225 _LensOffset("Surface offset in diameters",Float)=0 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            float _SectorCenter, _SectorArc, _LensOffset;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; float3 world:TEXCOORD1; float bearing:TEXCOORD2; };
            Varyings Vert(Attributes input)
            {
                Varyings o;
                float3 center=TransformObjectToWorld(float3(0,0,0));
                center += normalize(_WorldSpaceCameraPos-center) * length(UNITY_MATRIX_M._m00_m10_m20) * _LensOffset;
                float3 viewer=TransformWorldToObject(_WorldSpaceCameraPos);
                o.bearing=atan2(viewer.x,viewer.z)*180/PI;
                float metersPerPixel=2*max(-TransformWorldToView(center).z,0.1)/max(abs(UNITY_MATRIX_P._m11)*_ScreenParams.y,1);
                float diameter=max(length(UNITY_MATRIX_M._m00_m10_m20),metersPerPixel*2.2);
                o.world=center+UNITY_MATRIX_V[0].xyz*input.positionOS.x*diameter+UNITY_MATRIX_V[1].xyz*input.positionOS.y*diameter;
                o.positionCS=TransformWorldToHClip(o.world);
                o.uv=input.uv;
                return o;
            }
            half4 Frag(Varyings i):SV_Target
            {
                float angle=abs(frac((i.bearing-_SectorCenter+180)/360)*360-180);
                clip(_SectorArc*0.5-angle);
                float radius=length(i.uv*2-1);
                half alpha=1-smoothstep(0.3,1,radius);
                half3 color=MixFog(_BaseColor.rgb,InitializeInputDataFog(float4(i.world,1),0));
                return half4(color,alpha);
            }
            ENDHLSL
        }
    }
}
