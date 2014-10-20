#define PI 3.14159265f
#define DBLPI 6.2831853f

sampler2D mainTexture : register(s0) = sampler_state {
	AddressU = REPEAT;
	AddressV = REPEAT;
	AddressW = REPEAT;
};

float tilingX : register(c0);
float dot : register(c1);
float dash : register(c2);
float arcPosition : register(c3);
float arcLength : register(c4);
float minimalIntensity : register(c5);
float doubleLineWidth : register(c6);
float clampValue : register(c7);

float4 main(float2 uv : TEXCOORD) : COLOR {
	float mask = 0;
	float interpolatedIntensity;

	float2 uv2 = float2(uv.x, uv.y) * 2 - 1;
	float2 uv2Inv = float2(uv2.x * -1, uv2.y * -1);

	float r = sqrt(pow(uv2.x, 2) + pow(uv2.y, 2));
	float theta = atan2(uv2Inv.y, uv2Inv.x);

	float2 texCoordsInv = float2((theta + PI) / DBLPI * tilingX % 1, r);
	float2 texFade = float2((theta + PI) / DBLPI, r);

	float _lowThreshold = arcPosition;
	float _highThreshold = arcPosition + arcLength;

	float _doubleLine = _lowThreshold + doubleLineWidth;
	
	interpolatedIntensity = 1 - (((texFade.x - _lowThreshold)) /
		abs(_highThreshold - _lowThreshold));

	interpolatedIntensity = clamp(interpolatedIntensity, minimalIntensity, 1);

	//interpolatedIntensity = 1 - lerp(0.0f, 1.0f, (texFade.x - _lowThreshold) / (_highThreshold - _lowThreshold));
	
	mask = (texFade.x < _highThreshold) * (texFade.x > _lowThreshold) * interpolatedIntensity;

	float _doubleMask = (texFade.x < _doubleLine) * (texFade.x > _lowThreshold) *
		lerp(0.0f, 1.0f, texFade.x / doubleLineWidth) * 10.0f;

	if (_highThreshold > 1.0f) {
		_highThreshold = frac(_highThreshold);

		float lerp1 = 1 - lerp(0.0f, (1.0f - _lowThreshold) / arcLength, (texFade.x - _lowThreshold) / (1.0f - _lowThreshold));
		float lerp2 = 1 - lerp((1.0f - _lowThreshold), 1.0f, texFade.x / _highThreshold);

		mask = ((texFade.x <= _highThreshold) * (texFade.x >= 0.0f)) * lerp2;

		mask += ((texFade.x >= _lowThreshold) * (texFade.x <= 1.0f)) * lerp1;
	}

	float4 colorInv = clamp(tex2D(mainTexture, texCoordsInv), 0.0f, 1.0f) *
			mask;

	colorInv += clamp(colorInv * _doubleMask,0.0f,clampValue);

	return colorInv * (r % dot > dash);
}