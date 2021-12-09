using sadx_model_view.Extensions;
using SharpDX;

namespace sadx_model_view
{
	public class Camera
	{
		private const float PitchThreshold = MathUtil.PiOverTwo - 0.0017453292519943296f;

		public bool Invalid { get; private set; } = true;

		private Vector3 _position;
		private Vector3 _rotation;
		private Matrix  _rotationMatrix = Matrix.Identity;

		// ReSharper disable once UnusedMember.Global
		public Vector3 Position
		{
			get => _position;
			set
			{
				_position = value;
				Invalid  = true;
			}
		}

		// ReSharper disable once UnusedMember.Global
		public Vector3 Rotation
		{
			get => _rotation;
			set
			{
				_rotation = value;
				UpdateRotationMatrix();
				Invalid = true;
			}
		}

		public Matrix RotationMatrix => _rotationMatrix;

		public Matrix View { get; private set; }

		public Matrix Projection { get; private set; }

		public float           FieldOfView     { get; private set; }
		public float           AspectRatio     { get; private set; }
		public float           MinDrawDistance { get; private set; }
		public float           MaxDrawDistance { get; private set; }
		public BoundingFrustum Frustum         { get; private set; }

		public Camera()
		{
			_position = Vector3.Zero;
			_rotation = Vector3.Zero;

			Rotate(_rotation);
			Update();
		}

		public void Update()
		{
			if (!Invalid)
			{
				return;
			}

			View = Matrix.LookAtRH(_position, _position + (Vector3)Vector3.Transform(Vector3.ForwardRH, _rotationMatrix), Vector3.Up);
			UpdateRotationMatrix();
			UpdateProjectionMatrix();

			Frustum = new BoundingFrustum(View * Projection);

			Invalid = false;
		}

		public void SetProjection(float fov, float ratio, float near, float far)
		{
			if (FieldOfView.NearEqual(fov) && ratio.NearEqual(AspectRatio) && MinDrawDistance.NearEqual(near) && MaxDrawDistance.NearEqual(far))
			{
				return;
			}

			Invalid = true;

			FieldOfView     = fov;
			AspectRatio     = ratio;
			MinDrawDistance = near;
			MaxDrawDistance = far;
		}

		private void UpdateProjectionMatrix()
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
			v = (Vector3)Vector3.Transform(v, _rotationMatrix);
			_position += v;
		}

		/// <summary>
		/// Rotate the camera by <paramref name="v"/>.
		/// </summary>
		/// <param name="v">The amount to rotate (in radians)</param>
		public void Rotate(Vector3 v)
		{
			Invalid = true;

			_rotation -= v;
			LimitRotation(ref _rotation);
			UpdateRotationMatrix();
		}

		public void LookAt(Vector3 point)
		{
			Invalid = true;

			View = Matrix.LookAtRH(_position == Vector3.Zero ? Vector3.BackwardRH : _position, point, Vector3.Up);

			View.Decompose(out _, out Quaternion q, out _);

			_rotation = q.GetYawPitchRollVector();
			LimitRotation(ref _rotation);
			UpdateRotationMatrix();
		}

		private void UpdateRotationMatrix()
		{
			if (!Invalid)
			{
				return;
			}

			_rotationMatrix = Matrix.RotationX(_rotation.X) *
			                  Matrix.RotationY(_rotation.Y) *
			                  Matrix.RotationZ(_rotation.Z);
		}

		private static void LimitRotation(ref Vector3 v)
		{
			v.X = MathUtil.Clamp(v.X, -PitchThreshold, PitchThreshold);
			v.Y = MathUtil.Wrap(v.Y, 0, MathUtil.TwoPi);
			v.Z = MathUtil.Wrap(v.Z, 0, MathUtil.TwoPi);
		}
	}
}