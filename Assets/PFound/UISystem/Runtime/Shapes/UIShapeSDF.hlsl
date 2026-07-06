#ifndef PFOUND_UISYSTEM_SHAPES_SDF_INCLUDED
#define PFOUND_UISYSTEM_SHAPES_SDF_INCLUDED

// ─────────────────────────────────────────────────────────────────────
// UIShapeSDF.hlsl
// Signed-distance-field helpers for the UISystem/Shape shader.
//
// Conventions:
//   - SDF < 0 inside the shape, SDF > 0 outside, SDF == 0 on the edge.
//   - All shape SDFs operate in centered rect-space:
//       uv in [-halfSize, +halfSize], halfSize = _RectSize.xy * 0.5.
//   - Distances are in PIXELS (matches RectTransform px space). AA helper
//     uses fwidth so screen-space coverage is correct regardless of scale.
// ─────────────────────────────────────────────────────────────────────

// Screen-space derivative-based anti-aliasing.
// Returns a 0..1 coverage factor for the edge band around sdf == 0.
// Independent of MSAA, Canvas scale, or RectTransform size.
// `_AAWidth` (default 1.0) scales the AA band — 1.0 = 1-pixel crisp band,
// 1.5–2.0 = softer edge (less stepping when displayed at non-native scale).
float UIShape_SDFEdgeAA(float sdf)
{
    float widthScale = max(_AAWidth, 1e-3);
    float aaWidth = max(fwidth(sdf) * widthScale, 1e-5);
    return saturate(0.5 - sdf / aaWidth);
}

// ─── Shape SDFs ──────────────────────────────────────────────────────

// Axis-aligned rectangle. uv is in centered rect-space; halfSize is _RectSize.xy * 0.5.
// Standard box SDF (Inigo Quilez): outside-distance + inside-correction.
float UIShape_SDFRect(float2 uv, float2 halfSize)
{
    float2 d = abs(uv) - halfSize;
    return length(max(d, 0.0)) + min(max(d.x, d.y), 0.0);
}

// Rounded-rect with independent per-corner radii.
// radii order matches CornerRadii Vector4: (top-left, top-right, bottom-right, bottom-left).
// Quadrant selection (UV-space: +x right, +y up):
//   uv.x < 0, uv.y > 0  ->  top-left      (radii.x)
//   uv.x > 0, uv.y > 0  ->  top-right     (radii.y)
//   uv.x > 0, uv.y < 0  ->  bottom-right  (radii.z)
//   uv.x < 0, uv.y < 0  ->  bottom-left   (radii.w)
// Each radius is clamped to half the smaller rect dimension.
float UIShape_SDFRoundedRect(float2 uv, float2 halfSize, float4 radii)
{
    float maxR = min(halfSize.x, halfSize.y);
    radii = clamp(radii, 0.0, maxR);

    float r;
    if (uv.y > 0.0)
    {
        r = (uv.x < 0.0) ? radii.x : radii.y;
    }
    else
    {
        r = (uv.x > 0.0) ? radii.z : radii.w;
    }

    float2 d = abs(uv) - halfSize + r;
    return length(max(d, 0.0)) + min(max(d.x, d.y), 0.0) - r;
}

// Capsule (pill / stadium) inscribed in the RectTransform.
// Oriented along the LONGER axis; radius is half the shorter axis.
// halfSize is _RectSize.xy * 0.5.
float UIShape_SDFCapsule(float2 uv, float2 halfSize)
{
    if (halfSize.x >= halfSize.y)
    {
        // Horizontal pill: line segment from (-halfSize.x + r, 0) to (+halfSize.x - r, 0), radius r.
        float r = halfSize.y;
        float segHalf = max(halfSize.x - r, 0.0);
        float2 p = float2(uv.x - clamp(uv.x, -segHalf, segHalf), uv.y);
        return length(p) - r;
    }
    else
    {
        // Vertical pill.
        float r = halfSize.x;
        float segHalf = max(halfSize.y - r, 0.0);
        float2 p = float2(uv.x, uv.y - clamp(uv.y, -segHalf, segHalf));
        return length(p) - r;
    }
}

// Ellipse SDF — Inigo Quilez exact ellipse distance function.
// Iterative Newton-style solve; one of the few "exact" SDFs in common use.
// Returns negative inside, positive outside. halfSize is _RectSize.xy * 0.5.
//
// Degenerate case: when ab.x == ab.y (square aspect → circle), the formula's
// `l = ab.y² - ab.x²` becomes zero and downstream divisions by `l` produce NaN.
// Branch out to the exact circle SDF; the branch is uniform per draw call because
// ab comes from a material property, so there is no GPU divergence cost.
float UIShape_SDFEllipse(float2 uv, float2 ab)
{
    if (abs(ab.x - ab.y) < 1e-4)
    {
        return length(uv) - ab.x;
    }

    float2 p = abs(uv);
    bool flip = false;
    if (p.x > p.y)
    {
        p = p.yx;
        ab = ab.yx;
        flip = true;
    }
    float l = ab.y * ab.y - ab.x * ab.x;
    float m = ab.x * p.x / l;
    float m2 = m * m;
    float n = ab.y * p.y / l;
    float n2 = n * n;
    float c = (m2 + n2 - 1.0) / 3.0;
    float c3 = c * c * c;
    float q = c3 + m2 * n2 * 2.0;
    float d = c3 + m2 * n2;
    float g = m + m * n2;
    float co;
    if (d < 0.0)
    {
        float h = acos(q / c3) / 3.0;
        float s = cos(h);
        float t = sin(h) * sqrt(3.0);
        float rx = sqrt(-c * (s + t + 2.0) + m2);
        float ry = sqrt(-c * (s - t + 2.0) + m2);
        co = (ry + sign(l) * rx + abs(g) / (rx * ry) - m) / 2.0;
    }
    else
    {
        float h = 2.0 * m * n * sqrt(d);
        float s = sign(q + h) * pow(abs(q + h), 1.0 / 3.0);
        float t = sign(q - h) * pow(abs(q - h), 1.0 / 3.0);
        float rx = -s - t - c * 4.0 + 2.0 * m2;
        float ry = (s - t) * sqrt(3.0);
        float rm = sqrt(rx * rx + ry * ry);
        co = (ry / sqrt(rm - rx) + 2.0 * g / rm - m) / 2.0;
    }
    float2 r = ab * float2(co, sqrt(1.0 - co * co));
    float dist = length(r - p) * sign(p.y - r.y);
    return flip ? dist : dist;
}

// ─── Shape-keyword dispatch ──────────────────────────────────────────

float UIShape_EvaluateSDF(float2 uv)
{
    float2 halfSize = _RectSize.xy * 0.5;

#if defined(SHAPE_TYPE_RECT)
    return UIShape_SDFRect(uv, halfSize);
#elif defined(SHAPE_TYPE_CAPSULE)
    return UIShape_SDFCapsule(uv, halfSize);
#elif defined(SHAPE_TYPE_ELLIPSE)
    return UIShape_SDFEllipse(uv, halfSize);
#else
    // Default + SHAPE_TYPE_ROUNDEDRECT: per-corner rounded rect.
    return UIShape_SDFRoundedRect(uv, halfSize, _CornerRadii);
#endif
}

#endif // PFOUND_UISYSTEM_SHAPES_SDF_INCLUDED
