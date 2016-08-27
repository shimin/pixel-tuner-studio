#version 130

in vec2 texcoordPs;
in vec4 positionPs;

out vec4 out_Color;

uniform float g_alphaStep = 0.005;

void main() {
	out_Color = vec4(g_alphaStep, g_alphaStep, g_alphaStep, g_alphaStep);
}
