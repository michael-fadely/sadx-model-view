using System.Collections.Generic;
using System.IO;

namespace sadx_model_view.Ninja
{
	internal static class ObjectCache
	{
		private static readonly Dictionary<long, NJS_OBJECT> objectCache = new Dictionary<long, NJS_OBJECT>();

		public static NJS_OBJECT FromStream(Stream stream, long offset)
		{
			lock (objectCache)
			{
				objectCache.TryGetValue(offset, out NJS_OBJECT result);

				if (result != null)
				{
					return result;
				}

				stream.Position = offset;
				result = new NJS_OBJECT(stream);
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
				foreach (NJS_OBJECT obj in objectCache.Values)
				{
					obj.Dispose();
				}

				objectCache.Clear();
			}
		}
	}
}
