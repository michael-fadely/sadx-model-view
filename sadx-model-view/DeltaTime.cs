using System;
using System.Diagnostics;

namespace sadx_model_view
{
	internal static class DeltaTime
	{
		private static readonly Stopwatch s_stopwatch = new();

		private static double s_secondsElapsedD;
		private static float  s_secondsElapsed;

		public static double SecondsElapsedD
		{
			get => s_secondsElapsedD;
			private set
			{
				s_secondsElapsedD = value;
				s_secondsElapsed = (float)value;
			}
		}

		public static float SecondsElapsed
		{
			get => s_secondsElapsed;
			private set
			{
				s_secondsElapsedD = value;
				s_secondsElapsed = value;
			}
		}

		static DeltaTime()
		{
			s_stopwatch.Start();
		}

		public static void Update()
		{
			s_stopwatch.Stop();
			TimeSpan span = s_stopwatch.Elapsed;
			SecondsElapsedD = span.TotalSeconds;
			s_stopwatch.Restart();
		}
	}
}
