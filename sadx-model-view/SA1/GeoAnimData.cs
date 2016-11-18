﻿using System;
using System.IO;
using sadx_model_view.Ninja;

namespace sadx_model_view.SA1
{
	/// <summary>
	/// Animation definition for a <see cref="LandTable"/>.
	/// </summary>
	class GeoAnimData : IDisposable
	{
		public static int SizeInBytes => 0x18;

		private float anonymous_0;
		private float anonymous_1;
		private float anonymous_2;
		public NJS_OBJECT Model;
		public NJS_ACTION Animation;
		public NJS_TEXLIST TexList;

		public GeoAnimData(Stream stream)
		{
			var buffer = new byte[SizeInBytes];
			stream.Read(buffer, 0, buffer.Length);
			var position = stream.Position;

			anonymous_0 = BitConverter.ToSingle(buffer, 0x00);
			anonymous_1 = BitConverter.ToSingle(buffer, 0x04);
			anonymous_2 = BitConverter.ToSingle(buffer, 0x08);

			var object_ptr  = BitConverter.ToUInt32(buffer, 0x0C);
			var anim_ptr    = BitConverter.ToUInt32(buffer, 0x10);
			var texlist_ptr = BitConverter.ToUInt32(buffer, 0x14);

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

		~GeoAnimData()
		{
			Dispose();
		}

		public void Dispose()
		{
			Model?.Dispose();
			Model = null;

			Animation?.Dispose();
			Animation = null;
		}
	}
}