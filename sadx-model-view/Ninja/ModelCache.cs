using System.Collections.Generic;
using System.IO;

namespace sadx_model_view.Ninja
{
	static class ModelCache
	{
		static readonly Dictionary<long, NJS_MODEL> modelCache = new Dictionary<long, NJS_MODEL>();

		public static NJS_MODEL FromStream(Stream stream, long offset)
		{
			lock (modelCache)
			{
				//modelCache.TryGetValue(offset, out NJS_MODEL result);

				//if (result != null)
				//{
				//	return result;
				//}

				stream.Position = offset;
				NJS_MODEL result = new NJS_MODEL(stream);
				//modelCache[offset] = result;
				return result;
			}
		}

		public static void Clear()
		{
			lock (modelCache)
			{
				modelCache.Clear();
			}
		}

		public static void DisposeObjects()
		{
			lock (modelCache)
			{
				foreach (NJS_MODEL model in modelCache.Values)
				{
					model.Dispose();
				}

				modelCache.Clear();
			}
		}
	}
}
