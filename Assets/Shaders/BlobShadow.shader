// Soft radial contact shadow under the pot. Works even when no shadow map is available
// (older phones, shadow distance exceeded) and grounds the twin in the studio view too.
Shader "PlantDoctor/BlobShadow"
{
    Properties
    {
        _Color    ("Color", Color) = (0, 0.02, 0.01, 0.6)
        _Softness ("Softness", Range(0.05, 1)) = 0.6
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent-20" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }

        Pass
        {
            Name "Blob"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Softness;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings   { float4 positionHCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                half d = length(IN.uv - 0.5) * 2.0;           // 0 centre .. 1 edge
                half a = 1.0 - smoothstep(1.0 - _Softness, 1.0, d);
                return half4(_Color.rgb, _Color.a * a * a);
            }
            ENDHLSL
        }
    }
}
