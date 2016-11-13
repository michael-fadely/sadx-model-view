using System.Collections.Generic;
using System.IO;

namespace sadx_model_view.Ninja
{
	internal static class ObjectCache
	{
		private static readonly Dictionary<long, NJS_OBJECT> objectCache = new Dictionary<long, NJS_OBJECT>();

		public static NJS_OBJECT FromStream(Stream stream, long offset, NJS_OBJECT parent = null, NJS_OBJECT previousSibling = null)
		{
			lock (objectCache)
			{
				NJS_OBJECT result;
				objectCache.TryGetValue(offset, out result);

				if (result != null)
					return result;

				stream.Position = offset;
				result = new NJS_OBJECT(stream, parent, previousSibling);
				objectCache[offset] = result;
				return result;
			}
		}

		public static void Clear()
		{
			lock (objectCache)
			{
				objectCache.Clear();
			}
		}

		public static void DisposeObjects()
		{
			lock (objectCache)
			{
				foreach (var pair in objectCache)
				{
					pair.Value.Dispose();
				}

				objectCache.Clear();
			}
		}
	}
}
