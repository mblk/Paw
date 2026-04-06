#version 330 core

in vec4 fColor;
in vec2 fUV;

out vec4 FragColor;

uniform vec2 uClipMin;
uniform vec2 uClipMax;
uniform sampler2D uTex;
uniform int uPass;

void main()
{
    vec2 p = gl_FragCoord.xy; // pixel coordinates, origin at bottom left

    // step(edge, x) function: return 0.0 if x < edge, else 1.0

    // clipping
    float a = step(uClipMin.x, p.x) *
              step(uClipMin.y, p.y) *
              step(p.x, uClipMax.x) *
              step(p.y, uClipMax.y);

    FragColor = vec4(fColor.xyz, a);

    // font check
    vec4 data = texture(uTex, fUV);

    // from BMFont:
    // tex.a: outline
    // tex.r: fill
    a *= data.r;

//    vec4 color = vec4(0,0,0,0);
//
//    color += vec4(0, 0, 0, outlineA) * pass1;
//    color += vec4(1, 1, 1, fillA) * pass2;
//
//    if (color.a < 0.5) {
//        discard; // TODO discard bad
//    }
    
    // result
    FragColor = vec4(fColor.xzy, fColor.a * a);
}