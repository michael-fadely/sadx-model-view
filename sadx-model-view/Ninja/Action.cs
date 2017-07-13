using System;
using System.IO;

namespace sadx_model_view.Ninja
{
	public class NJS_MOTION
	{
		public int mdata; // TODO: void*
		public uint nbFrame;
		public ushort type;
		/// <summary>
		/// Interpolation function index.
		/// </summary>
		public ushort inp_fn;

		public NJS_MOTION()
		{
			mdata   = 0;
			nbFrame = 0;
			type    = 0;
			inp_fn  = 0;
		}
	}

	public class NJS_ACTION : IDisposable
	{
		public static int SizeInBytes => 0x8;

		public NJS_OBJECT _object;
		public NJS_MOTION motion;

		public NJS_ACTION(Stream stream)
		{
			var buffer = new byte[SizeInBytes];
			stream.Read(buffer, 0, buffer.Length);
			var position = stream.Position;

			var object_ptr = BitConverter.ToUInt32(buffer, 0);
			var motion_ptr = BitConverter.ToUInt32(buffer, 4);

			if (object_ptr > 0)
			{
				_object = ObjectCache.FromStream(stream, object_ptr);
			}

			if (motion_ptr > 0)
			{
				// TODO: actually implement
				motion = new NJS_MOTION();
			}

			stream.Position = position;
		}

		public NJS_ACTION()
		{
			_object = null;
			motion  = new NJS_MOTION();
		}

		~NJS_ACTION()
		{
			Dispose();
		}

		public void Dispose()
		{
			_object?.Dispose();
			_object = null;
		}
	}
}
