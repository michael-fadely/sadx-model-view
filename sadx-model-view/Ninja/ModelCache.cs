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
				NJS_MODEL result;
				modelCache.TryGetValue(offset, out result);

				if (result != null)
					return result;

				stream.Position = offset;
				result = new NJS_MODEL(stream);
				modelCache[offset] = result;
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
				foreach (var pair in modelCache)
				{
					pair.Value.Dispose();
				}

				modelCache.Clear();
			}
		}
	}
}
