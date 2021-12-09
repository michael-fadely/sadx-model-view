using System.Collections.Generic;
using System.IO;

namespace sadx_model_view.Ninja
{
	internal static class ModelCache
	{
		private static readonly Dictionary<long, NJS_MODEL> modelCache = new Dictionary<long, NJS_MODEL>();

		public static NJS_MODEL FromStream(Stream stream, long offset)
		{
			lock (modelCache)
			{
				// HACK: disabled
				// TODO: replace with offset as unique identifier to update shared references, but allow multiple instances
				//modelCache.TryGetValue(offset, out NJS_MODEL result);

				//if (result != null)
				//{
				//	return result;
				//}

				stream.Position = offset;
				var result = new NJS_MODEL(stream);
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
