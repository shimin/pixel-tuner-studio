#version 130

in vec2 texcoord;
in vec4 position;

out vec2 texcoordPs;
out vec4 positionPs;

void main() {
	gl_Position = position;
	
	texcoordPs = texcoord;
	positionPs = position;
}
