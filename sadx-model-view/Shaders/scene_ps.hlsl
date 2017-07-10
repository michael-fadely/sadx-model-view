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
	float4 diffuse = DiffuseMap.Sample(DiffuseSampler, input.tex);
	if (!any(diffuse))
	{
		return input.color * material.diffuse;
	}
	else
	{
		return diffuse * input.color * material.diffuse;
	}
}
