#version 130

#define PI 3.14159265f
#define DBLPI 6.2831853f

uniform sampler2D sliceTexture;

in vec2 texcoordPs;
in vec4 positionPs;

out vec4 out_Color;

uniform float circleRadius = 0.4;
uniform float circleWidth = 0.01;

uniform float rotationRadius = 0.25;
uniform float arcPosition = 0;

uniform float sliceShift = 0;

void main() {
	float circleCenterX = rotationRadius * cos(arcPosition * PI / 180.0);
	float circleCenterY = rotationRadius * sin(arcPosition * PI / 180.0);

	vec2 uv = texcoordPs;
	vec2 uv2 = vec2(uv.x + circleCenterX + 0.035, uv.y + circleCenterY) * 2 - 1;

	float r = sqrt(pow(uv2.x, 2) + pow(uv2.y, 2));
	float theta = atan(uv2.y, uv2.x);

	out_Color = 
		(r < circleRadius) && (r > circleRadius - circleWidth) ?
			texture(sliceTexture, vec2(r, theta * 0.25 + sliceShift)) : vec4(0,0,0,1);	
}
