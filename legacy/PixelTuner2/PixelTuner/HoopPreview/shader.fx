#define PI 3.14159265f
#define DBLPI 6.2831853f

SamplerState g_SamplerLinear
{
	Filter = MIN_MAG_MIP_LINEAR;
	AddressU = WRAP;
	AddressV = WRAP;
};

BlendState SrcAlphaBlendingAdd
{
	BlendEnable[0] = TRUE;
	SrcBlend = ONE;
	DestBlend = ONE;
	BlendOp = MAX;
	SrcBlendAlpha = SRC_ALPHA;
	DestBlendAlpha = INV_SRC_ALPHA;
	BlendOpAlpha = ADD;
	RenderTargetWriteMask[0] = 0x0F;
};

BlendState SrcAlphaBlendingSubstract
{
	BlendEnable[0] = TRUE;
	SrcBlend = ONE;
	DestBlend = ONE;
	BlendOp = REV_SUBTRACT;
	SrcBlendAlpha = SRC_ALPHA;
	DestBlendAlpha = INV_SRC_ALPHA;
	BlendOpAlpha = ADD;
	RenderTargetWriteMask[0] = 0x0F;
};

Texture2D g_ContentTexture;

float g_Slice = 0.01;

float circleRadius = 0.4;
float circleWidth = 0.01;

float rotationRadius = 0.25;
float arcPosition = 0;

float g_alphaStep = 0.005;

Texture2D g_FillRenderTexture;

struct VS_IN
{
	float4 pos : POSITION;
	float2 tex : TEXCOORD0;
};

struct PS_IN
{
	float4 pos : SV_POSITION;
	float2 tex : TEXCOORD0;
};

PS_IN FillTextureVS(VS_IN input)
{
	PS_IN output = (PS_IN)0;
	
	output.pos = input.pos;
	output.tex = input.tex;
	
	return output;
}

float4 FillTexturePS(PS_IN input) : SV_Target
{
	float2 uv = float2(g_Slice, input.tex.y);
	return g_ContentTexture.Sample(g_SamplerLinear, uv);
}

PS_IN WarpTextureVS(VS_IN input) {
	PS_IN output = (PS_IN)0;

	output.pos = input.pos;
	output.tex = input.tex;

	return output;
}

float4 WarpTexturePS(PS_IN input) : SV_Target{
	float circleCenterX = rotationRadius * cos(arcPosition * PI / 180.0);
	float circleCenterY = rotationRadius * sin(arcPosition * PI / 180.0);

	float2 uv = input.tex;
	float2 uv2 = float2(uv.x + circleCenterX, uv.y + circleCenterY) * 2 - 1;

	float r = sqrt(pow(uv2.x, 2) + pow(uv2.y, 2));
	float theta = atan2(uv2.y, uv2.x);

	return
		(r < circleRadius) * (r > circleRadius - circleWidth) ? 
			g_FillRenderTexture.Sample(g_SamplerLinear, float2(r, theta * 0.25)) : float4(0, 0, 0, 1);
			
}


PS_IN WarpSubstracVS(VS_IN input) {
	PS_IN output = (PS_IN)0;

	output.pos = input.pos;
	output.tex = input.tex;

	return output;
}


float4 WarpTextureSubstractPS(PS_IN input) : SV_Target
{
	return float4(g_alphaStep, g_alphaStep, g_alphaStep, g_alphaStep);
}

technique10 FillTexture
{
	pass P0
	{
		SetGeometryShader( 0 );
		SetVertexShader( CompileShader( vs_4_0, FillTextureVS() ) );
		SetPixelShader(CompileShader(ps_4_0, FillTexturePS()));
	}
}

technique10 WarpRender {

	pass P0 {
		SetGeometryShader(0);
		SetVertexShader(CompileShader(vs_4_0, WarpSubstracVS()));
		SetPixelShader(CompileShader(ps_4_0, WarpTextureSubstractPS()));
		SetBlendState(SrcAlphaBlendingSubstract, float4(0.0f, 0.0f, 0.0f, 0.0f), 0xFFFFFFFF);
	}

	pass P1
	{
		SetGeometryShader(0);
		SetVertexShader(CompileShader(vs_4_0, WarpTextureVS()));
		SetPixelShader(CompileShader(ps_4_0, WarpTexturePS()));
		SetBlendState(SrcAlphaBlendingAdd, float4(0.0f, 0.0f, 0.0f, 0.0f), 0xFFFFFFFF);
	}

	
};