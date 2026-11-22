Shader "UI/HoleMask"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _HoleCenter ("Hole Center (UV)", Vector) = (0.5, 0.5, 0, 0)
        _HoleSize ("Hole Size (UV)", Vector) = (0.3, 0.3, 0, 0)
        _Radius ("Corner Radius (UV)", Range(0, 0.2)) = 0.05
        _Feather ("Feather Width", Range(0.001, 0.1)) = 0.02
        _MaskColor ("Mask Color", Color) = (0, 0, 0, 1) 
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "RenderType"="Transparent"
            "IgnoreProjector"="True"
        }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        Lighting Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _HoleCenter;
            float4 _HoleSize;
            float _Radius;
            float _Feather;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            // 圆角矩形有符号距离场
            float RoundedBoxSDF(float2 p, float2 halfSize, float r)
            {
                float2 d = abs(p) - halfSize + r;
                return length(max(d, 0.0)) + min(max(d.x, d.y), 0.0) - r;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 p = i.uv - _HoleCenter.xy;
                float dist = RoundedBoxSDF(p, _HoleSize.xy * 0.5, _Radius);

                // 洞内 dist < 0，洞外 dist > 0
                float alpha = smoothstep(0, _Feather, dist);
                return fixed4(0, 0, 0, alpha);
            }
            ENDCG
        }
    }
}