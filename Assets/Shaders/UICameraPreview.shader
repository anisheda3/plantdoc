// Full-screen camera preview for the non-AR camera mode. Applies the same affine
// viewport -> texture mapping (rotation, mirroring, aspect-fill) that
// WebCamFrameSource uses for capture, so preview and analysed frame always match.
Shader "PlantDoctor/UI/CameraPreview"
{
    Properties
    {
        [PerRendererData] _MainTex ("Camera", 2D) = "black" {}
        _RowU ("Row U", Vector) = (1, 0, 0, 0)
        _RowV ("Row V", Vector) = (0, 1, 0, 0)
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "CanUseSpriteAtlas"="False" }
        Cull Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend One Zero

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _RowU, _RowV;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 p = float3(i.uv, 1.0);
                float2 uv = float2(dot(_RowU.xyz, p), dot(_RowV.xyz, p));
                return fixed4(tex2D(_MainTex, uv).rgb, 1.0);
            }
            ENDCG
        }
    }
}
