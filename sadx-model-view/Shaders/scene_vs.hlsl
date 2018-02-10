#include "scene_common.hlsli"

VS_OUTPUT main(VS_INPUT input)
{
	VS_OUTPUT result;

	result.position = float4(input.position, 1.0f);
	result.position = mul(worldMatrix, result.position);
	
	float3 worldPos = result.position.xyz;

	result.position = mul(viewMatrix, result.position);
	result.position = mul(projectionMatrix, result.position);

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
		float3 lightDir = normalize(-float3(1, -1, 1));

		float3 n = normalize(mul((float3x3)worldMatrix, input.normal));
		float d = saturate(dot(n, lightDir));

		const float ambient = 0.25f;

		result.diffuse.rgb = max(0, idiffuse * d).rgb + ambient;
		result.diffuse.a = idiffuse.a;

		if (material.useSpecular && d >= 0.0)
		{
			float3 h = normalize(normalize(cameraPos - worldPos) + lightDir);
			result.specular = max(0, pow(saturate(dot(h, n)), material.exponent) * material.specular);
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
