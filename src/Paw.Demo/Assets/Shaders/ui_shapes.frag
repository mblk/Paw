#version 330 core

in vec3 fColor;
in vec2 fUV;

uniform vec2 uClipMin;
uniform vec2 uClipMax;

out vec4 FragColor;

void main()
{
    vec2 p = gl_FragCoord.xy; // pixel coordinates, origin at bottom left

    if (p.x < uClipMin.x ||
        p.y < uClipMin.y ||
        p.x > uClipMax.x ||
        p.y > uClipMax.y)
    {
        discard; // TODO discard bad
    }

    FragColor = vec4(fColor, 1.0);
}
