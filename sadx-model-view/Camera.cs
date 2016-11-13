using SharpDX;

namespace sadx_model_view
{
	public class Camera
	{
		private const float pitchThreshold = MathUtil.PiOverTwo - 0.0017453292519943296f;

		public bool Invalid { get; private set; } = true;
		private Vector3 position;
		private Vector3 rotation;
		private Matrix rotationMatrix = Matrix.Identity;

		public Vector3 Position
		{
			// ReSharper disable once UnusedMember.Global
			get { return position; }
			set
			{
				position = value;
				Invalid = true;
			}
		}

		// ReSharper disable once UnusedMember.Global
		public Vector3 Rotation
		{
			get { return rotation; }
			set
			{
				rotation = value;
				UpdateRotationMatrix();
				Invalid = true;
			}
		}

		public Matrix Matrix { get; private set; }

		public Camera()
		{
			position = Vector3.Zero;
			rotation = Vector3.Zero;

			Rotate(rotation);
			UpdateMatrix();
		}

		public void UpdateMatrix()
		{
			if (!Invalid)
				return;

			Matrix = Matrix.LookAtRH(position, position + (Vector3)Vector3.Transform(Vector3.ForwardRH, rotationMatrix), Vector3.Up);
			UpdateRotationMatrix();
			Invalid = false;
		}

		/// <summary>
		/// Translate by <paramref name="amount"/> in the direction specified by <paramref name="direction"/>.
		/// </summary>
		/// <param name="direction">The direction to translate.</param>
		/// <param name="amount">The amount by which to translate.</param>
		public void Translate(Vector3 direction, float amount = 1.0f)
		{
			Invalid = true;

			var v = Vector3.Normalize(amount * direction) * amount;
			v = (Vector3)Vector3.Transform(v, rotationMatrix);
			position += v;
		}

		/// <summary>
		/// Rotate the camera by <paramref name="v"/>.
		/// </summary>
		/// <param name="v">The amount to rotate (in radians)</param>
		public void Rotate(Vector3 v)
		{
			Invalid = true;

			rotation -= v;
			RotateLimit(ref rotation);
			UpdateRotationMatrix();
		}

		public void LookAt(Vector3 point)
		{
			Invalid = true;
			Matrix = Matrix.LookAtRH(position == Vector3.Zero ? Vector3.BackwardRH : position, point, Vector3.Up);

			Quaternion q;
			Vector3 dummy;
			Matrix.Decompose(out dummy, out q, out dummy);

			rotation = q.GetYawPitchRollVector();
			RotateLimit(ref rotation);
			UpdateRotationMatrix();
		}

		private void UpdateRotationMatrix()
		{
			if (!Invalid)
				return;

			rotationMatrix = Matrix.RotationX(rotation.X)
							 * Matrix.RotationY(rotation.Y)
							 * Matrix.RotationZ(rotation.Z);

			Invalid = true;
		}

		private static void RotateLimit(ref Vector3 v)
		{
			v.X = MathUtil.Clamp(v.X, -pitchThreshold, pitchThreshold);
			v.Y = MathUtil.Wrap(v.Y, 0, MathUtil.TwoPi);
			v.Z = MathUtil.Wrap(v.Z, 0, MathUtil.TwoPi);
		}
	}
}
