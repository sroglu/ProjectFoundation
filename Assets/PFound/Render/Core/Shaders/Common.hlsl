#ifndef PFOUND_RENDER_COMMON_INCLUDED
#define PFOUND_RENDER_COMMON_INCLUDED

// -----------------------------------------------------------------------------
// PFound.Render.Core — Common.hlsl
//
// Universal constants + saturate/remap helpers used by every Phase 2+ effect.
// All macros prefixed `M_RENDER_*`; all functions prefixed `MRender_*` (see
// MODULE.md shader-library conventions).
// -----------------------------------------------------------------------------

#define M_RENDER_PI       3.14159265358979323846
#define M_RENDER_INV_PI   0.31830988618379067154
#define M_RENDER_TWO_PI   6.28318530717958647692
#define M_RENDER_HALF_PI  1.57079632679489661923
#define M_RENDER_EPSILON  1e-6

float MRender_Saturate(float x)
{
    return saturate(x);
}

float MRender_RemapClamped(float v, float fromLo, float fromHi, float toLo, float toHi)
{
    float t = saturate((v - fromLo) / max(fromHi - fromLo, M_RENDER_EPSILON));
    return lerp(toLo, toHi, t);
}

#endif // PFOUND_RENDER_COMMON_INCLUDED
