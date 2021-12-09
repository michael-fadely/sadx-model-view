using System;
using System.Diagnostics;

namespace sadx_model_view
{
	internal static class DeltaTime
	{
		private static readonly Stopwatch _stopwatch = new();

		private static double _secondsElapsedD;
		private static float  _secondsElapsed;

		public static double SecondsElapsedD
		{
			get => _secondsElapsedD;
			private set
			{
				_secondsElapsedD = value;
				_secondsElapsed = (float)value;
			}
		}

		public static float SecondsElapsed
		{
			get => _secondsElapsed;
			private set
			{
				_secondsElapsedD = value;
				_secondsElapsed = value;
			}
		}

		static DeltaTime()
		{
			_stopwatch.Start();
		}

		public static void Update()
		{
			_stopwatch.Stop();
			TimeSpan span = _stopwatch.Elapsed;
			SecondsElapsedD = span.TotalSeconds;
			_stopwatch.Restart();
		}
	}
}
