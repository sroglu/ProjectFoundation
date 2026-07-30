#ifndef PFOUND_RENDER_MATH_INCLUDED
#define PFOUND_RENDER_MATH_INCLUDED

// -----------------------------------------------------------------------------
// PFound.Render.Core — Math.hlsl
//
// Pow/lerp/smoothstep/gamma helpers. Designed to be safe to include alongside
// URP's Core HLSL library (`UNITY_*` macros) without symbol collisions.
// -----------------------------------------------------------------------------

float MRender_Pow2(float x) { return x * x; }
float MRender_Pow3(float x) { return x * x * x; }
float MRender_Pow4(float x) { float x2 = x * x; return x2 * x2; }

// Assumes simple-gamma (2.2) approximation — for the *correct* sRGB curve use
// UnityEngine.ColorUtility / URP's `LinearToSRGB` in `Color.hlsl` instead.
float MRender_LinearToGamma(float linearValue) { return pow(max(linearValue, 0.0), 1.0 / 2.2); }
float MRender_GammaToLinear(float gammaValue)  { return pow(max(gammaValue,  0.0), 2.2);       }

#endif // PFOUND_RENDER_MATH_INCLUDED
