using System.Collections.Generic;
using System.IO;

namespace sadx_model_view.Ninja
{
	internal static class ModelCache
	{
		private static readonly Dictionary<long, NJS_MODEL> s_modelCache = new();

		public static NJS_MODEL FromStream(Stream stream, long offset)
		{
			lock (s_modelCache)
			{
				// HACK: disabled
				// TODO: replace with offset as unique identifier to update shared references, but allow multiple instances
				//s_modelCache.TryGetValue(offset, out NJS_MODEL? result);

				//if (result is not null)
				//{
				//	return result;
				//}

				stream.Position = offset;
				var result = new NJS_MODEL(stream);
				//s_modelCache[offset] = result;
				return result;
			}
		}

		public static void Clear()
		{
			lock (s_modelCache)
			{
				s_modelCache.Clear();
			}
		}

		public static void DisposeObjects()
		{
			lock (s_modelCache)
			{
				foreach (NJS_MODEL model in s_modelCache.Values)
				{
					model.Dispose();
				}

				s_modelCache.Clear();
			}
		}
	}
}
