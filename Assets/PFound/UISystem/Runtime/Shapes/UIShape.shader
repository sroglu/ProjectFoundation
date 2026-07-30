// ─────────────────────────────────────────────────────────────────────
// UISystem/Shape — Monolithic SDF UI shader for UI Toolkit VisualElements.
// Targets URP UI material type (Unity 6.3+); assigned via IStyle.unityMaterial.
//
// Feasibility ground (specs/008-gpu-sdf-feasibility/research.md):
//   - Plain ShaderLab + URP UI subshader tags accepted by IsMaterialValid (R&D-1)
//   - 50 elements sharing one Material batch to 1 Draw Mesh (R&D-2)
//   - shader_feature_local effect keywords strip cleanly (R&D-3)
//   - iOS Metal cross-compile clean (R&D-4)
//   - UXML -unity-material: url(...) round-trips through resolvedStyle (R&D-5)
//
// Composition order (UIShape_Composite in UIShapeEffects.hlsl):
//   shadow → fill → gradient → banding → noise → dots → outline
// ─────────────────────────────────────────────────────────────────────

Shader "UISystem/Shape"
{
    Properties
    {
        // Stub texture — UI Toolkit may bind one regardless of material. Never sampled.
        [HideInInspector] _MainTex ("Main Texture (unused)", 2D) = "white" {}

        // ─── Shape ───────────────────────────────────────────────────
        [Enum(Rect,0,RoundedRect,1,Capsule,2,Ellipse,3)] _ShapeType ("Shape Type", Float) = 1
        _CornerRadii ("Corner Radii (TL,TR,BR,BL)", Vector) = (8, 8, 8, 8)
        _RectSize ("Shape Size (W,H,_,_) in px", Vector) = (100, 100, 0, 0)
        // Quad size when the UI quad is larger than the shape (e.g., margin for drop shadow).
        // Set (0,0,0,0) to match _RectSize (shape fills the quad, no shadow margin).
        _QuadSize ("Quad Size (W,H,_,_) in px; (0,0) = match _RectSize", Vector) = (0, 0, 0, 0)

        // ─── Anti-aliasing ───────────────────────────────────────────
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
        // Default Perlin (0) — Worley (1) is fillrate-heavier, opt-in.
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

        // ─── M3 Overlays (spec 009) ──────────────────────────────────
        // When ON, fragment shader composites state overlay + tonal overlay + ripple
        // on top of the base shape. Opacities are per-instance (via Vertex.tint bytes
        // 1/3); colors are category-level (material uniforms).
        [Toggle(EFFECT_M3_OVERLAYS_ON)] _M3OverlaysEnable ("M3 Overlays Enable", Float) = 0
        _M3StateOverlayColor ("M3 State Overlay Color", Color) = (0.18, 0.16, 0.20, 1) // M3 on-surface
        _M3TonalOverlayColor ("M3 Tonal Overlay Color", Color) = (0.40, 0.32, 0.65, 1) // M3 primary
        _M3RippleCenter ("M3 Ripple Center (UV, normalized)", Vector) = (0.5, 0.5, 0, 0)
        _M3RippleRadius ("M3 Ripple Radius (px)", Float) = 0
        _M3RippleAlpha ("M3 Ripple Alpha", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType"        = "Transparent"
            "Queue"             = "Transparent"
            "RenderPipeline"    = "UniversalPipeline"
            "IgnoreProjector"   = "True"
            "PreviewType"       = "Plane"
            // Required for UI Toolkit acceptance (Unity 6.3+). Without this tag the
            // material assigns via IStyle.unityMaterial but Unity logs:
            // "Selected material 'X' is not compatible with UITK".
            "isCustomUITKShader" = "true"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Lighting Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex   Vert
            #pragma fragment Frag

            // Shape type (always-shipped variants — runtime shape change is supported)
            #pragma multi_compile_local _ SHAPE_TYPE_RECT SHAPE_TYPE_ROUNDEDRECT SHAPE_TYPE_CAPSULE SHAPE_TYPE_ELLIPSE

            // Effect toggles (build-time stripped to what materials use)
            #pragma shader_feature_local _ EFFECT_GRADIENT_ON
            #pragma shader_feature_local _ EFFECT_OUTLINE_ON
            #pragma shader_feature_local _ EFFECT_BANDING_ON
            #pragma shader_feature_local _ EFFECT_NOISE_ON
            #pragma shader_feature_local _ EFFECT_DOTS_ON
            #pragma shader_feature_local _ EFFECT_SHADOW_ON
            // A+ palette path (spec 009 AD-001): when ON, IN.color (Vertex.tint Color32)
            // is decoded as palette indices per data-model.md SdfShapePalette encoding
            // table. When OFF, legacy UI Toolkit behavior (IN.color * _FillColor).
            #pragma shader_feature_local _ EFFECT_VERTEX_TINT_PALETTE_ON
            // M3 overlay composite (spec 009 — state layer + tonal + ripple).
            #pragma shader_feature_local _ EFFECT_M3_OVERLAYS_ON

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

                // A+ palette uniform — 16 float4 slots, indexed via Vertex.tint encoding.
                // Set programmatically by SdfShapePalette.FlushToMaterials() (C#). Only
                // sampled when EFFECT_VERTEX_TINT_PALETTE_ON is set.
                // SRP-batcher safe: fixed-size array inside UnityPerMaterial CBUFFER.
                float4 _ColorPalette[16];

                // M3 overlay uniforms (spec 009). Active when EFFECT_M3_OVERLAYS_ON.
                // Colors are category-level (theme-bound). Ripple bundle is per-event
                // (temporary per-instance material during ~300ms ripple lifetime).
                float  _M3OverlaysEnable;
                float4 _M3StateOverlayColor;
                float4 _M3TonalOverlayColor;
                float4 _M3RippleCenter;
                float  _M3RippleRadius;
                float  _M3RippleAlpha;
            CBUFFER_END

            // Bundled HLSL libraries — SDF + noise + effect composition
            #include "UIShapeSDF.hlsl"
            #include "UIShapeNoise.hlsl"
            #include "UIShapeEffects.hlsl"

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

                // Resolve base fill color.
                // - A+ palette path: IN.color.r is a quantized palette index (0-255 → slot 0-15).
                //   Used by SdfShape/M3Surface in spec 009 to allow 16+ unique fill colors to
                //   share ONE material instance + still batch.
                // - Legacy path: IN.color × _FillColor is the canonical UI Toolkit fill modulation
                //   that all existing showcase materials (Card_M3_Elevated.mat etc.) rely on.
                // SPEC 026: byte 0 holds (fillIdx<<4 | stateOp4) — see
                // M3Surface.EncodeTint for rationale (non-zero byte 1 corrupts
                // the entire backgroundColor through UIR's Linear-space pipeline).
                // Decoded once and reused in both palette and overlay paths below.
                int rawR = (int)round(IN.color.r * 255.0);
                float4 baseColor;
                #if EFFECT_VERTEX_TINT_PALETTE_ON
                    int fillIdx = clamp(rawR >> 4, 0, 15);
                    baseColor = _ColorPalette[fillIdx];
                #else
                    baseColor = _FillColor * IN.color;
                #endif

                float4 outColor = UIShape_Composite(sdf, uv, baseColor);

                // ─── M3 overlays (spec 009) ──────────────────────────────
                // Composited on top of base shape (after fill+outline+shadow). Only
                // visible inside the shape (sdf < 0). State + tonal opacities come
                // from IN.color bytes 1 + 2; ripple comes from per-event uniforms.
                #if EFFECT_M3_OVERLAYS_ON
                    float insideMask = saturate(-sdf / max(_AAWidth, 1e-4));

                    // State overlay (hover/focus/press) — opacity is now packed into the
                    // low nibble of byte 0 (spec 026 anti-corruption encoding). Decode
                    // by masking the low 4 bits and normalising to [0, 1].
                    float stateOpacity = (float)(rawR & 0xF) / 15.0;
                    float stateA = stateOpacity * _M3StateOverlayColor.a * insideMask;
                    outColor.rgb = lerp(outColor.rgb, _M3StateOverlayColor.rgb, stateA);

                    // Tonal overlay (M3 dark elevation primary tint) — opacity in tint byte 2 (IN.color.b).
                    // Spec 009 hotfix 2026-05-29: moved from byte 3 (alpha) to byte 2; UIR drops
                    // elements whose backgroundColor.a == 0 (the default state with no overlays
                    // set produced 0,0,0,0 previously). M3Surface.EncodeTint now forces alpha=255.
                    float tonalA = IN.color.b * _M3TonalOverlayColor.a * insideMask;
                    outColor.rgb = lerp(outColor.rgb, _M3TonalOverlayColor.rgb, tonalA);

                    // Ripple — animated soft circle around _M3RippleCenter (UV-normalized 0-1)
                    if (_M3RippleAlpha > 0.001)
                    {
                        float2 rippleUV = IN.uv - _M3RippleCenter.xy;
                        // Convert to pixel space using effectiveQuad
                        float2 rippleDeltaPx = rippleUV * effectiveQuad;
                        float rippleDistPx = length(rippleDeltaPx);
                        float rippleEdge = smoothstep(_M3RippleRadius + _AAWidth, _M3RippleRadius - _AAWidth, rippleDistPx);
                        float rippleA = _M3RippleAlpha * 0.20 * rippleEdge * insideMask;
                        outColor.rgb = lerp(outColor.rgb, _M3StateOverlayColor.rgb, rippleA);
                    }
                #endif

                return outColor;
            }
            ENDHLSL
        }
    }

    FallBack Off
    CustomEditor "PFound.UISystem.Editor.Shapes.UIShapeMaterialInspector"
}
