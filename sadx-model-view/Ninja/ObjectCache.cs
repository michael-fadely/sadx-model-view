using System.Collections.Generic;
using System.IO;

namespace sadx_model_view.Ninja
{
	static class ObjectCache
	{
		static readonly Dictionary<long, NJS_OBJECT> objectCache = new Dictionary<long, NJS_OBJECT>();

		public static NJS_OBJECT FromStream(Stream stream, long offset)
		{
			lock (objectCache)
			{
				// HACK: disabled
				// TODO: replace with offset as unique identifier to update shared references, but allow multiple instances
				//objectCache.TryGetValue(offset, out NJS_OBJECT result);

				//if (result != null)
				//{
				//	return result;
				//}

				stream.Position = offset;
				var result = new NJS_OBJECT(stream);
				//objectCache[offset] = result;
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
