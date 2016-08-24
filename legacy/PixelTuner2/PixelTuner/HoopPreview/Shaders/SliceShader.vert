#version 130

in vec4 position;
in vec2 texcoord;

out vec2 texcoordPs;
out vec4 positionPs;

void main() {
	gl_Position = position;
	
	texcoordPs = texcoord;
	positionPs = position;
}
