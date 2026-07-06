#ifndef PFOUND_UISYSTEM_SHAPES_EFFECTS_INCLUDED
#define PFOUND_UISYSTEM_SHAPES_EFFECTS_INCLUDED

// ─────────────────────────────────────────────────────────────────────
// UIShapeEffects.hlsl
// Effect composition for the UISystem/Shape shader.
//
// Composition order:
//   shadow → fill → gradient → banding → noise → dots → outline
//
// Each effect is gated by a shader_feature_local keyword — the entire
// block (sample + composite) is preprocessed out when the keyword is off,
// producing zero ALU cost.
// ─────────────────────────────────────────────────────────────────────

// ─── Helpers ────────────────────────────────────────────────────────

// "Over" (Porter-Duff) compositing with straight alpha.
// Returns top over bottom; both inputs have straight (non-premultiplied) alpha.
float4 UIShape_OverStraight(float4 top, float4 bottom)
{
    float outA = top.a + bottom.a * (1.0 - top.a);
    if (outA < 1e-6) return float4(0.0, 0.0, 0.0, 0.0);
    float3 outRGB = (top.rgb * top.a + bottom.rgb * bottom.a * (1.0 - top.a)) / outA;
    return float4(outRGB, outA);
}

// ─── Gradient ───────────────────────────────────────────────────────

#ifdef EFFECT_GRADIENT_ON
float4 UIShape_Gradient(float2 uv)
{
    float2 halfSize = max(_RectSize.xy * 0.5, 1e-3);
    float t;
    #ifdef GRADIENT_MODE_RADIAL
        float r = length(uv / halfSize);
        float fall = max(_GradientFalloff, 1e-3);
        t = saturate(pow(saturate(r), fall));
    #else
        float rad = radians(_GradientAngle);
        float2 axis = float2(cos(rad), sin(rad));
        float dist = dot(uv / halfSize, axis);
        t = saturate(dist * 0.5 + 0.5);
    #endif
    return lerp(_GradientColorA, _GradientColorB, t);
}
#endif

// ─── Outline ────────────────────────────────────────────────────────

#ifdef EFFECT_OUTLINE_ON
float4 UIShape_Outline(float sdf)
{
    float thickness = max(_OutlineThickness, 0.0);
    if (thickness <= 0.0) return float4(0.0, 0.0, 0.0, 0.0);

    // 1dp outlines were rendering with varying perceived thickness — the shared
    // SDFEdgeAA bandwidth (~1 screen pixel) plus the 1px outline created a 2-px
    // soft band that subpixel positioning split unevenly between adjacent pixels.
    // Two fixes layered together:
    //   (a) Force the effective thickness to ALWAYS be ≥ 1 screen pixel so at
    //       least one pixel sits fully inside the outline regardless of subpixel
    //       alignment.
    //   (b) Use a tight 0.5-pixel AA bandwidth (instead of the 1-2 px shape edge
    //       AA) so the outline edge is nearly binary and adjacent-pixel bleed
    //       stops cycling with subpixel offset.
    float pixelSize = max(fwidth(sdf), 1e-5);
    float effectiveThickness = max(thickness, pixelSize);
    float bandDist = abs(sdf) - effectiveThickness * 0.5;
    float aaWidth = pixelSize * 0.5;
    float coverage = saturate(0.5 - bandDist / aaWidth);
    return float4(_OutlineColor.rgb, _OutlineColor.a * coverage);
}
#endif

// ─── Banding ────────────────────────────────────────────────────────

#ifdef EFFECT_BANDING_ON
float4 UIShape_Banding(float sdf)
{
    float spacing = max(_BandingSpacing, 1e-3);
    float band = frac(-sdf / spacing); // -sdf so bands grow inward
    return lerp(_BandingColorA, _BandingColorB, step(0.5, band));
}
#endif

// ─── Noise ──────────────────────────────────────────────────────────

