// Detected-surface visualisation: a soft grid of dots in world space that fades toward
// the plane boundary. Light enough not to obscure the real surface, clear enough to
// tell the user "you can place the twin here".
Shader "PlantDoctor/ARPlaneDots"
{
    Properties
    {
        _Color    ("Dot Color", Color) = (0.55, 1.0, 0.75, 0.9)
        _Spacing  ("Dot Spacing (m)", Range(0.02, 0.3)) = 0.07
        _DotSize  ("Dot Radius (fraction)", Range(0.02, 0.5)) = 0.12
        _Tint     ("Surface Tint", Color) = (0.4, 1.0, 0.7, 0.06)
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent-5" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }

        Pass
        {
            Name "Dots"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color, _Tint;
                float _Spacing, _DotSize;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float4 color : COLOR; };
            struct Varyings   { float4 positionHCS : SV_POSITION; float3 positionWS : TEXCOORD0; float edge : TEXCOORD1; };

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionHCS = TransformWorldToHClip(OUT.positionWS);
                // ARPlaneMeshVisualizer meshes carry no edge data; distance from the
                // plane origin is a good-enough proxy for a centre-weighted fade.
                OUT.edge = length(IN.positionOS.xz);
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                float2 cell = frac(IN.positionWS.xz / _Spacing) - 0.5;
                half dotMask = 1.0 - smoothstep(_DotSize * 0.7, _DotSize, length(cell));
                half fade = saturate(1.0 - IN.edge * 0.9);
                half a = (dotMask * _Color.a + _Tint.a) * fade;
                half3 rgb = lerp(_Tint.rgb, _Color.rgb, dotMask);
                return half4(rgb, a);
            }
            ENDHLSL
        }
    }
}
