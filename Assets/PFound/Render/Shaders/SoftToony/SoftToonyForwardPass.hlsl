#ifndef PFOUND_SOFTTOONY_FORWARDPASS_INCLUDED
#define PFOUND_SOFTTOONY_FORWARDPASS_INCLUDED

// -----------------------------------------------------------------------------
// PFound/Render/SoftToony — SoftToonyForwardPass.hlsl
//
// UniversalForward vertex + fragment. Assembles the albedo (SoftToonySurface),
// runs the banded toon lighting (SoftToonyLighting) for the main + additional
// lights, then layers rim, LUT grading, emission and dissolve on top.
// -----------------------------------------------------------------------------

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "SoftToonyInput.hlsl"
#include "SoftToonyLighting.hlsl"
#include "SoftToonySurface.hlsl"

struct Attributes
{
    float4 positionOS : POSITION;
    float3 normalOS   : NORMAL;
    float4 tangentOS  : TANGENT;
    float2 uv         : TEXCOORD0;
    float2 uv2        : TEXCOORD1;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float4 positionCS  : SV_POSITION;
    float3 positionWS  : TEXCOORD0;
    float3 positionOS  : TEXCOORD1;
    float3 normalWS    : TEXCOORD2;
    float4 tangentWS   : TEXCOORD3;   // w = handedness
    float2 uv          : TEXCOORD4;
    float2 uv2         : TEXCOORD5;
    float4 screenPos   : TEXCOORD6;
    half   fogFactor   : TEXCOORD7;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

// Unity forward vector from euler degrees (pitch=x, yaw=y). Used by the art
// light-direction override so lighting can be authored independent of the scene.
float3 EulerToForwardWS(half3 eulerDeg)
{
    float3 r = radians(eulerDeg);
    float sx, cx, sy, cy;
    sincos(r.x, sx, cx);
    sincos(r.y, sy, cy);
    return normalize(float3(cx * sy, -sx, cx * cy));
}

Varyings SoftToonyVertex(Attributes input)
{
    Varyings output = (Varyings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
    VertexNormalInputs   nrmInputs = GetVertexNormalInputs(input.normalOS, input.tangentOS);

    output.positionCS = posInputs.positionCS;
    output.positionWS = posInputs.positionWS;
    output.positionOS = input.positionOS.xyz;
    output.normalWS   = nrmInputs.normalWS;
    output.tangentWS  = float4(nrmInputs.tangentWS, input.tangentOS.w * GetOddNegativeScale());
    output.uv         = TRANSFORM_TEX(input.uv, _BaseMap);
    output.uv2        = TRANSFORM_TEX(input.uv2, _SecondMap);
    output.screenPos  = ComputeScreenPos(posInputs.positionCS);
    output.fogFactor  = ComputeFogFactor(posInputs.positionCS.z);
    return output;
}

half4 SoftToonyFragment(Varyings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

    float3 positionWS = input.positionWS;
    float2 screenUV   = input.screenPos.xy / max(input.screenPos.w, 1e-4h);

    // --- UV resolution (planar projection + time scroll) ---------------------
    float2 uv = input.uv;
#if defined(_PLANAR_ON)
    uv = PlanarProjectUV(positionWS, _PlanarTiling);
#endif
#if defined(_UVSCROLL_ON)
    uv = ScrollUV(uv, _ScrollSpeed.xy);
#endif

    // --- Dissolve (clip early to skip shading on removed fragments) ----------
#if defined(_DISSOLVE_ON)
    #if defined(_DISSOLVE_LOCAL)
        float dissolveAxis = input.positionOS.z;
    #else
        float dissolveAxis = positionWS.z;
    #endif
    DissolveResult dissolve = ComputeDissolve(dissolveAxis, uv);
    clip(dissolve.keep);
#endif

    // --- Albedo assembly -----------------------------------------------------
    half4 baseSample = SampleBaseAlbedo(uv);
    half3 albedo = baseSample.rgb;
    half  alpha  = baseSample.a;

#if defined(_ALPHATEST_ON)
    clip(alpha - _Cutoff);
#endif

#if defined(_SECONDTEX_ON)
    albedo = ApplySecondaryTexture(albedo, input.uv2, uv);
#endif
#if defined(_GRADIENT_ON)
    albedo = ApplyWorldGradient(albedo, positionWS);
#endif

    // --- Normal --------------------------------------------------------------
    half3 normalWS = normalize(input.normalWS);
#if defined(_NORMALMAP)
    half3 normalTS = UnpackNormalScale(
        SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uv), _BumpScale);
    float sgn = input.tangentWS.w;
    float3 bitangent = sgn * cross(input.normalWS, input.tangentWS.xyz);
    half3x3 tbn = half3x3(input.tangentWS.xyz, bitangent, input.normalWS);
    normalWS = normalize(mul(normalTS, tbn));
#endif

    half3 viewDirWS = GetWorldSpaceNormalizeViewDir(positionWS);

    // --- Main light ----------------------------------------------------------
    float4 shadowCoord = TransformWorldToShadowCoord(positionWS);
    Light mainLight = GetMainLight(shadowCoord);

    float3 lightDir = mainLight.direction;
#if defined(_ARTLIGHT_ON)
    lightDir = -EulerToForwardWS(_ArtLightEuler.xyz);   // toward the light
#endif

    half shadowAtten = 1.0h;
#if !defined(_RECEIVE_SHADOWS_OFF)
    shadowAtten = mainLight.shadowAttenuation;
#endif

    half ndlRaw     = dot(normalWS, lightDir);
    half halfLambert = ndlRaw * 0.5h + 0.5h;
    half band = SoftBandedDiffuse(halfLambert, _ToonBands, _BandThreshold, _BandSoftness);
    band *= shadowAtten;

    half3 litColor = albedo * mainLight.color;

    // Stylized shadow colour (auto-derived or manual tint), further tinted by
    // the flat fake-shadow colour so shadowed areas read as authored.
#if defined(_AUTOSHADOW_ON)
    half3 shadowColor = DeriveStylizedShadow(litColor, _ShadowStrength, _ShadowDarkness,
                                             _ShadowCoolness, _ShadowSaturation, _ShadowMinBrightness);
#else
    half3 shadowColor = albedo * _ShadowColor.rgb;
#endif
    shadowColor *= _FakeShadowColor.rgb;

    half3 diffuse = lerp(shadowColor, litColor, band);

    // Light ridge (thin terminator highlight)
#if defined(_LIGHTRIDGE_ON)
    half ridge = ComputeLightRidge(halfLambert, _RidgeThreshold, _RidgeWidth, _RidgeIntensity);
    diffuse += _RidgeColor.rgb * (ridge * shadowAtten);
#endif

    // Directional band tint
#if defined(_BANDTINT_ON)
    diffuse += DirectionalBandTint(band, _ToonBands, _BandTintBand, normalWS,
                                   _BandTintAxisWS.xyz, _BandTintColor.rgb, _BandTintDirScale);
#endif

    // --- Additional lights (lightweight banded) ------------------------------
#if defined(_ADDITIONAL_LIGHTS) || defined(_FORWARD_PLUS)
    // Forward+ clustering (LIGHT_LOOP_BEGIN) reads screen UV + world pos from a
    // local InputData; populate the fields the cluster iterator needs.
    InputData clusterInput = (InputData)0;
    clusterInput.positionWS = positionWS;
    clusterInput.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
    uint pixelLightCount = GetAdditionalLightsCount();
    LIGHT_LOOP_BEGIN(pixelLightCount)
        Light addLight = GetAdditionalLight(lightIndex, positionWS);
        half addAtten = addLight.distanceAttenuation * addLight.shadowAttenuation;
        half addNdl = dot(normalWS, addLight.direction) * 0.5h + 0.5h;
        half addBand = SoftBandedDiffuse(addNdl, _ToonBands, _BandThreshold, _BandSoftness);
        diffuse += albedo * addLight.color * (addBand * addAtten);
    LIGHT_LOOP_END
#endif

    // Ambient fill from spherical harmonics.
    half3 color = diffuse + SampleSH(normalWS) * albedo;

    // --- Rim -----------------------------------------------------------------
#if defined(_RIM_ON)
    half rim = ComputeRimLight(normalWS, viewDirWS, half3(0.0h, 1.0h, 0.0h),
                               _RimPower, _RimSmoothness, _RimWorldViewBlend);
    color += _RimColor.rgb * rim;
#endif

    // --- Emission / side highlight / mask overlay ----------------------------
#if defined(_EMISSION)
    color += SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, uv).rgb * _EmissionColor.rgb;
#endif
#if defined(_SIDEHIGHLIGHT_ON)
    color = ApplySideHighlight(color, normalWS);
#endif
#if defined(_MASKOVERLAY_ON)
    color = ApplyMaskOverlay(color, uv);
#endif

    // --- Dissolve edge glow --------------------------------------------------
#if defined(_DISSOLVE_ON)
    color += _DissolveEdgeColor.rgb * dissolve.glow;
#endif

    // --- Colour grading LUT --------------------------------------------------
#if defined(_LUT_ON)
    color = ApplyColorGrading(color, screenUV);
#endif

    color = MixFog(color, input.fogFactor);
    return half4(color, alpha);
}

#endif // PFOUND_SOFTTOONY_FORWARDPASS_INCLUDED
