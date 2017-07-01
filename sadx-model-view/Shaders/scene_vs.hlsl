#include "common.hlsli"

cbuffer MatrixBuffer : register(b0)
{
	matrix worldMatrix;
	matrix viewMatrix;
	matrix projectionMatrix;
}

VS_OUTPUT main(VS_INPUT input)
{
	VS_OUTPUT result;

	result.position = float4(input.position, 1.0f);
	result.position = mul(result.position, worldMatrix);
	result.position = mul(result.position, viewMatrix);
	result.position = mul(result.position, projectionMatrix);
	
	output.normal = input.normal;
	output.color = input.color;
	output.tex = input.tex;

	return result;
}
