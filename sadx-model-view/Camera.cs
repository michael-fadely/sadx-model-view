﻿using SharpDX;

namespace sadx_model_view
{
	public class Camera
	{
		private const float pitchThreshold = MathUtil.PiOverTwo - 0.0017453292519943296f;

		public bool Invalid { get; private set; } = true;
		private Vector3 position;
		private Vector3 rotation;
		private Matrix rotationMatrix = Matrix.Identity;
		private Matrix _view;
		private Matrix _projection;

		// ReSharper disable once UnusedMember.Global
		public Vector3 Position
		{
			get => position;
			set
			{
				position = value;
				Invalid = true;
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

		public Matrix View
		{
			get => _view;
			private set { _view = value; InverseView = -value; }
		}

		public Matrix InverseView { get; private set; }

		public Matrix Projection => _projection;

		public float FieldOfView { get; private set; }
		public float AspectRatio { get; private set; }
		public float MinDrawDistance { get; private set; }
		public float MaxDrawDistance { get; private set; }

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
			MinDrawDistance = MathUtil.Clamp(near, -65535.0f, -1.0f);
			MaxDrawDistance = MathUtil.Clamp(far, -65535.0f, -1.0f);
		}

		private void UpdateProjectionMatrix()
		{
			if (!Invalid)
			{
				return;
			}

			Matrix.PerspectiveFovRH(FieldOfView, AspectRatio, -MinDrawDistance, -MaxDrawDistance, out _projection);
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
			LimitRotation(ref rotation);
			UpdateRotationMatrix();
		}

		public void LookAt(Vector3 point)
		{
			Invalid = true;

			View = Matrix.LookAtRH(position == Vector3.Zero ? Vector3.BackwardRH : position, point, Vector3.Up);

			Quaternion q;
			Vector3 dummy;
			View.Decompose(out dummy, out q, out dummy);

			rotation = q.GetYawPitchRollVector();
			LimitRotation(ref rotation);
			UpdateRotationMatrix();
		}

		private void UpdateRotationMatrix()
		{
			if (!Invalid)
			{
				return;
			}

			rotationMatrix = Matrix.RotationX(rotation.X)
							 * Matrix.RotationY(rotation.Y)
							 * Matrix.RotationZ(rotation.Z);
		}

		private static void LimitRotation(ref Vector3 v)
		{
			v.X = MathUtil.Clamp(v.X, -pitchThreshold, pitchThreshold);
			v.Y = MathUtil.Wrap(v.Y, 0, MathUtil.TwoPi);
			v.Z = MathUtil.Wrap(v.Z, 0, MathUtil.TwoPi);
		}
	}
}
