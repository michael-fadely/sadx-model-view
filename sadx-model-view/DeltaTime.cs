using System;

namespace sadx_model_view
{
	static class DeltaTime
	{
		public static TimeSpan Target = TimeSpan.FromMilliseconds(1000.0 / 60.0);
		public static double   DeltaD { get; private set; }
		public static float    Delta  => (float)DeltaD;

		static DateTime _lastTime = DateTime.Now;

		public static void Update()
		{
			DateTime now  = DateTime.Now;
			TimeSpan span = now - _lastTime;

			_lastTime = now;
			DeltaD    = span.TotalMilliseconds / Target.TotalMilliseconds;
		}
	}
}