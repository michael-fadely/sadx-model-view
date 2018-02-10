#include "common.hlsli"

void main(in float3 position : POSITION, in float4 color : COLOR,
				out float4 o_position : SV_POSITION, out float4 o_color : COLOR)
{
	o_position = mul(viewMatrix, float4(position, 1));
	o_position = mul(projectionMatrix, o_position);
	o_color    = color;
}
