using SharpDX;

namespace sadx_model_view
{
	public class Camera
	{
		private bool invalid = true;
		private Vector3 position;
		private Vector3 rotation;
		private Matrix rotationMatrix = Matrix.Identity;

		public Vector3 Position
		{
			get { return position; }
			set
			{
				position = value;
				invalid = true;
			}
		}

		public Vector3 Rotation
		{
			get { return rotation; }
			set
			{
				rotation = value;
				updateRotationMatrix();
				invalid = true;
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
			if (!invalid)
				return;

			Matrix = Matrix.LookAtLH(position, position + (Vector3)Vector3.Transform(Vector3.ForwardLH, rotationMatrix), Vector3.Up);
			updateRotationMatrix();
			invalid = false;
		}

		/// <summary>
		/// Translate by <paramref name="amount"/> in the direction specified by <paramref name="direction"/>.
		/// </summary>
		/// <param name="direction">The direction to translate.</param>
		/// <param name="amount">The amount by which to translate.</param>
		public void Translate(Vector3 direction, float amount = 1.0f)
		{
			invalid = true;

			var v = Vector3.Normalize(new Vector3(amount, amount, amount) * direction) * amount;
			v = (Vector3)Vector3.Transform(v, rotationMatrix);
			position += v;
		}

		/// <summary>
		/// Rotate the camera by <paramref name="v"/>.
		/// </summary>
		/// <param name="v">The amount to rotate (in radians)</param>
		public void Rotate(Vector3 v)
		{
			invalid = true;

			rotation += v;
			rotateLimit(ref rotation);
			updateRotationMatrix();
		}

		public void LookAt(Vector3 point)
		{
			invalid = true;
			Matrix = Matrix.LookAtLH(position == Vector3.Zero ? Vector3.BackwardLH : position, point, Vector3.Up);

			Quaternion q;
			Vector3 dummy;
			Matrix.Decompose(out dummy, out q, out dummy);

			rotation = q.GetYawPitchRollVector();
			rotateLimit(ref rotation);
			updateRotationMatrix();
		}

		private void updateRotationMatrix()
		{
			if (!invalid)
				return;

			rotationMatrix = Matrix.RotationX(rotation.X)
							 * Matrix.RotationY(rotation.Y)
							 * Matrix.RotationZ(rotation.Z);

			invalid = true;
		}

		private static void rotateLimit(ref Vector3 v)
		{
			rotateLimit(ref v.X);
			rotateLimit(ref v.Y);
			rotateLimit(ref v.Z);
		}
		private static void rotateLimit(ref float f)
		{
			while (f >= MathUtil.TwoPi)
			{
				f -= MathUtil.TwoPi;
			}

			while (f < 0.0f)
			{
				f += MathUtil.TwoPi;
			}
		}
	}
}
