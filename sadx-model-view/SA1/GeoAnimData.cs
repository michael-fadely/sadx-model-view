using System;
using System.IO;

using sadx_model_view.Extensions;
using sadx_model_view.Ninja;

namespace sadx_model_view.SA1
{
	/// <summary>
	/// Animation definition for a <see cref="LandTable"/>.
	/// </summary>
	public class GeoAnimData : IDisposable
	{
		public static readonly int SizeInBytes = 0x18;

		// ReSharper disable once NotAccessedField.Local
		private float anonymous_0;
		// ReSharper disable once NotAccessedField.Local
		private float anonymous_1;
		// ReSharper disable once NotAccessedField.Local
		private float       anonymous_2;
		public  NJS_OBJECT? Model;
		public  NJS_ACTION? Animation;
		// ReSharper disable once NotAccessedField.Local
		public NJS_TEXLIST? TexList;

		public GeoAnimData(Stream stream)
		{
			byte[] buffer = new byte[SizeInBytes];
			stream.ReadExact(buffer);
			long position = stream.Position;

			anonymous_0 = BitConverter.ToSingle(buffer, 0x00);
			anonymous_1 = BitConverter.ToSingle(buffer, 0x04);
			anonymous_2 = BitConverter.ToSingle(buffer, 0x08);

			uint objectOffset    = BitConverter.ToUInt32(buffer, 0x0C);
			uint animationOffset = BitConverter.ToUInt32(buffer, 0x10);
			uint texlistOffset   = BitConverter.ToUInt32(buffer, 0x14);

			if (objectOffset > 0)
			{
				Model = ObjectCache.FromStream(stream, objectOffset);
			}

			if (animationOffset > 0)
			{
				stream.Position = animationOffset;
				Animation = new NJS_ACTION(stream);
			}

			if (texlistOffset > 0)
			{
				stream.Position = texlistOffset;
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
			DisposableExtensions.DisposeAndNullify(ref Model);
			DisposableExtensions.DisposeAndNullify(ref Animation);
		}
	}
}
