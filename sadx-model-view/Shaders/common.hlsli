struct Material
{
	float4 diffuse;
	float4 specular;
	float  exponent;
	bool   useLight;
	bool   useAlpha;
	bool   useEnv;
	bool   useTexture;
	bool   useSpecular;
};

cbuffer PerSceneBuffer : register(b0)
{
	matrix viewMatrix;
	matrix projectionMatrix;
	float3 cameraPos;
};
