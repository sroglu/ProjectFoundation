Shader "Hidden/KUI-Combined"
{
    Properties
    {
        _MainTex ("Font Atlas", 2D) = "white" {}
    }

    SubShader
    {
        Tags { "Queue" = "Overlay" }
        ZWrite Off
        ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color  : COLOR;
                float2 uv     : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos   : SV_POSITION;
                float4 color : COLOR;
                float2 uv    : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos   = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                o.uv    = v.uv;
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float4 color = i.color;

                #ifndef UNITY_COLORSPACE_GAMMA
                color.rgb = GammaToLinearSpace(color.rgb);
                #endif

                float isRect = step(i.uv.x, -0.5);
                float texAlpha = tex2D(_MainTex, i.uv).a;
                float alpha = lerp(texAlpha, 1.0, isRect);

                return float4(color.rgb, color.a * alpha);
            }
            ENDCG
        }
    }
}
