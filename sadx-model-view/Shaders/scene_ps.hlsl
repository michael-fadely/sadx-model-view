#include "common.hlsli"

Texture2D<float4> DiffuseMap : register(t0);

SamplerState DiffuseSampler
{
	Filter = MIN_MAG_MIP_LINEAR;
	AddressU = Wrap;
	AddressV = Wrap;
};

cbuffer MaterialBuffer : register(b1)
{
	Material material;
};

float4 main(VS_OUTPUT input) : SV_TARGET
{
	float4 result;

	if (material.useAlpha)
	{
		result = material.diffuse;
	}
	else
	{
		result = input.color;
	}

	if (material.useTexture)
	{
		result *= DiffuseMap.Sample(DiffuseSampler, input.tex);
	}
	else if (!material.useAlpha)
	{
		result *= input.color;
	}

	return result;
}
