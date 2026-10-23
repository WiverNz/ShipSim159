Shader "ShipSimulator/HullWaterMask"
{
    // Invisible quads in a vessel's hatch openings. Where one is the nearest surface it sets stencil bit 8,
    // which RiverWater tests, so the river plane passing through the hull is not drawn inside an open hold.
    // It is drawn after opaque geometry and before the water, behind closed covers and other hulls.
    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent-10"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "HullWaterMask"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            ColorMask 0
            ZWrite Off
            ZTest LEqual
            Cull Off
            Stencil
            {
                Ref 8
                WriteMask 8
                Comp Always
                Pass Replace
            }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            float4 Vert(float3 positionOS : POSITION) : SV_POSITION
            {
                return TransformObjectToHClip(positionOS);
            }

            half4 Frag() : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }
}
