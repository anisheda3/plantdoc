// URP disease visualisation for the plant twin. Driven per-renderer by PlantVisualBinder
// through a MaterialPropertyBlock (_LesionDensity, _Yellowing, _Wilt, _LeafDamage,
// _HealthyGrowth, _RecoveryGlow). Property names are a contract: keep them stable.
//
// Leaves are single-sheet meshes, so everything here is two-sided: back faces flip their
// normal, light passing through a leaf tints it (translucency), and the shadow caster
// culls nothing. Wind and wilt are applied in a shared vertex function so the shadow
// always matches the visible silhouette.
Shader "PlantDoctor/PlantDisease"
{
    Properties
    {
        _BaseMap        ("Base Map", 2D) = "white" {}
        _BaseColor      ("Base Color", Color) = (1, 1, 1, 1)
        _LesionMap      ("Lesion Mask (R = appears first)", 2D) = "black" {}
        _YellowMap      ("Chlorosis Mask (R)", 2D) = "gray" {}
        _LesionColor    ("Lesion Color", Color) = (0.24, 0.14, 0.06, 1)
        _HaloColor      ("Lesion Halo Color", Color) = (0.86, 0.80, 0.30, 1)
        _YellowColor    ("Chlorosis Color", Color) = (0.86, 0.78, 0.26, 1)

        [Header(Health state (driven at runtime))]
        _LesionDensity  ("Lesion Density", Range(0,1)) = 0
        _Yellowing      ("Yellowing", Range(0,1)) = 0
        _Wilt           ("Wilt", Range(0,1)) = 0
        _LeafDamage     ("Leaf Damage", Range(0,1)) = 0
        _HealthyGrowth  ("Healthy Growth", Range(0,1)) = 1
        _RecoveryGlow   ("Recovery Glow", Range(0,1)) = 0
        _DiseaseMask    ("Disease Affects This Part", Range(0,1)) = 1

        [Header(Look)]
        _Smoothness     ("Sheen", Range(0,1)) = 0.35
        _Translucency   ("Translucency", Range(0,2)) = 0.8
        _TranslucencyColor ("Translucency Tint", Color) = (0.75, 1.0, 0.35, 1)
        _RimColor       ("Rim Color", Color) = (0.70, 1.0, 0.80, 1)
        _RimStrength    ("Rim Strength", Range(0,1)) = 0.18
        _GlowColor      ("Recovery Glow Color", Color) = (0.45, 1.0, 0.65, 1)
        _WindStrength   ("Wind Strength", Range(0,0.1)) = 0.012
        _WindSpeed      ("Wind Speed", Range(0,5)) = 1.3
        _Cutoff         ("Damage Cutoff (legacy)", Range(0,1)) = 0.5

        [Header(Cutout (photographic leaf outline))]
        [Toggle(_ALPHATEST_ON)] _AlphaClip ("Use Base Map alpha as the leaf outline", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        LOD 300

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_BaseMap);   SAMPLER(sampler_BaseMap);

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            float4 _BaseColor, _LesionColor, _HaloColor, _YellowColor;
            float4 _TranslucencyColor, _RimColor, _GlowColor;
            float _LesionDensity, _Yellowing, _Wilt, _LeafDamage, _HealthyGrowth, _RecoveryGlow, _DiseaseMask;
            float _Smoothness, _Translucency, _RimStrength, _WindStrength, _WindSpeed, _Cutoff;
        CBUFFER_END

        // Wilt droops the blade downward, strongest away from the stem; wind adds a
        // slow sway that also grows toward the tip. Both scale with distance from the
        // mesh pivot (leaf base), so stems and pots stay put.
        float3 DeformObjectPosition(float3 posOS)
        {
            float reach = saturate(length(posOS.xz) + posOS.z * 0.5);
            posOS.y -= _Wilt * 0.18 * reach * reach;

            float3 pivotWS = TransformObjectToWorld(float3(0, 0, 0));
            float phase = _Time.y * _WindSpeed + pivotWS.x * 1.7 + pivotWS.z * 1.3;
            float sway = sin(phase) * 0.7 + sin(phase * 2.3 + 1.1) * 0.3;
            posOS.y += sway * _WindStrength * reach;
            posOS.x += sway * _WindStrength * 0.6 * reach;
            return posOS;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Cull Off
            AlphaToMask On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #pragma shader_feature_local_fragment _ALPHATEST_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                float2 maskUv     : TEXCOORD1;   // per-leaflet window into the disease masks; 0 = no disease
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float3 positionWS  : TEXCOORD2;
                float  fogFactor   : TEXCOORD3;
                float2 maskUv      : TEXCOORD4;
            };

            TEXTURE2D(_LesionMap); SAMPLER(sampler_LesionMap);
            TEXTURE2D(_YellowMap); SAMPLER(sampler_YellowMap);

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                float3 posOS = DeformObjectPosition(IN.positionOS.xyz);
                VertexPositionInputs p = GetVertexPositionInputs(posOS);
                OUT.positionHCS = p.positionCS;
                OUT.positionWS = p.positionWS;
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                OUT.maskUv = IN.maskUv;
                OUT.fogFactor = ComputeFogFactor(p.positionCS.z);
                return OUT;
            }

            half3 Saturation(half3 c, half s)
            {
                half l = dot(c, half3(0.299, 0.587, 0.114));
                return lerp(l.xxx, c, s);
            }

            half4 frag (Varyings IN, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
            {
                half4 baseSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);
            #if defined(_ALPHATEST_ON)
                clip(baseSample.a - 0.02);
                half outline = baseSample.a;
            #else
                half outline = 1.0;
            #endif
                half3 albedo = baseSample.rgb * _BaseColor.rgb;
                // Parts without mask UVs (stems, rachis) only dull with stress; they never spot.
                half hasMask = step(1e-4, IN.maskUv.x + IN.maskUv.y);
                half disease = saturate(_DiseaseMask) * hasMask;

                // Vigour: healthy growth is greener and richer; stressed tissue dulls.
                albedo = lerp(Saturation(albedo * 0.82, 0.7), Saturation(albedo * 1.06, 1.12), saturate(_HealthyGrowth));

                // Chlorosis spreads in patches (between the veins, from the margins).
                half yMask = SAMPLE_TEXTURE2D(_YellowMap, sampler_YellowMap, IN.maskUv).r;
                half yellow = smoothstep(0.0, 1.0, saturate((yMask - (1.0 - _Yellowing * 1.1)) * 2.2 + _Yellowing * 0.2)) * 0.8 * disease;
                // Keep the leaf's own texture (veins, grain): chlorosis shifts green toward olive-yellow
                // instead of painting a flat colour over it.
                half luma = dot(albedo, half3(0.30, 0.59, 0.11));
                half3 chlorotic = half3(albedo.g * 1.30 + 0.05, albedo.g * 1.45 + 0.07, albedo.b * 0.45);
                chlorotic = lerp(chlorotic, _YellowColor.rgb * (0.30 + luma * 1.9), 0.30);
                albedo = lerp(albedo, chlorotic, yellow * 0.85);

                // Lesions are revealed progressively by thresholding the mask, with a
                // chlorotic halo just outside each lesion (diagnostic of early blight).
                half m = SAMPLE_TEXTURE2D(_LesionMap, sampler_LesionMap, IN.maskUv).r;
                half density = _LesionDensity * disease;
                half lesion = saturate((m - (1.0 - density * 0.9)) * 9.0);
                half halo = saturate(saturate((m - (1.0 - density * 1.05)) * 5.0) - lesion);
                albedo = lerp(albedo, _HaloColor.rgb * (0.40 + luma * 1.5), halo * 0.65);
                half3 lesionTint = _LesionColor.rgb * (0.75 + 0.5 * frac(m * 9.0));   // concentric rings
                albedo = lerp(albedo, lesionTint, lesion);

                // Necrosis: severe damage darkens and dries lesion centres.
                albedo *= lerp(1.0, 0.55, saturate(_LeafDamage) * lesion);

                // Two-sided normal.
                float3 n = normalize(IN.normalWS);
                n = IS_FRONT_VFACE(face, n, -n);
                float3 v = GetWorldSpaceNormalizeViewDir(IN.positionWS);

                float4 shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                half atten = mainLight.shadowAttenuation * mainLight.distanceAttenuation;

                // Wrapped diffuse keeps thin foliage from going black at grazing angles.
                half ndl = saturate((dot(n, mainLight.direction) + 0.3) / 1.3);
                half3 diffuse = mainLight.color * ndl * atten;

                // Light through the leaf: back-lit blades glow yellow-green.
                half back = saturate(dot(-n, mainLight.direction));
                half3 transmitted = mainLight.color * _TranslucencyColor.rgb * back * _Translucency * (0.35 + 0.65 * atten) * (1.0 - lesion);

                // Soft waxy sheen.
                float3 h = SafeNormalize(mainLight.direction + v);
                half spec = pow(saturate(dot(n, h)), lerp(8.0, 64.0, _Smoothness)) * _Smoothness * 0.35 * atten * (1.0 - lesion);

                half3 ambient = SampleSH(n);
                half3 color = albedo * (diffuse + ambient + transmitted) + mainLight.color * spec;

            #if defined(_ADDITIONAL_LIGHTS)
                uint count = GetAdditionalLightsCount();
                for (uint li = 0u; li < count; ++li)
                {
                    Light l = GetAdditionalLight(li, IN.positionWS);
                    half la = l.distanceAttenuation * l.shadowAttenuation;
                    color += albedo * l.color * saturate((dot(n, l.direction) + 0.3) / 1.3) * la;
                }
            #endif

                // Rim + healing glow read clearly over a busy camera background.
                half fresnel = pow(1.0 - saturate(dot(n, v)), 3.0);
                color += _RimColor.rgb * fresnel * _RimStrength;
                color += _GlowColor.rgb * _RecoveryGlow * (0.35 + 0.9 * fresnel);

                color = MixFog(color, IN.fogFactor);
                return half4(color, outline);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            Cull Off
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex vertShadow
            #pragma fragment fragShadow
            #pragma shader_feature_local_fragment _ALPHATEST_ON

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct Varyings   { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vertShadow (Attributes IN)
            {
                Varyings OUT;
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                float3 posWS = TransformObjectToWorld(DeformObjectPosition(IN.positionOS.xyz));
                float3 nWS = TransformObjectToWorldNormal(IN.normalOS);
                float4 cs = TransformWorldToHClip(ApplyShadowBias(posWS, nWS, _LightDirection));
            #if UNITY_REVERSED_Z
                cs.z = min(cs.z, UNITY_NEAR_CLIP_VALUE);
            #else
                cs.z = max(cs.z, UNITY_NEAR_CLIP_VALUE);
            #endif
                OUT.positionCS = cs;
                return OUT;
            }

            half4 fragShadow (Varyings IN) : SV_Target
            {
            #if defined(_ALPHATEST_ON)
                clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv).a - 0.5);
            #endif
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            Cull Off
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex vertDepth
            #pragma fragment fragDepth
            #pragma shader_feature_local_fragment _ALPHATEST_ON

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings   { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vertDepth (Attributes IN)
            {
                Varyings OUT;
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                OUT.positionCS = TransformObjectToHClip(DeformObjectPosition(IN.positionOS.xyz));
                return OUT;
            }

            half4 fragDepth (Varyings IN) : SV_Target
            {
            #if defined(_ALPHATEST_ON)
                clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv).a - 0.5);
            #endif
                return 0;
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Lit"
}
