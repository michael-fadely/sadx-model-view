using System;
using System.Runtime.CompilerServices;
using SharpDX;

namespace sadx_model_view.Extensions
{
	static class MathExtensions
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool NearEqual(this float a, float b)
		{
			return MathUtil.NearEqual(a, b);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int Clamp(this int value, int low, int high)
		{
			return MathUtil.Clamp(value, low, high);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float Clamp(this float value, float low, float high)
		{
			return MathUtil.Clamp(value, low, high);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int Wrap(this int value, int low, int high)
		{
			return MathUtil.Wrap(value, low, high);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float Wrap(this float value, float low, float high)
		{
			return MathUtil.Wrap(value, low, high);
		}

		public static int RoundToMultiple(this int value, int multiple)
		{
			var m = (double)multiple;
			return (int)(Math.Ceiling(value / m) * m);
		}
	}
}