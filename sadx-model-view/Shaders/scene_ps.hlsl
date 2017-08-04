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
	const float THRESHOLD = 0.9;

	if (material.useAlpha)
	{
		if (writeDepth == true)
		{
			if (result.a >= THRESHOLD)
			{
				result.a = 1.0;
			}
			else
			{
				discard;
			}
		}
		else if (result.a >= THRESHOLD)
		{
			discard;
		}
	}

	return result;
}
