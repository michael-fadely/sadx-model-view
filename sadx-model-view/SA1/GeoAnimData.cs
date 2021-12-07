using System;
using System.IO;
using sadx_model_view.Ninja;

namespace sadx_model_view.SA1
{
	/// <summary>
	/// Animation definition for a <see cref="LandTable"/>.
	/// </summary>
	public class GeoAnimData : IDisposable
	{
		public static int SizeInBytes => 0x18;

		// ReSharper disable once NotAccessedField.Local
		float anonymous_0;
		// ReSharper disable once NotAccessedField.Local
		float anonymous_1;
		// ReSharper disable once NotAccessedField.Local
		float anonymous_2;
		public NJS_OBJECT Model;
		public NJS_ACTION Animation;
		// ReSharper disable once NotAccessedField.Local
		public NJS_TEXLIST TexList;

		public GeoAnimData(Stream stream)
		{
			byte[] buffer = new byte[SizeInBytes];
			stream.Read(buffer, 0, buffer.Length);
			long position = stream.Position;

			anonymous_0 = BitConverter.ToSingle(buffer, 0x00);
			anonymous_1 = BitConverter.ToSingle(buffer, 0x04);
			anonymous_2 = BitConverter.ToSingle(buffer, 0x08);

			uint object_ptr  = BitConverter.ToUInt32(buffer, 0x0C);
			uint anim_ptr    = BitConverter.ToUInt32(buffer, 0x10);
			uint texlist_ptr = BitConverter.ToUInt32(buffer, 0x14);

			if (object_ptr > 0)
			{
				Model = ObjectCache.FromStream(stream, object_ptr);
			}

			if (anim_ptr > 0)
			{
				stream.Position = anim_ptr;
				Animation = new NJS_ACTION(stream);
			}

			if (texlist_ptr > 0)
			{
				stream.Position = texlist_ptr;
				TexList = new NJS_TEXLIST(stream);
			}

			stream.Position = position;
		}

		public GeoAnimData()
		{
			anonymous_0 = 0.0f;
			anonymous_1 = 0.0f;
			anonymous_2 = 0.0f;
			Model       = null;
			Animation   = null;
			TexList     = null;
		}

		public void Dispose()
		{
			Model?.Dispose();
			Animation?.Dispose();
		}
	}
}