#ifdef EFFECT_NOISE_ON
float4 UIShape_Noise(float2 uv)
{
    float scale = max(_NoiseScale, 1e-3);
    float2 p = uv / scale;
    #ifdef NOISE_MODE_WORLEY
        float n = UIShape_WorleyNoise2D(p);
    #else
        float n = UIShape_PerlinNoise2D(p);
    #endif
    float amp = saturate(_NoiseAmplitude);
    return float4(_NoiseColor.rgb, _NoiseColor.a * amp * n);
}
#endif

// ─── Dots ───────────────────────────────────────────────────────────

#ifdef EFFECT_DOTS_ON
float4 UIShape_Dots(float2 uv)
{
    float spacing = max(_DotsSpacing, 1e-3);
    float radius = max(_DotsRadius, 0.0);
    float2 cell = float2(
        uv.x - floor(uv.x / spacing) * spacing - spacing * 0.5,
        uv.y - floor(uv.y / spacing) * spacing - spacing * 0.5);
    float dot_d = length(cell) - radius;
    float coverage = UIShape_SDFEdgeAA(dot_d);
    return float4(_DotsColor.rgb, _DotsColor.a * coverage);
}
#endif

// ─── Shadow ─────────────────────────────────────────────────────────

#ifdef EFFECT_SHADOW_ON
float4 UIShape_Shadow(float2 uv)
{
    // Evaluate the shape's SDF at the shadow's source position.
    float2 srcUv = uv - _ShadowOffset.xy;
    float srcSdf = UIShape_EvaluateSDF(srcUv);
    float blur = max(_ShadowBlur, 1e-3);
    // Smooth shadow falloff: C1-continuous S-curve from full shadow (srcSdf < -blur/2)
    // to zero (srcSdf > +blur/2). Replaces saturate-linear ramp which had visible
    // banding at the band edges due to C0 discontinuity.
    float coverage = 1.0 - smoothstep(-blur * 0.5, blur * 0.5, srcSdf);
    return float4(_ShadowColor.rgb, _ShadowColor.a * coverage);
}
#endif

// ─── Composite ──────────────────────────────────────────────────────

float4 UIShape_Composite(float sdf, float2 uv, float4 fill)
{
    // Shape coverage (edge AA).
    float shapeCoverage = UIShape_SDFEdgeAA(sdf);
    float maskInside = shapeCoverage;

    // Step 1: fill (gradient REPLACES fill when enabled).
    float4 base = fill;
    #ifdef EFFECT_GRADIENT_ON
        base = UIShape_Gradient(uv);
    #endif
    base.a *= maskInside;

    // Step 2: overlays (banding, noise, dots) — masked by the shape interior.
    #ifdef EFFECT_BANDING_ON
        float4 banding = UIShape_Banding(sdf);
        banding.a *= maskInside;
        base = UIShape_OverStraight(banding, base);
    #endif
    #ifdef EFFECT_NOISE_ON
        float4 noiseLayer = UIShape_Noise(uv);
        noiseLayer.a *= maskInside;
        base = UIShape_OverStraight(noiseLayer, base);
    #endif
    #ifdef EFFECT_DOTS_ON
        float4 dotsLayer = UIShape_Dots(uv);
        dotsLayer.a *= maskInside;
        base = UIShape_OverStraight(dotsLayer, base);
    #endif

    // Step 3: outline (top of interior stack).
    #ifdef EFFECT_OUTLINE_ON
        float4 outline = UIShape_Outline(sdf);
        base = UIShape_OverStraight(outline, base);
    #endif

    // Step 4: shadow behind the entire shape stack.
    #ifdef EFFECT_SHADOW_ON
        float4 shadow = UIShape_Shadow(uv);
        // Only render shadow where the shape is NOT covering (1 - maskInside).
        shadow.a *= (1.0 - maskInside);
        base = UIShape_OverStraight(base, shadow);
    #endif

    return base;
}

#endif // PFOUND_UISYSTEM_SHAPES_EFFECTS_INCLUDED
