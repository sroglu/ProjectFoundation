#ifndef PFOUND_SOFTTOONY_LIGHTING_INCLUDED
#define PFOUND_SOFTTOONY_LIGHTING_INCLUDED

// -----------------------------------------------------------------------------
// PFound/Render/SoftToony — SoftToonyLighting.hlsl
//
// Stateless toon-shading math. Every function takes its inputs as arguments so
// the same helpers can be exercised by unit-style pixel tests without a lit
// scene. These are the "soft toony" primitives:
//   * SoftBandedDiffuse   — quantized N.L with smoothstep-softened risers
//   * DeriveStylizedShadow — procedural cool-shifted shadow colour
//   * ComputeRimLight      — Fresnel rim with world<->view reference blend
//   * DirectionalBandTint  — tints one diffuse band by a world-space direction
//   * ComputeLightRidge    — thin highlight strip riding the N.L terminator
// -----------------------------------------------------------------------------

// Quantise a 0..1 gradient into `bands` steps. Each riser is softened by a
// smoothstep whose width is `softness` (0 => hard cel step, 1 => nearly linear).
// `threshold` slides the whole ramp so the lit/shadow break can be tuned.
half SoftBandedDiffuse(half ndl, half bands, half threshold, half softness)
{
    bands = max(bands, 1.0h);
    half shaped = saturate((ndl - threshold) / max(1.0h - threshold, 1e-3h));

    half scaled  = shaped * bands;
    half stepIdx = floor(scaled);
    half riser   = scaled - stepIdx;                 // 0..1 within the current band

    half halfWidth = clamp(softness, 0.0h, 1.0h) * 0.5h;
    half soft = smoothstep(0.5h - halfWidth - 1e-3h,
                           0.5h + halfWidth + 1e-3h, riser);

    return (stepIdx + soft) / bands;
}

// Procedurally build a shadow colour that is a cool, desaturated, darkened
// version of the incoming lit colour. All knobs are 0..1 except darkness which
// is a multiplier floor.
//   strength      — how strongly the derived tint replaces plain darkening
//   darkness      — luminance the deepest shadow collapses toward
//   coolness      — how far the hue is pushed to the cool reference
//   saturation    — 0 greys the shadow, 1 keeps chroma
//   minBrightness — clamps the shadow so it never reaches pure black
half3 DeriveStylizedShadow(half3 litColor, half strength, half darkness,
                           half coolness, half saturation, half minBrightness)
{
    const half3 coolReference = half3(0.55h, 0.70h, 1.0h);

    half3 darkened = litColor * darkness;
    half3 cooled   = lerp(darkened, darkened * coolReference, coolness);

    half luma = dot(cooled, half3(0.299h, 0.587h, 0.114h));
    half3 graded = lerp(luma.xxx, cooled, saturation);

    half3 shadow = lerp(litColor * darkness, graded, strength);
    return max(shadow, minBrightness.xxx);
}

// Fresnel-style rim. The reference vector is blended between the true view
// direction (view-locked rim) and a fixed world axis (world-locked rim), so the
// artist can anchor the rim to the camera or to the world.
half ComputeRimLight(half3 normalWS, half3 viewDirWS, half3 worldAxis,
                     half power, half smoothness, half worldViewBlend)
{
    half3 refDir = normalize(lerp(viewDirWS, worldAxis, saturate(worldViewBlend)));
    half fresnel = 1.0h - saturate(dot(normalWS, refDir));
    half rim = pow(fresnel, max(power, 1e-2h));
    half lo = saturate(1.0h - smoothness);
    return smoothstep(lo, 1.0h, rim);
}

// Directional band tint: isolate one band of the quantised ramp and modulate a
// tint colour by how much the surface faces `axisWS`. Cheap — one dot + a
// triangular band-window. Returns an additive tint contribution (pre-masked).
half3 DirectionalBandTint(half diffuseBand, half bands, half tintBand,
                          half3 normalWS, half3 axisWS, half3 tintColor,
                          half dirScale)
{
    bands = max(bands, 1.0h);
    // Window that is 1 at the centre of the requested band, 0 at its neighbours.
    half bandPos   = diffuseBand * bands;             // 0..bands
    half distance  = abs(bandPos - (tintBand + 0.5h));
    half window    = saturate(1.0h - distance);

    half facing = saturate(dot(normalWS, normalize(axisWS)) * 0.5h + 0.5h);
    facing = lerp(1.0h, facing, saturate(dirScale));

    return tintColor * window * facing;
}

// Light ridge: a thin bright strip that rides the terminator (the N.L value
// where light meets shadow). Built from the difference of two smoothsteps so it
// is a symmetric band of controllable width — no extra texture, one N.L reuse.
half ComputeLightRidge(half ndl, half threshold, half width, half intensity)
{
    half w = max(width, 1e-3h);
    half rising  = smoothstep(threshold - w, threshold,       ndl);
    half falling = smoothstep(threshold,       threshold + w, ndl);
    return (rising - falling) * intensity;            // peaks at `threshold`
}

#endif // PFOUND_SOFTTOONY_LIGHTING_INCLUDED
