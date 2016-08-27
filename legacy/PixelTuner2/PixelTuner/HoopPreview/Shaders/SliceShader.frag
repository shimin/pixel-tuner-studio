#version 130

uniform sampler2D sliceTexture;

uniform float slicePosition = 0.01;

out vec4 out_Color;

in vec2 texcoordPs;
in vec4 positionPs;

void main() {
	out_Color = texture(sliceTexture, 
		vec2(slicePosition, texcoordPs.x));
}
