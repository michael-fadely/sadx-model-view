using SharpDX;

namespace sadx_model_view
{
	public struct DebugPoint
	{
		public static int SizeInBytes => Vector3.SizeInBytes + Vector4.SizeInBytes;

		public Vector3 Point;
		public Color4 Color;

		public DebugPoint(Vector3 point, Color4 color)
		{
			Point = point;
			Color = color;
		}

		public override bool Equals(object obj)
		{
			if (obj is null)
			{
				return false;
			}

			if (!(obj is DebugPoint other))
			{
				return false;
			}

			return Point == other.Point && Color == other.Color;
		}

		public override int GetHashCode()
		{
			throw new System.NotImplementedException();
		}

		public static bool operator ==(DebugPoint left, DebugPoint right)
		{
			return left.Equals(right);
		}

		public static bool operator !=(DebugPoint left, DebugPoint right)
		{
			return !(left == right);
		}
	}
}
