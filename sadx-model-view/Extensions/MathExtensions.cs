using System.Runtime.CompilerServices;

using SharpDX;

namespace sadx_model_view.Extensions
{
	internal static class MathExtensions
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

		public static int AlignUp(this int value, int alignment)
		{
			if (value == 0 || alignment < 2)
			{
				return value;
			}

			value += alignment - (value % alignment);
			return value;
		}

		public static int AlignDown(this int value, int alignment)
		{
			if (value == 0 || alignment < 2)
			{
				return value;
			}

			value -= value % alignment;
			return value;
		}

		public static int RoundPow2(this int value)
		{
			--value;
			value |= value >> 1;
			value |= value >> 2;
			value |= value >> 4;
			value |= value >> 8;
			value |= value >> 16;
			++value;
			return value;
		}
	}
}
