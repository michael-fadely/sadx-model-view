using System;

namespace sadx_model_view
{
	public static class CoreExtensions
	{
		public static void DisposeAndNullify<T>(ref T disposable) where T : class, IDisposable
		{
			disposable?.Dispose();
			disposable = null;
		}
	}
}
