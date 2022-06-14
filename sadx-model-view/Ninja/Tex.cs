using System;

namespace sadx_model_view.Ninja
{
	/// <summary>
	/// <para>Defines UV coordinates for <see cref="NJS_MESHSET"/>.</para>
	/// See also:
	/// <seealso cref="NJS_MESHSET"/>
	/// </summary>
	public struct NJS_TEX
	{
		/// <summary>
		/// Native structure size in bytes.
		/// </summary>
		public static readonly int SizeInBytes = sizeof(short) * 2;

		public NJS_TEX(ref byte[] buffer, int offset = 0)
		{
			u = BitConverter.ToInt16(buffer, offset);
			v = BitConverter.ToInt16(buffer, offset + sizeof(short));
		}

		public short u, v;
	}
}