using System;

namespace sadx_model_view.Extensions
{
	public static class DisposableExtensions
	{
		public static void DisposeAndNullify<T>(ref T? disposable) where T : class, IDisposable
		{
			disposable?.Dispose();
			disposable = null;
		}
	}
}
