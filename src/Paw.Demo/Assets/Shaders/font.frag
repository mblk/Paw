#version 330 core

in vec3 fColor;
in vec2 fUV;

out vec4 FragColor;

uniform sampler2D uTex;

uniform float uPass1;
uniform float uPass2;

void main()
{
    vec4 data = texture(uTex, fUV);

    float outlineA = data.a;
    float fillA = data.r;

    vec4 color = vec4(0,0,0,0);

    color += vec4(0, 0, 0, outlineA) * uPass1;
    color += vec4(1, 1, 1, fillA) * uPass2;

    if (color.a < 0.5) {
        discard;
    }
    
    FragColor = color;
}