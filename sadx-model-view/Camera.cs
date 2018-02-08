using sadx_model_view.Extensions;
using SharpDX;

namespace sadx_model_view
{
	public class Camera
	{
		const float pitchThreshold = MathUtil.PiOverTwo - 0.0017453292519943296f;

		public bool Invalid { get; private set; } = true;
		Vector3     position;
		Vector3     rotation;
		Matrix      rotationMatrix = Matrix.Identity;

		// ReSharper disable once UnusedMember.Global
		public Vector3 Position
		{
			get => position;
			set
			{
				position = value;
				Invalid  = true;
			}
		}

		// ReSharper disable once UnusedMember.Global
		public Vector3 Rotation
		{
			get => rotation;
			set
			{
				rotation = value;
				UpdateRotationMatrix();
				Invalid = true;
			}
		}

		public Matrix View { get; private set; }

		public Matrix Projection { get; private set; }

		public float           FieldOfView     { get; private set; }
		public float           AspectRatio     { get; private set; }
		public float           MinDrawDistance { get; private set; }
		public float           MaxDrawDistance { get; private set; }
		public BoundingFrustum Frustum         { get; private set; }

		public Camera()
		{
			position = Vector3.Zero;
			rotation = Vector3.Zero;

			Rotate(rotation);
			Update();
		}

		public void Update()
		{
			if (!Invalid)
			{
				return;
			}

			View = Matrix.LookAtRH(position, position + (Vector3)Vector3.Transform(Vector3.ForwardRH, rotationMatrix), Vector3.Up);
			UpdateRotationMatrix();
			UpdateProjectionMatrix();

			Frustum = new BoundingFrustum(View * Projection);

			Invalid = false;
		}

		public void SetProjection(float fov, float ratio, float near, float far)
		{
			if (FieldOfView == fov && ratio == AspectRatio && MinDrawDistance == near && MaxDrawDistance == far)
			{
				return;
			}

			Invalid = true;

			FieldOfView     = fov;
			AspectRatio     = ratio;
			MinDrawDistance = near;
			MaxDrawDistance = far;
		}

		void UpdateProjectionMatrix()
		{
			if (!Invalid)
			{
				return;
			}

			Projection = Matrix.PerspectiveFovRH(FieldOfView, AspectRatio, MinDrawDistance, MaxDrawDistance);
		}

		/// <summary>
		/// Translate by <paramref name="amount"/> in the direction specified by <paramref name="direction"/>.
		/// </summary>
		/// <param name="direction">The direction to translate.</param>
		/// <param name="amount">The amount by which to translate.</param>
		public void Translate(Vector3 direction, float amount = 1.0f)
		{
			Invalid = true;

			Vector3 v = Vector3.Normalize(amount * direction) * amount;
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
			LimitRotation(ref rotation);
			UpdateRotationMatrix();
		}

		public void LookAt(Vector3 point)
		{
			Invalid = true;

			View = Matrix.LookAtRH(position == Vector3.Zero ? Vector3.BackwardRH : position, point, Vector3.Up);

			View.Decompose(out Vector3 dummy, out Quaternion q, out dummy);

			rotation = q.GetYawPitchRollVector();
			LimitRotation(ref rotation);
			UpdateRotationMatrix();
		}

		void UpdateRotationMatrix()
		{
			if (!Invalid)
			{
				return;
			}

			rotationMatrix = Matrix.RotationX(rotation.X) *
							 Matrix.RotationY(rotation.Y) *
							 Matrix.RotationZ(rotation.Z);
		}

		static void LimitRotation(ref Vector3 v)
		{
			v.X = MathUtil.Clamp(v.X, -pitchThreshold, pitchThreshold);
			v.Y = MathUtil.Wrap(v.Y, 0, MathUtil.TwoPi);
			v.Z = MathUtil.Wrap(v.Z, 0, MathUtil.TwoPi);
		}
	}
}