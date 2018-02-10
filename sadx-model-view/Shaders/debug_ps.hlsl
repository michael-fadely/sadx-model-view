
float4 main(in float4 position : SV_POSITION, in float4 color : COLOR) : SV_TARGET
{
	return float4(color.rgb, 1);
}
