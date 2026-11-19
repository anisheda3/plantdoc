// Invisible ground that only shows shadows. Placed under the plant twin in AR so the
// twin casts a real-time shadow onto the camera image of the real table or floor —
// the single strongest cue that a virtual object is actually sitting in the room.
Shader "PlantDoctor/ARShadowReceiver"
{
    Properties
    {
        _ShadowColor ("Shadow Color", Color) = (0.02, 0.04, 0.03, 1)
        _Strength    ("Shadow Strength", Range(0,1)) = 0.55
        _Fade        ("Edge Fade", Range(0.01,1)) = 0.35
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent-10" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }

        Pass
        {
            Name "ShadowOnly"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _ShadowColor;
                float _Strength, _Fade;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings   { float4 positionHCS : SV_POSITION; float3 positionWS : TEXCOORD0; float2 uv : TEXCOORD1; };

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionHCS = TransformWorldToHClip(OUT.positionWS);
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                Light l = GetMainLight(TransformWorldToShadowCoord(IN.positionWS));
                half shadow = 1.0 - l.shadowAttenuation;
                // Fade toward the quad edges so the receiver never shows a hard border.
                half edge = saturate((0.5 - length(IN.uv - 0.5)) / _Fade);
                return half4(_ShadowColor.rgb, shadow * _Strength * edge);
            }
            ENDHLSL
        }
    }
}
