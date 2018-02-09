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
			return World == other.World
			       && View == other.View
			       && Projection == other.Projection
			       && Texture == other.Texture
			       && CameraPosition == other.CameraPosition;
		}

		public override int GetHashCode()
		{
			return 1;
		}

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