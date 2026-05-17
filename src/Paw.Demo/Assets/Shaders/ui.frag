#version 330 core

in vec4 fColor;
in vec2 fUV;
in vec2 fLocalPos;
in vec2 fHalfSize;
in float fCornerRadius;
in float fBorderThickness;

out vec4 FragColor;

uniform sampler2D uTex;

// p: fragment position in the rectangle’s local space, usually centered at (0, 0)
// halfSize: half of the rectangle size
// width = halfSize.x * 2
// height = halfSize.y * 2
// radius: corner radius
float sdRoundRect(vec2 p, vec2 halfSize, float radius)
{
    // Move the point into one quadrant and measure against the inner box where corners begin.
    // q tells you how far the point is from that inner-corner region.
    vec2 q = abs(p) - (halfSize - vec2(radius));

    return length(max(q, 0.0))      // If the point is beyond that box in both axes, compute corner distance.
        + min(max(q.x, q.y), 0.0)   // If the point is still inside, keep a negative distance for the flat-edge region.
        - radius;                   // Turn that into distance from the rounded boundary.
}
//The return value is:
// negative: point is inside the shape
// zero: point is on the edge
// positive: point is outside the shape

// Computes perceived brightness of a color.
// The weights match human vision: green contributes most, then red, then blue.
float luminance(vec3 c)
{
    return dot(c, vec3(0.299, 0.587, 0.114));
}

// Derives a border color from the fill color.
// For bright fills, the border is darkened.
// For dark fills, the border is lightened.
// 'strength' controls how strong the border contrast should be.
vec4 deriveBorderColor(vec4 fill, float strength)
{
    // Estimate how bright the fill color appears to the eye.
    float l = luminance(fill.rgb);

    // Create a darker version by scaling the RGB channels down.
    vec3 darker = fill.rgb * (1.0 - strength);

    // Create a lighter version by blending toward white.
    vec3 lighter = mix(fill.rgb, vec3(1.0), strength);

    // If the fill is bright enough, use the darker border.
    // Otherwise use the lighter border to keep the outline visible.
    float useDarker = step(0.6, l);
    vec3 rgb = mix(lighter, darker, useDarker);

    // Keep the original alpha so border opacity follows the fill color setup.
    return vec4(rgb, fill.a);
}

void main()
{
    float borderThickness = fBorderThickness;
    vec4 borderColor = deriveBorderColor(fColor, 0.25);

    vec2 outerHalfSize = fHalfSize;
    vec2 innerHalfSize = max(fHalfSize - vec2(borderThickness), vec2(0.0));

    float outerRadius = min(fCornerRadius, min(fHalfSize.x, fHalfSize.y));
    float innerRadius = min(max(outerRadius - borderThickness, 0.0), min(innerHalfSize.x, innerHalfSize.y));

    // step(edge, x) function: return 0.0 if x < edge, else 1.0
    // fwidth function: return the sum of the absolute value of derivatives in x and y
    // smoothstep(float edge0, float edge1, genType x): Hermite interpolation between 0 and 1 when edge0 < x < edge1.

    float dOuter = sdRoundRect(fLocalPos, outerHalfSize, outerRadius);
    float dInner = sdRoundRect(fLocalPos, innerHalfSize, innerRadius);

    float outerEdge = max(fwidth(dOuter) * 0.5, 0.0001);
    float innerEdge = max(fwidth(dInner) * 0.5, 0.0001);

    float outerMask = 1.0 - smoothstep(-outerEdge, outerEdge, dOuter);
    float fillMask = 1.0 - smoothstep(-innerEdge, innerEdge, dInner);

    //float aa = max(fwidth(dOuter) * 0.5, 0.0001);
    //float aa = 1;

    //float outerMask = 1.0 - smoothstep(-aa, aa, dOuter);
    //float fillMask = 1.0 - smoothstep(-aa, aa, dOuter + borderThickness);

    float borderAlpha = max(outerMask - fillMask, 0.0);
    float fillAlpha = fillMask;

    //float d = sdRoundRect(fLocalPos, fHalfSize, radius);
    // d < 0 → fragment is inside the rounded rectangle
    // d == 0 → exactly on the border
    // d > 0 → outside the rounded rectangle

    //float edge = fwidth(d);
    //float edge = fwidth(d) * 0.75;
    //float edge = fwidth(d) * 0.5;
    //float edge = max(fwidth(d) * 0.5, 0.0001);

    //float shapeAlpha = 1.0 - smoothstep(0.0, edge, d);
    //float shapeAlpha = 1.0 - smoothstep(-edge, edge, d);
    // inside > ~1
    // outside > ~0

    float no_tex = step(1, fUV.x) * step(1, fUV.y);
    float use_tex = 1.0 - no_tex;

    float t = texture(uTex, fUV).r;
    float a = use_tex * t + no_tex * fillAlpha;

    FragColor = fColor * vec4(1,1,1,t) * use_tex
              + fColor * vec4(1,1,1,1) * fillAlpha * no_tex
              + borderColor * vec4(1,1,1,1) * borderAlpha * no_tex
              ;
}