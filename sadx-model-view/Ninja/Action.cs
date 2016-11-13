using System;
using System.IO;

namespace sadx_model_view.Ninja
{
	class NJS_MOTION
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

	class NJS_ACTION : IDisposable
	{
		public NJS_OBJECT _object;
		public NJS_MOTION motion;

		public NJS_ACTION(Stream stream)
		{
			throw new NotImplementedException();
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
