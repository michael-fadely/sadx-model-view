using System;
using System.IO;
using SharpDX;

namespace sadx_model_view.Ninja
{
	public static class Util
	{
		/// <summary>
		/// <para>Constructs a new <see cref="Vector3"/> with data provided by a stream.</para>
		/// </summary>
		/// <param name="stream">Stream containing data.</param>
		/// <returns>The new vector.</returns>
		public static Vector3 VectorFromStream(Stream stream)
		{
			Vector3 vector;

			var buffer = new byte[sizeof(float)];
			stream.Read(buffer, 0, buffer.Length);
			vector.X = BitConverter.ToSingle(buffer, 0);

			stream.Read(buffer, 0, buffer.Length);
			vector.Y = BitConverter.ToSingle(buffer, 0);

			stream.Read(buffer, 0, buffer.Length);
			vector.Z = BitConverter.ToSingle(buffer, 0);

			return vector;
		}

		/// <summary>
		/// <para>Constructs a new <see cref="Vector3"/> with data provided by a buffer.</para>
		/// </summary>
		/// <param name="buffer">Buffer containing data.</param>
		/// <param name="offset">Offset in buffer to read from.</param>
		/// <returns>The new vector.</returns>
		public static Vector3 VectorFromStream(ref byte[] buffer, int offset = 0)
		{
			return new Vector3(BitConverter.ToSingle(buffer, offset + 0), BitConverter.ToSingle(buffer, offset + 4), BitConverter.ToSingle(buffer, offset + 8));
		}

		public static float DegreeToRadian(float n)
		{
			return (float)(n * Math.PI / 180.0f);
		}
		public static int DegreeToAngle(float n)
		{
			return (int)(n * 65536.0 / 360.0);
		}

		public static int RadToAngle(float n)
		{
			return (int)(n * 65536.0 / (2 * Math.PI));
		}
		public static float RadToDegree(float n)
		{
			return (float)(n * 180.0 / Math.PI);
		}

		public static float AngleToDegree(int n)
		{
			return (float)(n * 360.0 / 65536.0);
		}
		public static float AngleToRadian(int n)
		{
			return (float)(n * (2 * Math.PI) / 65536.0);
		}

		public static Vector3 AngleToRadian(ref Rotation3 n)
		{
			return new Vector3(AngleToRadian(n.X), AngleToRadian(n.Y), AngleToRadian(n.Z));
		}
		public static Vector3 AngleToDegree(ref Rotation3 n)
		{
			return new Vector3(AngleToDegree(n.X), AngleToDegree(n.Y), AngleToDegree(n.Z));
		}
	}
}
