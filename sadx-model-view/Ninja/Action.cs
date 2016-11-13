using System;
using System.IO;

namespace sadx_model_view.Ninja
{
	class NJS_MOTION
	{
		public int mdata; // TODO: void*
		public uint nbFrame;
		public ushort type;
		public ushort inp_fn;
	}

	class NJS_ACTION : IDisposable
	{
		public NJS_OBJECT _object;
		public NJS_MOTION motion;

		public NJS_ACTION(Stream stream)
		{
			throw new NotImplementedException();
		}

		public void Dispose()
		{
			_object?.Dispose();
			_object = null;
		}
	}
}
