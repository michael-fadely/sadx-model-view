using System.Runtime.InteropServices;

namespace sadx_model_view.Ninja
{
	/// <summary>
	/// A color represented by 4 bytes.
	/// </summary>
	public struct NJS_BGRA
	{
		/// <summary>
		/// Native structure size in bytes.
		/// </summary>
		public static int SizeInBytes => sizeof(uint);

		public byte b, g, r, a;
	}

	/// <summary>
	/// <para>A union defining a color represented by 4 bytes.
	/// It can be manipulated with direct access to the color integer, or on an individual byte level.</para>
	/// See also:
	/// <seealso cref="NJS_BGRA"/>
	/// </summary>
	[StructLayout(LayoutKind.Explicit)]
	public struct NJS_COLOR // union
	{
		/// <summary>
		/// Native structure size in bytes.
		/// </summary>
		public static int SizeInBytes => sizeof(int);

		/// <summary>
		/// Constructs <see cref="NJS_COLOR"/> from a 32-bit integer.
		/// </summary>
		/// <param name="color">An integer containing an ARGB color.</param>
		public NJS_COLOR(int color)
		{
			argb = new NJS_BGRA
			{
				b = 255,
				g = 255,
				r = 255,
				a = 255
			};

			this.color = color;
		}

		/// <summary>
		/// Constructs <see cref="NJS_COLOR"/> from <see cref="NJS_BGRA"/>.<para/>
		/// See also:
		/// <seealso cref="NJS_BGRA"/>
		/// </summary>
		/// <param name="argb"></param>
		public NJS_COLOR(NJS_BGRA argb)
		{
			color = -1;
			this.argb = argb;
		}

		[FieldOffset(0)]
		public int color;

		[FieldOffset(0)]
		public NJS_BGRA argb;
	}
}
