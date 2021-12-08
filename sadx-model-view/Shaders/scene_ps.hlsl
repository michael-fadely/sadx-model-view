#include "scene_common.hlsli"

#ifdef RS_OIT
#define OIT_NODE_WRITE
#include "oit.hlsli"
#endif

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

	if (material.useAlpha)
	{
	#if RS_OIT == 1
		do_oit(result, input, isStandardBlending);
	#else
		float alpha = floor(result.a * 256.0f);

		if (writeDepth == true)
		{
			clip(isStandardBlending && alpha < 255 ? -1 : 1);
		}
	#endif
	}

	return result;
}
