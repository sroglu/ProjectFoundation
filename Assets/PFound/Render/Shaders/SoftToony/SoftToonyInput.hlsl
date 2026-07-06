#ifndef PFOUND_SOFTTOONY_INPUT_INCLUDED
#define PFOUND_SOFTTOONY_INPUT_INCLUDED

// -----------------------------------------------------------------------------
// PFound/Render/SoftToony — SoftToonyInput.hlsl
//
// Per-material property block + texture bindings, shared by every pass.
//
// SRP-Batcher contract: every per-material scalar/vector lives inside the single
// CBUFFER_START(UnityPerMaterial) below. Texture handles + sampler states are
// declared outside the cbuffer (they are not batched constants). Keep it that way.
// -----------------------------------------------------------------------------

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

// --- Textures / samplers -----------------------------------------------------
TEXTURE2D(_BaseMap);            SAMPLER(sampler_BaseMap);
TEXTURE2D(_BumpMap);            SAMPLER(sampler_BumpMap);
TEXTURE2D(_EmissionMap);        SAMPLER(sampler_EmissionMap);
TEXTURE2D(_SecondMap);          SAMPLER(sampler_SecondMap);
TEXTURE2D(_SecondMask);         SAMPLER(sampler_SecondMask);
TEXTURE2D(_MaskOverlayMap);     SAMPLER(sampler_MaskOverlayMap);
TEXTURE2D(_DissolveNoise);      SAMPLER(sampler_DissolveNoise);
TEXTURE2D(_GradingLut);         SAMPLER(sampler_GradingLut);

// --- Per-material constants (SRP-Batcher block) ------------------------------
CBUFFER_START(UnityPerMaterial)
    float4 _BaseMap_ST;
    float4 _SecondMap_ST;
    half4  _BaseColor;

    // Main-tex options
    half   _MainTexMix;
    half   _MainTexCurve;
    half   _PlanarTiling;

    // Alpha
    half   _Cutoff;

    // Toon core (banded soft diffuse)
    half   _ToonBands;
    half   _BandThreshold;
    half   _BandSoftness;
    half4  _ShadowColor;        // manual HDR shadow tint

    // Auto stylized shadow derivation
    half   _ShadowStrength;
    half   _ShadowDarkness;
    half   _ShadowCoolness;
    half   _ShadowSaturation;
    half   _ShadowMinBrightness;

    // Rim
    half4  _RimColor;
    half   _RimPower;
    half   _RimSmoothness;
    half   _RimWorldViewBlend;

    // Directional band tint
    half4  _BandTintColor;
    half   _BandTintBand;       // which band index (0..bands-1) receives the tint
    half   _BandTintDirScale;
    half4  _BandTintAxisWS;     // world-space axis the tint reacts to

    // Light ridge
    half4  _RidgeColor;
    half   _RidgeThreshold;
    half   _RidgeWidth;
    half   _RidgeIntensity;

    // LUT grading
    half   _LutAmount;
    half   _LutDither;

    // Art light direction override (euler degrees)
    half4  _ArtLightEuler;

    // Fake / real shadow controls
    half4  _FakeShadowColor;

    // UV time scroll
    half4  _ScrollSpeed;        // xy = main uv velocity

    // World vertical gradient tint
    half4  _GradientTopColor;
    half4  _GradientBottomColor;
    half   _GradientTopHeight;
    half   _GradientBottomHeight;
    half   _GradientAmount;

    // Normal / emission
    half   _BumpScale;
    half4  _EmissionColor;

    // Secondary texture blend
    half4  _SecondColor;
    half   _SecondAmount;
    half   _SecondOverlay;      // 0 = alpha blend, 1 = overlay

    // Side highlight + mask overlay
    half4  _SideHighlightColor;
    half   _SideHighlightPower;
    half4  _SideHighlightAxisWS;
    half4  _MaskOverlayColor;
    half   _MaskOverlayAmount;

    // Dissolve
    half   _DissolveProgress;
    half   _DissolveEdgeSmoothness;
    half   _DissolveSinkPoint;
    half   _DissolveTravel;
    half   _DissolveReverse;
    half4  _DissolveEdgeColor;  // HDR glow
CBUFFER_END

#endif // PFOUND_SOFTTOONY_INPUT_INCLUDED
