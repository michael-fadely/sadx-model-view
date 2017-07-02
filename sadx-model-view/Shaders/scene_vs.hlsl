#include "common.hlsli"

cbuffer MatrixBuffer : register(b0)
{
	matrix worldMatrix;
	matrix viewMatrix;
	matrix projectionMatrix;
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
	result.position = mul(result.position, worldMatrix);
	result.position = mul(result.position, viewMatrix);
	result.position = mul(result.position, projectionMatrix);
	
	result.normal = input.normal;
	result.color = input.color;
	result.tex = input.tex;

	return result;
}
