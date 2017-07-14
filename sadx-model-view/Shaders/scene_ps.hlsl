#include "common.hlsli"

Texture2D<float4> DiffuseMap : register(t0);

SamplerState DiffuseSampler
{
	Filter = MIN_MAG_MIP_LINEAR;
	AddressU = Wrap;
	AddressV = Wrap;
};

float4 main(VS_OUTPUT input) : SV_TARGET
{
	float4 result;

	if (material.useTexture)
	{
		result = DiffuseMap.Sample(DiffuseSampler, input.tex);
	}
	else
	{
		result = float4(1, 1, 1, 1);
	}

	result = result * input.diffuse + input.specular;

	if (result.a < 16.0f / 255.0f)
	{
		discard;
	}

	return result;
}
