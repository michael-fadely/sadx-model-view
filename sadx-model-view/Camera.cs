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
			invalid = false;
		}

		/// <summary>
		/// Translate by <paramref name="amount"/> in the direction specified by <paramref name="direction"/>.
		/// </summary>
		/// <param name="direction">The direction to translate.</param>
		/// <param name="amount">The amount by which to translate.</param>
		public void Translate(Vector3 direction, float amount = 1.0f)
		{
			var v = new Vector3(amount, amount, amount) * direction;
			v.Normalize();
			v *= amount;
			v = (Vector3)Vector3.Transform(v, rotationMatrix);
			position += v;
			invalid = true;
		}

		/// <summary>
		/// Rotate the camera by <paramref name="v"/>.
		/// </summary>
		/// <param name="v">The amount to rotate (in radians)</param>
		public void Rotate(Vector3 v)
		{
			rotation += v;

			rotateLimit(ref rotation);

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
			if (f >= MathUtil.TwoPi)
			{
				f -= MathUtil.TwoPi;
			}
			else if (f < 0.0f)
			{
				f += MathUtil.TwoPi;
			}
		}
	}
}
