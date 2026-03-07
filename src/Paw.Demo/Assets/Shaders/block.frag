#version 330 core

in vec3 fColor;

out vec4 FragColor;

void main()
{
    FragColor = vec4(fColor, 1.0);
    //FragColor = vec4(0.95, 0.4, 0.2, 1.0);
}


// qws qwstt1