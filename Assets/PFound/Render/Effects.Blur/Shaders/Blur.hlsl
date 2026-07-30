#ifndef PFOUND_RENDER_EFFECTS_BLUR_INCLUDED
#define PFOUND_RENDER_EFFECTS_BLUR_INCLUDED

// -----------------------------------------------------------------------------
// PFound.Render.Effects.Blur — Blur.hlsl
//
// Separable 5-tap Gaussian helpers (H + V variants). Weight values match Phase 1's
// MRender_GaussianFilter5Tap to avoid filter drift across the module (spec FR-011
// soft-constraint: consistent with Phase 1 Sampling.hlsl conventions, locally
// defined per research R2 to minimize Phase 1 churn).
//
// Naming convention follows Phase 1: macros M_RENDER_*, functions MRender_*.
// -----------------------------------------------------------------------------

#define M_RENDER_EFFECTS_BLUR_W0 0.227027
#define M_RENDER_EFFECTS_BLUR_W1 0.1945946
#define M_RENDER_EFFECTS_BLUR_W2 0.1216216

half4 MRender_GaussianFilter5TapH(TEXTURE2D_PARAM(tex, samp), float2 uv, float2 texelSize)
{
    half4 sum  = SAMPLE_TEXTURE2D(tex, samp, uv) * M_RENDER_EFFECTS_BLUR_W0;
    sum += SAMPLE_TEXTURE2D(tex, samp, uv + float2( texelSize.x, 0)) * M_RENDER_EFFECTS_BLUR_W1;
    sum += SAMPLE_TEXTURE2D(tex, samp, uv + float2(-texelSize.x, 0)) * M_RENDER_EFFECTS_BLUR_W1;
    sum += SAMPLE_TEXTURE2D(tex, samp, uv + float2( texelSize.x * 2.0, 0)) * M_RENDER_EFFECTS_BLUR_W2;
    sum += SAMPLE_TEXTURE2D(tex, samp, uv + float2(-texelSize.x * 2.0, 0)) * M_RENDER_EFFECTS_BLUR_W2;
    return sum;
}

half4 MRender_GaussianFilter5TapV(TEXTURE2D_PARAM(tex, samp), float2 uv, float2 texelSize)
{
    half4 sum  = SAMPLE_TEXTURE2D(tex, samp, uv) * M_RENDER_EFFECTS_BLUR_W0;
    sum += SAMPLE_TEXTURE2D(tex, samp, uv + float2(0,  texelSize.y)) * M_RENDER_EFFECTS_BLUR_W1;
    sum += SAMPLE_TEXTURE2D(tex, samp, uv + float2(0, -texelSize.y)) * M_RENDER_EFFECTS_BLUR_W1;
    sum += SAMPLE_TEXTURE2D(tex, samp, uv + float2(0,  texelSize.y * 2.0)) * M_RENDER_EFFECTS_BLUR_W2;
    sum += SAMPLE_TEXTURE2D(tex, samp, uv + float2(0, -texelSize.y * 2.0)) * M_RENDER_EFFECTS_BLUR_W2;
    return sum;
}

#endif // PFOUND_RENDER_EFFECTS_BLUR_INCLUDED
