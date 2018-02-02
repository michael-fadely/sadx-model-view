using SharpDX;

namespace sadx_model_view
{
	public struct MatrixBuffer
	{
		public Matrix World;
		public Matrix View;
		public Matrix Projection;
		public Matrix Texture;
		public Vector3 CameraPosition;

		public override bool Equals(object obj)
		{
			return base.Equals(obj);
		}

		public bool Equals(MatrixBuffer other)
		{
			return World.Equals(other.World)
			       && View.Equals(other.View)
			       && Projection.Equals(other.Projection)
			       && Texture.Equals(other.Texture)
			       && CameraPosition.Equals(other.CameraPosition);
		}

		public override int GetHashCode() => 1;

		public static bool operator ==(MatrixBuffer lhs, MatrixBuffer rhs)
		{
			return lhs.Equals(rhs);
		}

		public static bool operator !=(MatrixBuffer lhs, MatrixBuffer rhs)
		{
			return !(lhs == rhs);
		}
	}
}