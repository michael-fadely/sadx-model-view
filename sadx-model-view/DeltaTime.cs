using System;
using System.Diagnostics;

namespace sadx_model_view
{
	internal static class DeltaTime
	{
		public static TimeSpan Target = TimeSpan.FromMilliseconds(1000.0 / 60.0);
		public static double   DeltaD { get; private set; }
		public static float    Delta  => (float)DeltaD;

		private static readonly Stopwatch Stopwatch = new();

		static DeltaTime()
		{
			Stopwatch.Start();
		}

		public static void Update()
		{
			TimeSpan span = Stopwatch.Elapsed;
			DeltaD = span.TotalMilliseconds / Target.TotalMilliseconds;
			Stopwatch.Restart();
		}
	}
}
