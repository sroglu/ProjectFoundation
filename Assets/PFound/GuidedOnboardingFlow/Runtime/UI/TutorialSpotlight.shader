Shader "PFound/GuidedOnboardingFlow/Spotlight"
{
    // Screen-space dim with a soft cutout. Fed a viewport-space center/size + softness by HighlightMask;
    // keeps the cutout region clear and darkens everything outside it. The cutout is a rounded rectangle
    // by default (_CutoutShape = 0) or shaped by a mask sprite's alpha (_CutoutShape = 1, _CutoutMask).
    Properties
    {
        _Color ("Dim Color", Color) = (0, 0, 0, 0.75)
        _CutoutCenter ("Cutout Center (viewport)", Vector) = (0.5, 0.5, 0, 0)
        _CutoutSize ("Cutout Size (viewport)", Vector) = (0.2, 0.1, 0, 0)
        _Softness ("Edge Softness", Range(0, 0.5)) = 0.02
        _CutoutShape ("Cutout Shape (0=rect,1=sprite)", Float) = 0
        _CutoutMask ("Cutout Mask", 2D) = "white" {}
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
            float _CutoutShape;
            sampler2D _CutoutMask;

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
                float2 halfSize = max(_CutoutSize.xy * 0.5, 1e-5);
                fixed4 c = _Color;

                if (_CutoutShape > 0.5)
                {
                    // Sprite-shaped cutout: map the fragment into the cutout rect's [0,1] UV and read the
                    // mask alpha — high alpha = keep clear. Fragments outside the rect stay fully dimmed.
                    float2 minc = _CutoutCenter.xy - halfSize;
                    float2 uv = (i.vp - minc) / max(_CutoutSize.xy, 1e-5);
                    float inBounds = step(0.0, uv.x) * step(uv.x, 1.0) * step(0.0, uv.y) * step(uv.y, 1.0);
                    float m = tex2D(_CutoutMask, uv).a;
                    float clear = inBounds * smoothstep(0.5 - _Softness, 0.5 + _Softness, m);
                    c.a *= (1.0 - clear);
                    return c;
                }

                float2 d = abs(i.vp - _CutoutCenter.xy);
                // 0 inside the rect, ramps to 1 across the softness band outside it.
                float2 edge = smoothstep(halfSize, halfSize + _Softness, d);
                float outside = max(edge.x, edge.y);
                c.a *= outside;
                return c;
            }
            ENDCG
        }
    }
}
