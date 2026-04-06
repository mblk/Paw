#version 330 core

in vec4 fColor;
in vec2 fUV;

uniform vec2 uClipMin;
uniform vec2 uClipMax;

out vec4 FragColor;

void main()
{
    vec2 p = gl_FragCoord.xy; // pixel coordinates, origin at bottom left

//    float a = 1.0;
//
//    if (p.x < uClipMin.x ||
//        p.y < uClipMin.y ||
//        p.x > uClipMax.x ||
//        p.y > uClipMax.y)
//    {
//        a = 0.0;
//    }

    // step(edge, x) function: return 0.0 if x < edge, else 1.0

    float a = step(uClipMin.x, p.x) *
              step(uClipMin.y, p.y) *
              step(p.x, uClipMax.x) *
              step(p.y, uClipMax.y);

    FragColor = vec4(fColor.xyz, fColor.a * a);
}
