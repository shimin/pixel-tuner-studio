sampler2D implicitInput : register(s0) = sampler_state {
	MinFilter = POINT;
	MagFilter = POINT;
	MipFilter = POINT;
	AddressU = CLAMP;
	AddressV = CLAMP;
};

float tilingX : register(c0);
float dot : register(c1);
float dash : register(c2);

float4 main(float2 uv : TEXCOORD) : COLOR {
	float2 uv2 = uv * 2 - 1;
	float2 uv3 = float2(uv2.x * -1, uv2.y * -1);

	float r = sqrt(pow(uv2.x, 2) + pow(uv2.y, 2));
	float theta = atan2(uv2.y, uv2.x);

	float r2 = sqrt(pow(uv3.x, 2) + pow(uv3.y, 2));
	float theta2 = atan2(uv3.y, uv3.x);

	float2 texCoords = float2(theta * tilingX % 1.05, r);
		float2 texCoords2 = float2(theta2 * tilingX % 1.05, r);

		float4 color = tex2D(implicitInput, texCoords);
		float4 colorRev = tex2D(implicitInput, texCoords2);

	return color * (theta % dot > dash) * (r % dot > dash) * uv.x * 0.5 * 2 + 
	colorRev * (theta2 % dot > dash) * (r2 % dot > dash) * clamp(0.5,1,1-uv3.x) * 2; 
}