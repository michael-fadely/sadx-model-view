using System;
using System.IO;

namespace sadx_model_view.Ninja
{
	public class NJS_MOTION
	{
		public int    mdata; // TODO: void*
		public uint   nbFrame;
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

		public NJS_OBJECT @object;
		public NJS_MOTION motion;

		public NJS_ACTION(Stream stream)
		{
			byte[] buffer = new byte[SizeInBytes];

			if (stream.Read(buffer, 0, buffer.Length) < SizeInBytes)
			{
				throw new InvalidOperationException();
			}

			long position = stream.Position;

			uint objectOffset = BitConverter.ToUInt32(buffer, 0);
			uint motionOffset = BitConverter.ToUInt32(buffer, 4);

			if (objectOffset > 0)
			{
				@object = ObjectCache.FromStream(stream, objectOffset);
			}

			if (motionOffset > 0)
			{
				// TODO: actually implement
				motion = new NJS_MOTION();
			}

			stream.Position = position;
		}

		public NJS_ACTION()
		{
			@object = null;
			motion  = new NJS_MOTION();
		}

		public void Dispose()
		{
			@object?.Dispose();
		}
	}
}