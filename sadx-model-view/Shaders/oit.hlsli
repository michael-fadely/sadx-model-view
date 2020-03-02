#ifndef OIT_HLSLI
#define OIT_HLSLI

#include "scene_common.hlsli"
#include "include.hlsli"

void do_oit(inout float4 result, in VS_OUTPUT input, bool standard_blending)
{
#if RS_OIT == 1
	#if !defined(FVF_RHW)
		// if the pixel is effectively opaque with actual blending,
		// write it directly to the backbuffer as opaque
		if (standard_blending)
		{
			if (result.a > 254.0f / 255.0f)
			{
				return;
			}
		}
	#endif

	#if 0
		uint frag_count;
		InterlockedAdd(FragListCount[input.position.xy], 1, frag_count);

		float f = (float)frag_count / (float)MAX_FRAGMENTS;
		result = float4(f, f, f, 1);

		if (frag_count >= MAX_FRAGMENTS)
		{
			clip(-1);
		}
	#endif

	uint new_index = FragListNodes.IncrementCounter();

	if (new_index >= bufferLength)
	{
		clip(-1);
	}

	uint old_index;
	InterlockedExchange(FragListHead[input.position.xy], new_index, old_index);

	OitNode n;

	n.depth = input.depth.x / input.depth.y;
	n.color = float4_to_unorm(result);
	n.flags = ((drawCall & 0xFFFF) << 16) | (blend_op << 8) | (src_blend << 4) | dst_blend;
	n.next  = old_index;

	FragListNodes[new_index] = n;
	clip(-1);
#endif
}

#endif