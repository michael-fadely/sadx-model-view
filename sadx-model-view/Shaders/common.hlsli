struct Material
{
	float4 diffuse;
	float4 specular;
	float exponent;
	bool useLight;
	bool useAlpha;
	bool useEnv;
	bool useTexture;
	bool useSpecular;
};

struct VS_INPUT
{
	float3 position : POSITION;
	float3 normal   : NORMAL;
	float4 color    : COLOR;
	float2 tex      : TEXCOORD;
};

struct VS_OUTPUT
{
	float4 position : SV_POSITION;
	float3 normal   : NORMAL;
	float2 tex      : TEXCOORD;
	float4 diffuse  : COLOR0;
	float4 specular : COLOR1;
};

cbuffer MaterialBuffer : register(b1)
{
	Material material;
	float3 cameraPos;
};
