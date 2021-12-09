using System.Collections.Generic;
using System.IO;

namespace sadx_model_view.Ninja
{
	internal static class ObjectCache
	{
		private static readonly Dictionary<long, NJS_OBJECT> s_objectCache = new Dictionary<long, NJS_OBJECT>();

		public static NJS_OBJECT FromStream(Stream stream, long offset)
		{
			lock (s_objectCache)
			{
				// HACK: disabled
				// TODO: replace with offset as unique identifier to update shared references, but allow multiple instances
				//s_objectCache.TryGetValue(offset, out NJS_OBJECT? result);

				//if (result is not null)
				//{
				//	return result;
				//}

				stream.Position = offset;
				var result = new NJS_OBJECT(stream);
				//s_objectCache[offset] = result;
				return result;
			}
		}

		public static void Clear()
		{
			lock (s_objectCache)
			{
				s_objectCache.Clear();
			}
		}

		public static void DisposeObjects()
		{
			lock (s_objectCache)
			{
				foreach (NJS_OBJECT obj in s_objectCache.Values)
				{
					obj.Dispose();
				}

				s_objectCache.Clear();
			}
		}
	}
}
