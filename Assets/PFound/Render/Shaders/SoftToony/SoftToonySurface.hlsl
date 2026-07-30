#ifndef PFOUND_SOFTTOONY_SURFACE_INCLUDED
#define PFOUND_SOFTTOONY_SURFACE_INCLUDED

// -----------------------------------------------------------------------------
// PFound/Render/SoftToony — SoftToonySurface.hlsl
//
// Albedo assembly + non-lighting surface effects that every lit/unlit path
// shares: animated UVs, planar UV projection, base-tex mix/curve, secondary
// blend, world gradient tint, side highlight, mask overlay, dissolve, and the
// horizontal-strip colour-grading LUT.
// -----------------------------------------------------------------------------

#include "SoftToonyInput.hlsl"

// Scrolls a UV set by material velocity * time. Cheap; time comes from _Time.y.
float2 ScrollUV(float2 uv, half2 velocity)
{
    return uv + velocity * _Time.y;
}

// World XY planar projection — drops the mesh UVs and projects along +Z so the
// texture tiles in world space (useful for terrain-like / decal-ish looks).
float2 PlanarProjectUV(float3 positionWS, half tiling)
{
    return positionWS.xy * tiling;
}

// Base albedo with mix + curve. `mix` cross-fades between the flat base colour
// and the sampled texture; `curve` is a gamma-style contrast on the sampled RGB.
half4 SampleBaseAlbedo(float2 uv)
{
    half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv);
    half3 curved = pow(max(tex.rgb, 1e-4h), max(_MainTexCurve, 1e-2h));
    half3 rgb = lerp(_BaseColor.rgb, curved * _BaseColor.rgb, saturate(_MainTexMix));
    return half4(rgb, tex.a * _BaseColor.a);
}

// Secondary texture blend: alpha-over at _SecondOverlay=0, additive top-blend at 1.
half3 ApplySecondaryTexture(half3 baseRGB, float2 uvSecond, float2 uvMask)
{
    half4 second = SAMPLE_TEXTURE2D(_SecondMap, sampler_SecondMap, uvSecond) * _SecondColor;
    half mask = SAMPLE_TEXTURE2D(_SecondMask, sampler_SecondMask, uvMask).r;
    half weight = saturate(mask * _SecondAmount) * second.a;

    half3 alphaOver = lerp(baseRGB, second.rgb, weight);
    half3 topBlend  = baseRGB + second.rgb * weight;
    return lerp(alphaOver, topBlend, saturate(_SecondOverlay));
}

// World-relative vertical gradient tint (multiplicative, amount-weighted).
half3 ApplyWorldGradient(half3 rgb, float3 positionWS)
{
    half t = saturate((positionWS.y - _GradientBottomHeight) /
                      max(_GradientTopHeight - _GradientBottomHeight, 1e-3h));
    half3 grad = lerp(_GradientBottomColor.rgb, _GradientTopColor.rgb, t);
    return lerp(rgb, rgb * grad, saturate(_GradientAmount));
}

// Side highlight: brightens the surface as its normal faces a world axis.
half3 ApplySideHighlight(half3 rgb, half3 normalWS)
{
    half side = saturate(dot(normalWS, normalize(_SideHighlightAxisWS.xyz)));
    half hi = pow(side, max(_SideHighlightPower, 1e-2h));
    return rgb + _SideHighlightColor.rgb * hi;
}

// Mask overlay: tints RGB where the mask texture is present.
half3 ApplyMaskOverlay(half3 rgb, float2 uv)
{
    half mask = SAMPLE_TEXTURE2D(_MaskOverlayMap, sampler_MaskOverlayMap, uv).r;
    return rgb + _MaskOverlayColor.rgb * (mask * _MaskOverlayAmount);
}

// --- Dissolve ---------------------------------------------------------------
struct DissolveResult
{
    half keep;   // >0 => fragment survives; used with clip()
    half glow;   // 0..1 edge factor for the glowing rim
};

// Dissolve along an axis coordinate (world- or local-Z is chosen by the caller).
// A noise texture breaks up the sweep line. `progress` 0 => intact, 1 => gone.
DissolveResult ComputeDissolve(float axisCoord, float2 noiseUV)
{
    half noise = SAMPLE_TEXTURE2D(_DissolveNoise, sampler_DissolveNoise, noiseUV).r;

    // Normalise the sweep coordinate to ~0..1 across the travel distance,
    // centred on the sink point, with optional direction reversal.
    half gradient = (axisCoord - _DissolveSinkPoint) / max(_DissolveTravel, 1e-3h);
    gradient = lerp(gradient, -gradient, saturate(_DissolveReverse));
    half field = saturate(gradient + 0.5h);

    // Perturb the cut line by noise, then subtract the animated progress.
    half perturbed = field + (noise - 0.5h) * 0.5h;
    half keep = perturbed - _DissolveProgress;

    DissolveResult r;
    r.keep = keep;
    r.glow = 1.0h - smoothstep(0.0h, max(_DissolveEdgeSmoothness, 1e-3h), keep);
    return r;
}

// --- Colour grading LUT (horizontal 256x16 strip, 16^3) ---------------------
// Classic 16-cell strip: blue selects the cell, red is the intra-cell U, green
// is V. Two neighbouring blue slices are sampled and lerped for smoothness.
half3 SampleGradingLut(half3 color)
{
    const half size    = 16.0h;
    const half sizeM1   = size - 1.0h;      // 15
    const half texW     = size * size;      // 256
    const half texH     = size;             // 16

    color = saturate(color);
    half blue = color.b * sizeM1;
    half b0 = floor(blue);
    half b1 = min(b0 + 1.0h, sizeM1);
    half frac = blue - b0;

    half red = color.r * sizeM1;
    half v   = (color.g * sizeM1 + 0.5h) / texH;
    half u0  = (b0 * size + red + 0.5h) / texW;
    half u1  = (b1 * size + red + 0.5h) / texW;

    half3 s0 = SAMPLE_TEXTURE2D_LOD(_GradingLut, sampler_GradingLut, float2(u0, v), 0).rgb;
    half3 s1 = SAMPLE_TEXTURE2D_LOD(_GradingLut, sampler_GradingLut, float2(u1, v), 0).rgb;
    return lerp(s0, s1, frac);
}

// Cheap value hash for LUT dither (breaks banding at low LUT resolution).
half Hash12(float2 p)
{
    float3 q = frac(float3(p.xyx) * 0.1031);
    q += dot(q, q.yzx + 33.33);
    return frac((q.x + q.y) * q.z);
}

half3 ApplyColorGrading(half3 rgb, float2 screenUV)
{
    half dither = (Hash12(screenUV * _ScreenParams.xy) - 0.5h) * _LutDither * (1.0h / 255.0h);
    half3 graded = SampleGradingLut(saturate(rgb + dither));
    return lerp(rgb, graded, saturate(_LutAmount));
}

#endif // PFOUND_SOFTTOONY_SURFACE_INCLUDED
