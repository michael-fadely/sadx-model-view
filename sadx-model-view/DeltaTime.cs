using System;

namespace sadx_model_view
{
	internal static class DeltaTime
	{
		public static TimeSpan Target = TimeSpan.FromMilliseconds(1000.0 / 60.0);
		public static double DeltaD { get; private set; }
		public static float Delta => (float)DeltaD;

		private static DateTime lastTime = DateTime.Now;

		public static void Update()
		{
			var now = DateTime.Now;
			var span = now - lastTime;
			lastTime = now;
			DeltaD = span.TotalMilliseconds / Target.TotalMilliseconds;
		}
	}
}
