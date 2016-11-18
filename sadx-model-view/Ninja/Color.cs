using System.Runtime.InteropServices;

namespace sadx_model_view.Ninja
{
	/// <summary>
	/// A color represented by 4 bytes.
	/// </summary>
	[StructLayout(LayoutKind.Explicit)]
	public struct NJS_BGRA
	{
		/// <summary>
		/// Native structure size in bytes.
		/// </summary>
		public static int SizeInBytes => sizeof(int);

		/// <summary>
		/// Blue component.
		/// </summary>
		[FieldOffset(0)]
		public byte b;
		/// <summary>
		/// Green component.
		/// </summary>
		[FieldOffset(1)]
		public byte g;
		/// <summary>
		/// Red component.
		/// </summary>
		[FieldOffset(2)]
		public byte r;
		/// <summary>
		/// Alpha component.
		/// </summary>
		[FieldOffset(3)]
		public byte a;

		/// <summary>
		/// Constructs <see cref="NJS_BGRA"/> from a 32-bit integer.
		/// </summary>
		/// <param name="color">An integer containing an ARGB color.</param>
		public NJS_BGRA(int color)
		{
			b = (byte)color;
			g = (byte)(color >> 8);
			r = (byte)(color >> 16);
			a = (byte)(color >> 24);
		}

		/// <summary>
		/// Constructs <see cref="NJS_BGRA"/> from a 32-bit integer.
		/// </summary>
		/// <param name="color">An integer containing an ARGB color.</param>
		public NJS_BGRA(uint color) : this((int)color)
		{
		}

		/// <summary>
		/// Constructs <see cref="NJS_BGRA"/> from individual color components.
		/// </summary>
		/// <param name="b">Blue component.</param>
		/// <param name="g">Green component.</param>
		/// <param name="r">Red component.</param>
		/// <param name="a">Alpha component.</param>
		public NJS_BGRA(byte b, byte g, byte r, byte a)
		{
			this.b = b;
			this.g = g;
			this.r = r;
			this.a = a;
		}
	}

	/// <summary>
	/// <para>
	/// A union defining a color represented by 4 bytes.
	/// It can be manipulated with direct access to the color integer, or on an individual byte level.
	/// </para>
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
			this.color = -1;
			argb = new NJS_BGRA(color);
		}

		/// <summary>
		/// Constructs <see cref="NJS_COLOR"/> from a 32-bit integer.
		/// </summary>
		/// <param name="color">An integer containing an ARGB color.</param>
		public NJS_COLOR(uint color) : this((int)color)
		{
		}

		/// <summary>
		/// Constructs <see cref="NJS_COLOR"/> from a <see cref="NJS_BGRA"/>.
		/// </summary>
		/// <param name="argb">Color.</param>
		public NJS_COLOR(NJS_BGRA argb)
		{
			color = -1;
			this.argb = argb;
		}

		/// <summary>
		/// Constructs <see cref="NJS_COLOR"/> from individual color components.
		/// </summary>
		/// <param name="b">Blue component.</param>
		/// <param name="g">Green component.</param>
		/// <param name="r">Red component.</param>
		/// <param name="a">Alpha component.</param>
		public NJS_COLOR(byte b, byte g, byte r, byte a)
		{
			color = -1;
			argb = new NJS_BGRA(b, g, r, a);
		}

		/// <summary>
		/// The 32-bit integer representation of the color.
		/// </summary>
		[FieldOffset(0)]
		public int color;

		/// <summary>
		/// The ARGB representation of the color.
		/// </summary>
		[FieldOffset(0)]
		public NJS_BGRA argb;
	}
}
