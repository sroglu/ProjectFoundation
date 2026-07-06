#ifndef PFOUND_SOFTTOONY_DEPTHPASSES_INCLUDED
#define PFOUND_SOFTTOONY_DEPTHPASSES_INCLUDED

// -----------------------------------------------------------------------------
// PFound/Render/SoftToony — SoftToonyDepthPasses.hlsl
//
// ShadowCaster, DepthOnly and DepthNormals. All three honour the same alpha-clip
// and dissolve cut as the forward pass so silhouettes, self-shadows and the
// depth prepass stay consistent with the visible surface.
// -----------------------------------------------------------------------------

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
#include "SoftToonyInput.hlsl"
#include "SoftToonySurface.hlsl"

// Shared clip: reproduces the forward UV resolution then applies alpha + dissolve.
void ApplyToonGeometryClip(float2 baseUV, float3 positionWS, float3 positionOS)
{
    float2 uv = baseUV;
#if defined(_PLANAR_ON)
    uv = PlanarProjectUV(positionWS, _PlanarTiling);
#endif
#if defined(_UVSCROLL_ON)
    uv = ScrollUV(uv, _ScrollSpeed.xy);
#endif

#if defined(_ALPHATEST_ON)
    clip(SampleBaseAlbedo(uv).a - _Cutoff);
#endif

#if defined(_DISSOLVE_ON)
    #if defined(_DISSOLVE_LOCAL)
        float dissolveAxis = positionOS.z;
    #else
        float dissolveAxis = positionWS.z;
    #endif
    clip(ComputeDissolve(dissolveAxis, uv).keep);
#endif
}

struct DepthAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS   : NORMAL;
    float4 tangentOS  : TANGENT;
    float2 uv         : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct DepthVaryings
{
    float4 positionCS : SV_POSITION;
    float3 positionWS : TEXCOORD0;
    float3 positionOS : TEXCOORD1;
    float3 normalWS   : TEXCOORD2;
    float2 uv         : TEXCOORD3;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

// ---------------------------------------------------------------- ShadowCaster
float3 _LightDirection;
float3 _LightPosition;

float4 ToonShadowClipPos(float3 positionWS, float3 normalWS)
{
#if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
    float3 lightDirWS = normalize(_LightPosition - positionWS);
#else
    float3 lightDirWS = _LightDirection;
#endif
    float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirWS));
#if UNITY_REVERSED_Z
    positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
#else
    positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
#endif
    return positionCS;
}

DepthVaryings ShadowCasterVertex(DepthAttributes input)
{
    DepthVaryings output = (DepthVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
    output.positionOS = input.positionOS.xyz;
    output.normalWS   = TransformObjectToWorldNormal(input.normalOS);
    output.uv         = TRANSFORM_TEX(input.uv, _BaseMap);
    output.positionCS = ToonShadowClipPos(output.positionWS, output.normalWS);
    return output;
}

half4 ShadowCasterFragment(DepthVaryings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    ApplyToonGeometryClip(input.uv, input.positionWS, input.positionOS);
    return 0;
}

// ------------------------------------------------------------------- DepthOnly
DepthVaryings DepthOnlyVertex(DepthAttributes input)
{
    DepthVaryings output = (DepthVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
    output.positionOS = input.positionOS.xyz;
    output.positionCS = TransformWorldToHClip(output.positionWS);
    output.uv         = TRANSFORM_TEX(input.uv, _BaseMap);
    return output;
}

half4 DepthOnlyFragment(DepthVaryings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    ApplyToonGeometryClip(input.uv, input.positionWS, input.positionOS);
    return 0;
}

// ---------------------------------------------------------------- DepthNormals
DepthVaryings DepthNormalsVertex(DepthAttributes input)
{
    DepthVaryings output = (DepthVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
    output.positionOS = input.positionOS.xyz;
    output.normalWS   = TransformObjectToWorldNormal(input.normalOS);
    output.positionCS = TransformWorldToHClip(output.positionWS);
    output.uv         = TRANSFORM_TEX(input.uv, _BaseMap);
    return output;
}

half4 DepthNormalsFragment(DepthVaryings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    ApplyToonGeometryClip(input.uv, input.positionWS, input.positionOS);
    return half4(NormalizeNormalPerPixel(input.normalWS), 0.0h);
}

#endif // PFOUND_SOFTTOONY_DEPTHPASSES_INCLUDED
