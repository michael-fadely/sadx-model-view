#include "common.hlsli"

Texture2D<float4> DiffuseTexture : register(t0);

float4 main(VS_OUTPUT input) : SV_TARGET
{
	return input.color;
}
