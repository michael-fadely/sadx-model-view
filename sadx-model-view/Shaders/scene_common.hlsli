#ifndef SCENE_COMMON_HLSLI
#define SCENE_COMMON_HLSLI

#include "common.hlsli"

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
	float2 tex      : TEXCOORD;
	float4 diffuse  : COLOR0;
	float4 specular : COLOR1;
	float2 depth    : DEPTH;
};

static const matrix textureTransform = {
	-0.5f, 0.0f, 0.0f, 0.0f,
	0.0f, 0.5f, 0.0f, 0.0f,
	0.0f, 0.0f, 1.0f, 0.0f,
	0.5f, 0.5f, 0.0f, 1.0f
};

cbuffer MaterialBuffer : register(b1)
{
	Material material;
	bool     writeDepth;
};

cbuffer PerModelBuffer : register(b2)
{
	matrix worldMatrix;
	matrix wvMatrixInvT;
	uint   drawCall;
	uint   sourceBlend;
	uint   destinationBlend;
	uint   blendOp;
	bool   isStandardBlending;
};

#endif
