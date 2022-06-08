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

			byte[] buffer = new byte[sizeof(float)];
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
		public static Vector3 VectorFromStream(in byte[] buffer, int offset)
		{
			return new Vector3(BitConverter.ToSingle(buffer, offset + 0), BitConverter.ToSingle(buffer, offset + 4), BitConverter.ToSingle(buffer, offset + 8));
		}

		public static float DegreeToRadian(float n)
		{
			return n * MathF.PI / 180.0f;
		}

		public static int DegreeToAngle(float n)
		{
			return (int)(n * 65536.0f / 360.0f);
		}

		public static int RadToAngle(float n)
		{
			return (int)(n * 65536.0f / (2 * MathF.PI));
		}

		public static float RadToDegree(float n)
		{
			return n * 180.0f / MathF.PI;
		}

		public static float AngleToDegree(int n)
		{
			return n * 360.0f / 65536.0f;
		}

		public static float AngleToRadian(int n)
		{
			return n * (2 * MathF.PI) / 65536.0f;
		}

		public static Vector3 AngleToRadian(in Rotation3 n)
		{
			return new Vector3(AngleToRadian(n.X), AngleToRadian(n.Y), AngleToRadian(n.Z));
		}

		public static Vector3 AngleToDegree(in Rotation3 n)
		{
			return new Vector3(AngleToDegree(n.X), AngleToDegree(n.Y), AngleToDegree(n.Z));
		}
	}
}
