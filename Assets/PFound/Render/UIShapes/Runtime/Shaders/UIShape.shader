Shader "Render/UI/Shape"
{
    Properties
    {
        // Stub texture required by UnityEngine.UI.Image — never sampled.
        [HideInInspector] _MainTex ("Main Texture (unused)", 2D) = "white" {}

        // ─── Shape ───────────────────────────────────────────────────
        [Enum(Rect,0,RoundedRect,1,Capsule,2,Ellipse,3)] _ShapeType ("Shape Type", Float) = 1
        _CornerRadii ("Corner Radii (TL,TR,BR,BL)", Vector) = (8, 8, 8, 8)
        _RectSize ("Shape Size — pixel size of the SDF shape (W,H,_,_)", Vector) = (100, 100, 0, 0)
        // Optional larger pixel size when the RectTransform is bigger than the shape
        // (e.g., to leave margin for a drop shadow). Set (0,0,0,0) to match _RectSize
        // (default — shape fills the quad, no shadow margin).
        _QuadSize ("Quad Size — pixel size of the UI quad (W,H,_,_); (0,0) = match _RectSize", Vector) = (0, 0, 0, 0)

        // ─── Anti-aliasing ───────────────────────────────────────────
        // 1.0 = 1-pixel crisp band (default, industry standard for SDF UI).
        // 1.5–2.0 = softer edge — less stepping when the canvas is displayed
        // at non-native scale or on high-DPI screens. Trade-off: slight loss
        // of crispness at native resolution.
        _AAWidth ("AA Band Width (1.0 = crisp, 2.0 = soft)", Range(0.5, 3.0)) = 1.0

        // ─── Fill ────────────────────────────────────────────────────
        _FillColor ("Fill Color", Color) = (1, 1, 1, 1)

        // ─── Gradient ────────────────────────────────────────────────
        [Toggle(EFFECT_GRADIENT_ON)] _GradientEnable ("Gradient Enable", Float) = 0
        [Enum(Linear,0,Radial,1)] _GradientMode ("Gradient Mode", Float) = 0
        _GradientAngle ("Gradient Angle", Float) = 90
        _GradientFalloff ("Gradient Falloff", Float) = 1.0
        _GradientColorA ("Gradient Color A", Color) = (1, 1, 1, 1)
        _GradientColorB ("Gradient Color B", Color) = (0, 0, 0, 1)

        // ─── Outline ─────────────────────────────────────────────────
        [Toggle(EFFECT_OUTLINE_ON)] _OutlineEnable ("Outline Enable", Float) = 0
        _OutlineThickness ("Outline Thickness", Float) = 2
        _OutlineColor ("Outline Color", Color) = (0, 0, 0, 1)

        // ─── Banding ─────────────────────────────────────────────────
        [Toggle(EFFECT_BANDING_ON)] _BandingEnable ("Banding Enable", Float) = 0
        _BandingSpacing ("Banding Spacing", Float) = 8
        _BandingColorA ("Banding Color A", Color) = (1, 1, 1, 0.5)
        _BandingColorB ("Banding Color B", Color) = (0, 0, 0, 0.5)

        // ─── Noise ───────────────────────────────────────────────────
        [Toggle(EFFECT_NOISE_ON)] _NoiseEnable ("Noise Enable", Float) = 0
        [Enum(Perlin,0,Worley,1)] _NoiseMode ("Noise Mode", Float) = 0
        _NoiseScale ("Noise Scale", Float) = 8
        _NoiseAmplitude ("Noise Amplitude", Range(0, 1)) = 0.25
        _NoiseColor ("Noise Color", Color) = (1, 1, 1, 1)

        // ─── Dots ────────────────────────────────────────────────────
        [Toggle(EFFECT_DOTS_ON)] _DotsEnable ("Dots Enable", Float) = 0
        _DotsRadius ("Dots Radius", Float) = 2
        _DotsSpacing ("Dots Spacing", Float) = 8
        _DotsColor ("Dots Color", Color) = (1, 1, 1, 1)

        // ─── Shadow ──────────────────────────────────────────────────
        [Toggle(EFFECT_SHADOW_ON)] _ShadowEnable ("Shadow Enable", Float) = 0
        _ShadowOffset ("Shadow Offset (X,Y,_,_)", Vector) = (2, -2, 0, 0)
        _ShadowBlur ("Shadow Blur", Float) = 4
        _ShadowColor ("Shadow Color", Color) = (0, 0, 0, 0.5)
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Lighting Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            // Shape type (always-shipped variants)
            #pragma multi_compile_local _ SHAPE_TYPE_RECT SHAPE_TYPE_ROUNDEDRECT SHAPE_TYPE_CAPSULE SHAPE_TYPE_ELLIPSE

            // Effect toggles (build-time stripped to what materials use)
            #pragma shader_feature_local _ EFFECT_GRADIENT_ON
            #pragma shader_feature_local _ EFFECT_OUTLINE_ON
            #pragma shader_feature_local _ EFFECT_BANDING_ON
            #pragma shader_feature_local _ EFFECT_NOISE_ON
            #pragma shader_feature_local _ EFFECT_DOTS_ON
            #pragma shader_feature_local _ EFFECT_SHADOW_ON

            // Sub-mode keywords (only matter when parent effect on)
            #pragma shader_feature_local _ GRADIENT_MODE_RADIAL
            #pragma shader_feature_local _ NOISE_MODE_WORLEY

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // ─── Uniforms ──────────────────────────────────────────────
            CBUFFER_START(UnityPerMaterial)
                float  _ShapeType;
                float4 _CornerRadii;
                float4 _RectSize;
                float4 _QuadSize;
                float  _AAWidth;
                float4 _FillColor;

                float  _GradientEnable;
                float  _GradientMode;
                float  _GradientAngle;
                float  _GradientFalloff;
                float4 _GradientColorA;
                float4 _GradientColorB;

                float  _OutlineEnable;
                float  _OutlineThickness;
                float4 _OutlineColor;

                float  _BandingEnable;
                float  _BandingSpacing;
                float4 _BandingColorA;
                float4 _BandingColorB;

                float  _NoiseEnable;
                float  _NoiseMode;
                float  _NoiseScale;
                float  _NoiseAmplitude;
                float4 _NoiseColor;

                float  _DotsEnable;
                float  _DotsRadius;
                float  _DotsSpacing;
                float4 _DotsColor;

                float  _ShadowEnable;
                float4 _ShadowOffset;
                float  _ShadowBlur;
                float4 _ShadowColor;
            CBUFFER_END

            // Bundled HLSL libraries — SDF + effect composition + noise
            #include "../UIShapeSDF.hlsl"
            #include "../UIShapeNoise.hlsl"
            #include "../UIShapeEffects.hlsl"

            // ─── Vertex / Fragment ─────────────────────────────────────

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float4 color       : COLOR;
            };

            Varyings Vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                OUT.color = IN.color;
                return OUT;
            }

            float4 Frag(Varyings IN) : SV_Target
            {
                // UV in pixel space. When _QuadSize is set larger than _RectSize, the quad
                // extends beyond the shape — leaves room for drop-shadow rendering.
                // Backward compat: _QuadSize == (0,0) → uv range matches _RectSize.
                float2 effectiveQuad = max(_QuadSize.xy, _RectSize.xy);
                float2 uv = (IN.uv - 0.5) * effectiveQuad;
                float sdf = UIShape_EvaluateSDF(uv);
                float4 baseColor = _FillColor * IN.color;
                float4 outColor = UIShape_Composite(sdf, uv, baseColor);
                return outColor;
            }
            ENDHLSL
        }
    }

    FallBack Off
    CustomEditor "PFound.Render.UIShapes.Editor.UIShapeMaterialInspector"
}
