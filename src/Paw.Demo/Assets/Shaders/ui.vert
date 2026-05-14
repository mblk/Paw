#version 330 core

layout (location=0) in vec2 vPos;
layout (location=1) in vec4 vColor;
layout (location=2) in vec2 vUV;
layout (location=3) in vec2 vLocalPos;
layout (location=4) in vec2 vHalfSize;
layout (location=5) in float vCornerRadius;

uniform mat4 uMVP;

out vec4 fColor;
out vec2 fUV;
out vec2 fLocalPos;
out vec2 fHalfSize;
out float fCornerRadius;

void main()
{
    gl_Position = uMVP * vec4(vPos, 0.0, 1.0);
    fColor = vColor;
    fUV = vUV;
    fLocalPos = vLocalPos;
    fHalfSize = vHalfSize;
    fCornerRadius = vCornerRadius;
}
