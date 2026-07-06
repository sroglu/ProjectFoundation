// PFound/Render/SoftToony
// -----------------------------------------------------------------------------
// A "soft toony" URP surface shader: banded N.L diffuse with smoothstep-softened
// risers, procedural cool-shifted stylized shadows, Fresnel rim, LUT grading,
// dissolve, secondary blend and a handful of art-directable extras.
//
// Hand-authored HLSL. SRP-Batcher compatible (single UnityPerMaterial cbuffer).
// Passes: UniversalForward, ShadowCaster, DepthOnly, DepthNormals.
// -----------------------------------------------------------------------------
Shader "PFound/Render/SoftToony"
{
    Properties
    {
        [Header(Main Texture)][Space(4)]
        [HDR] _BaseColor        ("Base Color (HDR)", Color) = (1,1,1,1)
        _BaseMap                ("Base Map", 2D) = "white" {}
        _MainTexMix             ("Texture Mix", Range(0,1)) = 1
        _MainTexCurve           ("Texture Curve", Range(0.1,4)) = 1
        [Toggle(_PLANAR_ON)] _PlanarProjection ("World XY Planar Projection", Float) = 0
        _PlanarTiling           ("Planar Tiling", Float) = 1
        [Toggle(_ALPHATEST_ON)] _AlphaClip ("Alpha Clip", Float) = 0
        _Cutoff                 ("Alpha Cutoff", Range(0,1)) = 0.5

        [Header(Toon Bands)][Space(4)]
        _ToonBands              ("Band Count", Range(1,8)) = 2
        _BandThreshold          ("Band Threshold", Range(0,1)) = 0.2
        _BandSoftness           ("Band Softness", Range(0,1)) = 0.35

        [Header(Stylized Shadow)][Space(4)]
        [HDR] _ShadowColor      ("Manual Shadow Tint (HDR)", Color) = (0.35,0.4,0.55,1)
        [Toggle(_AUTOSHADOW_ON)] _AutoShadow ("Auto Stylized Shadow", Float) = 1
        _ShadowStrength         ("Auto Strength", Range(0,1)) = 0.8
        _ShadowDarkness         ("Auto Darkness", Range(0,1)) = 0.45
        _ShadowCoolness         ("Auto Coolness", Range(0,1)) = 0.5
        _ShadowSaturation       ("Auto Saturation", Range(0,1)) = 0.7
        _ShadowMinBrightness    ("Auto Min Brightness", Range(0,1)) = 0.05
        [Toggle(_RECEIVE_SHADOWS_OFF)] _ReceiveShadowsOff ("Ignore Real Shadows", Float) = 0
        [HDR] _FakeShadowColor  ("Fake Shadow Tint (HDR)", Color) = (1,1,1,1)

        [Header(Rim Light)][Space(4)]
        [Toggle(_RIM_ON)] _RimOn ("Rim Enabled", Float) = 1
        [HDR] _RimColor         ("Rim Color (HDR)", Color) = (1,1,1,1)
        _RimPower               ("Rim Power", Range(0.1,16)) = 4
        _RimSmoothness          ("Rim Smoothness", Range(0,1)) = 0.3
        _RimWorldViewBlend      ("World / View Blend", Range(0,1)) = 0

        [Header(Directional Band Tint)][Space(4)]
        [Toggle(_BANDTINT_ON)] _BandTintOn ("Band Tint Enabled", Float) = 0
        [HDR] _BandTintColor    ("Band Tint Color (HDR)", Color) = (1,0.6,0.3,1)
        _BandTintBand           ("Target Band Index", Range(0,7)) = 1
        _BandTintDirScale       ("Direction Response", Range(0,1)) = 1
        _BandTintAxisWS         ("Direction Axis (WS)", Vector) = (1,0,0,0)

        [Header(Light Ridge)][Space(4)]
        [Toggle(_LIGHTRIDGE_ON)] _LightRidgeOn ("Light Ridge Enabled", Float) = 0
        [HDR] _RidgeColor       ("Ridge Color (HDR)", Color) = (1,1,0.9,1)
        _RidgeThreshold         ("Ridge Position", Range(0,1)) = 0.5
        _RidgeWidth             ("Ridge Width", Range(0.01,0.5)) = 0.08
        _RidgeIntensity         ("Ridge Intensity", Range(0,4)) = 1

        [Header(Color Grading LUT)][Space(4)]
        [Toggle(_LUT_ON)] _LutOn ("LUT Enabled", Float) = 0
        [NoScaleOffset] _GradingLut ("Grading LUT (256x16)", 2D) = "white" {}
        _LutAmount              ("LUT Amount", Range(0,1)) = 1
        _LutDither              ("LUT Dither", Range(0,4)) = 1

        [Header(Art Light Direction)][Space(4)]
        [Toggle(_ARTLIGHT_ON)] _ArtLightOn ("Override Light Direction", Float) = 0
        _ArtLightEuler          ("Light Euler (deg)", Vector) = (50,-30,0,0)

        [Header(UV Scroll)][Space(4)]
        [Toggle(_UVSCROLL_ON)] _UvScroll ("UV Scroll Enabled", Float) = 0
        _ScrollSpeed            ("Scroll Speed (xy)", Vector) = (0.1,0,0,0)

        [Header(World Gradient)][Space(4)]
        [Toggle(_GRADIENT_ON)] _GradientOn ("Gradient Enabled", Float) = 0
        [HDR] _GradientTopColor    ("Top Color (HDR)", Color) = (1,1,1,1)
        [HDR] _GradientBottomColor ("Bottom Color (HDR)", Color) = (0.6,0.6,0.7,1)
        _GradientTopHeight      ("Top Height (WS Y)", Float) = 2
        _GradientBottomHeight   ("Bottom Height (WS Y)", Float) = 0
        _GradientAmount         ("Gradient Amount", Range(0,1)) = 1

        [Header(Maps)][Space(4)]
        [Toggle(_NORMALMAP)] _NormalMapOn ("Normal Map Enabled", Float) = 0
        [Normal] _BumpMap       ("Normal Map", 2D) = "bump" {}
        _BumpScale              ("Normal Scale", Float) = 1
        [Toggle(_EMISSION)] _EmissionOn ("Emission Enabled", Float) = 0
        [NoScaleOffset] _EmissionMap ("Emission Map", 2D) = "black" {}
        [HDR] _EmissionColor    ("Emission Color (HDR)", Color) = (0,0,0,1)

        [Header(Secondary Texture)][Space(4)]
        [Toggle(_SECONDTEX_ON)] _SecondTexOn ("Secondary Blend Enabled", Float) = 0
        _SecondMap              ("Secondary Map", 2D) = "white" {}
        [NoScaleOffset] _SecondMask ("Secondary Mask", 2D) = "white" {}
        [HDR] _SecondColor      ("Secondary Color (HDR)", Color) = (1,1,1,1)
        _SecondAmount           ("Secondary Amount", Range(0,1)) = 1
        _SecondOverlay          ("Overlay (0 blend / 1 add)", Range(0,1)) = 0

        [Header(Side Highlight and Mask)][Space(4)]
        [Toggle(_SIDEHIGHLIGHT_ON)] _SideHighlightOn ("Side Highlight Enabled", Float) = 0
        [HDR] _SideHighlightColor ("Side Highlight Color (HDR)", Color) = (1,1,1,1)
        _SideHighlightPower     ("Side Highlight Power", Range(0.1,16)) = 3
        _SideHighlightAxisWS    ("Side Axis (WS)", Vector) = (1,0,0,0)
        [Toggle(_MASKOVERLAY_ON)] _MaskOverlayOn ("Mask Overlay Enabled", Float) = 0
        [NoScaleOffset] _MaskOverlayMap ("Mask Overlay Map", 2D) = "black" {}
        [HDR] _MaskOverlayColor ("Mask Overlay Color (HDR)", Color) = (1,1,1,1)
        _MaskOverlayAmount      ("Mask Overlay Amount", Range(0,1)) = 1

        [Header(Dissolve)][Space(4)]
        [Toggle(_DISSOLVE_ON)] _DissolveOn ("Dissolve Enabled", Float) = 0
        [Toggle(_DISSOLVE_LOCAL)] _DissolveLocalSpace ("Local Z (else World Z)", Float) = 0
        [NoScaleOffset] _DissolveNoise ("Dissolve Noise", 2D) = "gray" {}
        _DissolveProgress       ("Progress", Range(0,1)) = 0
        _DissolveEdgeSmoothness ("Edge Smoothness", Range(0.001,0.5)) = 0.1
        _DissolveSinkPoint      ("Sink Point (Z)", Float) = 0
        _DissolveTravel         ("Travel Distance", Float) = 2
        [Toggle] _DissolveReverse ("Reverse", Float) = 0
        [HDR] _DissolveEdgeColor ("Edge Glow (HDR)", Color) = (2,1,0.2,1)
    }

    SubShader
    {
        Tags
        {
            "RenderType"      = "Opaque"
            "RenderPipeline"  = "UniversalPipeline"
            "Queue"           = "Geometry"
            "UniversalMaterialType" = "Lit"
        }
        LOD 300

        // ---------------------------------------------------------- Forward
        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex SoftToonyVertex
            #pragma fragment SoftToonyFragment

            // URP lighting keywords
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile _ _FORWARD_PLUS
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            // Feature toggles
            #pragma shader_feature_local _PLANAR_ON
            #pragma shader_feature_local _UVSCROLL_ON
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local _AUTOSHADOW_ON
            #pragma shader_feature_local _RECEIVE_SHADOWS_OFF
            #pragma shader_feature_local _RIM_ON
            #pragma shader_feature_local _BANDTINT_ON
            #pragma shader_feature_local _LIGHTRIDGE_ON
            #pragma shader_feature_local _LUT_ON
            #pragma shader_feature_local _ARTLIGHT_ON
            #pragma shader_feature_local _GRADIENT_ON
            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local _EMISSION
            #pragma shader_feature_local _SECONDTEX_ON
            #pragma shader_feature_local _SIDEHIGHLIGHT_ON
            #pragma shader_feature_local _MASKOVERLAY_ON
            #pragma shader_feature_local _DISSOLVE_ON
            #pragma shader_feature_local _DISSOLVE_LOCAL

            #include "SoftToonyForwardPass.hlsl"
            ENDHLSL
        }

        // ------------------------------------------------------ ShadowCaster
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex ShadowCasterVertex
            #pragma fragment ShadowCasterFragment

            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile_instancing

            #pragma shader_feature_local _PLANAR_ON
            #pragma shader_feature_local _UVSCROLL_ON
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local _DISSOLVE_ON
            #pragma shader_feature_local _DISSOLVE_LOCAL

            #include "SoftToonyDepthPasses.hlsl"
            ENDHLSL
        }

        // ---------------------------------------------------------- DepthOnly
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment

            #pragma multi_compile_instancing

            #pragma shader_feature_local _PLANAR_ON
            #pragma shader_feature_local _UVSCROLL_ON
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local _DISSOLVE_ON
            #pragma shader_feature_local _DISSOLVE_LOCAL

            #include "SoftToonyDepthPasses.hlsl"
            ENDHLSL
        }

        // ------------------------------------------------------- DepthNormals
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment

            #pragma multi_compile_instancing

            #pragma shader_feature_local _PLANAR_ON
            #pragma shader_feature_local _UVSCROLL_ON
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local _DISSOLVE_ON
            #pragma shader_feature_local _DISSOLVE_LOCAL

            #include "SoftToonyDepthPasses.hlsl"
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
