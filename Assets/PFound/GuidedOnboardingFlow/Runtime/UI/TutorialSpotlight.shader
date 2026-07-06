Shader "PFound/GuidedOnboardingFlow/Spotlight"
{
    // Screen-space dim with a soft rectangular cutout. Fed a viewport-space center/size + softness by
    // HighlightMask; keeps the cutout region clear and darkens everything outside it.
    Properties
    {
        _Color ("Dim Color", Color) = (0, 0, 0, 0.75)
        _CutoutCenter ("Cutout Center (viewport)", Vector) = (0.5, 0.5, 0, 0)
        _CutoutSize ("Cutout Size (viewport)", Vector) = (0.2, 0.1, 0, 0)
        _Softness ("Edge Softness", Range(0, 0.5)) = 0.02
    }

    SubShader
    {
        Tags { "Queue" = "Overlay" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Cull Off
        ZWrite Off
        ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha

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
                float4 pos : SV_POSITION;
                float2 vp : TEXCOORD0;
            };

            fixed4 _Color;
            float4 _CutoutCenter;
            float4 _CutoutSize;
            float _Softness;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                // Screen-space UV of this fragment in [0,1] viewport coordinates.
                float4 sp = ComputeScreenPos(o.pos);
                o.vp = sp.xy / sp.w;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 half = max(_CutoutSize.xy * 0.5, 1e-5);
                float2 d = abs(i.vp - _CutoutCenter.xy);
                // 0 inside the rect, ramps to 1 across the softness band outside it.
                float2 edge = smoothstep(half, half + _Softness, d);
                float outside = max(edge.x, edge.y);
                fixed4 c = _Color;
                c.a *= outside;
                return c;
            }
            ENDCG
        }
    }
}
