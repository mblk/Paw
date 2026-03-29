#version 330 core

layout (location=0) in vec2 vPos;
layout (location=1) in vec3 vColor;
layout (location=2) in vec2 vUV;

uniform mat4 uMVP;

out vec3 fColor;
out vec2 fUV;

void main()
{
    gl_Position = uMVP * vec4(vPos, 0.0, 1.0);
    fColor = vColor;
    fUV = vUV;
}
