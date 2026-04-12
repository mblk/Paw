#version 330 core

in vec4 fColor;
in vec2 fUV;

out vec4 FragColor;

//uniform vec2 uClipMin;
//uniform vec2 uClipMax;
uniform sampler2D uTex;
//uniform int uPass;

void main()
{
    //vec2 p = gl_FragCoord.xy; // pixel coordinates, origin at bottom left

    // step(edge, x) function: return 0.0 if x < edge, else 1.0

    // clipping
//    float a = step(uClipMin.x, p.x) *
//              step(uClipMin.y, p.y) *
//              step(p.x, uClipMax.x) *
//              step(p.y, uClipMax.y);

    float no_tex = step(0.999, fUV.x) * step(0.999, fUV.y);
    float use_tex = 1.0 - no_tex;

    float f = texture(uTex, fUV).r;
    float a = use_tex * f + no_tex;

    FragColor = fColor * vec4(1,1,1,a);
}