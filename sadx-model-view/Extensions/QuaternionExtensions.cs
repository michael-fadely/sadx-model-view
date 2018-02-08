using System;
using SharpDX;

namespace sadx_model_view.Extensions
{
	// Source: http://stackoverflow.com/a/23328415
	static class QuaternionExtensions
	{
		internal static Vector3 GetYawPitchRollVector(this Quaternion q)
		{
			return new Vector3(q.GetPitch(), -q.GetYaw(), q.GetRoll());
		}

		static float GetYaw(this Quaternion q)
		{
			float x2 = q.X * q.X;
			float y2 = q.Y * q.Y;
			return (float)Math.Atan2(2.0f * q.Y * q.W - 2.0f * q.Z * q.X,
				1.0f - 2.0f * y2 - 2.0f * x2);
		}

		static float GetPitch(this Quaternion q)
		{
			return (float)-Math.Asin(2.0f * q.Z * q.Y + 2.0f * q.X * q.W);
		}

		static float GetRoll(this Quaternion q)
		{
			float x2 = q.X * q.X;
			float z2 = q.Z * q.Z;
			return (float)-Math.Atan2(2.0f * q.Z * q.W - 2.0f * q.Y * q.X,
				1.0f - 2.0f * z2 - 2.0f * x2);
		}
	}
}