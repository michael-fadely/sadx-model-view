#include "common.hlsli"

cbuffer MatrixBuffer : register(b0)
{
	matrix wvpMatrix;
	matrix wvMatrixInvT;
	matrix textureTransform;
};

cbuffer MaterialBuffer : register(b1)
{
	Material material;
};

VS_OUTPUT main(VS_INPUT input)
{
	VS_OUTPUT result;

	result.position = float4(input.position, 1.0f);
	result.position = mul(result.position, wvpMatrix);

	result.normal = input.normal;
	result.color = input.color;

	if (material.useEnv)
	{
		result.tex = (float2)mul(float4(input.normal, 1), wvMatrixInvT);
		result.tex = (float2)mul(float4(result.tex, 0, 1), textureTransform);
	}
	else
	{
		result.tex = input.tex;
	}

	return result;
}
