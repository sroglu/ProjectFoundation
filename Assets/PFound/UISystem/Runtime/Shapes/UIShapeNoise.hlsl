#ifndef PFOUND_UISYSTEM_SHAPES_NOISE_INCLUDED
#define PFOUND_UISYSTEM_SHAPES_NOISE_INCLUDED

// ─────────────────────────────────────────────────────────────────────
// UIShapeNoise.hlsl
// Clean-room noise library for the UISystem/Shape shader.
// Implementations are original to this codebase — derived from public
// algorithmic descriptions (Perlin '02 simplex-style gradient hashing,
// Worley '96 cellular F1 distance).
//
// All functions return values normalized to [0, 1].
// ─────────────────────────────────────────────────────────────────────

// Deterministic hash producing a pseudo-random 2D vector in [-1, 1]² from
// an integer-ish lattice coordinate. Uses two large irrational-ish primes
// and a fract(sin()) trick — fast, branchless, deterministic per-pixel.
float2 UIShape_Hash22(float2 p)
{
    p = float2(dot(p, float2(127.1, 311.7)),
               dot(p, float2(269.5, 183.3)));
    return -1.0 + 2.0 * frac(sin(p) * 43758.5453123);
}

// Quintic smoothstep: 6t^5 - 15t^4 + 10t^3. C2-continuous at integer cell
// boundaries — eliminates the visible grid artefacts that show with linear
// or cubic interpolation. Standard choice for gradient noise.
float2 UIShape_Fade22(float2 t)
{
    return t * t * t * (t * (t * 6.0 - 15.0) + 10.0);
}

// 2D gradient noise in the Perlin style.
// At each integer lattice corner: hash to a unit-ish gradient, dot with
// offset from corner to sample point, interpolate quintic-smoothly across
// the cell. Output normalized to [0, 1].
float UIShape_PerlinNoise2D(float2 p)
{
    float2 pi = floor(p);
    float2 pf = p - pi;

    float2 g00 = UIShape_Hash22(pi + float2(0.0, 0.0));
    float2 g10 = UIShape_Hash22(pi + float2(1.0, 0.0));
    float2 g01 = UIShape_Hash22(pi + float2(0.0, 1.0));
    float2 g11 = UIShape_Hash22(pi + float2(1.0, 1.0));

    float d00 = dot(g00, pf - float2(0.0, 0.0));
    float d10 = dot(g10, pf - float2(1.0, 0.0));
    float d01 = dot(g01, pf - float2(0.0, 1.0));
    float d11 = dot(g11, pf - float2(1.0, 1.0));

    float2 fade = UIShape_Fade22(pf);
    float nx0 = lerp(d00, d10, fade.x);
    float nx1 = lerp(d01, d11, fade.x);
    float n   = lerp(nx0, nx1, fade.y);

    // Perlin output spans roughly [-0.7, 0.7]; remap to [0, 1].
    return saturate(n * 0.7142857 + 0.5);
}

// Worley (cellular) noise — F1 distance, 3×3 neighbor lattice scan.
// At each integer lattice cell, hash a feature point inside the cell;
// the noise value is the distance to the nearest such point.
// Output normalized so flat regions are near 0 and cell-boundary peaks near 1.
float UIShape_WorleyNoise2D(float2 p)
{
    float2 pi = floor(p);
    float2 pf = p - pi;

    float minDist = 1.5;
    [unroll]
    for (int j = -1; j <= 1; j++)
    {
        [unroll]
        for (int i = -1; i <= 1; i++)
        {
            float2 neighbor = float2(i, j);
            // Random feature point inside the neighbor cell in [0, 1]^2.
            float2 featurePoint = neighbor + 0.5 + 0.5 * UIShape_Hash22(pi + neighbor);
            float d = length(featurePoint - pf);
            minDist = min(minDist, d);
        }
    }
    return saturate(minDist);
}

#endif // PFOUND_UISYSTEM_SHAPES_NOISE_INCLUDED
