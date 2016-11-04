using System;
using System.IO;
using SharpDX;

namespace sadx_model_view
{
	// Source: http://stackoverflow.com/a/23328415
	internal static class QuaternionExtensions
	{
		internal static Vector3 GetYawPitchRollVector(this Quaternion q)
		{
			return new Vector3(q.GetPitch(), -q.GetYaw(), q.GetRoll());
		}

		private static float GetYaw(this Quaternion q)
		{
			float x2 = q.X * q.X;
			float y2 = q.Y * q.Y;
			return (float)Math.Atan2((2.0f * q.Y * q.W) - (2.0f * q.Z * q.X),
				1.0f - (2.0f * y2) - 2.0f * x2);
		}

		private static float GetPitch(this Quaternion q)
		{
			return (float)-Math.Asin((2.0f * q.Z * q.Y) + (2.0f * q.X * q.W));
		}

		private static float GetRoll(this Quaternion q)
		{
			float x2 = q.X * q.X;
			float z2 = q.Z * q.Z;
			return (float)-Math.Atan2((2.0f * q.Z * q.W) - (2.0f * q.Y * q.X),
				1.0f - (2.0f * z2) - (2.0f * x2));
		}
	}

	public static class StreamExtensions
	{
		/// <summary>
		/// Reads a null terminated string from <paramref name="stream"/> into <paramref name="buffer"/>.
		/// </summary>
		/// <param name="stream">The stream to read from.</param>
		/// <param name="buffer">The buffer to output to.</param>
		/// <returns>The length of the string.</returns>
		public static int ReadString(this Stream stream, ref byte[] buffer)
		{
			int i = 0;
			do
			{
				stream.Read(buffer, i, 1);
			} while (buffer[i++] != 0);
			return i > 0 ? i - 1 : i;
		}
	}
}
