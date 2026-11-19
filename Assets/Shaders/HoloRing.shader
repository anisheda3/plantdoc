// Holographic ring drawn flat on the ground: the twin's base marker and the AR placement
// reticle. A slow rotating dash, a soft glow and an expanding pulse (driven by _Pulse
// from PlantTwinPresenter) signal "this is a digital twin" without hiding the plant.
Shader "PlantDoctor/HoloRing"
{
    Properties
    {
        _Color     ("Color", Color) = (0.45, 1.0, 0.7, 1)
        _Radius    ("Radius", Range(0.1, 0.5)) = 0.42
        _Width     ("Ring Width", Range(0.002, 0.2)) = 0.025
        _Glow      ("Glow", Range(0, 2)) = 0.8
        _Dashes    ("Dashes", Range(0, 64)) = 36
        _Spin      ("Spin Speed", Range(-3, 3)) = 0.25
        _Pulse     ("Pulse (runtime)", Range(0, 1)) = 0
        _Opacity   ("Opacity", Range(0, 1)) = 0.85
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }

        Pass
        {
            Name "Ring"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha One          // additive: glows over the camera feed
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Radius, _Width, _Glow, _Dashes, _Spin, _Pulse, _Opacity;
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

            half Band(half d, half r, half w) { return saturate(1.0 - abs(d - r) / w); }

            half4 frag (Varyings IN) : SV_Target
            {
                float2 p = IN.uv - 0.5;
                half d = length(p);
                half angle = atan2(p.y, p.x) / (2.0 * PI) + 0.5;

                half dash = _Dashes > 0.5 ? step(0.35, frac(angle * _Dashes + _Time.y * _Spin)) : 1.0;
                half ring = Band(d, _Radius, _Width) * (0.55 + 0.45 * dash);
                half inner = Band(d, _Radius * 0.72, _Width * 0.45) * 0.45;
                half glow = exp(-abs(d - _Radius) * 22.0) * _Glow * 0.35;

                // Expanding pulse ring on placement / treatment.
                half pulseR = lerp(_Radius * 0.6, 0.5, 1.0 - _Pulse);
                half pulse = Band(d, pulseR, _Width * 1.6) * _Pulse;

                half a = saturate(ring + inner + glow + pulse) * _Opacity * step(d, 0.5);
                return half4(_Color.rgb, a);
            }
            ENDHLSL
        }
    }
}
