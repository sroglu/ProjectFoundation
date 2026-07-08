#ifndef PFOUND_RENDER_EFFECTS_OVERDRAW_INCLUDED
#define PFOUND_RENDER_EFFECTS_OVERDRAW_INCLUDED

// -----------------------------------------------------------------------------
// PFound.Render.Effects.Overdraw — Overdraw.hlsl
//
// Threshold-ramp lookup for the composite pass. Up to 8 (threshold, color) tier
// pairs. Sorted ascending by threshold on the C# side; unused trailing slots
// receive 1e9 sentinel to keep the unroll-loop branch-free.
//
// Naming convention follows Phase 1: macros M_RENDER_*, functions MRender_*.
// -----------------------------------------------------------------------------

#define M_RENDER_EFFECTS_OVERDRAW_MAX_TIERS 8

float  _Thresholds[M_RENDER_EFFECTS_OVERDRAW_MAX_TIERS];
half4  _Colors[M_RENDER_EFFECTS_OVERDRAW_MAX_TIERS];
int    _ThresholdCount;

// Walk ascending thresholds; highest matching tier wins.
half4 MRender_OverdrawSampleRamp(float overdrawValue)
{
    half4 color = _Colors[0];
    [unroll]
    for (int i = 0; i < M_RENDER_EFFECTS_OVERDRAW_MAX_TIERS; i++)
    {
        if (i < _ThresholdCount && overdrawValue >= _Thresholds[i])
            color = _Colors[i];
    }
    return color;
}

#endif // PFOUND_RENDER_EFFECTS_OVERDRAW_INCLUDED
