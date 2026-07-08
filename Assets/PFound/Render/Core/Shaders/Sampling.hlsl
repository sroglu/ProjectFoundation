#ifndef PFOUND_RENDER_SAMPLING_INCLUDED
#define PFOUND_RENDER_SAMPLING_INCLUDED

// -----------------------------------------------------------------------------
// PFound.Render.Core — Sampling.hlsl
//
// Multi-tap filter helpers used by Phase 3+ Blur, Phase 4+ Outline.
// All taps assume the supplied SamplerState is a clamp+linear configuration.
// -----------------------------------------------------------------------------

float4 MRender_BoxFilter4Tap(Texture2D tex, SamplerState s, float2 uv, float2 texelSize)
{
    float2 o = texelSize * 0.5;
    float4 sum = tex.SampleLevel(s, uv + float2(-o.x, -o.y), 0);
    sum += tex.SampleLevel(s, uv + float2( o.x, -o.y), 0);
    sum += tex.SampleLevel(s, uv + float2(-o.x,  o.y), 0);
    sum += tex.SampleLevel(s, uv + float2( o.x,  o.y), 0);
    return sum * 0.25;
}

float4 MRender_GaussianFilter5Tap(Texture2D tex, SamplerState s, float2 uv, float2 texelSize)
{
    // 5-tap separable approximation, centered.
    // Weights chosen so they sum to 1: { 0.227027, 0.1945946 * 2, 0.1216216 * 2 }
    float4 sum  = tex.SampleLevel(s, uv,                              0) * 0.227027;
    sum +=        tex.SampleLevel(s, uv + float2( texelSize.x, 0),    0) * 0.1945946;
    sum +=        tex.SampleLevel(s, uv + float2(-texelSize.x, 0),    0) * 0.1945946;
    sum +=        tex.SampleLevel(s, uv + float2( texelSize.x * 2, 0), 0) * 0.1216216;
    sum +=        tex.SampleLevel(s, uv + float2(-texelSize.x * 2, 0), 0) * 0.1216216;
    return sum;
}

#endif // PFOUND_RENDER_SAMPLING_INCLUDED
