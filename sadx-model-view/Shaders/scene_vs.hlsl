#include "common.hlsli"

cbuffer MatrixBuffer : register(b0)
{
	matrix wvpMatrix;
	matrix wvMatrixInvT;
	matrix worldMatrix;
	matrix textureTransform;
};

VS_OUTPUT main(VS_INPUT input)
{
	VS_OUTPUT result;

	result.position = float4(input.position, 1.0f);
	result.position = mul(result.position, wvpMatrix);

	result.normal = input.normal;

	if (material.useEnv)
	{
		result.tex = (float2)mul(float4(input.normal, 1), wvMatrixInvT);
		result.tex = (float2)mul(float4(result.tex, 0, 1), textureTransform);
	}
	else
	{
		result.tex = input.tex;
	}

	float4 idiffuse = !material.useAlpha ? input.color : material.diffuse;

	if (material.useLight)
	{
		float3 lightDir = normalize(float3(-1, 1, -1));

		float3 worldNormal = mul(input.normal, (float3x3)worldMatrix);
		float _dot = dot(lightDir, worldNormal);
		float3 halfAngle = normalize(normalize(result.position.xyz + cameraPos) + lightDir);

		const float ambient = 0.25f;

		result.diffuse.rgb = max(0, idiffuse * _dot).rgb + float3(ambient, ambient, ambient);
		result.diffuse.a = idiffuse.a;

		if (material.useSpecular)
		{
			result.specular = max(0, pow(max(0, dot(halfAngle, worldNormal)), material.exponent) * material.specular);
		}
		else
		{
			result.specular = float4(0, 0, 0, 0);
		}
	}
	else
	{
		result.diffuse = idiffuse;
		result.specular = float4(0, 0, 0, 0);
	}

	return result;
}
