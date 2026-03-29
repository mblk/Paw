#version 330 core

in vec3 fColor;
in vec2 fUV;

out vec4 FragColor;

uniform sampler2D uTex;
uniform int uPass;

void main()
{
    float pass1 = uPass == 1 ? 1.0f : 0.0f; // TODO check if this is producing branches
    float pass2 = uPass == 2 ? 1.0f : 0.0f;

    vec4 data = texture(uTex, fUV);

    float outlineA = data.a;
    float fillA = data.r;

    vec4 color = vec4(0,0,0,0);

    color += vec4(0, 0, 0, outlineA) * pass1;
    color += vec4(1, 1, 1, fillA) * pass2;

    if (color.a < 0.5) { // TODO do without branching somehow
        discard;
    }
    
    FragColor = color;
}