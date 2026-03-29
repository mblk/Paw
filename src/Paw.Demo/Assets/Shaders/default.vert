#version 330 core

layout(location=0) in vec2 vPos;
layout(location=1) in vec3 vColor;

uniform mat4 uMVP;

out vec3 fColor;

void main()
{
    gl_Position = uMVP * vec4(vPos, 0.0, 1.0);
    fColor = vColor;
}
