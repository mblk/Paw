#version 330 core

in vec3 fColor;
in vec2 fUV;

out vec4 FragColor;

uniform sampler2D uTex;

void main()
{
    FragColor = texture(uTex, fUV) * vec4(fColor, 1.0);

    //FragColor = vec4(fColor, 1.0);
    //FragColor = vec4(0.95, 0.4, 0.2, 1.0);
}