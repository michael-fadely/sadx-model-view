#include "common.hlsli"

Texture2D<float4> DiffuseMap : register(t0);

SamplerState DiffuseSampler
{
	Filter = MIN_MAG_MIP_LINEAR;
	AddressU = Wrap;
	AddressV = Wrap;
};

float4 main(VS_OUTPUT input, out float depth : SV_Depth) : SV_TARGET
{
	depth = input.depth.x / input.depth.y;
	float4 diffuse = DiffuseMap.Sample(DiffuseSampler, input.tex);
	return diffuse;
}
