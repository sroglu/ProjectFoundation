#ifndef PFOUND_RENDER_EFFECTS_OUTLINE_INCLUDED
#define PFOUND_RENDER_EFFECTS_OUTLINE_INCLUDED

// -----------------------------------------------------------------------------
// PFound.Render.Effects.Outline — Outline.hlsl
//
// 4-tap Roberts cross depth-edge detection: samples camera depth at four
// diagonal-neighbour taps, forms two cross gradients, and thresholds the
// magnitude into an outline mask. Diagonal taps catch silhouettes a
// cardinal-only (Laplacian) kernel misses, at roughly half the cost of an
// 8-tap Sobel.
// -----------------------------------------------------------------------------

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

// Edge normalization constant. Tuned for Linear01Depth on typical scenes.
// If depth distribution differs significantly, expose via uniform in a future
// Phase 4.x Sensitivity knob (see research R6 deferred items).
#define M_RENDER_EFFECTS_OUTLINE_EDGE_NORMALIZE 100.0

// Roberts cross depth-edge detection (4 diagonal taps, L1 magnitude).
// Returns per-pixel edge intensity in [0, 1] (after saturate).
float MRender_RobertsCrossDepthEdge(float2 uv, float2 texelSize, float thickness)
{
    float2 o = texelSize * thickness;
    float d00 = SampleSceneDepth(uv + float2( o.x,  o.y));
    float d11 = SampleSceneDepth(uv + float2(-o.x, -o.y));
    float d01 = SampleSceneDepth(uv + float2( o.x, -o.y));
    float d10 = SampleSceneDepth(uv + float2(-o.x,  o.y));
    float magnitude = abs(d00 - d11) + abs(d01 - d10);
    return saturate(magnitude * M_RENDER_EFFECTS_OUTLINE_EDGE_NORMALIZE);
}

#endif // PFOUND_RENDER_EFFECTS_OUTLINE_INCLUDED
